// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors SystemSnapshot in Sources/Vorssaint/Services/SystemMonitor/SystemMonitor.swift (lines 27-79),
// PowerReading in Services/Metrics/PowerSampler.swift (lines 10-27) and DiskDeviceReading in
// Services/Metrics/DiskSupport.swift (lines 25-61). Temperatures, fans, SMART and peripheral
// batteries have no Windows source yet and are left out.

namespace Faqra.Core.Metrics;

/// <summary>One sampling tick's readings. Null means "not measured", which the UI shows as a placeholder.</summary>
public sealed record SystemSnapshot
{
    public static readonly SystemSnapshot Empty = new();

    public double? CpuUsage { get; init; }
    public double? GpuUsage { get; init; }

    public ulong? MemoryUsed { get; init; }
    public ulong? MemoryAppUsed { get; init; }
    public ulong? MemoryTotal { get; init; }
    public ulong? MemoryCompressed { get; init; }
    public ulong? MemoryCached { get; init; }
    public ulong? MemorySwapUsed { get; init; }
    public MemoryPressure MemoryPressure { get; init; } = MemoryPressure.Unknown;

    public double? NetDownBytesPerSec { get; init; }
    public double? NetUpBytesPerSec { get; init; }
    public ulong? NetTotalDown { get; init; }
    public ulong? NetTotalUp { get; init; }

    public PowerReading? Power { get; init; }
    public IReadOnlyList<DiskDeviceReading> Disks { get; init; } = [];

    public long UptimeSeconds { get; init; }

    public IReadOnlyList<double> CpuHistory { get; init; } = [];
    public IReadOnlyList<double> GpuHistory { get; init; } = [];
    public IReadOnlyList<double> MemoryHistory { get; init; } = [];
    public IReadOnlyList<double> MemoryAppHistory { get; init; } = [];
    public IReadOnlyList<double> NetDownHistory { get; init; } = [];
    public IReadOnlyList<double> NetUpHistory { get; init; } = [];
    public IReadOnlyList<double> DiskReadHistory { get; init; } = [];
    public IReadOnlyList<double> DiskWriteHistory { get; init; } = [];
    public IReadOnlyList<double> SystemPowerHistory { get; init; } = [];
    public IReadOnlyList<double> BatteryHistory { get; init; } = [];

    /// <summary>The disk upstream calls primary: the system drive, else the first internal one, else the first.</summary>
    public DiskDeviceReading? PrimaryDisk => Disks.FirstOrDefault(d => d.IsSystem) ?? Disks.FirstOrDefault(d => d.IsInternal) ?? Disks.FirstOrDefault();
}

/// <summary>Battery and power draw. Watts are positive while charging and negative while discharging.</summary>
public sealed record PowerReading
{
    public double? SystemWatts { get; init; }
    public double? AdapterWatts { get; init; }
    public double? BatteryWatts { get; init; }
    public int? ChargePercent { get; init; }
    public double? TimeRemainingSeconds { get; init; }
    public double? HealthPercent { get; init; }
    public int? CycleCount { get; init; }
    public bool IsCharging { get; init; }
    public bool ExternalConnected { get; init; }
    public bool HasBattery { get; init; }

    public bool IsEmpty => SystemWatts is null && AdapterWatts is null && BatteryWatts is null && !HasBattery;
}

/// <summary>One mounted volume.</summary>
public sealed record DiskDeviceReading
{
    /// <summary>The volume root, e.g. <c>C:\</c>; stable across ticks.</summary>
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? FileSystem { get; init; }
    public ulong TotalBytes { get; init; }
    public ulong FreeBytes { get; init; }
    public bool IsInternal { get; init; }
    public bool IsSystem { get; init; }
    public double? ReadBytesPerSec { get; init; }
    public double? WriteBytesPerSec { get; init; }
    public ulong? TotalReadBytes { get; init; }
    public ulong? TotalWrittenBytes { get; init; }

    public ulong UsedBytes => TotalBytes > FreeBytes ? TotalBytes - FreeBytes : 0;

    public double UsedFraction => TotalBytes > 0 ? Math.Min(1, Math.Max(0, (double)UsedBytes / TotalBytes)) : 0;
}

/// <summary>What the memory reader returns, before pressure and history are derived.</summary>
public readonly record struct MemorySample(ulong TotalBytes, ulong AvailableBytes, ulong? CompressedBytes, ulong? CachedBytes, ulong? SwapUsedBytes);

/// <summary>What the volume reader returns for one volume, before rates are derived.</summary>
public sealed record VolumeSample(
    string Id,
    string Name,
    string? FileSystem,
    ulong TotalBytes,
    ulong FreeBytes,
    bool IsInternal,
    bool IsSystem,
    DiskIOCounters? Counters);
