using System.Globalization;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Localization;
using Faqra.Core.Metrics;
using Faqra.Core.Tray;

namespace Faqra.Core.Tests;

public class TrayMetricsTests
{
    private static readonly CultureInfo Posix = CultureInfo.InvariantCulture;
    private static readonly MonitorStrings S = MonitorStrings.EnUS;

    private static IReadOnlyList<TrayMetric> Enabled(string? order, IEnumerable<string> on, bool hasBattery = false, Func<AppFeature, bool>? available = null)
    {
        var set = on.ToHashSet();
        return TrayMetrics.Enabled(order, key => set.Contains(key), available ?? (_ => true), hasBattery);
    }

    [Fact]
    public void NothingIsInTheTrayByDefault()
    {
        var store = DefaultsStore.InMemory();

        Assert.Empty(TrayMetrics.Enabled(store.String(DefaultsKey.MenuBarMetricOrder), store.Bool, _ => true, hasBattery: true));
    }

    [Fact]
    public void EnabledMetricsFollowTheSavedOrder()
    {
        var metrics = Enabled("network,cpu", [DefaultsKey.MenuBarCPU, DefaultsKey.MenuBarNetwork, DefaultsKey.MenuBarMemory]);

        Assert.Equal([TrayMetric.Network, TrayMetric.Cpu, TrayMetric.Memory], metrics);
    }

    [Fact]
    public void BatteryMetricsNeedABatteryAndMetricsNeedTheirFeature()
    {
        string[] on = [DefaultsKey.MenuBarBattery, DefaultsKey.MenuBarBatteryTime, DefaultsKey.MenuBarPower, DefaultsKey.MenuBarGPU];

        Assert.Equal([TrayMetric.Gpu, TrayMetric.Power], Enabled(null, on, hasBattery: false));
        Assert.Equal([TrayMetric.Battery, TrayMetric.BatteryTime, TrayMetric.Power], Enabled(null, on, hasBattery: true, available: f => f != AppFeature.MonitorGPU));
    }

    [Fact]
    public void TemperatureAndFanMetricsHaveNoWindowsIcon()
    {
        Assert.Empty(Enabled(null, [DefaultsKey.MenuBarCPUTemperature, DefaultsKey.MenuBarFanSpeed, DefaultsKey.MenuBarPeripheralBattery]));
    }

    [Fact]
    public void UsageMetricsShowUpstreamLabelsAndPercentages()
    {
        var snapshot = SystemSnapshot.Empty with
        {
            CpuUsage = 0.234,
            GpuUsage = 0.64,
            MemoryUsed = 79,
            MemoryTotal = 100,
            Disks = [new DiskDeviceReading { Id = @"C:\", Name = "C:", TotalBytes = 100, FreeBytes = 10, IsSystem = true, IsInternal = true }],
        };

        Assert.Equal(new TrayMetricText("CPU", "23%", "CPU: 23%"), TrayMetrics.Text(TrayMetric.Cpu, snapshot, S, "used", Posix));
        Assert.Equal(new TrayMetricText("GPU", "64%", "GPU: 64%"), TrayMetrics.Text(TrayMetric.Gpu, snapshot, S, "used", Posix));
        Assert.Equal(new TrayMetricText("RAM", "79%", "Memory: 79%"), TrayMetrics.Text(TrayMetric.Memory, snapshot, S, "used", Posix));
        Assert.Equal(new TrayMetricText("DSK", "90%", "Disk usage: 90%"), TrayMetrics.Text(TrayMetric.DiskUsage, snapshot, S, "used", Posix));
    }

    [Fact]
    public void RatesUseTheCompactFormOnTwoLines()
    {
        var snapshot = SystemSnapshot.Empty with
        {
            NetDownBytesPerSec = 1.2 * 1024 * 1024,
            NetUpBytesPerSec = 320 * 1024,
            Disks = [new DiskDeviceReading { Id = @"C:\", Name = "C:", ReadBytesPerSec = 2048, WriteBytesPerSec = 0, IsSystem = true }],
        };

        Assert.Equal(new TrayMetricText("↓1.2M", "↑320K", "Network: ↓1.2 MB/s ↑320 KB/s"), TrayMetrics.Text(TrayMetric.Network, snapshot, S, "used", Posix));
        Assert.Equal(new TrayMetricText("R2.0K", "W0B", "Live activity: R 2.0 KB/s W 0 B/s"), TrayMetrics.Text(TrayMetric.DiskActivity, snapshot, S, "used", Posix));
    }

    [Fact]
    public void PowerAndBatteryReadings()
    {
        var snapshot = SystemSnapshot.Empty with
        {
            Power = new PowerReading { HasBattery = true, ChargePercent = 82, SystemWatts = 8.6, TimeRemainingSeconds = 13_320 },
        };

        Assert.Equal(new TrayMetricText("BAT", "82%", "Battery: 82%"), TrayMetrics.Text(TrayMetric.Battery, snapshot, S, "used", Posix));
        Assert.Equal(new TrayMetricText("PWR", "9W", "Power: 8.6 W"), TrayMetrics.Text(TrayMetric.Power, snapshot, S, "used", Posix));
        Assert.Equal(new TrayMetricText("BAT", "3h42m", "Battery time remaining: 3h 42m"), TrayMetrics.Text(TrayMetric.BatteryTime, snapshot, S, "used", Posix));
    }

    [Fact]
    public void AMissingReadingShowsAPlaceholderSoTheIconKeepsItsPlace()
    {
        Assert.Equal(new TrayMetricText("CPU", "-", "CPU: Measuring…"), TrayMetrics.Text(TrayMetric.Cpu, SystemSnapshot.Empty, S, "used", Posix));
        Assert.Equal("--%", TrayMetrics.Text(TrayMetric.Memory, SystemSnapshot.Empty, S, "used", Posix).Bottom);
    }

    [Fact]
    public void EveryMetricOpensTheMatchingPanelSection()
    {
        Assert.Equal(Panel.PanelSectionId.System, TrayMetric.Memory.Section());
        Assert.Equal(Panel.PanelSectionId.Network, TrayMetric.Network.Section());
        Assert.Equal(Panel.PanelSectionId.Disk, TrayMetric.DiskActivity.Section());
        Assert.Equal(Panel.PanelSectionId.Power, TrayMetric.BatteryTime.Section());
    }
}
