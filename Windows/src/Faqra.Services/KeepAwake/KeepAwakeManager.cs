// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the OS side of Sources/Vorssaint/Services/KeepAwakeManager.swift: power assertions (applyAssertions
// 515-544), the end timer (scheduleEnd 501-511), the 30 s battery watch (722-745) and the lock, display
// and power-source observers. The decisions live in Faqra.Core's KeepAwakeSession.

using Faqra.Core.Defaults;
using Faqra.Core.KeepAwake;
using Faqra.Win32.Display;
using Faqra.Win32.Power;
using Faqra.Win32.Windows;

namespace Faqra.Services.KeepAwake;

/// <summary>The machine as the automation rules see it.</summary>
internal sealed class WindowsKeepAwakeEnvironment : IKeepAwakeEnvironment
{
    private bool _lastExternalDisplay;

    public bool ExternalDisplayConnected
    {
        get
        {
            // Upstream keeps the last known answer when the display list cannot be read.
            if (DisplayTopology.BuiltInFlags() is { } flags)
            {
                _lastExternalDisplay = KeepAwakeAutomationSupport.HasExternalDisplay(flags);
            }
            return _lastExternalDisplay;
        }
    }

    public PowerState Power
    {
        get
        {
            var details = Win32.Power.Power.Details();
            return new PowerState(details.HasBattery, details.HasBattery && !details.OnMains, details.Percent ?? 100);
        }
    }

    public bool SelectedAppsRunning => false;
}

/// <summary>
/// Keeps the PC awake while a session holds it. Everything runs on the thread that created it (the UI
/// thread): the execution state belongs to the calling thread, so it must be set and cleared from one
/// long-lived thread.
/// </summary>
public sealed class KeepAwakeManager : IDisposable
{
    private static readonly HashSet<string> AutomationKeys =
    [
        DefaultsKey.KeepAwakeExternalDisplay, DefaultsKey.KeepAwakeConnectedToPower,
        DefaultsKey.KeepAwakeRunningApps, DefaultsKey.KeepAwakePauseWhenLocked,
    ];

    private readonly ISettingsStore _store;
    private readonly Func<bool> _featureAvailable;
    private readonly SynchronizationContext _context;
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly Timer _endTimer;
    private readonly Timer _batteryTimer;
    private SystemEventsWindow? _events;
    private (bool Holds, bool Display) _applied;
    private bool _batteryWatching;
    private bool _disposed;

    public KeepAwakeManager(ISettingsStore store, Func<bool> featureAvailable, SynchronizationContext context)
        : this(store, featureAvailable, context, new WindowsKeepAwakeEnvironment())
    {
    }

    internal KeepAwakeManager(ISettingsStore store, Func<bool> featureAvailable, SynchronizationContext context, IKeepAwakeEnvironment environment)
    {
        _store = store;
        _featureAvailable = featureAvailable;
        _context = context;
        Session = new KeepAwakeSession(store, featureAvailable, environment, () => DateTime.UtcNow);
        Session.Changed += OnSessionChanged;
        Session.SessionEnded += reason => SessionEnded?.Invoke(reason);
        _endTimer = new Timer(_ => Post(Session.Tick));
        _batteryTimer = new Timer(_ => Post(Session.CheckBattery));
        _store.Changed += OnSettingChanged;
    }

    public KeepAwakeSession Session { get; }

    /// <summary>Raised on the owning thread after any change to the session.</summary>
    public event Action? Changed;

    /// <summary>Raised when a session ends on its own (timer or battery).</summary>
    public event Action<KeepAwakeEndReason>? SessionEnded;

    /// <summary>What was last asked of Windows, for the self-test and tests.</summary>
    internal (bool Holds, bool Display) AppliedState => _applied;

    /// <summary>Starts listening to lock, power and display changes, then runs launch automation and auto-start.</summary>
    public void Start(SystemEventsWindow events)
    {
        _events = events;
        events.SessionLockChanged += Session.ScreenLockChanged;
        events.PowerChanged += OnPowerChanged;
        events.DisplayChanged += Session.EvaluateAutomation;
        Session.MarkRecoveryCompleted();
        Session.EvaluateAutomation();
        Session.ActivateOnLaunchIfNeeded();
    }

    /// <summary>Keep awake leaving the hub ends any running session; its settings stay for its return.</summary>
    public void SyncWithFeatures()
    {
        if (!_featureAvailable())
        {
            if (Session.IsActive)
            {
                Session.Deactivate(KeepAwakeEndReason.Manual);
            }
            return;
        }
        Session.EvaluateAutomation();
        Apply();
    }

    private void OnPowerChanged()
    {
        // A resume can land after the end time without the timer having fired yet.
        Session.Tick();
        Session.CheckBattery();
        Session.EvaluateAutomation();
    }

    private void OnSessionChanged()
    {
        Apply();
        ScheduleEnd();
        SyncBatteryWatch();
        Changed?.Invoke();
    }

    private void Apply()
    {
        var wanted = (Holds: Session.HoldsAssertion, Display: Session.KeepsDisplayOn);
        if (wanted == _applied)
        {
            return;
        }
        if (wanted.Holds)
        {
            ExecutionState.KeepAwake(wanted.Display);
        }
        else
        {
            ExecutionState.Clear();
        }
        _applied = wanted;
    }

    private void ScheduleEnd()
    {
        if (Session.EndsAt is not { } end || !Session.IsActive)
        {
            _endTimer.Change(Timeout.Infinite, Timeout.Infinite);
            return;
        }
        var due = end - DateTime.UtcNow;
        var clamped = due < TimeSpan.Zero ? TimeSpan.Zero : due > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : due;
        _endTimer.Change(clamped, Timeout.InfiniteTimeSpan);
    }

    private void SyncBatteryWatch()
    {
        var needed = Session.HoldsAssertion;
        if (needed == _batteryWatching)
        {
            return;
        }
        _batteryWatching = needed;
        if (needed)
        {
            _batteryTimer.Change(TimeSpan.Zero, KeepAwakeAutomationSupport.BatteryCheckInterval);
        }
        else
        {
            _batteryTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    private void OnSettingChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.Key == DefaultsKey.KeepAwakeAllowDisplaySleep)
        {
            Post(Apply);
        }
        else if (AutomationKeys.Contains(e.Key))
        {
            Post(Session.AutomationPreferencesDidChange);
        }
        else if (e.Key == DefaultsKey.BatteryLimit)
        {
            Post(Session.CheckBattery);
        }
    }

    private void Post(Action action) =>
        _context.Post(_ =>
        {
            if (!_disposed)
            {
                action();
            }
        }, null);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        // The execution state belongs to the owning thread; clearing it anywhere else would leave the PC held awake.
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
        {
            _context.Send(_ => Dispose(), null);
            return;
        }
        _store.Changed -= OnSettingChanged;
        if (_events is { } events)
        {
            events.SessionLockChanged -= Session.ScreenLockChanged;
            events.PowerChanged -= OnPowerChanged;
            events.DisplayChanged -= Session.EvaluateAutomation;
        }
        Session.Deactivate(KeepAwakeEndReason.Quit);
        _disposed = true;
        _endTimer.Dispose();
        _batteryTimer.Dispose();
    }
}
