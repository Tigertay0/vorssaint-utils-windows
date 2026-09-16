// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the macOS readers behind Sources/Vorssaint/Services/SystemMonitor/SystemMonitor.swift:
// host_processor_info, IOAccelerator, vm_statistics64, getifaddrs, IOBlockStorageDriver and IOPS.

using Faqra.Core.Metrics;
using Faqra.Win32.Perf;
using Faqra.Win32.Power;

namespace Faqra.Services.Monitor;

/// <summary>
/// Reads the live machine through Performance Data Helper counters and Win32. Each source degrades
/// to null on its own, so a missing GPU counter never costs the CPU reading. Single-threaded: the
/// monitor calls it from its sampling thread only.
/// </summary>
public sealed class WindowsMetricReaders : IMetricReaders
{
    private const string CpuCounter = @"\Processor Information(_Total)\% Processor Utility";
    private const string GpuCounter = @"\GPU Engine(*)\Utilization Percentage";

    private static readonly string[] CachedCounters =
    [
        @"\Memory\Standby Cache Core Bytes",
        @"\Memory\Standby Cache Normal Priority Bytes",
        @"\Memory\Standby Cache Reserve Bytes",
        @"\Memory\Modified Page List Bytes",
    ];

    private const string PageFileUsageCounter = @"\Paging File(_Total)\% Usage";
    private const string CompressedCounter = @"\Process(Memory Compression)\Working Set - Private";

    private readonly PdhQuery? _cpuQuery;
    private readonly int _cpu = -1;
    private readonly PdhQuery? _memoryQuery;
    private readonly int[] _cached = [];
    private readonly int _pageFileUsage = -1;
    private readonly int _compressed = -1;
    private readonly string _systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";

    // The GPU query expands one instance per process per engine (hundreds), so it opens on first use.
    private PdhQuery? _gpuQuery;
    private int _gpu = -1;
    private bool _gpuOpened;

    public WindowsMetricReaders()
    {
        _cpuQuery = PdhQuery.Open();
        if (_cpuQuery is not null)
        {
            _cpu = _cpuQuery.AddCounter(CpuCounter);
        }
        _memoryQuery = PdhQuery.Open();
        if (_memoryQuery is not null)
        {
            _cached = CachedCounters.Select(_memoryQuery.AddCounter).Where(id => id >= 0).ToArray();
            _pageFileUsage = _memoryQuery.AddCounter(PageFileUsageCounter);
            _compressed = _memoryQuery.AddCounter(CompressedCounter);
        }
        HasBattery = Power.Details().HasBattery;
    }

    public bool HasBattery { get; }

    public double? ReadCpu()
    {
        if (_cpuQuery is null || _cpu < 0 || !_cpuQuery.Collect())
        {
            return null;
        }
        // Processor Utility runs past 100% while the CPU turbo-boosts; Task Manager caps it too.
        return _cpuQuery.ReadDouble(_cpu) is { } percent ? Math.Clamp(percent / 100, 0, 1) : null;
    }

    public double? ReadGpu()
    {
        if (!_gpuOpened)
        {
            _gpuOpened = true;
            _gpuQuery = PdhQuery.Open();
            _gpu = _gpuQuery?.AddCounter(GpuCounter) ?? -1;
        }
        if (_gpuQuery is null || _gpu < 0 || !_gpuQuery.Collect())
        {
            return null;
        }
        return GpuUsage.FromEngineInstances(_gpuQuery.ReadInstances(_gpu).Select(instance => (instance.Name, instance.Value)));
    }

    public MemorySample? ReadMemory()
    {
        if (MemoryStatus.Read() is not { } status)
        {
            return null;
        }
        ulong? cached = null, swap = null, compressed = null;
        if (_memoryQuery is not null && _memoryQuery.Collect())
        {
            var parts = _cached.Select(_memoryQuery.ReadDouble).ToList();
            if (parts.Count > 0 && parts.All(part => part is not null))
            {
                cached = (ulong)parts.Sum(part => part!.Value);
            }
            if (_memoryQuery.ReadDouble(_pageFileUsage) is { } usage && status.CommitLimitBytes > status.TotalBytes)
            {
                swap = (ulong)(usage / 100 * (status.CommitLimitBytes - status.TotalBytes));
            }
            if (_memoryQuery.ReadDouble(_compressed) is { } bytes)
            {
                compressed = (ulong)bytes;
            }
        }
        return new MemorySample(status.TotalBytes, status.AvailableBytes, compressed, cached, swap);
    }

    public NetworkCounters? ReadNetwork()
    {
        var interfaces = NetworkInterfaces.Read();
        return interfaces.Count == 0 ? null : SumPhysicalAdapters(interfaces);
    }

    /// <summary>
    /// Sums the physical adapters only. Windows lists every filter driver (QoS, WFP, Wi-Fi filters)
    /// and virtual switch as its own interface carrying a copy of the same counters, so summing
    /// everything would count one download five or six times.
    /// </summary>
    public static NetworkCounters SumPhysicalAdapters(IEnumerable<NetworkInterfaceReading> interfaces)
    {
        ulong received = 0, sent = 0;
        foreach (var nic in interfaces)
        {
            if (!nic.IsHardware || nic.IsFilter || nic.Type is NetworkInterfaces.TypeSoftwareLoopback or NetworkInterfaces.TypeTunnel)
            {
                continue;
            }
            received = Saturating(received, nic.InOctets);
            sent = Saturating(sent, nic.OutOctets);
        }
        return new NetworkCounters(received, sent);
    }

    public IReadOnlyList<VolumeSample> ReadVolumes()
    {
        var volumes = new List<VolumeSample>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable) || VolumeFor(drive) is not { } volume)
            {
                continue;
            }
            volumes.Add(volume);
        }
        return volumes;
    }

    private VolumeSample? VolumeFor(DriveInfo drive)
    {
        try
        {
            if (!drive.IsReady || DiskSpace.Read(drive.Name) is not { } space)
            {
                return null;
            }
            var letter = drive.Name.TrimEnd('\\');
            var label = drive.VolumeLabel;
            var counters = DiskPerformance.Read(letter) is { } io ? new DiskIOCounters(io.Read, io.Written) : (DiskIOCounters?)null;
            return new VolumeSample(
                drive.Name,
                string.IsNullOrWhiteSpace(label) ? letter : $"{label} ({letter})",
                drive.DriveFormat,
                space.TotalBytes,
                space.FreeBytes,
                IsInternal: drive.DriveType == DriveType.Fixed,
                IsSystem: string.Equals(drive.Name, _systemRoot, StringComparison.OrdinalIgnoreCase),
                counters);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Media ejected between enumeration and the query; the next tick will not list it.
            return null;
        }
    }

    public PowerReading? ReadPower()
    {
        var details = Power.Details();
        int? minutes = details.SecondsRemaining is { } seconds ? seconds / 60 : null;
        return new PowerReading
        {
            HasBattery = details.HasBattery,
            ExternalConnected = details.OnMains,
            IsCharging = details.Charging,
            ChargePercent = details.Percent,
            BatteryWatts = details.RateMilliwatts is { } rate ? rate / 1000d : null,
            TimeRemainingSeconds = BatteryTime.RemainingSeconds(minutes, details.OnMains, details.Charging),
        };
    }

    public long UptimeSeconds() => Environment.TickCount64 / 1000;

    private static ulong Saturating(ulong total, ulong add) => ulong.MaxValue - total < add ? ulong.MaxValue : total + add;

    public void Dispose()
    {
        _cpuQuery?.Dispose();
        _memoryQuery?.Dispose();
        _gpuQuery?.Dispose();
    }
}
