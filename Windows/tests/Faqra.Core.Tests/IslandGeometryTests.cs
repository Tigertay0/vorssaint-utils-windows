using Faqra.Core.Island;

namespace Faqra.Core.Tests;

public class IslandGeometryTests
{
    // The user's primary display.
    private static IslandGeometry Ultrawide(IslandSize layout = IslandSize.Spacious, double bar = 24) =>
        new(3440, 1440, bar, layout);

    [Fact]
    public void RestingPillIsUpstreamsFormulaForAScreenWithoutACutout()
    {
        var geometry = Ultrawide();

        // 180 * 24 / 32 = 135
        Assert.Equal(135, geometry.CameraWidth);
        Assert.Equal(24, geometry.CameraHeight);
        Assert.Equal(24, geometry.BarHeight);
        Assert.Equal(34, geometry.SafeContentTop);
        Assert.Equal(new IslandSizeValue(135, 24), geometry.RestingSize(showsContent: false));
        Assert.Equal(new IslandSizeValue(135, 24), geometry.Collapsed);
    }

    [Theory]
    [InlineData(32, 180)]
    [InlineData(16, 90)]
    [InlineData(48, 270)]
    public void PillScalesWithTheBarHeight(double bar, double expectedWidth) =>
        Assert.Equal(expectedWidth, Ultrawide(bar: bar).CameraWidth);

    [Theory]
    [InlineData(8, 16)]   // below the floor
    [InlineData(200, 64)] // above the ceiling
    [InlineData(double.NaN, 24)]
    public void BarHeightIsClampedToUpstreamsRange(double bar, double expected) =>
        Assert.Equal(expected, Ultrawide(bar: bar).CameraHeight);

    [Fact]
    public void PillNeverTakesMoreThanSeventyPercentOfANarrowScreen()
    {
        var tiny = new IslandGeometry(200, 800, barHeight: 64);
        Assert.Equal(140, tiny.CameraWidth); // 200 * 0.7, not 360
    }

    [Theory]
    [InlineData(IslandSize.Compact, 480)]
    [InlineData(IslandSize.Spacious, 560)]
    public void ExpandedWidthFollowsTheSizePreset(IslandSize layout, double expected) =>
        Assert.Equal(expected, Ultrawide(layout).ExpandedWidth);

    [Fact]
    public void CustomWidthIsClampedIntoUpstreamsRange()
    {
        Assert.Equal(360, new IslandGeometry(3440, 1440, 24, IslandSize.Custom, customWidth: 100).ExpandedWidth);
        Assert.Equal(600, new IslandGeometry(3440, 1440, 24, IslandSize.Custom, customWidth: 5000).ExpandedWidth);
        Assert.Equal(440, new IslandGeometry(3440, 1440, 24, IslandSize.Custom, customWidth: double.NaN).ExpandedWidth);
    }

    [Fact]
    public void ExpandedWidthLeavesRoomForTheQuickAccessGuttersOnANarrowScreen()
    {
        var narrow = new IslandGeometry(600, 900);
        // 600 - 24 - 72 * 2 = 432
        Assert.Equal(432, narrow.ExpandedWidth);
        Assert.True(narrow.UsesCompactContent);
        Assert.Equal(2, narrow.SystemColumns);
        Assert.Equal(2, narrow.SectionColumns);
    }

    [Fact]
    public void PeekIsTheSmallHoverState()
    {
        var geometry = Ultrawide();
        // max(135 + 110, 340) = 340, and 34 + 52 = 86
        Assert.Equal(new IslandSizeValue(340, 86), geometry.Peek);
    }

    [Fact]
    public void ExpandedHeightsMatchUpstreamsContentBudget()
    {
        var geometry = Ultrawide();

        // Controls: chrome 76 + one level row 94 + two action rows 104 + spacing 8 + group gaps 18 = 300, plus safe top 34.
        Assert.Equal(new IslandSizeValue(560, 334), geometry.ExpandedSize(IslandModule.Controls));
        // System: 34 + 76 + (3 * 96 + 2 * 10) = 418, plus the spacious bonus 40.
        Assert.Equal(new IslandSizeValue(560, 458), geometry.ExpandedSize(IslandModule.System));
        // Timer with no session: 34 + 76 + 202 = 312; Controls, Music and Timer take no bonus.
        Assert.Equal(new IslandSizeValue(560, 312), geometry.ExpandedSize(IslandModule.Timer));
        // Music with content: 34 + 76 + 216 = 326.
        Assert.Equal(new IslandSizeValue(560, 326), geometry.ExpandedSize(IslandModule.Music));
    }

    [Fact]
    public void ExpandedHeightNeverFillsTheWholeScreen()
    {
        var shortScreen = new IslandGeometry(1920, 300);
        var size = shortScreen.ExpandedSize(IslandModule.Mixer);
        Assert.Equal(300 - 48, size.Height);
    }

    [Fact]
    public void OriginIsCenteredAndFlushWithTheTopEdge()
    {
        var geometry = Ultrawide();
        var resting = geometry.TopCenterOrigin(geometry.RestingSize(showsContent: false));
        Assert.Equal((1652.5, 0.0), resting);

        var expanded = geometry.TopCenterOrigin(geometry.ExpandedSize(IslandModule.Controls));
        Assert.Equal((1440.0, 0.0), expanded);
    }

    [Fact]
    public void MotionGrowsSlowerThanItShrinks()
    {
        var small = new IslandSizeValue(135, 24);
        var big = new IslandSizeValue(560, 334);

        Assert.Equal(IslandMotion.Grow, IslandMotion.Duration(small, big));
        Assert.Equal(IslandMotion.Shrink, IslandMotion.Duration(big, small));
        Assert.Equal(TimeSpan.FromSeconds(0.34), IslandMotion.Grow);
        Assert.Equal(TimeSpan.FromSeconds(0.26), IslandMotion.Shrink);
        // Same height, wider still counts as growing.
        Assert.Equal(IslandMotion.Grow, IslandMotion.Duration(small, new IslandSizeValue(200, 24)));
    }

    [Fact]
    public void SizeAndDisplayRawValuesRoundTripWithUpstreamSpellings()
    {
        Assert.Equal(IslandSize.Compact, IslandSizes.FromRawValue("compact"));
        Assert.Equal(IslandSize.Spacious, IslandSizes.FromRawValue(null));
        Assert.Equal("spacious", IslandSize.Spacious.RawValue());
        // Upstream's builtIn has no Windows meaning and falls back to the primary display.
        Assert.Equal(IslandDisplay.Automatic, IslandSizes.DisplayFromRawValue("builtIn"));
        Assert.Equal(IslandDisplay.Main, IslandSizes.DisplayFromRawValue("main"));
        Assert.Equal(IslandIdleContent.Music, IslandSizes.IdleContentFromRawValue(null));
        Assert.Equal(IslandIdleContent.Battery, IslandSizes.IdleContentFromRawValue("battery"));
    }
}
