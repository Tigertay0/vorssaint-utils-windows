using Faqra.Core.Island;

namespace Faqra.Core.Tests;

public class IslandHoverStateTests
{
    [Fact]
    public void TimingsMatchUpstream()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(100), IslandHoverState.OpenDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(180), IslandHoverState.ExpandedExitDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(120), IslandHoverState.PeekExitDelay);
    }

    [Fact]
    public void HoverExpandsOrPeeksAccordingToTheSetting()
    {
        var state = new IslandHoverState();

        Assert.Equal(IslandPresentation.Expanded, state.PresentationForHover(openOnHover: true, hoverExpands: true));
        Assert.Equal(IslandPresentation.Peek, state.PresentationForHover(openOnHover: true, hoverExpands: false));
        Assert.Null(state.PresentationForHover(openOnHover: false, hoverExpands: true));
    }

    [Fact]
    public void ClosingUnderAStillPointerSuppressesTheNextHoverUntilThePointerLeaves()
    {
        var state = new IslandHoverState();

        state.Close(pointerInside: true);
        Assert.True(state.Suppressed);
        Assert.Null(state.PresentationForHover(openOnHover: true, hoverExpands: true));

        // Still inside: a resize-induced re-entry must not re-open it.
        state.Update(pointerInside: true);
        Assert.True(state.Suppressed);

        state.Update(pointerInside: false);
        Assert.False(state.Suppressed);
        Assert.Equal(IslandPresentation.Expanded, state.PresentationForHover(openOnHover: true, hoverExpands: true));
    }

    [Fact]
    public void ClosingAwayFromThePillDoesNotSuppress()
    {
        var state = new IslandHoverState();
        state.Close(pointerInside: false);
        Assert.False(state.Suppressed);
    }

    [Fact]
    public void OpeningClearsSuppression()
    {
        var state = new IslandHoverState();
        state.Close(pointerInside: true);
        state.Open();
        Assert.False(state.Suppressed);
    }

    [Theory]
    [InlineData(IslandPresentation.Expanded, true, false, 180)]
    [InlineData(IslandPresentation.Peek, true, false, 120)]
    [InlineData(IslandPresentation.Peek, false, false, 120)]
    public void ExitDelayPerPresentation(IslandPresentation presentation, bool openedByHover, bool pinned, int expectedMs) =>
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), IslandHoverState.ExitDelay(presentation, openedByHover, pinned));

    [Fact]
    public void AnIslandOpenedByClickOrPinnedDoesNotAutoClose()
    {
        Assert.Null(IslandHoverState.ExitDelay(IslandPresentation.Expanded, openedByHover: false, pinned: false));
        Assert.Null(IslandHoverState.ExitDelay(IslandPresentation.Expanded, openedByHover: true, pinned: true));
        Assert.Null(IslandHoverState.ExitDelay(IslandPresentation.Peek, openedByHover: true, pinned: true));
        Assert.Null(IslandHoverState.ExitDelay(IslandPresentation.Collapsed, openedByHover: true, pinned: false));
    }
}
