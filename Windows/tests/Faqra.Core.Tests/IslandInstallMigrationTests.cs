using Faqra.Core.Defaults;
using Faqra.Core.Features;

namespace Faqra.Core.Tests;

public class IslandInstallMigrationTests
{
    private const string Marker = "faqraIslandInstalled";

    [Fact]
    public void InstallsTheIslandForAnInstallThatPredatesIt()
    {
        var store = DefaultsStore.InMemory();
        // An earlier first run seeded Essentials, which leaves the island uninstalled.
        store.Set(AppFeature.Notch.AvailabilityKey(), false);
        store.Set(AppFeature.NotchTimer.AvailabilityKey(), false);

        DefaultsMigrations.InstallIslandOnce(store);

        Assert.True(store.Bool(AppFeature.Notch.AvailabilityKey()));
        Assert.True(store.Bool(AppFeature.NotchTimer.AvailabilityKey()));
        Assert.True(store.Bool(Marker));
    }

    [Fact]
    public void RunsOnlyOnceSoALaterUninstallSticks()
    {
        var store = DefaultsStore.InMemory();
        DefaultsMigrations.InstallIslandOnce(store);

        // The user switches the island off afterwards.
        store.Set(AppFeature.Notch.AvailabilityKey(), false);
        DefaultsMigrations.InstallIslandOnce(store);

        Assert.False(store.Bool(AppFeature.Notch.AvailabilityKey()));
    }

    [Fact]
    public void RunLeavesTheIslandInstalledOnAFreshStore()
    {
        var store = DefaultsStore.InMemory();
        DefaultsMigrations.Run(store, "0.1.0");
        Assert.True(store.Bool(AppFeature.Notch.AvailabilityKey()));
    }
}
