using Faqra.Core.Defaults;

namespace Faqra.Core.Tests;

public class DefaultsSanitizersTests
{
    [Theory]
    [InlineData(15, 15)]
    [InlineData(480, 480)]
    [InlineData(0, 0)]
    [InlineData(7, 0)]
    public void DefaultDuration_AcceptsPresetsOnly(int input, int expected) =>
        Assert.Equal(expected, DefaultsSanitizers.DefaultDuration(input));

    [Theory]
    [InlineData(20, 20)]
    [InlineData(0, 0)]
    [InlineData(11, 10)]
    public void BatteryLimit_FallsBackToTen(int input, int expected) =>
        Assert.Equal(expected, DefaultsSanitizers.BatteryLimit(input));

    [Fact]
    public void IntervalsAndCooldowns()
    {
        Assert.Equal(5, DefaultsSanitizers.KeepAwakeMouseJiggleInterval(3));
        Assert.Equal(10, DefaultsSanitizers.KeepAwakeMouseJiggleInterval(10));
        Assert.Equal(2, DefaultsSanitizers.MonitorInterval(3));
        Assert.Equal(5, DefaultsSanitizers.MonitorInterval(5));
        Assert.Equal(15, DefaultsSanitizers.MonitorAlertCooldown(7));
        Assert.Equal(60, DefaultsSanitizers.MonitorAlertCooldown(60));
    }

    [Fact]
    public void MenuBarStringsFallBackToUpstreamDefaults()
    {
        Assert.Equal("dense", DefaultsSanitizers.MenuBarPreset("classic"));
        Assert.Equal("standard", DefaultsSanitizers.MenuBarMetricSpacing("standard"));
        Assert.Equal("compact", DefaultsSanitizers.MenuBarMetricSpacing(null));
        Assert.Equal("bars", DefaultsSanitizers.MenuBarMetricAppearance("bars"));
        Assert.Equal("values", DefaultsSanitizers.MenuBarMetricAppearance("x"));
        Assert.Equal("classic", DefaultsSanitizers.MenuBarLabelStyle("classic"));
        Assert.Equal("percent", DefaultsSanitizers.MenuBarMemoryStyle("nope"));
        Assert.Equal("app", DefaultsSanitizers.MonitorMemoryMetric("app"));
    }

    [Fact]
    public void MenuBarMetricOrder_ExpandsLegacyTokenAndAppendsMissing()
    {
        var order = DefaultsSanitizers.MenuBarMetricOrder("network,temperature,bogus,network");

        Assert.Equal(RegisteredDefaults.DefaultMenuBarMetricOrder.Count, order.Count);
        Assert.Equal(["network", "cpuTemperature", "gpuTemperature", "batteryTemperature", "cpu"], order.Take(5));
        Assert.Equal(RegisteredDefaults.DefaultMenuBarMetricOrder.Order(), order.Order());
    }

    [Fact]
    public void PanelItemOrder_KeepsKnownIdsThenAppendsDefaults()
    {
        var order = DefaultsSanitizers.PanelItemOrder("c,a,c,zz", ["a", "b", "c"]);
        Assert.Equal(["c", "a", "b"], order);
        Assert.Equal(["a", "b", "c"], DefaultsSanitizers.PanelItemOrder(null, ["a", "b", "c"]));
    }

    [Fact]
    public void NumericClampsAndFallbacks()
    {
        Assert.Equal(1.0, DefaultsSanitizers.AppVolume(double.NaN));
        Assert.Equal(2.0, DefaultsSanitizers.AppVolume(9));
        Assert.Equal(0.5, DefaultsSanitizers.AppVolume(0.5));
        Assert.Equal(10, DefaultsSanitizers.MixerHeadphonesDisconnectVolumePercent(3));
        Assert.Equal(100, DefaultsSanitizers.MixerHeadphonesDisconnectVolumePercent(250));
        Assert.Equal(50, DefaultsSanitizers.Percent(50, 10, 0, 100));
        Assert.Equal(10, DefaultsSanitizers.Percent(150, 10, 0, 100));
    }

    [Fact]
    public void EnumRawValuesRoundTripWithUpstreamSpellings()
    {
        Assert.Equal(KeepAwakeIconTint.Orange, DefaultsSanitizers.IconTint(null));
        Assert.Equal(KeepAwakeIconTint.None, DefaultsSanitizers.IconTint("none"));
        Assert.Equal("purple", KeepAwakeIconTint.Purple.RawValue());
        Assert.Equal(KeepAwakeActiveIcon.Brand, DefaultsSanitizers.ActiveIcon("vorssaint"));
        Assert.Equal(KeepAwakeActiveIcon.Brand, DefaultsSanitizers.ActiveIcon("garbage"));
        Assert.Equal("vorssaint", KeepAwakeActiveIcon.Brand.RawValue());
        Assert.Equal("coffee", KeepAwakeActiveIcon.Coffee.RawValue());
        Assert.Equal(AppAppearance.Dark, DefaultsSanitizers.Appearance("dark"));
        Assert.Equal(AppAppearance.System, DefaultsSanitizers.Appearance("blue"));
    }
}
