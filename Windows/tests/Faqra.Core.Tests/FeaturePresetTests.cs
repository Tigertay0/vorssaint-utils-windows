using Faqra.Core.Defaults;
using Faqra.Core.Features;

namespace Faqra.Core.Tests;

public class FeaturePresetTests
{
    [Fact]
    public void EssentialIsMixerKeepAwakeAndSixMonitors()
    {
        var essential = FeaturePreset.Essential.Features();
        Assert.Equal(8, essential.Count);
        Assert.Contains(AppFeature.Mixer, essential);
        Assert.Contains(AppFeature.KeepAwake, essential);
        Assert.Contains(AppFeature.MonitorPower, essential);
        Assert.Empty(FeaturePreset.Essential.EnableKeys());
    }

    [Fact]
    public void WindowsPresetSwitchesItsEnableKeysOn() =>
        Assert.Equal(
            [DefaultsKey.SwitcherEnabled, DefaultsKey.DockPreviewEnabled, DefaultsKey.DockClickMinimize, DefaultsKey.WindowMaximizeEnabled],
            FeaturePreset.Windows.EnableKeys());

    [Fact]
    public void FirstRunSetIsEssentialsPlusTheIsland()
    {
        Assert.Equal(11, FeaturePresets.FirstRunFeatures.Count);
        Assert.Contains(AppFeature.Notch, FeaturePresets.FirstRunFeatures);
        Assert.Contains(AppFeature.NotchTimer, FeaturePresets.FirstRunFeatures);
        Assert.Contains(AppFeature.FaqraAgents, FeaturePresets.FirstRunFeatures);
        Assert.All(FeaturePreset.Essential.Features(), feature => Assert.Contains(feature, FeaturePresets.FirstRunFeatures));
        // The island is Faqra's addition; upstream's Essential preset itself is untouched.
        Assert.DoesNotContain(AppFeature.Notch, FeaturePreset.Essential.Features());
    }

    [Fact]
    public void FirstRun_AppliesTheFirstRunSetOnly()
    {
        var store = DefaultsStore.InMemory();
        Assert.True(store.Bool(AppFeature.DockClick.AvailabilityKey())); // registered default before first run

        FeaturePresets.PrepareFirstRunAvailability(store);

        foreach (var feature in AppFeatures.All)
        {
            Assert.Equal(FeaturePresets.FirstRunFeatures.Contains(feature), store.Bool(feature.AvailabilityKey()));
        }
    }

    [Fact]
    public void FirstRun_IsSkippedOnceOnboardedOrInterruptedPastPurpose()
    {
        var onboarded = DefaultsStore.InMemory();
        onboarded.Set(DefaultsKey.HasOnboarded, true);
        FeaturePresets.PrepareFirstRunAvailability(onboarded);
        Assert.True(onboarded.Bool(AppFeature.DockClick.AvailabilityKey()));

        var interrupted = DefaultsStore.InMemory();
        interrupted.Set(DefaultsKey.OnboardingStep, 2);
        FeaturePresets.PrepareFirstRunAvailability(interrupted);
        Assert.True(interrupted.Bool(AppFeature.DockClick.AvailabilityKey()));
    }
}
