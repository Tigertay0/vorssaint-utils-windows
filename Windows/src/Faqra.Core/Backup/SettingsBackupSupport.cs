// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/SettingsBackupSupport.swift. The media-preset and mouse-exception
// transforms arrive with those features; the key partition and type checks are complete.

using Faqra.Core.Defaults;
using Faqra.Core.Features;

namespace Faqra.Core.Backup;

public static class SettingsBackupSupport
{
    // Envelope keys are kept verbatim so a backup written by the macOS app is recognized.
    public const string FormatVersionKey = "vorssaintBackupVersion";
    public const string AppVersionKey = "vorssaintBackupAppVersion";
    public const string SettingsKey = "settings";
    public const int FormatVersion = 1;

    /// <summary>Preferences with no registered default that still belong in a backup.</summary>
    public static readonly IReadOnlyList<string> UnregisteredPreferenceKeys =
    [
        DefaultsKey.AutoQuitEnabled, DefaultsKey.ShelfEnabled, DefaultsKey.FinderCutPasteEnabled,
        "textSnippets", DefaultsKey.RadialMenuItems, "radialMenuProfiles",
        DefaultsKey.CommandBarLinks, DefaultsKey.CommandBarRowShortcuts,
        DefaultsKey.Language, DefaultsKey.AppVolumes, DefaultsKey.AppOutputDevices, DefaultsKey.MixerHiddenApps,
        DefaultsKey.PreferredInputDevice, DefaultsKey.SoundOutputSwitcherDeviceUIDs,
        DefaultsKey.MenuBarCPU, DefaultsKey.MenuBarGPU, DefaultsKey.MenuBarMemory, DefaultsKey.MenuBarNetwork,
        DefaultsKey.MenuBarBattery, DefaultsKey.MenuBarPower,
        DefaultsKey.PanelSectionOrder, DefaultsKey.PanelUtilityOrder, DefaultsKey.PanelControlOrder, DefaultsKey.PanelToggleOrder,
        DefaultsKey.PanelSystemOrder, DefaultsKey.PanelNetworkOrder, DefaultsKey.PanelDiskOrder, DefaultsKey.PanelPowerOrder,
        DefaultsKey.PanelCollapsedSections, "quickLauncherItemOrder",
        DefaultsKey.NotchQuickAccessSide, DefaultsKey.NotchQuickAccessSecond, DefaultsKey.NotchQuickAccessThird,
        DefaultsKey.HasOnboarded, DefaultsKey.OnboardingStep, DefaultsKey.FeaturesOnboardingVersion,
        DefaultsKey.LastUpdateIntroVersion, DefaultsKey.SupportUpdateIntroVersion, DefaultsKey.UpdateHighlightsSeenVersion,
        DefaultsKey.PanelCollapsedResetVersion,
    ];

    /// <summary>Per-machine or per-moment state that never travels in a backup.</summary>
    public static readonly IReadOnlyList<string> MachineStateKeys =
    [
        "bluetoothSleepRestorePending", DefaultsKey.MicMuteActive, "micMuteSavedVolume", "micMuteSavedVolumes", "micMuteMutedDevices",
        "cleanerLastAutoRun", "appUpdatesLastCheck", "appUpdatesLastCount", "appUpdatesNotifiedIDs", "cleanerLastAutoFreed",
        "whatsAppDownloadsAutomaticStartDate", "whatsAppDownloadsLastAutoRun", "whatsAppDownloadsLastCleanup",
        "whatsAppDownloadsLastCleanupCount", "whatsAppDownloadsLastCleanupBytes", "whatsAppDownloadsLastCleanupFailed",
        "whatsAppDownloadsLastCleanupAutomatic", "whatsAppDownloadsExclusions", "whatsAppDownloadsAccessConfirmed",
        "whatsAppOrganizerDestinationPath", "whatsAppOrganizerRecords", "whatsAppOrganizerUndoTransaction",
        "whatsAppOrganizerLastRun", "whatsAppOrganizerLastMoved", "whatsAppOrganizerLastDuplicates", "whatsAppOrganizerLastFailed",
        DefaultsKey.CommandBarUsage, DefaultsKey.CommandBarQueryHabits, DefaultsKey.CommandBarFileScopes,
        DefaultsKey.NotchDownloadsFolderBookmark, "mediaImageWatermarkLogoPath",
        DefaultsKey.SimulateUpdate, DefaultsKey.UpdateShowcaseIntroVersion, DefaultsKey.UpdateShowcaseMediaOverride,
        "unifiedScreenCaptureShortcutMigrated", "restoredScreenCaptureShortcutsMigrated", "orphanedCaptureShortcutMigrated",
        DefaultsKey.SettingsWindowWidth, DefaultsKey.SettingsWindowHeight,
        "screenshotLoupeLastZoom", "screenshotSharingDeveloperEndpoint", "recorderSystemAudioTapVerified",
        "fanControlRecoveryNeeded", "fanControlHelperVersion", "switcherNativeHotkeysSuppressed", "systemShortcutsSuppressed",
        "brightnessDDCWriteOnlyPaths",
    ];

    private static readonly Lazy<IReadOnlySet<string>> ExportKeySet = new(() =>
    {
        var keys = new HashSet<string>(RegisteredDefaults.All.Keys, StringComparer.Ordinal);
        keys.UnionWith(AppFeatures.AvailabilityDefaults.Keys);
        keys.UnionWith(UnregisteredPreferenceKeys);
        keys.ExceptWith(MachineStateKeys);
        return keys;
    });

    /// <summary>registered ∪ availability ∪ unregistered preferences − machine state.</summary>
    public static IReadOnlySet<string> ExportKeys() => ExportKeySet.Value;

    /// <summary>A complete snapshot: every export key whose value (stored or registered) is non-null.</summary>
    public static Dictionary<string, object> Payload(string appVersion, Func<string, object?> valueFor)
    {
        var settings = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var key in ExportKeys())
        {
            if (valueFor(key) is { } value)
            {
                settings[key] = value;
            }
        }
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [FormatVersionKey] = (long)FormatVersion,
            [AppVersionKey] = appVersion,
            [SettingsKey] = settings,
        };
    }

    public static int? FormatVersionOf(IReadOnlyDictionary<string, object> payload)
    {
        if (!payload.TryGetValue(FormatVersionKey, out var raw))
        {
            return null;
        }
        return raw switch
        {
            long l => (int)l,
            int i => i,
            double d when Math.Abs(d - Math.Round(d)) < double.Epsilon => (int)d,
            string s when int.TryParse(s.Trim(), out var parsed) => parsed,
            _ => null,
        };
    }

    /// <summary>
    /// The importable subset of a payload: null unless the envelope is a known version and carries
    /// a settings object; unknown, renamed, never-exported and mistyped keys are dropped silently.
    /// </summary>
    public static Dictionary<string, object>? SanitizedSettings(IReadOnlyDictionary<string, object> payload)
    {
        var version = FormatVersionOf(payload);
        if (version is null || version < 1 || version > FormatVersion)
        {
            return null;
        }
        if (!payload.TryGetValue(SettingsKey, out var raw) || raw is not IReadOnlyDictionary<string, object> settings)
        {
            return null;
        }
        var exportKeys = ExportKeys();
        return settings
            .Where(pair => exportKeys.Contains(pair.Key) && ValueLooksRight(pair.Key, pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// A value is accepted when the key has no registered default (the allow-list is the only gate)
    /// or when it matches the registered default's type. Bool and integer are distinguished strictly.
    /// </summary>
    public static bool ValueLooksRight(string key, object value)
    {
        if (key is DefaultsKey.NotchQuickAccessSide or DefaultsKey.NotchQuickAccessSecond or DefaultsKey.NotchQuickAccessThird)
        {
            return value is string;
        }
        if (!RegisteredDefaults.All.TryGetValue(key, out var registered))
        {
            return true;
        }
        return registered switch
        {
            bool => value is bool,
            long or int => value is long or int,
            double => value is double or long or int && IsFinite(value),
            string => value is string,
            byte[] => value is byte[],
            IReadOnlyList<string> => value is IReadOnlyList<string>,
            IReadOnlyDictionary<string, string> => value is IReadOnlyDictionary<string, string>,
            IReadOnlyDictionary<string, double> => value is IReadOnlyDictionary<string, double>,
            _ => true,
        };
    }

    private static bool IsFinite(object value) => value is not double d || double.IsFinite(d);
}
