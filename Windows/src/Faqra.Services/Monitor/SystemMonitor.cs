// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors SystemMonitor in Sources/Vorssaint/Services/SystemMonitor/SystemMonitor.swift (lines 128-903):
// the activation inputs, the timer cadence, and the refresh loop with its missed-read bridging.

using System.Diagnostics;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Metrics;

namespace Faqra.Services.Monitor;

/// <summary>
/// Samples the machine on a background timer while something on screen needs a metric, and stops
/// entirely when nothing does. <see cref="SnapshotChanged"/> is raised on the sampling thread, so UI
/// consumers marshal to their dispatcher. The public methods are safe to call from any thread.
/// </summary>
public sealed partial class SystemMonitor : IDisposable
{
    private const int MaxBridgedMisses = 3;
    private const int MaxMemoryMisses = 4;
    private const double MaxMemoryAgeSeconds = 12;
    private static readonly TimeSpan MaxShutdownWait = TimeSpan.FromSeconds(2);

    private readonly IMetricReaders _readers;
    private readonly ISettingsStore _store;
    private readonly Func<AppFeature, bool> _isAvailable;
    private readonly bool _useTimer;
    private readonly object _gate = new();
    private readonly object _tickGate = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private Timer? _timer;
    private TimeSpan _timerPeriod;
    private PanelNeeds _panel;
    private bool _notchVisible;
    private long _tick;
    private int _wakeTicks = 1;
    private bool _disposed;

    public SystemMonitor(IMetricReaders readers, ISettingsStore store, Func<AppFeature, bool> isAvailable)
        : this(readers, store, isAvailable, useTimer: true)
    {
    }

    internal SystemMonitor(IMetricReaders readers, ISettingsStore store, Func<AppFeature, bool> isAvailable, bool useTimer)
    {
        _readers = readers;
        _store = store;
        _isAvailable = isAvailable;
        _useTimer = useTimer;
        _store.Changed += OnSettingChanged;
    }

    /// <summary>The latest published readings.</summary>
    public SystemSnapshot Snapshot { get; private set; } = SystemSnapshot.Empty;

    /// <summary>Raised after every refresh, on the sampling thread.</summary>
    public event Action<SystemSnapshot>? SnapshotChanged;

    public bool HasBattery => _readers.HasBattery;

    /// <summary>Which panel sections are on screen. Opening one refreshes immediately.</summary>
    public void SetPanelNeeds(PanelNeeds needs)
    {
        lock (_gate)
        {
            if (_panel == needs)
            {
                return;
            }
            _panel = needs;
        }
        Resync(refreshNow: needs.Any);
    }

    /// <summary>Whether the island's System module is on screen.</summary>
    public void SetNotchVisible(bool visible)
    {
        lock (_gate)
        {
            if (_notchVisible == visible)
            {
                return;
            }
            _notchVisible = visible;
        }
        Resync(refreshNow: visible);
    }

    /// <summary>Re-evaluates what to sample after tray metrics or feature availability changed.</summary>
    public void PlanDidChange() => Resync(refreshNow: true);

    private void OnSettingChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.Key == DefaultsKey.MonitorInterval
            || e.Key.StartsWith("menuBar", StringComparison.Ordinal)
            || e.Key.StartsWith("monitorSys", StringComparison.Ordinal)
            || e.Key.StartsWith(DefaultsKey.FeatureAvailable(string.Empty), StringComparison.Ordinal))
        {
            Resync(refreshNow: false);
        }
    }

    private SamplingPlan CurrentPlan() =>
        SamplingPlan.Build(_panel, _notchVisible, _store.Bool, _isAvailable, _readers.HasBattery);

    private int Interval() => DefaultsSanitizers.MonitorInterval(_store.Int(DefaultsKey.MonitorInterval));

    /// <summary>Starts, reschedules or stops the timer to match the current plan and interval.</summary>
    private void Resync(bool refreshNow)
    {
        if (!_useTimer)
        {
            return;
        }
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            var plan = CurrentPlan();
            if (!plan.Any)
            {
                _timer?.Dispose();
                _timer = null;
                return;
            }
            SyncCadence(plan);
            var period = TimeSpan.FromSeconds(Interval() * _wakeTicks);
            var due = refreshNow ? TimeSpan.Zero : period;
            if (_timer is null)
            {
                _timer = new Timer(OnTimer, null, due, period);
            }
            else if (refreshNow || period != _timerPeriod)
            {
                _timer.Change(due, period);
            }
            _timerPeriod = period;
        }
    }

    /// <summary>Recomputes the wake cadence and keeps the tick counter in phase with it.</summary>
    private void SyncCadence(SamplingPlan plan)
    {
        var wake = MonitorSamplingPolicy.WakeTicks(plan.NeededKinds(), Interval(), plan.Foreground);
        if (wake != _wakeTicks)
        {
            _tick = MonitorSamplingPolicy.AlignedTick(_tick, wake);
            _wakeTicks = wake;
        }
    }

    private void OnTimer(object? state)
    {
        // A slow tick (a GPU query can take tens of milliseconds) must not pile up behind itself.
        if (!System.Threading.Monitor.TryEnter(_tickGate))
        {
            return;
        }
        try
        {
            Refresh(_clock.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            // A reader failure must not take the app down from a timer thread; the next tick retries.
            Trace.TraceError($"SystemMonitor refresh failed: {ex}");
        }
        finally
        {
            System.Threading.Monitor.Exit(_tickGate);
        }
    }

    /// <summary>
    /// Stops sampling and releases the readers, which the monitor owns. Waits for a tick already
    /// running to finish, so nothing is published and no reader is touched once this returns.
    /// </summary>
    public void Dispose()
    {
        _store.Changed -= OnSettingChanged;
        Timer? timer;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            timer = _timer;
            _timer = null;
        }
        if (timer is not null)
        {
            using var stopped = new ManualResetEvent(false);
            if (timer.Dispose(stopped))
            {
                // Bounded so a hung reader can never hang app exit.
                stopped.WaitOne(MaxShutdownWait);
            }
        }
        lock (_tickGate)
        {
            _readers.Dispose();
        }
    }
}
