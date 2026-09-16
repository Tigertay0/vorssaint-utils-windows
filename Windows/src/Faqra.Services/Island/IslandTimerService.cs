// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors NotchTimerService in Sources/Vorssaint/Services/Notch: the countdown lives in a service
// rather than the view, so it keeps running while the island is collapsed or showing another module.

using System.Diagnostics;

namespace Faqra.Services.Island;

/// <summary>A countdown the island can start, pause and reset. Ticks once a second while running.</summary>
public sealed class IslandTimerService : IDisposable
{
    /// <summary>The presets the island offers, in minutes.</summary>
    public static readonly IReadOnlyList<int> Presets = [5, 10, 25, 45];

    private readonly Stopwatch _elapsed = new();
    private readonly Timer _ticker;
    private TimeSpan _duration;

    public IslandTimerService()
    {
        _ticker = new Timer(_ => OnTick(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>Raised once a second while running, and once whenever the state changes.</summary>
    public event Action? Changed;

    /// <summary>Raised when the countdown reaches zero.</summary>
    public event Action? Finished;

    public bool HasSession => _duration > TimeSpan.Zero;

    public bool IsRunning => _elapsed.IsRunning;

    public TimeSpan Remaining => HasSession
        ? TimeSpan.FromTicks(Math.Max(0, (_duration - _elapsed.Elapsed).Ticks))
        : TimeSpan.Zero;

    /// <summary>0 when there is no session, else how much of it is gone, 0 through 1.</summary>
    public double Progress => HasSession
        ? Math.Clamp(_elapsed.Elapsed.TotalSeconds / _duration.TotalSeconds, 0, 1)
        : 0;

    public void Start(TimeSpan duration)
    {
        _duration = duration;
        _elapsed.Restart();
        _ticker.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        Changed?.Invoke();
    }

    public void TogglePause()
    {
        if (!HasSession)
        {
            return;
        }
        if (_elapsed.IsRunning)
        {
            _elapsed.Stop();
            _ticker.Change(Timeout.Infinite, Timeout.Infinite);
        }
        else
        {
            _elapsed.Start();
            _ticker.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
        Changed?.Invoke();
    }

    public void Reset()
    {
        _duration = TimeSpan.Zero;
        _elapsed.Reset();
        _ticker.Change(Timeout.Infinite, Timeout.Infinite);
        Changed?.Invoke();
    }

    /// <summary>Formats the remaining time the way a clock does: m:ss under an hour, h:mm:ss over.</summary>
    public static string Format(TimeSpan remaining) =>
        remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
            : $"{remaining.Minutes}:{remaining.Seconds:D2}";

    private void OnTick()
    {
        if (Remaining <= TimeSpan.Zero && HasSession)
        {
            _elapsed.Stop();
            _ticker.Change(Timeout.Infinite, Timeout.Infinite);
            Changed?.Invoke();
            Finished?.Invoke();
            return;
        }
        Changed?.Invoke();
    }

    public void Dispose() => _ticker.Dispose();
}
