// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the rate math of Sources/Vorssaint/Services/Metrics/NetworkSampler.swift (lines 29, 39-81) and
// DiskSampler.swift (lines 38, 114-132). The nettop process fallback is left behind: it works around
// a macOS interface-counter bug that Windows does not have.

namespace Faqra.Core.Metrics;

/// <summary>Live network rates plus what has moved since Faqra started watching.</summary>
public readonly record struct NetworkRates(double? DownBytesPerSec, double? UpBytesPerSec, ulong TotalDown, ulong TotalUp);

/// <summary>Live disk rates plus what has moved since Faqra started watching.</summary>
public readonly record struct DiskRates(double? ReadBytesPerSec, double? WriteBytesPerSec, ulong TotalRead, ulong TotalWritten);

/// <summary>Turns cumulative network counters into rates. Not thread-safe; times are monotonic seconds.</summary>
public sealed class NetworkRateTracker
{
    /// <summary>A longer gap (sleep, a stalled timer) would average a burst into nonsense, so it starts over.</summary>
    public const double MaxGap = 10;

    private (NetworkCounters Counters, double Time)? _previous;
    private ulong _totalDown;
    private ulong _totalUp;

    /// <summary>A missing reading keeps the old baseline, so the next good one still yields a rate.</summary>
    public NetworkRates Sample(NetworkCounters? counters, double now)
    {
        if (counters is not { } current)
        {
            return new NetworkRates(null, null, _totalDown, _totalUp);
        }
        var previous = _previous;
        _previous = (current, now);
        if (previous is not { } prev || now <= prev.Time || now - prev.Time > MaxGap)
        {
            return new NetworkRates(null, null, _totalDown, _totalUp);
        }

        var (down, up) = MetricFormat.NetSpeed(prev.Counters, current, now - prev.Time);
        _totalDown = AddDelta(_totalDown, prev.Counters.Received, current.Received);
        _totalUp = AddDelta(_totalUp, prev.Counters.Sent, current.Sent);
        return new NetworkRates(down, up, _totalDown, _totalUp);
    }

    internal static ulong AddDelta(ulong total, ulong previous, ulong current)
    {
        if (current < previous)
        {
            return total;
        }
        var delta = current - previous;
        return ulong.MaxValue - total < delta ? ulong.MaxValue : total + delta;
    }
}

/// <summary>Turns cumulative per-disk counters into rates, one baseline per disk. Not thread-safe.</summary>
public sealed class DiskRateTracker
{
    public const double MaxGap = 15;

    private readonly Dictionary<string, (DiskIOCounters Counters, double Time)> _previous = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (ulong Read, ulong Written)> _totals = new(StringComparer.OrdinalIgnoreCase);

    public DiskRates Sample(string diskId, DiskIOCounters counters, double now)
    {
        var totals = _totals.GetValueOrDefault(diskId);
        var hasPrevious = _previous.TryGetValue(diskId, out var prev);
        _previous[diskId] = (counters, now);
        if (!hasPrevious || now <= prev.Time || now - prev.Time > MaxGap)
        {
            _totals[diskId] = totals;
            return new DiskRates(null, null, totals.Read, totals.Written);
        }

        var (read, write) = MetricFormat.DiskSpeed(prev.Counters, counters, now - prev.Time);
        totals = (
            NetworkRateTracker.AddDelta(totals.Read, prev.Counters.Read, counters.Read),
            NetworkRateTracker.AddDelta(totals.Written, prev.Counters.Written, counters.Written));
        _totals[diskId] = totals;
        return new DiskRates(read, write, totals.Read, totals.Written);
    }
}
