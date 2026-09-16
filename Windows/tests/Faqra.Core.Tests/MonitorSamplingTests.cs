using Faqra.Core.Metrics;

namespace Faqra.Core.Tests;

// Cases ported from Tests/MetricsTests.swift; line numbers in comments.
public class MonitorSamplingTests
{
    [Fact]
    public void SampleStrideStretchesBackgroundKinds()
    {
        Assert.Equal(1, MonitorSamplingPolicy.SampleStride(MonitorSamplingKind.Cpu, 2, foreground: false));                // 14665
        Assert.Equal(5, MonitorSamplingPolicy.SampleStride(MonitorSamplingKind.Disk, 2, foreground: false));               // 14667
        Assert.Equal(30, MonitorSamplingPolicy.SampleStride(MonitorSamplingKind.PeripheralBattery, 2, foreground: false)); // 14669
        Assert.Equal(3, MonitorSamplingPolicy.SampleStride(MonitorSamplingKind.FanSpeed, 2, foreground: false));           // 14671
        Assert.Equal(1, MonitorSamplingPolicy.SampleStride(MonitorSamplingKind.Disk, 2, foreground: true));                // 14673
        Assert.Equal(8, MonitorSamplingPolicy.SampleStride(MonitorSamplingKind.Power, 2, foreground: false));
        Assert.Equal(3, MonitorSamplingPolicy.SampleStride(MonitorSamplingKind.PeripheralBattery, 5, foreground: true));
    }

    [Fact]
    public void ShouldSampleOnStrideMultiples()
    {
        Assert.False(MonitorSamplingPolicy.ShouldSample(MonitorSamplingKind.Disk, 4, 2, foreground: false)); // 14675
        Assert.True(MonitorSamplingPolicy.ShouldSample(MonitorSamplingKind.Disk, 5, 2, foreground: false));  // 14677
    }

    [Fact]
    public void WakeTicksIsTheGcdOfNeededStrides()
    {
        Assert.Equal(1, MonitorSamplingPolicy.WakeTicks([MonitorSamplingKind.Cpu, MonitorSamplingKind.Disk], 2, false));             // 14680
        Assert.Equal(8, MonitorSamplingPolicy.WakeTicks([MonitorSamplingKind.Temperature], 2, false));                               // 14682
        Assert.Equal(3, MonitorSamplingPolicy.WakeTicks([MonitorSamplingKind.FanSpeed], 2, false));                                  // 14684
        Assert.Equal(30, MonitorSamplingPolicy.WakeTicks([MonitorSamplingKind.PeripheralBattery], 2, false));                        // 14686
        Assert.Equal(5, MonitorSamplingPolicy.WakeTicks([MonitorSamplingKind.Disk, MonitorSamplingKind.PeripheralBattery], 2, false)); // 14688
        Assert.Equal(1, MonitorSamplingPolicy.WakeTicks([MonitorSamplingKind.Temperature], 2, true));                                // 14690
        Assert.Equal(1, MonitorSamplingPolicy.WakeTicks([], 2, false));                                                              // 14692
    }

    [Fact]
    public void EveryStrideIsAMultipleOfTheCadence()
    {
        MonitorSamplingKind[] kinds =
        [
            MonitorSamplingKind.Disk, MonitorSamplingKind.Power, MonitorSamplingKind.GpuUsage,
            MonitorSamplingKind.Temperature, MonitorSamplingKind.FanSpeed, MonitorSamplingKind.PeripheralBattery,
        ];
        var cadence = MonitorSamplingPolicy.WakeTicks(kinds, 2, false);
        Assert.All(kinds, kind => Assert.Equal(0, MonitorSamplingPolicy.SampleStride(kind, 2, false) % cadence)); // 14696-14701
    }

    [Fact]
    public void AlignedTickRoundsUpToTheCadence()
    {
        Assert.Equal(16, MonitorSamplingPolicy.AlignedTick(16, 8)); // 14702
        Assert.Equal(8, MonitorSamplingPolicy.AlignedTick(7, 8));   // 14704
        Assert.Equal(9, MonitorSamplingPolicy.AlignedTick(9, 1));   // 14706
    }

    [Fact]
    public void MetricHistoryKeepsTheNewestValuesUpToCapacity()
    {
        var history = new MetricHistory(3);
        history.Push(1);
        history.Push(2);
        Assert.Equal([1d, 2d], history.Values);
        history.Push(3);
        history.Push(4);
        Assert.Equal([2d, 3d, 4d], history.Values);
        Assert.Empty(history.PublishedValues(whileVisible: false));
        Assert.Equal([2d, 3d, 4d], history.Values);
        Assert.Equal([2d, 3d, 4d], history.PublishedValues(whileVisible: true));

        var single = new MetricHistory(1);
        single.Push(5);
        single.Push(6);
        Assert.Equal([6d], single.Values); // 14722-14738
    }

    [Fact]
    public void BatteryTimeRemaining()
    {
        Assert.Equal(13_320, BatteryTime.RemainingSeconds(222, externalConnected: false, isCharging: false)); // 819
        Assert.Null(BatteryTime.RemainingSeconds(-1, false, false));      // 823
        Assert.Null(BatteryTime.RemainingSeconds(222, true, false));      // 827
        Assert.Null(BatteryTime.RemainingSeconds(222, false, true));      // 831
        Assert.Null(BatteryTime.RemainingSeconds(65_535, false, false));  // 835
        Assert.Null(BatteryTime.RemainingSeconds(null, false, false));
        Assert.Equal("3h 42m", BatteryTime.Formatted(13_320));            // 839
        Assert.Equal("0h 1m", BatteryTime.Formatted(30));                 // 841
        Assert.Null(BatteryTime.Formatted(0));
        Assert.Null(BatteryTime.Formatted(double.NaN));
    }
}
