// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/Metrics/MonitorSamplingPolicy.swift

namespace Faqra.Core.Metrics;

public enum MonitorSamplingKind
{
    Cpu, Memory, Network, Disk, Power, PeripheralBattery, GpuUsage, Temperature, FanSpeed,
}

/// <summary>
/// How often each reading is taken. While a panel or the island shows metrics everything refreshes
/// every second; with only tray metrics showing, the expensive readings slow down to spare the CPU.
/// </summary>
public static class MonitorSamplingPolicy
{
    public static bool ShouldSample(MonitorSamplingKind kind, long tick, int intervalSeconds, bool foreground) =>
        tick % SampleStride(kind, intervalSeconds, foreground) == 0;

    /// <summary>How many timer ticks pass between two readings of this kind.</summary>
    public static int SampleStride(MonitorSamplingKind kind, int intervalSeconds, bool foreground)
    {
        var interval = Math.Max(1, intervalSeconds);
        return Math.Max(1, (int)Math.Ceiling(TargetSeconds(kind, foreground) / interval));
    }

    /// <summary>The timer's wake cadence in ticks: the largest step that still lands on every needed stride.</summary>
    public static int WakeTicks(IEnumerable<MonitorSamplingKind> kinds, int intervalSeconds, bool foreground) =>
        Math.Max(1, kinds.Aggregate(0, (acc, kind) => Gcd(acc, SampleStride(kind, intervalSeconds, foreground))));

    /// <summary>Rounds a tick up to the next multiple of the cadence, so strides stay in phase after a cadence change.</summary>
    public static long AlignedTick(long tick, int wakeTicks)
    {
        if (wakeTicks <= 1)
        {
            return tick;
        }
        var remainder = tick % wakeTicks;
        return remainder == 0 ? tick : tick + (wakeTicks - remainder);
    }

    private static double TargetSeconds(MonitorSamplingKind kind, bool foreground) => kind switch
    {
        MonitorSamplingKind.Cpu or MonitorSamplingKind.Memory or MonitorSamplingKind.Network => 1,
        // Disk stays under DiskRateTracker.MaxGap so background rates never lose their baseline.
        MonitorSamplingKind.Disk => foreground ? 1 : 10,
        MonitorSamplingKind.Power or MonitorSamplingKind.Temperature => foreground ? 1 : 15,
        MonitorSamplingKind.GpuUsage => foreground ? 1 : 10,
        MonitorSamplingKind.FanSpeed => foreground ? 1 : 5,
        MonitorSamplingKind.PeripheralBattery => foreground ? 15 : 60,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }
        return a;
    }
}
