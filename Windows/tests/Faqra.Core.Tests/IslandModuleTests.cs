using Faqra.Core.Features;
using Faqra.Core.Island;

namespace Faqra.Core.Tests;

public class IslandModuleTests
{
    private static readonly HashSet<AppFeature> Essential = FeaturePreset.Essential.Features().ToHashSet();

    [Fact]
    public void ThirteenModulesWithUniqueRawValuesAndShortcutKeys()
    {
        Assert.Equal(13, IslandModules.All.Count);
        Assert.Equal(13, IslandModules.All.Select(m => m.RawValue()).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(13, IslandModules.All.Select(m => m.ShortcutKey()).Distinct().Count());
        foreach (var module in IslandModules.All)
        {
            Assert.Equal(module, IslandModules.FromRawValue(module.RawValue()));
            Assert.False(string.IsNullOrEmpty(module.Glyph()));
        }
        Assert.Null(IslandModules.FromRawValue("shelf"));
    }

    [Fact]
    public void ControlsAndMusicAreAlwaysAvailable()
    {
        Assert.True(IslandModule.Controls.IsAvailable(_ => false));
        Assert.True(IslandModule.Music.IsAvailable(_ => false));
        Assert.False(IslandModule.Clipboard.IsAvailable(_ => false));
    }

    [Fact]
    public void ModulesRideOnTheFeatureThatFillsThem()
    {
        bool Installed(AppFeature feature) => Essential.Contains(feature);

        Assert.True(IslandModule.Mixer.IsAvailable(Installed));
        Assert.True(IslandModule.System.IsAvailable(Installed));
        Assert.False(IslandModule.Timer.IsAvailable(Installed));
        Assert.False(IslandModule.Clipboard.IsAvailable(Installed));
        Assert.False(IslandModule.Captures.IsAvailable(Installed));
        // Any one capture feature is enough for the Captures section.
        Assert.True(IslandModule.Captures.IsAvailable(feature => feature == AppFeature.ColorPicker));
        // Any one metric is enough for the System section.
        Assert.True(IslandModule.System.IsAvailable(feature => feature == AppFeature.MonitorDisk));
    }

    [Fact]
    public void VisibleFollowsTheSavedOrderThenCanonicalOrder()
    {
        var visible = IslandModules.Visible("system,controls", hiddenList: null, _ => true);

        Assert.Equal(IslandModule.System, visible[0]);
        Assert.Equal(IslandModule.Controls, visible[1]);
        Assert.Equal(13, visible.Count);
        Assert.Equal(13, visible.Distinct().Count());
    }

    [Fact]
    public void VisibleDropsHiddenAndUnavailableModules()
    {
        var visible = IslandModules.Visible(null, "music,system", feature => Essential.Contains(feature));

        Assert.DoesNotContain(IslandModule.Music, visible);       // hidden
        Assert.DoesNotContain(IslandModule.System, visible);      // hidden
        Assert.DoesNotContain(IslandModule.Timer, visible);       // feature not installed
        Assert.Contains(IslandModule.Controls, visible);
        Assert.Contains(IslandModule.Mixer, visible);
    }

    [Fact]
    public void UnknownAndRepeatedTokensInASavedOrderAreIgnored()
    {
        var visible = IslandModules.Visible("controls,controls,bogus,timer", null, _ => true);
        Assert.Equal(IslandModule.Controls, visible[0]);
        Assert.Equal(IslandModule.Timer, visible[1]);
        Assert.Equal(13, visible.Count);
    }

    [Fact]
    public void AdjacentWrapsInBothDirections()
    {
        IReadOnlyList<IslandModule> visible = [IslandModule.Controls, IslandModule.System, IslandModule.Timer];

        Assert.Equal(IslandModule.System, IslandModules.Adjacent(IslandModule.Controls, 1, visible));
        Assert.Equal(IslandModule.Controls, IslandModules.Adjacent(IslandModule.Timer, 1, visible));
        Assert.Equal(IslandModule.Timer, IslandModules.Adjacent(IslandModule.Controls, -1, visible));
        // A module that is no longer visible falls back to the first one.
        Assert.Equal(IslandModule.Controls, IslandModules.Adjacent(IslandModule.Mixer, 1, visible));
        Assert.Null(IslandModules.Adjacent(IslandModule.Controls, 1, []));
    }
}
