using Faqra.Core.Island;

namespace Faqra.App.Tests;

/// <summary>
/// The cubic-bezier solver behind every animation. transitions.dev publishes its curves as CSS
/// cubic-beziers, so these must evaluate the way a browser would.
/// </summary>
public class MotionTests
{
    [Fact]
    public void EndpointsArePinned()
    {
        var ease = new CubicBezierEase(IslandMotion.Ease);
        Assert.Equal(0, ease.Ease(0), 6);
        Assert.Equal(1, ease.Ease(1), 6);
    }

    [Fact]
    public void LinearCurveIsTheIdentity()
    {
        var linear = new CubicBezierEase((0.0, 0.0, 1.0, 1.0));
        foreach (var t in new[] { 0.1, 0.25, 0.5, 0.75, 0.9 })
        {
            Assert.Equal(t, linear.Ease(t), 3);
        }
    }

    [Fact]
    public void TheSharedEaseFrontLoadsItsProgress()
    {
        var ease = new CubicBezierEase(IslandMotion.Ease);

        // cubic-bezier(0.22, 1, 0.36, 1) is a strong ease-out: most of the distance is covered early.
        Assert.True(ease.Ease(0.25) > 0.6, $"quarter way through, progress was {ease.Ease(0.25)}");
        Assert.True(ease.Ease(0.5) > 0.9, $"half way through, progress was {ease.Ease(0.5)}");
        // Monotonic throughout, or the island would jitter.
        var previous = 0.0;
        for (var step = 0; step <= 50; step++)
        {
            var value = ease.Ease(step / 50.0);
            Assert.True(value >= previous - 1e-9, $"went backwards at {step / 50.0}");
            previous = value;
        }
    }

    [Fact]
    public void TheToggleEaseOvershootsThenSettles()
    {
        var ease = new CubicBezierEase(IslandMotion.OvershootEase);

        // cubic-bezier(0.34, 1.35, 0.64, 1) passes 1 before the end, which is the overshoot.
        var peak = 0.0;
        for (var step = 0; step <= 100; step++)
        {
            peak = Math.Max(peak, ease.Ease(step / 100.0));
        }
        Assert.True(peak > 1.0, $"expected an overshoot, peaked at {peak}");
        Assert.Equal(1, ease.Ease(1), 6);
    }

    [Fact]
    public void AnimationsCarryTheSharedEaseAndHoldTheirFinalValue()
    {
        var animation = Motion.Double(0, 100, TimeSpan.FromMilliseconds(300));
        Assert.Equal(TimeSpan.FromMilliseconds(300), animation.Duration.TimeSpan);
        Assert.IsType<CubicBezierEase>(animation.EasingFunction);
        Assert.Equal(System.Windows.Media.Animation.FillBehavior.HoldEnd, animation.FillBehavior);
    }
}
