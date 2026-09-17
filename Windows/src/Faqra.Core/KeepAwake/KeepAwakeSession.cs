// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the session state machine of Sources/Vorssaint/Services/KeepAwakeManager.swift (toggle 108-117,
// activate 144-175, activateOnLaunchIfNeeded 177-183, extend 185-190, deactivate 192-213, screen lock
// 267-306, evaluateAutomation 389-428, continueAutomaticallyAfterTimerIfNeeded 489-499, checkBattery
// 738-745). The OS work (execution state, timers, notifications) lives in Faqra.Services; this class only
// decides. The closed-lid and mouse-jiggle branches are not ported.

using Faqra.Core.Defaults;

namespace Faqra.Core.KeepAwake;

public readonly record struct PowerState(bool HasBattery, bool OnBattery, int Percent);

/// <summary>What the automation rules read from the machine.</summary>
public interface IKeepAwakeEnvironment
{
    bool ExternalDisplayConnected { get; }

    PowerState Power { get; }

    bool SelectedAppsRunning { get; }
}

/// <summary>A keep-awake session: whether one runs, until when, and whether it holds the PC awake right now.</summary>
public sealed class KeepAwakeSession
{
    private readonly ISettingsStore _store;
    private readonly Func<bool> _featureAvailable;
    private readonly IKeepAwakeEnvironment _environment;
    private readonly Func<DateTime> _utcNow;

    private bool _suppressedUntilConditionsClear;
    private bool _recoveryCompleted;

    public KeepAwakeSession(ISettingsStore store, Func<bool> featureAvailable, IKeepAwakeEnvironment environment, Func<DateTime> utcNow)
    {
        _store = store;
        _featureAvailable = featureAvailable;
        _environment = environment;
        _utcNow = utcNow;
    }

    public bool IsActive { get; private set; }

    /// <summary>UTC end of a timed session; null while inactive or indefinite.</summary>
    public DateTime? EndsAt { get; private set; }

    public KeepAwakeTrigger? Trigger { get; private set; }

    public IReadOnlySet<KeepAwakeCondition> ActiveConditions { get; private set; } = new HashSet<KeepAwakeCondition>();

    public bool ScreenLocked { get; private set; }

    public bool PausedForLock { get; private set; }

    /// <summary>True while the PC must be kept from sleeping.</summary>
    public bool HoldsAssertion => IsActive && !PausedForLock;

    /// <summary>True while the display must stay on as well.</summary>
    public bool KeepsDisplayOn => HoldsAssertion && !_store.Bool(DefaultsKey.KeepAwakeAllowDisplaySleep);

    /// <summary>Raised after any change to the session.</summary>
    public event Action? Changed;

    /// <summary>Raised when a session ends on its own (timer or battery), for the user notification.</summary>
    public event Action<KeepAwakeEndReason>? SessionEnded;

    private bool PauseWhenLocked => _store.Bool(DefaultsKey.KeepAwakePauseWhenLocked);

    private int BatteryLimit => DefaultsSanitizers.BatteryLimit(_store.Int(DefaultsKey.BatteryLimit));

    /// <summary>Upstream waits for its closed-lid recovery before automations may act; Windows has none to run.</summary>
    public void MarkRecoveryCompleted() => _recoveryCompleted = true;

    public void Toggle()
    {
        if (IsActive)
        {
            if (Trigger == KeepAwakeTrigger.Automation || CurrentMatchingConditions().Count > 0)
            {
                _suppressedUntilConditionsClear = true;
            }
            Deactivate(KeepAwakeEndReason.Manual);
        }
        else
        {
            Activate(DefaultsSanitizers.DefaultDuration(_store.Int(DefaultsKey.DefaultDuration)));
        }
    }

    /// <summary>Starts a manual session; zero or an unknown duration means indefinitely.</summary>
    public void Activate(int minutes)
    {
        _suppressedUntilConditionsClear = false;
        Activate(minutes, KeepAwakeTrigger.Manual);
    }

    public void ActivateOnLaunchIfNeeded()
    {
        if (_featureAvailable() && _store.Bool(DefaultsKey.KeepAwakeAutoStart) && !IsActive)
        {
            Activate(DefaultsSanitizers.DefaultDuration(_store.Int(DefaultsKey.DefaultDuration)));
        }
    }

    /// <summary>Pushes a timed session's end out; an overdue end counts from now. Indefinite sessions are left alone.</summary>
    public void Extend(int minutes)
    {
        if (!IsActive || EndsAt is not { } current)
        {
            return;
        }
        var now = _utcNow();
        EndsAt = (current > now ? current : now).AddMinutes(minutes);
        Changed?.Invoke();
    }

    public void Deactivate(KeepAwakeEndReason reason)
    {
        var hadSession = IsActive;
        EndsAt = null;
        Trigger = null;
        ActiveConditions = new HashSet<KeepAwakeCondition>();
        IsActive = false;
        PausedForLock = false;
        Changed?.Invoke();
        if (hadSession && reason is KeepAwakeEndReason.Timer or KeepAwakeEndReason.Battery)
        {
            SessionEnded?.Invoke(reason);
        }
    }

    /// <summary>Called by the service's timer; ends a timed session whose end has come.</summary>
    public void Tick()
    {
        if (IsActive && EndsAt is { } end && end <= _utcNow() && !ContinueAutomaticallyAfterTimer())
        {
            Deactivate(KeepAwakeEndReason.Timer);
        }
    }

    public void CheckBattery()
    {
        var power = _environment.Power;
        if (KeepAwakeAutomationSupport.BatteryShouldEndSession(BatteryLimit, IsActive && !PausedForLock, power.OnBattery, power.Percent))
        {
            Deactivate(KeepAwakeEndReason.Battery);
        }
    }

    public void ScreenLockChanged(bool locked)
    {
        if (ScreenLocked == locked)
        {
            return;
        }
        ScreenLocked = locked;
        SyncWithScreenLock();
        EvaluateAutomation();
    }

    /// <summary>A deliberate change to an automation preference resumes evaluation after a manual stop.</summary>
    public void AutomationPreferencesDidChange()
    {
        _suppressedUntilConditionsClear = false;
        EvaluateAutomation();
    }

    public void EvaluateAutomation()
    {
        if (!_recoveryCompleted)
        {
            return;
        }
        var matches = CurrentMatchingConditions();

        if (_suppressedUntilConditionsClear)
        {
            if (matches.Count == 0)
            {
                _suppressedUntilConditionsClear = false;
            }
            if (Trigger == KeepAwakeTrigger.Automation)
            {
                Deactivate(KeepAwakeEndReason.Manual);
            }
            return;
        }

        if (ScreenLocked && PauseWhenLocked)
        {
            SetConditionsIfAutomatic(matches);
            return;
        }

        SetConditionsIfAutomatic(matches);
        var action = KeepAwakeAutomationSupport.Action(
            _featureAvailable(), matches, IsActive, IsActive && Trigger == KeepAwakeTrigger.Automation);
        switch (action)
        {
            case KeepAwakeAutomationAction.Activate when AutomaticSessionAllowed():
                ActiveConditions = matches;
                Activate(0, KeepAwakeTrigger.Automation);
                break;
            case KeepAwakeAutomationAction.Deactivate:
                Deactivate(KeepAwakeEndReason.Manual);
                break;
        }
    }

    private void Activate(int minutes, KeepAwakeTrigger trigger)
    {
        if (!_featureAvailable())
        {
            return;
        }
        minutes = DefaultsSanitizers.DefaultDuration(minutes);
        PausedForLock = ScreenLocked && PauseWhenLocked;
        Trigger = trigger;
        if (trigger == KeepAwakeTrigger.Manual)
        {
            ActiveConditions = new HashSet<KeepAwakeCondition>();
        }
        IsActive = true;
        EndsAt = minutes > 0 ? _utcNow().AddMinutes(minutes) : null;
        Changed?.Invoke();
    }

    private void SyncWithScreenLock()
    {
        if (!IsActive)
        {
            PausedForLock = false;
            return;
        }
        var shouldPause = ScreenLocked && PauseWhenLocked;
        if (shouldPause == PausedForLock)
        {
            return;
        }
        if (shouldPause)
        {
            PausedForLock = true;
            Changed?.Invoke();
            return;
        }

        PausedForLock = false;
        if (EndsAt is { } end && end <= _utcNow())
        {
            if (!ContinueAutomaticallyAfterTimer())
            {
                Deactivate(KeepAwakeEndReason.Timer);
            }
            return;
        }
        if (Trigger == KeepAwakeTrigger.Automation && CurrentMatchingConditions().Count == 0)
        {
            Deactivate(KeepAwakeEndReason.Manual);
            return;
        }
        Changed?.Invoke();
    }

    private bool ContinueAutomaticallyAfterTimer()
    {
        if (Trigger != KeepAwakeTrigger.Manual || !_featureAvailable() || _suppressedUntilConditionsClear || !AutomaticSessionAllowed())
        {
            return false;
        }
        var matches = CurrentMatchingConditions();
        if (matches.Count == 0)
        {
            return false;
        }
        ActiveConditions = matches;
        Activate(0, KeepAwakeTrigger.Automation);
        return true;
    }

    private void SetConditionsIfAutomatic(IReadOnlySet<KeepAwakeCondition> matches)
    {
        if (Trigger == KeepAwakeTrigger.Automation && !matches.SetEquals(ActiveConditions))
        {
            ActiveConditions = matches;
            Changed?.Invoke();
        }
    }

    private bool AutomaticSessionAllowed()
    {
        var power = _environment.Power;
        return KeepAwakeAutomationSupport.AutomaticSessionAllowed(BatteryLimit, power.OnBattery, power.Percent);
    }

    private IReadOnlySet<KeepAwakeCondition> CurrentMatchingConditions()
    {
        var externalEnabled = _store.Bool(DefaultsKey.KeepAwakeExternalDisplay);
        var powerEnabled = _store.Bool(DefaultsKey.KeepAwakeConnectedToPower);
        var appsEnabled = _store.Bool(DefaultsKey.KeepAwakeRunningApps);
        return KeepAwakeAutomationSupport.MatchingConditions(
            externalEnabled, externalEnabled && _environment.ExternalDisplayConnected,
            // Windows: a PC on mains is connected to power, battery or not.
            powerEnabled, powerEnabled && !_environment.Power.OnBattery,
            appsEnabled, appsEnabled && _environment.SelectedAppsRunning);
    }
}
