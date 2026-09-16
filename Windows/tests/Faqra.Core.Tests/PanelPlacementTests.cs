using Faqra.Core.Panel;

namespace Faqra.Core.Tests;

public class PanelPlacementTests
{
    // The development PC's primary display: 3440x1440 with a 48px taskbar at the bottom.
    private static readonly PixelRect Monitor = new(0, 0, 3440, 1440);
    private static readonly PixelRect WorkArea = new(0, 0, 3440, 1392);

    [Fact]
    public void SitsAboveABottomTaskbarCenteredOnTheIcon()
    {
        var icon = new PixelRect(3000, 1400, 3032, 1432);

        var origin = PanelPlacement.Origin(icon, Monitor, WorkArea, width: 332, height: 500, gap: 12);

        Assert.Equal(new PixelPoint(3016 - 166, 1392 - 12 - 500), origin);
    }

    [Fact]
    public void StaysInsideTheWorkAreaNearTheScreenEdge()
    {
        var icon = new PixelRect(3400, 1400, 3432, 1432);

        var origin = PanelPlacement.Origin(icon, Monitor, WorkArea, 332, 500, 12);

        Assert.Equal(3440 - 12 - 332, origin.X);
    }

    [Fact]
    public void HangsBelowATopTaskbar()
    {
        var work = new PixelRect(0, 48, 3440, 1440);
        var icon = new PixelRect(3000, 8, 3032, 40);

        var origin = PanelPlacement.Origin(icon, Monitor, work, 332, 500, 12);

        Assert.Equal(48 + 12, origin.Y);
    }

    [Fact]
    public void SitsBesideASideTaskbar()
    {
        var work = new PixelRect(0, 0, 3380, 1440);
        var icon = new PixelRect(3400, 1300, 3432, 1332);

        var origin = PanelPlacement.Origin(icon, Monitor, work, 332, 500, 12);

        Assert.Equal(3380 - 12 - 332, origin.X);
        Assert.Equal(1440 - 12 - 500, origin.Y);
    }

    [Fact]
    public void WithoutAnIconItOpensInTheWorkAreaCorner()
    {
        // The icon is in the overflow flyout, so there is no rectangle to anchor to.
        var origin = PanelPlacement.Origin(null, Monitor, WorkArea, 332, 500, 12);

        Assert.Equal(new PixelPoint(3440 - 12 - 332, 1392 - 12 - 500), origin);
    }

    [Fact]
    public void MaxHeightLeavesTheGapAndNeverDropsBelowTheFloor()
    {
        Assert.Equal(1392 - 24, PanelPlacement.MaxHeight(WorkArea, gap: 12, floor: 360));
        Assert.Equal(360, PanelPlacement.MaxHeight(new PixelRect(0, 0, 800, 300), gap: 12, floor: 360));
    }
}
