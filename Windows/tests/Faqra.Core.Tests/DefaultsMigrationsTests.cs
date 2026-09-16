using Faqra.Core.Defaults;

namespace Faqra.Core.Tests;

public class DefaultsMigrationsTests
{
    [Fact]
    public void FanControlVisibility_MovesLegacyKeyAndInstallsWhenShown()
    {
        var store = DefaultsStore.InMemory();
        store.Set(DefaultsKey.MonitorShowFanControlBeta, true);

        DefaultsMigrations.MigrateFanControlVisibility(store);

        Assert.True(store.Bool(DefaultsKey.PanelShowFanControl));
        Assert.True(store.Contains(DefaultsKey.PanelShowFanControl));
        Assert.True(store.Bool(DefaultsKey.FeatureAvailable("fanControl")));
        Assert.False(store.Contains(DefaultsKey.MonitorShowFanControlBeta));
    }

    [Fact]
    public void ScrollInverterAxes_CopiesTheOldSwitchOnce()
    {
        var store = DefaultsStore.InMemory();
        store.Set(DefaultsKey.ScrollInverterEnabled, true);

        DefaultsMigrations.MigrateScrollInverterAxes(store);
        store.Set(DefaultsKey.ScrollInverterEnabled, false);
        DefaultsMigrations.MigrateScrollInverterAxes(store);

        Assert.True(store.Bool(DefaultsKey.ScrollInverterHorizontalEnabled));
    }

    [Fact]
    public void BatteryTemperatureVisibility_FollowsSystemTemps()
    {
        var store = DefaultsStore.InMemory();
        store.Set(DefaultsKey.MonitorSysTemps, false);
        DefaultsMigrations.MigrateBatteryTemperatureVisibility(store);
        Assert.False(store.Bool(DefaultsKey.MonitorPwrTemperature));

        var fresh = DefaultsStore.InMemory();
        DefaultsMigrations.MigrateBatteryTemperatureVisibility(fresh);
        Assert.True(fresh.Bool(DefaultsKey.MonitorPwrTemperature));
    }

    [Fact]
    public void BetaChannel_ActivatesOncePerPreRelease()
    {
        var store = DefaultsStore.InMemory();
        DefaultsMigrations.ActivateBetaChannelIfRunningBeta(store, "1.0.0");
        Assert.False(store.Bool(DefaultsKey.IncludeBetaUpdates));

        DefaultsMigrations.ActivateBetaChannelIfRunningBeta(store, "1.1.0-beta.1");
        Assert.True(store.Bool(DefaultsKey.IncludeBetaUpdates));
        store.Set(DefaultsKey.IncludeBetaUpdates, false);
        DefaultsMigrations.ActivateBetaChannelIfRunningBeta(store, "1.1.0-beta.1");
        Assert.False(store.Bool(DefaultsKey.IncludeBetaUpdates)); // the user's later choice wins
    }

    [Fact]
    public void LegacyMenuBarTemperature_FansOutAndRewritesOrder()
    {
        var store = DefaultsStore.InMemory();
        store.Set(DefaultsKey.MenuBarTemperature, true);
        store.Set(DefaultsKey.MenuBarMetricOrder, "cpu,temperature,memory");

        DefaultsMigrations.MigrateLegacyMenuBarTemperatureMetric(store);

        Assert.True(store.Bool(DefaultsKey.MenuBarCPUTemperature));
        Assert.True(store.Bool(DefaultsKey.MenuBarBatteryTemperature));
        Assert.StartsWith("cpu,cpuTemperature,gpuTemperature,batteryTemperature,memory", store.String(DefaultsKey.MenuBarMetricOrder));
        Assert.False(store.Contains(DefaultsKey.MenuBarTemperature));
    }

    [Fact]
    public void SilentHeadphonesVolume_RaisesToTwentyFive()
    {
        var store = DefaultsStore.InMemory();
        store.Set(DefaultsKey.MixerHeadphonesDisconnectVolumePercent, 3);
        DefaultsMigrations.MigrateSilentHeadphonesDisconnectVolume(store);
        Assert.Equal(25, store.Int(DefaultsKey.MixerHeadphonesDisconnectVolumePercent));
    }

    [Fact]
    public void Run_IsIdempotentOnAFreshStore()
    {
        var store = DefaultsStore.InMemory();
        DefaultsMigrations.Run(store, "0.1.0");
        var after = store.Keys.Order(StringComparer.Ordinal).ToList();
        DefaultsMigrations.Run(store, "0.1.0");
        Assert.Equal(after, store.Keys.Order(StringComparer.Ordinal));
    }
}
