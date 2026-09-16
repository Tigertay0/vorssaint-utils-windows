using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Panel;

namespace Faqra.Core.Tests;

public class PanelLayoutTests
{
    private static string Joined(IEnumerable<PanelSectionId> ids) => string.Join(",", ids.Select(id => id.RawValue()));

    [Fact]
    public void ElevenSectionsWithStableRawValuesInCanonicalOrder()
    {
        Assert.Equal(
            "keepAwake,brightness,mixer,system,network,disk,power,fanControl,utilities,controls,toggles",
            Joined(PanelSections.All));
        foreach (var id in PanelSections.All)
        {
            Assert.Equal(id, PanelSections.FromRawValue(id.RawValue()));
            Assert.False(string.IsNullOrEmpty(id.Glyph()));
        }
        Assert.Null(PanelSections.FromRawValue("System"));
    }

    [Fact]
    public void VisibilityKeysMatchUpstream()
    {
        Assert.Equal(DefaultsKey.PanelShowKeepAwake, PanelSectionId.KeepAwake.VisibilityKey());
        Assert.Equal(DefaultsKey.MonitorShowMixer, PanelSectionId.Mixer.VisibilityKey());
        Assert.Equal(DefaultsKey.MonitorShowSystem, PanelSectionId.System.VisibilityKey());
        Assert.Equal(DefaultsKey.MonitorShowNetwork, PanelSectionId.Network.VisibilityKey());
        Assert.Equal(DefaultsKey.MonitorShowDisk, PanelSectionId.Disk.VisibilityKey());
        Assert.Equal(DefaultsKey.MonitorShowPower, PanelSectionId.Power.VisibilityKey());
        Assert.Equal(DefaultsKey.PanelShowFanControl, PanelSectionId.FanControl.VisibilityKey());
        Assert.Equal(DefaultsKey.PanelShowToggles, PanelSectionId.Toggles.VisibilityKey());
    }

    [Fact]
    public void SystemIsAvailableWithAnyOfItsThreeMetrics()
    {
        Assert.True(PanelSectionId.System.IsAvailable(feature => feature == AppFeature.MonitorMemory));
        Assert.False(PanelSectionId.System.IsAvailable(feature => feature == AppFeature.MonitorDisk));
        Assert.True(PanelSectionId.Disk.IsAvailable(feature => feature == AppFeature.MonitorDisk));
        Assert.True(PanelSectionId.Toggles.IsAvailable(feature => feature == AppFeature.MicMute));
    }

    [Theory]
    [InlineData(null, "keepAwake,brightness,mixer,system,network,disk,power,fanControl,utilities,controls,toggles")]
    [InlineData("", "keepAwake,brightness,mixer,system,network,disk,power,fanControl,utilities,controls,toggles")]
    [InlineData("power,network", "power,network,disk,keepAwake,brightness,mixer,system,fanControl,utilities,controls,toggles")]
    [InlineData("keepAwake,mixer", "keepAwake,brightness,mixer,system,network,disk,power,fanControl,utilities,controls,toggles")]
    [InlineData("utilities,bogus,utilities", "utilities,controls,keepAwake,brightness,mixer,system,network,disk,power,fanControl,toggles")]
    [InlineData(
        "toggles,mixer,network,utilities,keepAwake,system,power,fanControl",
        "toggles,mixer,network,disk,utilities,controls,keepAwake,brightness,system,power,fanControl")]
    public void OrderKeepsSavedIdsThenSlotsMissingOnesIntoTheirCanonicalPlace(string? saved, string expected)
    {
        Assert.Equal(expected, Joined(PanelLayout.Order(saved)));
    }

    [Fact]
    public void OrderMatchesExactRawValuesWithoutTrimming()
    {
        // Upstream splits on commas and matches exact raw values; " power" is not a section.
        Assert.Equal(PanelSectionId.KeepAwake, PanelLayout.Order(" power")[0]);
    }

    [Fact]
    public void SwapExchangesTwoSectionsAndLeavesTheRestInPlace()
    {
        var order = PanelLayout.Order(null);

        var swapped = PanelLayout.Swap(order, PanelSectionId.Power, PanelSectionId.System);

        Assert.Equal("keepAwake,brightness,mixer,power,network,disk,system,fanControl,utilities,controls,toggles", Joined(swapped));
        Assert.Equal(order, PanelLayout.Swap(order, PanelSectionId.Power, PanelSectionId.Power));
    }

    [Fact]
    public void SerializeJoinsRawValues()
    {
        Assert.Equal("system,disk", PanelLayout.Serialize([PanelSectionId.System, PanelSectionId.Disk]));
    }

    [Fact]
    public void ItemOrderKeepsSavedValidItemsFirstAndAppendsDefaults()
    {
        // Tests/MetricsTests.swift:6103-6106
        Assert.Equal(
            ["uninstaller", "homebrew", "media", "cleanURL", "cleaning"],
            DefaultsSanitizers.PanelItemOrder("uninstaller,homebrew,homebrew,bad", ["homebrew", "media", "uninstaller", "cleanURL", "cleaning"]));
    }

    [Fact]
    public void VisibleDropsUnavailableHiddenAndUnbuiltSections()
    {
        var order = PanelLayout.Order(null);

        var visible = PanelLayout.Visible(
            order,
            isShown: id => id != PanelSectionId.Disk,
            isAvailable: feature => feature is not AppFeature.MonitorPower,
            brightnessControlEnabled: false,
            isBuilt: id => id is PanelSectionId.System or PanelSectionId.Network or PanelSectionId.Disk or PanelSectionId.Power or PanelSectionId.Brightness);

        Assert.Equal("system,network", Joined(visible));
    }

    [Fact]
    public void BrightnessAlsoNeedsBrightnessControlEnabled()
    {
        var order = PanelLayout.Order(null);

        var off = PanelLayout.Visible(order, _ => true, _ => true, brightnessControlEnabled: false, isBuilt: _ => true);
        var on = PanelLayout.Visible(order, _ => true, _ => true, brightnessControlEnabled: true, isBuilt: _ => true);

        Assert.DoesNotContain(PanelSectionId.Brightness, off);
        Assert.Contains(PanelSectionId.Brightness, on);
    }

    [Fact]
    public void ActiveIsTheSelectionWhenVisibleElseTheFirstVisible()
    {
        IReadOnlyList<PanelSectionId> visible = [PanelSectionId.System, PanelSectionId.Network];

        Assert.Equal(PanelSectionId.Network, PanelLayout.Active(PanelSectionId.Network, visible));
        Assert.Equal(PanelSectionId.System, PanelLayout.Active(PanelSectionId.Power, visible));
        Assert.Equal(PanelSectionId.System, PanelLayout.Active(null, visible));
        Assert.Equal(PanelSectionId.KeepAwake, PanelLayout.Active(null, []));
    }
}
