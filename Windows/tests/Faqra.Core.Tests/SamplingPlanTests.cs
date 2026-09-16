using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Metrics;

namespace Faqra.Core.Tests;

public class SamplingPlanTests
{
    private static readonly HashSet<string> PanelDefaults =
    [
        DefaultsKey.MonitorSysCPU, DefaultsKey.MonitorSysGPU, DefaultsKey.MonitorSysMemory, DefaultsKey.MonitorSysBattery,
    ];

    private static SamplingPlan Plan(
        PanelNeeds needs = default,
        bool notchVisible = false,
        IEnumerable<string>? on = null,
        Func<AppFeature, bool>? available = null,
        bool hasBattery = false)
    {
        var enabled = new HashSet<string>(on ?? PanelDefaults);
        return SamplingPlan.Build(needs, notchVisible, key => enabled.Contains(key), available ?? (_ => true), hasBattery);
    }

    [Fact]
    public void NothingVisibleAndNoTrayMetricsSamplesNothing()
    {
        var plan = Plan();

        Assert.False(plan.Any);
        Assert.False(plan.Foreground);
        Assert.Empty(plan.NeededKinds());
    }

    [Fact]
    public void TheSystemSectionSamplesItsSwitchedOnReadings()
    {
        var plan = Plan(new PanelNeeds(System: true), on: [DefaultsKey.MonitorSysCPU, DefaultsKey.MonitorSysMemory]);

        Assert.True(plan.NeedCpu);
        Assert.True(plan.NeedMemory);
        Assert.False(plan.NeedGpu);
        Assert.False(plan.NeedNetwork);
        Assert.True(plan.Foreground);
    }

    [Fact]
    public void TheIslandSamplesEverythingItsCardsShow()
    {
        var plan = Plan(notchVisible: true, on: []);

        Assert.True(plan.NeedCpu && plan.NeedGpu && plan.NeedMemory && plan.NeedNetwork && plan.NeedPower && plan.NeedDisk);
        Assert.True(plan.Foreground);
    }

    [Fact]
    public void TrayMetricsAloneSampleInTheBackground()
    {
        var plan = Plan(on: [DefaultsKey.MenuBarNetwork, DefaultsKey.MenuBarDiskActivity]);

        Assert.True(plan.NeedNetwork);
        Assert.True(plan.NeedDisk);
        Assert.False(plan.NeedCpu);
        Assert.False(plan.Foreground);
    }

    [Fact]
    public void BatteryTrayMetricsNeedABattery()
    {
        Assert.False(Plan(on: [DefaultsKey.MenuBarBattery], hasBattery: false).NeedPower);
        Assert.True(Plan(on: [DefaultsKey.MenuBarBattery], hasBattery: true).NeedPower);
        Assert.True(Plan(on: [DefaultsKey.MenuBarPower], hasBattery: false).NeedPower);
    }

    [Fact]
    public void AnUninstalledFeatureIsNeverSampled()
    {
        var plan = Plan(notchVisible: true, available: feature => feature != AppFeature.MonitorGPU && feature != AppFeature.MonitorPower);

        Assert.False(plan.NeedGpu);
        Assert.False(plan.NeedPower);
        Assert.True(plan.NeedCpu);
    }

    [Fact]
    public void NeededKindsFollowUpstreamOrder()
    {
        var plan = Plan(notchVisible: true);

        Assert.Equal(
            [MonitorSamplingKind.Cpu, MonitorSamplingKind.Memory, MonitorSamplingKind.Network, MonitorSamplingKind.Disk,
                MonitorSamplingKind.Power, MonitorSamplingKind.GpuUsage],
            plan.NeededKinds());
    }

    [Fact]
    public void GpuUsageIsTheBusiestEngineSummedAcrossProcesses()
    {
        (string, double)[] instances =
        [
            ("pid_100_luid_0x00000000_0x0000EAB6_phys_0_eng_0_engtype_3D", 30),
            ("pid_200_luid_0x00000000_0x0000EAB6_phys_0_eng_0_engtype_3D", 25),
            ("pid_100_luid_0x00000000_0x0000EAB6_phys_0_eng_4_engtype_VideoDecode", 70),
            ("pid_300_luid_0x00000000_0x0000F001_phys_0_eng_0_engtype_3D", 10),
        ];

        Assert.Equal(0.70, GpuUsage.FromEngineInstances(instances)!.Value, 4);
        Assert.Equal(0.55, GpuUsage.FromEngineInstances(instances.Take(2))!.Value, 4);
        Assert.Null(GpuUsage.FromEngineInstances([]));
        Assert.Equal(1.0, GpuUsage.FromEngineInstances([("pid_1_luid_0x0_0x1_phys_0_eng_0_engtype_3D", 60), ("pid_2_luid_0x0_0x1_phys_0_eng_0_engtype_3D", 60)])!.Value, 4);
    }

    [Theory]
    [InlineData(16_000UL, 8_000UL, MemoryPressure.Normal)]
    [InlineData(16_000UL, 2_400UL, MemoryPressure.Normal)]    // exactly 15% free
    [InlineData(16_000UL, 2_000UL, MemoryPressure.Warning)]   // 12.5% free
    [InlineData(16_000UL, 700UL, MemoryPressure.Critical)]    // under 5% free
    [InlineData(0UL, 0UL, MemoryPressure.Unknown)]
    public void MemoryPressureFromFreePhysicalMemory(ulong total, ulong available, MemoryPressure expected)
    {
        Assert.Equal(expected, MemoryPressures.FromMemory(total, available));
    }

    [Fact]
    public void ANearlyFullCommitChargeIsNotPressure()
    {
        // Measured on the development PC: 34 GB of RAM with 9 GB free while the commit charge sat at
        // 99% of a limit Windows grows on demand. That machine is healthy, so commit is not an input.
        Assert.Equal(MemoryPressure.Normal, MemoryPressures.FromMemory(34_150_000_000, 9_060_000_000));
    }
}
