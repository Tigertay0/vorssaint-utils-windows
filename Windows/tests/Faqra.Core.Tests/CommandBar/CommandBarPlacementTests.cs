using Faqra.Core.CommandBar;
using Faqra.Core.Panel;

namespace Faqra.Core.Tests.CommandBar;

// CommandBarPreferences.clampedPanelOrigin (Sources/Vorssaint/Services/CommandBar/CommandBarPreferences.swift:343-355),
// flipped to Windows' top-down coordinates: the bar's top edge sits 28% down the work area.
public class CommandBarPlacementTests
{
    [Fact]
    public void CentersHorizontallyWithTheTopTwentyEightPercentDown()
    {
        var work = new PixelRect(0, 0, 3440, 1392);
        var origin = CommandBarPlacement.Origin(work, width: 560, height: 300, margin: 16);
        Assert.Equal(1440, origin.X);
        Assert.Equal(389, origin.Y); // 1392 * 0.28 = 389.76, floored
    }

    [Fact]
    public void WorksOnASecondMonitor()
    {
        var work = new PixelRect(3440, 0, 5360, 1040);
        var origin = CommandBarPlacement.Origin(work, 560, 100, 16);
        Assert.Equal(3440 + 680, origin.X);
        Assert.Equal(291, origin.Y);
    }

    [Fact]
    public void ClampsInsideTheMargin()
    {
        var work = new PixelRect(0, 0, 500, 400);
        var origin = CommandBarPlacement.Origin(work, 560, 390, 16);
        Assert.Equal(16, origin.X);
        Assert.Equal(16, origin.Y);
    }
}
