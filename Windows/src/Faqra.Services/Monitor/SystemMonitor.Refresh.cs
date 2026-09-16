// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors refresh() and stabilizedMemoryReading in Sources/Vorssaint/Services/SystemMonitor/SystemMonitor.swift
// (lines 646-944).

using System.Diagnostics;
using Faqra.Core.Metrics;

namespace Faqra.Services.Monitor;

public sealed partial class SystemMonitor
{
    private readonly MetricHistory _cpuHistory = new();
    private readonly MetricHistory _gpuHistory = new();
    private readonly MetricHistory _memoryHistory = new();
    private readonly MetricHistory _memoryAppHistory = new();
    private readonly MetricHistory _netDownHistory = new();
    private readonly MetricHistory _netUpHistory = new();
    private readonly MetricHistory _diskReadHistory = new();
    private readonly MetricHistory _diskWriteHistory = new();
    private readonly MetricHistory _systemPowerHistory = new();
    private readonly MetricHistory _batteryHistory = new();
    private readonly NetworkRateTracker _network = new();
    private readonly DiskRateTracker _disk = new();

    private double? _lastCpu;
    private int _missedCpu;
    private double? _lastGpu;
    private int _missedGpu;
    private (MemorySample Sample, double UpdatedAt, int Missed)? _memory;
    private IReadOnlyList<DiskDeviceReading> _lastDisks = [];
    private PowerReading? _lastPower;

    /// <summary>
    /// One sampling tick at monotonic time <paramref name="now"/> seconds. Every reading due on this
    /// tick is taken; the rest carry their last value forward. Returns the published snapshot.
    /// </summary>
    internal SystemSnapshot Refresh(double now)
    {
        SystemSnapshot next;
        lock (_gate)
        {
            var plan = CurrentPlan();
            if (_disposed || !plan.Any)
            {
                return Snapshot;
            }
            SyncCadence(plan);
            var interval = Interval();
            var tick = _tick;
            _tick += _wakeTicks;
            bool Take(MonitorSamplingKind kind) => MonitorSamplingPolicy.ShouldSample(kind, tick, interval, plan.Foreground);

            next = SystemSnapshot.Empty with { UptimeSeconds = _readers.UptimeSeconds() };
            if (plan.NeedCpu)
            {
                next = next with { CpuUsage = SampleCpu(Take(MonitorSamplingKind.Cpu)) };
            }
            if (plan.NeedMemory && Take(MonitorSamplingKind.Memory))
            {
                next = WithMemory(next, now);
            }
            if (plan.NeedNetwork && Take(MonitorSamplingKind.Network))
            {
                next = WithNetwork(next, now);
            }
            if (plan.NeedDisk)
            {
                next = next with { Disks = Take(MonitorSamplingKind.Disk) ? SampleDisks(now) : _lastDisks };
            }
            if (plan.NeedPower)
            {
                next = next with { Power = Take(MonitorSamplingKind.Power) ? SamplePower() : _lastPower };
            }
            if (plan.NeedGpu)
            {
                next = next with { GpuUsage = SampleGpu(Take(MonitorSamplingKind.GpuUsage)) };
            }
            next = WithHistories(next, plan);
            Snapshot = next;
        }
        Publish(next);
        return next;
    }

    /// <summary>
    /// Delivers to each subscriber on its own, so a consumer that throws (a dispatcher shutting down at
    /// exit, say) cannot keep the tray icons, the panel or the island from getting the tick.
    /// </summary>
    private void Publish(SystemSnapshot snapshot)
    {
        if (SnapshotChanged is not { } handlers)
        {
            return;
        }
        foreach (var handler in handlers.GetInvocationList().Cast<Action<SystemSnapshot>>())
        {
            try
            {
                handler(snapshot);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"SystemMonitor subscriber {handler.Method.DeclaringType?.Name}.{handler.Method.Name} failed: {ex}");
            }
        }
    }

    private double? SampleCpu(bool take)
    {
        if (take && _readers.ReadCpu() is { } cpu)
        {
            _lastCpu = Math.Clamp(cpu, 0, 1);
            _missedCpu = 0;
            _cpuHistory.Push(_lastCpu.Value);
        }
        else if (_missedCpu < MaxBridgedMisses)
        {
            _missedCpu++;
        }
        else
        {
            _lastCpu = null;
        }
        return _lastCpu;
    }

    /// <summary>A GPU reading only counts as missed on ticks that actually tried to read it.</summary>
    private double? SampleGpu(bool take)
    {
        if (!take)
        {
            return _lastGpu;
        }
        if (_readers.ReadGpu() is { } raw)
        {
            _lastGpu = MetricFormat.StabilizedGpuUsage(_lastGpu, raw);
            _missedGpu = 0;
            _gpuHistory.Push(_lastGpu.Value);
        }
        else if (_missedGpu < MaxBridgedMisses)
        {
            _missedGpu++;
        }
        else
        {
            _lastGpu = null;
        }
        return _lastGpu;
    }

    /// <summary>A failed memory read briefly shows the last good one instead of flashing blank.</summary>
    private SystemSnapshot WithMemory(SystemSnapshot next, double now)
    {
        var fresh = false;
        if (_readers.ReadMemory() is { TotalBytes: > 0 } sample)
        {
            _memory = (sample, now, 0);
            fresh = true;
        }
        else if (_memory is { } held)
        {
            var missed = held.Missed + 1;
            _memory = missed > MaxMemoryMisses || now - held.UpdatedAt > MaxMemoryAgeSeconds ? null : held with { Missed = missed };
        }
        if (_memory is not { Sample: var memory })
        {
            return next;
        }

        var used = memory.TotalBytes - Math.Min(memory.AvailableBytes, memory.TotalBytes);
        if (fresh)
        {
            _memoryHistory.Push((double)used / memory.TotalBytes);
            _memoryAppHistory.Push((double)used / memory.TotalBytes);
        }
        return next with
        {
            MemoryUsed = used,
            // Windows has no Activity Monitor style "app memory"; both measures read as in-use memory.
            MemoryAppUsed = used,
            MemoryTotal = memory.TotalBytes,
            MemoryCompressed = memory.CompressedBytes,
            MemoryCached = memory.CachedBytes,
            MemorySwapUsed = memory.SwapUsedBytes,
            MemoryPressure = MemoryPressures.FromMemory(memory.TotalBytes, memory.AvailableBytes),
        };
    }

    private SystemSnapshot WithNetwork(SystemSnapshot next, double now)
    {
        var rates = _network.Sample(_readers.ReadNetwork(), now);
        if (rates.DownBytesPerSec is { } down)
        {
            _netDownHistory.Push(down);
        }
        if (rates.UpBytesPerSec is { } up)
        {
            _netUpHistory.Push(up);
        }
        return next with
        {
            NetDownBytesPerSec = rates.DownBytesPerSec,
            NetUpBytesPerSec = rates.UpBytesPerSec,
            NetTotalDown = rates.TotalDown,
            NetTotalUp = rates.TotalUp,
        };
    }

    private IReadOnlyList<DiskDeviceReading> SampleDisks(double now)
    {
        var disks = new List<DiskDeviceReading>();
        foreach (var volume in _readers.ReadVolumes())
        {
            DiskRates? rates = volume.Counters is { } counters ? _disk.Sample(volume.Id, counters, now) : null;
            disks.Add(new DiskDeviceReading
            {
                Id = volume.Id,
                Name = volume.Name,
                FileSystem = volume.FileSystem,
                TotalBytes = volume.TotalBytes,
                FreeBytes = Math.Min(volume.FreeBytes, volume.TotalBytes),
                IsInternal = volume.IsInternal,
                IsSystem = volume.IsSystem,
                ReadBytesPerSec = rates?.ReadBytesPerSec,
                WriteBytesPerSec = rates?.WriteBytesPerSec,
                TotalReadBytes = rates?.TotalRead,
                TotalWrittenBytes = rates?.TotalWritten,
            });
        }
        // Upstream's order: internal first, the system volume first, then by name.
        _lastDisks = disks
            .OrderByDescending(d => d.IsInternal)
            .ThenByDescending(d => d.IsSystem)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var reads = _lastDisks.Where(d => d.ReadBytesPerSec is not null).Select(d => d.ReadBytesPerSec!.Value).ToList();
        var writes = _lastDisks.Where(d => d.WriteBytesPerSec is not null).Select(d => d.WriteBytesPerSec!.Value).ToList();
        if (reads.Count > 0)
        {
            _diskReadHistory.Push(reads.Sum());
        }
        if (writes.Count > 0)
        {
            _diskWriteHistory.Push(writes.Sum());
        }
        return _lastDisks;
    }

    private PowerReading? SamplePower()
    {
        if (_readers.ReadPower() is not { } reading)
        {
            return _lastPower;
        }
        var power = reading with
        {
            SystemWatts = MetricFormat.SystemPowerWatts(reading.SystemWatts, reading.BatteryWatts, reading.ExternalConnected),
        };
        _lastPower = power;
        if (power.SystemWatts is { } watts)
        {
            _systemPowerHistory.Push(watts);
        }
        if (power.ChargePercent is { } charge)
        {
            _batteryHistory.Push(charge / 100d);
        }
        return power;
    }

    private SystemSnapshot WithHistories(SystemSnapshot next, SamplingPlan plan)
    {
        var visible = plan.Foreground;
        IReadOnlyList<double> Published(bool needed, MetricHistory history) => needed ? history.PublishedValues(visible) : [];
        return next with
        {
            CpuHistory = Published(plan.NeedCpu, _cpuHistory),
            GpuHistory = Published(plan.NeedGpu, _gpuHistory),
            MemoryHistory = Published(plan.NeedMemory, _memoryHistory),
            MemoryAppHistory = Published(plan.NeedMemory, _memoryAppHistory),
            NetDownHistory = Published(plan.NeedNetwork, _netDownHistory),
            NetUpHistory = Published(plan.NeedNetwork, _netUpHistory),
            DiskReadHistory = Published(plan.NeedDisk, _diskReadHistory),
            DiskWriteHistory = Published(plan.NeedDisk, _diskWriteHistory),
            SystemPowerHistory = Published(plan.NeedPower, _systemPowerHistory),
            BatteryHistory = Published(plan.NeedPower, _batteryHistory),
        };
    }
}
