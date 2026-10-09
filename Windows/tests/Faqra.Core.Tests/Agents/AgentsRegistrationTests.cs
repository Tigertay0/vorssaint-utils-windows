using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Island;
using Faqra.Core.Localization;
using Faqra.Core.Settings;

namespace Faqra.Core.Tests.Agents;

public class AgentsRegistrationTests
{
    [Fact]
    public void TheFeatureIsFaqrasOwnAndShipsOn()
    {
        Assert.Equal("faqraAgents", AppFeature.FaqraAgents.RawValue());
        Assert.Equal("featureAvailable.faqraAgents", AppFeature.FaqraAgents.AvailabilityKey());
        Assert.Equal(FeatureGroup.Tools, AppFeature.FaqraAgents.Group());
        Assert.True(FeatureWindowsSupport.IsBuilt(AppFeature.FaqraAgents));
        Assert.True(AppFeature.FaqraAgents.IsSupported());
        Assert.Contains(AppFeature.FaqraAgents, FeaturePresets.FirstRunFeatures);
        Assert.Equal("Agents", FeatureHubStrings.For(AppLanguage.EnUS).FeatureTitles[AppFeature.FaqraAgents]);
    }

    [Fact]
    public void TheModuleAndPageRideOnTheFeature()
    {
        Assert.Equal("faqraAgents", IslandModule.FaqraAgents.RawValue());
        Assert.Equal('g', IslandModule.FaqraAgents.ShortcutKey());
        Assert.False(IslandModule.FaqraAgents.IsAvailable(_ => false));
        Assert.True(IslandModule.FaqraAgents.IsAvailable(feature => feature == AppFeature.FaqraAgents));
        Assert.Equal([AppFeature.FaqraAgents], SettingsDirectory.Gate(SettingsPage.Agents));
        Assert.Equal(SettingsPage.Agents, SettingsDirectory.Destination(AppFeature.FaqraAgents));
        Assert.Equal("Agents", Strings.EnUS.SettingsPageTitles[SettingsPage.Agents]);
    }

    [Fact]
    public void AnExistingInstallGetsAgentsOnceAndKeepsAnUninstall()
    {
        var store = DefaultsStore.InMemory();
        store.Set(AppFeature.FaqraAgents.AvailabilityKey(), false);
        DefaultsMigrations.InstallAgentsOnce(store);
        Assert.True(store.Bool(AppFeature.FaqraAgents.AvailabilityKey()));
        store.Set(AppFeature.FaqraAgents.AvailabilityKey(), false);
        DefaultsMigrations.InstallAgentsOnce(store);
        Assert.False(store.Bool(AppFeature.FaqraAgents.AvailabilityKey()));
    }
}
