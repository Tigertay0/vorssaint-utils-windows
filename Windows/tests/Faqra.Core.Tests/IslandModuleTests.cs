using Faqra.Core.Features;
using Faqra.Core.Island;

namespace Faqra.Core.Tests;

public class IslandModuleTests
{
    private static readonly HashSet<AppFeature> Essential = FeaturePreset.Essential.Features().ToHashSet();

    [Fact]
    public void FifteenModulesWithUniqueRawValuesAndShortcutKeys()
    {
        Assert.Equal(15, IslandModules.All.Count);
        Assert.Equal(15, IslandModules.All.Select(m => m.RawValue()).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(15, IslandModules.All.Select(m => m.ShortcutKey()).Distinct().Count());
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
        Assert.True(IslandModule.FaqraHome.IsAvailable(_ => false));
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

        // Home leads unless the owner placed it; then the saved order, then the canonical order.
        Assert.Equal(IslandModule.FaqraHome, visible[0]);
        Assert.Equal(IslandModule.System, visible[1]);
        Assert.Equal(IslandModule.Controls, visible[2]);
        Assert.Equal(15, visible.Count);
        Assert.Equal(15, visible.Distinct().Count());
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
        Assert.Equal(IslandModule.FaqraHome, visible[0]);
        Assert.Equal(IslandModule.Controls, visible[1]);
        Assert.Equal(IslandModule.Timer, visible[2]);
        Assert.Equal(15, visible.Count);
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

    [Fact]
    public void HomeIsWhereTheOwnerPutsIt()
    {
        Assert.Equal("faqraHome", IslandModule.FaqraHome.RawValue());
        Assert.Equal('h', IslandModule.FaqraHome.ShortcutKey());
        var visible = IslandModules.Visible("system,faqraHome", hiddenList: null, _ => true);
        Assert.Equal([IslandModule.System, IslandModule.FaqraHome], visible.Take(2));
        Assert.DoesNotContain(IslandModule.FaqraHome, IslandModules.Visible(null, "faqraHome", _ => true));
    }

    [Fact]
    public void HomeOpensWheneverItIsShown()
    {
        IReadOnlyList<IslandModule> withHome = [IslandModule.FaqraHome, IslandModule.Music, IslandModule.System];
        IReadOnlyList<IslandModule> withoutHome = [IslandModule.Music, IslandModule.System];
        Assert.Equal(IslandModule.FaqraHome, IslandModules.Opening(IslandModule.System, withHome));
        Assert.Equal(IslandModule.System, IslandModules.Opening(IslandModule.System, withoutHome));
        Assert.Equal(IslandModule.Music, IslandModules.Opening(IslandModule.Timer, withoutHome));
        Assert.Null(IslandModules.Opening(IslandModule.Timer, []));
    }
}
