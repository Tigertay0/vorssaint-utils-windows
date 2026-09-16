using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Localization;

namespace Faqra.Core.Tests;

public class FeatureCatalogTests
{
    [Fact]
    public void CatalogHasAllSixtySixUpstreamFeatures() => Assert.Equal(66, AppFeatures.All.Count);

    [Fact]
    public void RawValuesMatchUpstreamCaseNames()
    {
        // Spot-check the ones that do not follow plain lowerCamelCase.
        Assert.Equal("monitorCPU", AppFeature.MonitorCPU.RawValue());
        Assert.Equal("monitorGPU", AppFeature.MonitorGPU.RawValue());
        Assert.Equal("screenOCR", AppFeature.ScreenOCR.RawValue());
        Assert.Equal("urlCleaner", AppFeature.UrlCleaner.RawValue());
        Assert.Equal("quitWindowProtection", AppFeature.QuitWindowProtection.RawValue());
        Assert.Equal("notchNotifications", AppFeature.NotchNotifications.RawValue());
        Assert.Equal("fanControl", AppFeature.FanControl.RawValue());
    }

    [Fact]
    public void RawValuesRoundTripAndAreUnique()
    {
        var raws = AppFeatures.All.Select(f => f.RawValue()).ToList();
        Assert.Equal(raws.Count, raws.Distinct(StringComparer.Ordinal).Count());
        foreach (var feature in AppFeatures.All)
        {
            Assert.Equal(feature, AppFeatures.FromRawValue(feature.RawValue()));
        }
        Assert.Null(AppFeatures.FromRawValue("dock"));
    }

    [Fact]
    public void AvailabilityKeyUsesUpstreamPrefix() =>
        Assert.Equal("featureAvailable.keepAwake", AppFeature.KeepAwake.AvailabilityKey());

    [Fact]
    public void GroupsPartitionTheCatalogInUpstreamCounts()
    {
        Assert.Equal(6, AppFeatures.FeaturesIn(FeatureGroup.WindowsDock).Count);
        Assert.Equal(12, AppFeatures.FeaturesIn(FeatureGroup.MouseKeyboard).Count);
        Assert.Equal(7, AppFeatures.FeaturesIn(FeatureGroup.ClipboardFiles).Count);
        Assert.Equal(4, AppFeatures.FeaturesIn(FeatureGroup.Sound).Count);
        Assert.Equal(4, AppFeatures.FeaturesIn(FeatureGroup.EnergyDisplay).Count);
        Assert.Equal(26, AppFeatures.FeaturesIn(FeatureGroup.Tools).Count);
        Assert.Equal(7, AppFeatures.FeaturesIn(FeatureGroup.Monitor).Count);
    }

    [Fact]
    public void OnlyFanControlAndKillProcessAreBeta() =>
        Assert.Equal([AppFeature.KillProcess, AppFeature.FanControl], AppFeatures.All.Where(f => f.IsBeta()));

    [Fact]
    public void AvailabilityDefaultsShipEverythingOnExceptOptIns()
    {
        var defaults = AppFeatures.AvailabilityDefaults;
        Assert.Equal(66, defaults.Count);
        Assert.Equal(false, defaults["featureAvailable.focusFollowsMouse"]);
        Assert.Equal(false, defaults["featureAvailable.fanControl"]);
        Assert.Equal(false, defaults["featureAvailable.diskImageInstaller"]);
        Assert.Equal(false, defaults["featureAvailable.killProcess"]);
        Assert.Equal(true, defaults["featureAvailable.mixer"]);
        Assert.Equal(true, defaults["featureAvailable.dockClick"]);
    }

    [Fact]
    public void EverySymbolAndEnabledKeyResolves()
    {
        foreach (var feature in AppFeatures.All)
        {
            Assert.False(string.IsNullOrEmpty(feature.SymbolName()));
            Assert.NotNull(feature.EnabledKeys());
            Assert.NotNull(feature.Permissions());
        }
        Assert.Equal([DefaultsKey.NotchEnabled], AppFeature.Notch.EnabledKeys());
        Assert.Empty(AppFeature.Mixer.EnabledKeys());
    }

    [Fact]
    public void OnboardingPermissionsOnlyKeepBroadGrants()
    {
        Assert.Equal([AppPermission.Accessibility, AppPermission.ScreenRecording], AppFeature.Switcher.OnboardingPermissions());
        Assert.Empty(AppFeature.Mixer.OnboardingPermissions());
        Assert.Empty(AppFeature.Cleaner.OnboardingPermissions());
    }

    [Fact]
    public void ActiveFeatures_RespectsAvailabilityEnableKeysAndDynamicRules()
    {
        var available = new HashSet<AppFeature> { AppFeature.Switcher, AppFeature.KeepAwake, AppFeature.MonitorCPU };
        var bools = new Dictionary<string, bool>
        {
            [DefaultsKey.SwitcherEnabled] = true,
            [DefaultsKey.SwitcherSimpleMode] = true,
            [DefaultsKey.MonitorAlertCPU] = true,
        };

        var screenRecording = AppFeatures.ActiveFeatures(
            AppPermission.ScreenRecording, available.Contains, key => bools.GetValueOrDefault(key), _ => null);
        var accessibility = AppFeatures.ActiveFeatures(
            AppPermission.Accessibility, available.Contains, key => bools.GetValueOrDefault(key), _ => null);
        var notifications = AppFeatures.ActiveFeatures(
            AppPermission.Notifications, available.Contains, key => bools.GetValueOrDefault(key), _ => null);

        Assert.Empty(screenRecording); // simple mode needs no captures
        Assert.Equal([AppFeature.Switcher], accessibility); // keep awake only with the jiggle on
        Assert.Equal([AppFeature.MonitorCPU], notifications);
    }

    [Fact]
    public void EnergyProfile_FollowsSettingsForDynamicFeatures()
    {
        Assert.Equal(FeatureEnergyProfile.Idle, AppFeature.Mixer.EnergyProfile(_ => false, _ => null));
        Assert.Equal(FeatureEnergyProfile.Keyboard, AppFeature.Mixer.EnergyProfile(key => key == DefaultsKey.PreciseVolumeRollerEnabled, _ => null));
        Assert.Equal(FeatureEnergyProfile.Periodic, AppFeature.MonitorCPU.EnergyProfile(_ => false, _ => null));
        Assert.Equal(FeatureEnergyProfile.Periodic, AppFeature.AppUpdates.EnergyProfile(_ => false, _ => "daily"));
        Assert.Equal(FeatureEnergyProfile.Idle, AppFeature.AppUpdates.EnergyProfile(_ => false, _ => "off"));
        Assert.Equal(FeatureEnergyProfile.Mouse, AppFeature.Shelf.EnergyProfile(_ => false, _ => null));
    }

    [Fact]
    public void WindowsSupport_Stage1Classification()
    {
        Assert.Equal(WindowsSupport.Supported, FeatureWindowsSupport.Classify(AppFeature.Mixer));
        Assert.Equal(WindowsSupport.Supported, FeatureWindowsSupport.Classify(AppFeature.Notch));
        Assert.Equal(WindowsSupport.NotApplicable, FeatureWindowsSupport.Classify(AppFeature.DockClick));
        Assert.Equal(WindowsSupport.NotApplicable, FeatureWindowsSupport.Classify(AppFeature.Homebrew));
        Assert.Null(FeatureWindowsSupport.UnsupportedReason(AppFeature.KeepAwake, Strings.EnUS));
        Assert.Equal("Not available on Windows", FeatureWindowsSupport.UnsupportedReason(AppFeature.FinderCutPaste, Strings.EnUS));
        Assert.All(FeaturePreset.Essential.Features(), f => Assert.True(f.IsSupported()));
    }
}
