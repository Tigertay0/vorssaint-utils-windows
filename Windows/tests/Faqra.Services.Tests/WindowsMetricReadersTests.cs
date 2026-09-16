// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Faqra.Services.Monitor;
using Faqra.Win32.Perf;

namespace Faqra.Services.Tests;

public class WindowsMetricReadersTests
{
    private static NetworkInterfaceReading Nic(bool hardware, bool filter, uint type = 71, ulong received = 100, ulong sent = 10) =>
        new(1, "Wi-Fi", "Adapter", type, IsUp: true, IsHardware: hardware, IsFilter: filter, received, sent);

    [Fact]
    public void OnlyPhysicalAdaptersAreCountedSoFilterDriversDoNotMultiplyTraffic()
    {
        // Measured on the development PC: one Wi-Fi card plus five filter-driver copies of its counters.
        NetworkInterfaceReading[] interfaces =
        [
            Nic(hardware: true, filter: false),
            Nic(hardware: false, filter: true),
            Nic(hardware: false, filter: true),
            Nic(hardware: true, filter: false, type: NetworkInterfaces.TypeSoftwareLoopback, received: 5_000),
            Nic(hardware: false, filter: false, received: 7_000), // a virtual switch re-counting the same traffic
            Nic(hardware: true, filter: false, received: 50, sent: 5),
        ];

        var counters = WindowsMetricReaders.SumPhysicalAdapters(interfaces);

        Assert.Equal(150UL, counters.Received);
        Assert.Equal(15UL, counters.Sent);
    }

    [Fact]
    public void LiveReadersReturnPlausibleValues()
    {
        using var readers = new WindowsMetricReaders();

        readers.ReadCpu();
        Thread.Sleep(300);
        var cpu = readers.ReadCpu();
        var memory = readers.ReadMemory();
        var network = readers.ReadNetwork();
        var volumes = readers.ReadVolumes();
        var power = readers.ReadPower();

        Assert.NotNull(cpu);
        Assert.InRange(cpu.Value, 0, 1);
        Assert.NotNull(memory);
        Assert.InRange(memory.Value.AvailableBytes, 1UL, memory.Value.TotalBytes);
        Assert.NotNull(network);
        var system = Assert.Single(volumes, v => v.IsSystem);
        Assert.True(system.TotalBytes > 0);
        Assert.NotNull(system.Counters);
        Assert.NotNull(power);
        Assert.Equal(readers.HasBattery, power.HasBattery);
        Assert.True(readers.UptimeSeconds() > 0);
    }
}
