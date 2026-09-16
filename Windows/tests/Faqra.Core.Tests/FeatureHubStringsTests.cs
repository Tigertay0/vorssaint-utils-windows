using Faqra.Core.Features;
using Faqra.Core.Localization;

namespace Faqra.Core.Tests;

public class FeatureHubStringsTests
{
    [Fact]
    public void EveryLanguageCoversEveryEnumMember()
    {
        foreach (var language in AppLanguages.All)
        {
            var s = FeatureHubStrings.For(language);
            Assert.Equal(AppFeatures.All.Count, s.FeatureTitles.Count);
            Assert.Equal(AppFeatures.All.Count, s.FeatureDescriptions.Count);
            Assert.Equal(FeatureGroups.All.Count, s.GroupTitles.Count);
            Assert.Equal(FeaturePresets.All.Count, s.PresetNames.Count);
            Assert.Equal(FeaturePresets.All.Count, s.PresetDescriptions.Count);
            Assert.Equal(Enum.GetValues<FeatureEnergyProfile>().Length, s.EnergyLabels.Count);
            Assert.Equal(AppPermissions.All.Count, s.PermissionNames.Count);
            Assert.Equal(AppPermissions.All.Count, s.PermissionExplainers.Count);
            foreach (var feature in AppFeatures.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(s.FeatureTitles[feature]), feature.ToString());
                Assert.False(string.IsNullOrWhiteSpace(s.FeatureDescriptions[feature]), feature.ToString());
            }
        }
    }

    [Fact]
    public void PortedFeaturesDoNotMentionTheMac()
    {
        var s = FeatureHubStrings.EnUS;
        foreach (var feature in AppFeatures.All.Where(f => f.IsSupported()))
        {
            Assert.DoesNotContain("Mac", s.FeatureDescriptions[feature]);
            Assert.DoesNotContain("macOS", s.FeatureDescriptions[feature]);
        }
    }

    [Fact]
    public void FormatStringsUseDotNetPlaceholders()
    {
        var s = FeatureHubStrings.EnUS;
        Assert.Equal("3 of 66 features installed", string.Format(s.InstalledCountFormat, 3, 66));
        Assert.Contains("Essentials", string.Format(s.PresetConfirmFormat, s.PresetNames[FeaturePreset.Essential]));
    }
}
