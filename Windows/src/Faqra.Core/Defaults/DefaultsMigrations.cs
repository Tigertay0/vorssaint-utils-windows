// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/Defaults.swift register() (lines 1507-1527). Only the migrations whose
// keys exist in this port run; the capture-shortcut, switcher, debounce and utility-order migrations
// arrive with their features.

namespace Faqra.Core.Defaults;

public static class DefaultsMigrations
{
    /// <summary>Runs every migration once, in upstream's order. Idempotent.</summary>
    public static void Run(ISettingsStore store, string appVersion)
    {
        MigrateFanControlVisibility(store);
        MigrateScrollInverterAxes(store);
        MigrateWhatsAppDownloadsEnabled(store);
        MigrateBatteryTemperatureVisibility(store);
        ActivateBetaChannelIfRunningBeta(store, appVersion);
        MigrateLegacyMenuBarTemperatureMetric(store);
        MigrateSilentHeadphonesDisconnectVolume(store);
    }

    /// <summary>The old beta visibility key becomes the panel section key (and an install when it was on).</summary>
    public static void MigrateFanControlVisibility(ISettingsStore store)
    {
        if (!store.Contains(DefaultsKey.MonitorShowFanControlBeta))
        {
            return;
        }
        var wasShown = store.Bool(DefaultsKey.MonitorShowFanControlBeta);
        if (!store.Contains(DefaultsKey.PanelShowFanControl))
        {
            store.Set(DefaultsKey.PanelShowFanControl, wasShown);
        }
        var fanAvailability = DefaultsKey.FeatureAvailable("fanControl");
        if (wasShown && !store.Contains(fanAvailability))
        {
            store.Set(fanAvailability, true);
        }
        store.Remove(DefaultsKey.MonitorShowFanControlBeta);
    }

    /// <summary>The old single switch also flipped sideways scrolling; keep that behavior for existing users.</summary>
    public static void MigrateScrollInverterAxes(ISettingsStore store)
    {
        if (!store.Contains(DefaultsKey.ScrollInverterHorizontalEnabled))
        {
            store.Set(DefaultsKey.ScrollInverterHorizontalEnabled, store.Bool(DefaultsKey.ScrollInverterEnabled));
        }
    }

    public static void MigrateWhatsAppDownloadsEnabled(ISettingsStore store)
    {
        if (store.Contains(DefaultsKey.WhatsAppDownloadsEnabled))
        {
            return;
        }
        if (store.Bool(DefaultsKey.WhatsAppDownloadsAutomaticEnabled) || store.Bool(DefaultsKey.WhatsAppOrganizerEnabled))
        {
            store.Set(DefaultsKey.WhatsAppDownloadsEnabled, true);
        }
    }

    public static void MigrateBatteryTemperatureVisibility(ISettingsStore store)
    {
        if (!store.Contains(DefaultsKey.MonitorPwrTemperature))
        {
            var temps = store.Contains(DefaultsKey.MonitorSysTemps) ? store.Bool(DefaultsKey.MonitorSysTemps) : true;
            store.Set(DefaultsKey.MonitorPwrTemperature, temps);
        }
    }

    /// <summary>A pre-release build opts into the beta channel once per version.</summary>
    public static void ActivateBetaChannelIfRunningBeta(ISettingsStore store, string appVersion)
    {
        if (!AppInfo.IsPreRelease(appVersion))
        {
            return;
        }
        var marker = DefaultsKey.BetaChannelActivatedFor(appVersion);
        if (store.Bool(marker))
        {
            return;
        }
        store.Set(marker, true);
        store.Set(DefaultsKey.IncludeBetaUpdates, true);
    }

    /// <summary>The legacy single temperature metric fans out into the three per-component ones.</summary>
    public static void MigrateLegacyMenuBarTemperatureMetric(ISettingsStore store)
    {
        if (!store.Contains(DefaultsKey.MenuBarTemperature))
        {
            return;
        }
        var anyNew = store.Contains(DefaultsKey.MenuBarCPUTemperature)
            || store.Contains(DefaultsKey.MenuBarGPUTemperature)
            || store.Contains(DefaultsKey.MenuBarBatteryTemperature);
        if (!anyNew && store.Bool(DefaultsKey.MenuBarTemperature))
        {
            store.Set(DefaultsKey.MenuBarCPUTemperature, true);
            store.Set(DefaultsKey.MenuBarGPUTemperature, true);
            store.Set(DefaultsKey.MenuBarBatteryTemperature, true);
        }
        var order = DefaultsSanitizers.MenuBarMetricOrder(store.String(DefaultsKey.MenuBarMetricOrder));
        store.Set(DefaultsKey.MenuBarMetricOrder, string.Join(",", order));
        store.Remove(DefaultsKey.MenuBarTemperature);
    }

    public static void MigrateSilentHeadphonesDisconnectVolume(ISettingsStore store)
    {
        if (store.Contains(DefaultsKey.MixerHeadphonesDisconnectVolumePercent)
            && store.Int(DefaultsKey.MixerHeadphonesDisconnectVolumePercent) < 10)
        {
            store.Set(DefaultsKey.MixerHeadphonesDisconnectVolumePercent, 25);
        }
    }
}
