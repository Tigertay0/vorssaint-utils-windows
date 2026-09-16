// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Faqra.Core.Defaults;
using Faqra.Core.Metrics;
using Faqra.Services.Monitor;

namespace Faqra.Services.Tests;

public class SystemMonitorTests
{
    private sealed class FakeReaders : IMetricReaders
    {
        public Queue<double?> Cpu { get; } = new();
        public Queue<double?> Gpu { get; } = new();
        public Queue<NetworkCounters?> Network { get; } = new();
        public MemorySample? Memory { get; set; } = new(16_000, 4_000, 500, 3_000, 100);
        public List<VolumeSample> Volumes { get; set; } = [];
        public PowerReading? Power { get; set; }
        public int CpuReads { get; private set; }
        public int VolumeReads { get; private set; }

        public bool HasBattery { get; set; }

        public double? ReadCpu()
        {
            CpuReads++;
            return Cpu.Count > 0 ? Cpu.Dequeue() : null;
        }

        public double? ReadGpu() => Gpu.Count > 0 ? Gpu.Dequeue() : null;

        public MemorySample? ReadMemory() => Memory;

        public NetworkCounters? ReadNetwork() => Network.Count > 0 ? Network.Dequeue() : null;

        public IReadOnlyList<VolumeSample> ReadVolumes()
        {
            VolumeReads++;
            return Volumes;
        }

        public PowerReading? ReadPower() => Power;

        public long UptimeSeconds() => 3_600;

        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void DisposeReleasesTheReadersAndStopsPublishing()
    {
        var (monitor, readers, _) = Create();
        monitor.SetPanelNeeds(new PanelNeeds(System: true));
        var published = 0;
        monitor.SnapshotChanged += _ => published++;

        monitor.Dispose();
        monitor.Refresh(0);

        Assert.True(readers.Disposed);
        Assert.Equal(0, published);
        Assert.Equal(0, readers.CpuReads);
    }

    [Fact]
    public void AThrowingSubscriberDoesNotStarveTheOthers()
    {
        var (monitor, readers, _) = Create();
        monitor.SetPanelNeeds(new PanelNeeds(System: true));
        readers.Cpu.Enqueue(0.5);
        SystemSnapshot? received = null;
        monitor.SnapshotChanged += _ => throw new InvalidOperationException("Dispatcher has shut down");
        monitor.SnapshotChanged += snapshot => received = snapshot;

        monitor.Refresh(0);

        Assert.Equal(0.5, received?.CpuUsage);
    }

    private static (SystemMonitor Monitor, FakeReaders Readers, DefaultsStore Store) Create()
    {
        var store = DefaultsStore.InMemory();
        var readers = new FakeReaders();
        var monitor = new SystemMonitor(readers, store, _ => true, useTimer: false);
        return (monitor, readers, store);
    }

    [Fact]
    public void NothingOnScreenReadsNothing()
    {
        var (monitor, readers, _) = Create();

        var snapshot = monitor.Refresh(now: 0);

        Assert.Equal(0, readers.CpuReads);
        Assert.Null(snapshot.CpuUsage);
    }

    [Fact]
    public void CpuKeepsItsLastValueThroughThreeMissedReads()
    {
        var (monitor, readers, _) = Create();
        monitor.SetPanelNeeds(new PanelNeeds(System: true));
        foreach (var value in new double?[] { 0.4, null, null, null, null })
        {
            readers.Cpu.Enqueue(value);
        }

        double?[] seen = [.. Enumerable.Range(0, 5).Select(i => monitor.Refresh(i).CpuUsage)];

        Assert.Equal([0.4, 0.4, 0.4, 0.4, null], seen);
    }

    [Fact]
    public void HistoriesArePublishedOnlyWhileAPanelIsOpen()
    {
        var (monitor, readers, store) = Create();
        store.Set(DefaultsKey.MenuBarCPU, true);
        readers.Cpu.Enqueue(0.1);
        readers.Cpu.Enqueue(0.2);

        var tray = monitor.Refresh(0);
        monitor.SetPanelNeeds(new PanelNeeds(System: true));
        var panel = monitor.Refresh(1);

        Assert.Equal(0.1, tray.CpuUsage);
        Assert.Empty(tray.CpuHistory);
        Assert.Equal([0.1, 0.2], panel.CpuHistory);
    }

    [Fact]
    public void GpuReadingsAreStabilized()
    {
        var (monitor, readers, _) = Create();
        monitor.SetPanelNeeds(new PanelNeeds(System: true));
        readers.Gpu.Enqueue(0.03);
        readers.Gpu.Enqueue(0.80);

        monitor.Refresh(0);
        var second = monitor.Refresh(1);

        Assert.Equal(0.23, second.GpuUsage!.Value, 4);
    }

    [Fact]
    public void MemoryUsedIsTotalMinusAvailableWithDerivedPressure()
    {
        var (monitor, readers, _) = Create();
        monitor.SetPanelNeeds(new PanelNeeds(System: true));
        readers.Memory = new MemorySample(16_000, 1_000, 500, 3_000, 100);

        var snapshot = monitor.Refresh(0);

        Assert.Equal(15_000UL, snapshot.MemoryUsed);
        Assert.Equal(16_000UL, snapshot.MemoryTotal);
        Assert.Equal(3_000UL, snapshot.MemoryCached);
        Assert.Equal(MemoryPressure.Warning, snapshot.MemoryPressure);
        Assert.Equal([15_000d / 16_000], snapshot.MemoryHistory);
    }

    [Fact]
    public void NetworkRatesComeFromCumulativeCounters()
    {
        var (monitor, readers, _) = Create();
        monitor.SetPanelNeeds(new PanelNeeds(Network: true));
        readers.Network.Enqueue(new NetworkCounters(1_000, 500));
        readers.Network.Enqueue(new NetworkCounters(3_048, 1_524));

        var first = monitor.Refresh(0);
        var second = monitor.Refresh(2);

        Assert.Null(first.NetDownBytesPerSec);
        Assert.Equal(1_024, second.NetDownBytesPerSec);
        Assert.Equal(512, second.NetUpBytesPerSec);
        Assert.Equal(2_048UL, second.NetTotalDown);
        Assert.Equal([1_024d], second.NetDownHistory);
    }

    [Fact]
    public void DiskRatesArePerVolumeAndTheSystemDriveIsPrimary()
    {
        var (monitor, readers, _) = Create();
        monitor.SetPanelNeeds(new PanelNeeds(Disk: true));
        readers.Volumes = [new VolumeSample(@"D:\", "Data", "NTFS", 2_000, 1_500, true, false, new DiskIOCounters(0, 0)),
            new VolumeSample(@"C:\", "Windows", "NTFS", 1_000, 250, true, true, new DiskIOCounters(1_000, 500))];
        monitor.Refresh(0);
        readers.Volumes = [readers.Volumes[0] with { Counters = new DiskIOCounters(100, 0) },
            readers.Volumes[1] with { Counters = new DiskIOCounters(3_048, 1_524) }];

        var snapshot = monitor.Refresh(2);

        var system = snapshot.PrimaryDisk!;
        Assert.Equal(@"C:\", system.Id);
        Assert.Equal(1_024, system.ReadBytesPerSec);
        Assert.Equal(0.75, system.UsedFraction, 4);
        Assert.Equal(50, snapshot.Disks.Single(d => d.Id == @"D:\").ReadBytesPerSec);
        Assert.Equal([1_074d], snapshot.DiskReadHistory);
    }

    [Fact]
    public void BackgroundDiskReadingsSlowDownAndCarryOver()
    {
        var (monitor, readers, store) = Create();
        // CPU keeps the timer at one tick; alone, disk would simply wake the timer every 10 s.
        store.Set(DefaultsKey.MenuBarCPU, true);
        store.Set(DefaultsKey.MenuBarDiskUsage, true);
        readers.Volumes = [new VolumeSample(@"C:\", "Windows", "NTFS", 1_000, 250, true, true, null)];

        var snapshots = Enumerable.Range(0, 5).Select(i => monitor.Refresh(i * 2)).ToList();

        // Interval 2 s in the background samples disk every 5th tick, and carries the reading between.
        Assert.Equal(1, readers.VolumeReads);
        Assert.All(snapshots, snapshot => Assert.Single(snapshot.Disks));
    }

    [Fact]
    public void SystemPowerFallsBackToBatteryDrain()
    {
        var (monitor, readers, _) = Create();
        monitor.SetPanelNeeds(new PanelNeeds(Power: true));
        readers.HasBattery = true;
        readers.Power = new PowerReading { HasBattery = true, BatteryWatts = -9, ChargePercent = 80 };

        var snapshot = monitor.Refresh(0);

        Assert.Equal(9, snapshot.Power!.SystemWatts);
        Assert.Equal([9d], snapshot.SystemPowerHistory);
        Assert.Equal([0.8], snapshot.BatteryHistory);
    }

    [Fact]
    public void SnapshotChangedFiresOnRefresh()
    {
        var (monitor, readers, _) = Create();
        monitor.SetPanelNeeds(new PanelNeeds(System: true));
        readers.Cpu.Enqueue(0.5);
        SystemSnapshot? published = null;
        monitor.SnapshotChanged += snapshot => published = snapshot;

        monitor.Refresh(0);

        Assert.Equal(0.5, published?.CpuUsage);
        Assert.Same(published, monitor.Snapshot);
    }
}
