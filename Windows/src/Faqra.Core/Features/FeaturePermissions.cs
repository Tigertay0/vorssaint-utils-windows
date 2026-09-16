// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/FeatureCatalog.swift (permissions, onboardingPermissions, activeFeatures)
// Windows has no consent grants for these hooks; the hub's Permissions tab stays informational.

using Faqra.Core.Defaults;

namespace Faqra.Core.Features;

public static partial class AppFeatures
{
    /// <summary>Which permissions the feature can use at all (upstream's macOS grants).</summary>
    public static IReadOnlyList<AppPermission> Permissions(this AppFeature feature) => feature switch
    {
        AppFeature.NotchDownloads => [AppPermission.FilesAndFolders],
        AppFeature.NotchNotifications => [AppPermission.Accessibility],
        AppFeature.NotchCalendar => [AppPermission.Calendar],
        AppFeature.Notch => [AppPermission.Accessibility, AppPermission.AutomationPlayback],
        AppFeature.ScrollInverter or AppFeature.FocusFollowsMouse or AppFeature.SmoothScroll or AppFeature.MouseNavigation
            or AppFeature.MouseButtonShortcuts or AppFeature.MiddleClick or AppFeature.KeyboardDebounce
            or AppFeature.TextSnippets or AppFeature.SuperKey or AppFeature.MouseClickDebounce
            or AppFeature.DockClick or AppFeature.WindowMaximizer or AppFeature.WindowLayout
            or AppFeature.AutoQuit or AppFeature.QuitWindowProtection or AppFeature.CleaningMode
            or AppFeature.PastePlain or AppFeature.RadialMenu or AppFeature.CommandBar
            or AppFeature.FinderRename or AppFeature.KeepAwake or AppFeature.Brightness => [AppPermission.Accessibility],
        AppFeature.FinderCutPaste => [AppPermission.Accessibility, AppPermission.AutomationFinder],
        AppFeature.QuickToggles => [AppPermission.AutomationFinder],
        AppFeature.Switcher or AppFeature.DockPreview => [AppPermission.Accessibility, AppPermission.ScreenRecording],
        AppFeature.ScreenOCR or AppFeature.Screenshot => [AppPermission.ScreenRecording],
        AppFeature.ScreenRecorder => [AppPermission.ScreenRecording, AppPermission.Accessibility, AppPermission.AudioCapture, AppPermission.Microphone],
        AppFeature.CameraPreview => [AppPermission.Camera],
        AppFeature.Cleaner => [AppPermission.FullDiskAccess, AppPermission.FilesAndFolders, AppPermission.Notifications],
        AppFeature.Uninstaller => [AppPermission.FullDiskAccess, AppPermission.AutomationFinder],
        AppFeature.Homebrew => [AppPermission.AutomationTerminal, AppPermission.AppManagement],
        AppFeature.AppUpdates => [AppPermission.Notifications, AppPermission.AppManagement],
        AppFeature.DiskImageInstaller => [AppPermission.AppManagement],
        AppFeature.Mixer => [AppPermission.AudioCapture, AppPermission.Accessibility],
        AppFeature.MonitorCPU or AppFeature.MonitorMemory or AppFeature.MonitorDisk or AppFeature.MonitorPower => [AppPermission.Notifications],
        _ => [],
    };

    /// <summary>Broad grants worth explaining during first run; only Accessibility and Screen Recording qualify.</summary>
    public static IReadOnlyList<AppPermission> OnboardingPermissions(this AppFeature feature) => feature switch
    {
        AppFeature.KeepAwake or AppFeature.Brightness or AppFeature.RadialMenu or AppFeature.QuickToggles
            or AppFeature.Cleaner or AppFeature.Uninstaller or AppFeature.Homebrew or AppFeature.AppUpdates
            or AppFeature.Mixer or AppFeature.CameraPreview or AppFeature.MicMute => [],
        _ => feature.Permissions()
            .Where(permission => permission is AppPermission.Accessibility or AppPermission.ScreenRecording)
            .ToList(),
    };

    /// <summary>Monitor alert keys and the metric feature each belongs to.</summary>
    public static readonly IReadOnlyList<(string Key, AppFeature Feature)> MonitorAlertPairs =
    [
        (DefaultsKey.MonitorAlertCPU, AppFeature.MonitorCPU),
        (DefaultsKey.MonitorAlertCPUTemperature, AppFeature.MonitorCPU),
        (DefaultsKey.MonitorAlertBatteryTemperature, AppFeature.MonitorPower),
        (DefaultsKey.MonitorAlertMemory, AppFeature.MonitorMemory),
        (DefaultsKey.MonitorAlertDisk, AppFeature.MonitorDisk),
        (DefaultsKey.MonitorAlertBattery, AppFeature.MonitorPower),
    ];

    public static bool AnyMonitorAlertEnabled(Func<AppFeature, bool> isAvailable, Func<string, bool> boolFor) =>
        MonitorAlertPairs.Any(pair => boolFor(pair.Key) && isAvailable(pair.Feature));

    /// <summary>
    /// Features that are available, engaged and using <paramref name="permission"/> right now.
    /// Readers are injectable so the logic stays testable without a real store.
    /// </summary>
    public static IReadOnlyList<AppFeature> ActiveFeatures(
        AppPermission permission,
        Func<AppFeature, bool> isAvailable,
        Func<string, bool> boolFor,
        Func<string, string?> stringFor,
        Func<string, byte[]?>? dataFor = null)
    {
        dataFor ??= _ => null;
        return All.Where(feature => IsActive(feature, permission, isAvailable, boolFor, stringFor, dataFor)).ToList();
    }

    private static bool IsActive(
        AppFeature feature,
        AppPermission permission,
        Func<AppFeature, bool> isAvailable,
        Func<string, bool> boolFor,
        Func<string, string?> stringFor,
        Func<string, byte[]?> dataFor)
    {
        if (!feature.Permissions().Contains(permission) || !isAvailable(feature))
        {
            return false;
        }
        var keys = feature.EnabledKeys();
        if (keys.Count > 0 && !keys.Any(boolFor))
        {
            return false;
        }
        bool NotchShows(string module) =>
            isAvailable(AppFeature.Notch) && boolFor(DefaultsKey.NotchEnabled) && !HiddenModules(stringFor).Contains(module);

        return (feature, permission) switch
        {
            (AppFeature.Notch, AppPermission.AutomationPlayback) => !HiddenModules(stringFor).Contains("music"),
            (AppFeature.Switcher, AppPermission.ScreenRecording) => !boolFor(DefaultsKey.SwitcherSimpleMode),
            (AppFeature.NotchNotifications, AppPermission.Accessibility) => NotchShows("notifications"),
            (AppFeature.NotchDownloads, AppPermission.FilesAndFolders) =>
                NotchShows("downloads") && dataFor(DefaultsKey.NotchDownloadsFolderBookmark) is not null,
            (AppFeature.NotchCalendar, AppPermission.Calendar) => NotchShows("calendar"),
            (AppFeature.Notch, AppPermission.Accessibility) =>
                (boolFor(DefaultsKey.NotchVolume) && isAvailable(AppFeature.Mixer))
                || (boolFor(DefaultsKey.NotchKeyboardLight) && isAvailable(AppFeature.Brightness))
                || (boolFor(DefaultsKey.NotchBrightness) && isAvailable(AppFeature.Brightness) && boolFor(DefaultsKey.BrightnessControlEnabled))
                || (boolFor(DefaultsKey.NotchClipboardWindow) && isAvailable(AppFeature.ClipboardHistory)),
            // Upstream also inspects the radial menu's items for actions that need Accessibility;
            // the mouse trigger alone is mirrored here.
            (AppFeature.RadialMenu, AppPermission.Accessibility) => (stringFor(DefaultsKey.RadialMenuMouseButton) ?? "off") != "off",
            (AppFeature.KeepAwake, AppPermission.Accessibility) => boolFor(DefaultsKey.KeepAwakeMouseJiggleEnabled),
            (AppFeature.Mixer, AppPermission.Accessibility) => boolFor(DefaultsKey.PreciseVolumeRollerEnabled),
            (AppFeature.Brightness, AppPermission.Accessibility) =>
                boolFor(DefaultsKey.BrightnessKeysEnabled) || boolFor(DefaultsKey.BrightnessOSDEnabled),
            (AppFeature.MonitorCPU, AppPermission.Notifications) =>
                boolFor(DefaultsKey.MonitorAlertCPU) || boolFor(DefaultsKey.MonitorAlertCPUTemperature),
            (AppFeature.MonitorMemory, AppPermission.Notifications) => boolFor(DefaultsKey.MonitorAlertMemory),
            (AppFeature.MonitorDisk, AppPermission.Notifications) => boolFor(DefaultsKey.MonitorAlertDisk),
            (AppFeature.MonitorPower, AppPermission.Notifications) =>
                boolFor(DefaultsKey.MonitorAlertBattery) || boolFor(DefaultsKey.MonitorAlertBatteryTemperature),
            (AppFeature.AppUpdates, AppPermission.Notifications) =>
                (stringFor(DefaultsKey.AppUpdatesCheckFrequency) ?? "off") != "off" && boolFor(DefaultsKey.AppUpdatesNotify),
            (AppFeature.Cleaner, AppPermission.FilesAndFolders) => boolFor(DefaultsKey.WhatsAppDownloadsEnabled),
            (AppFeature.Cleaner, AppPermission.Notifications) =>
                ((stringFor(DefaultsKey.CleanerScheduleFrequency) ?? "off") != "off" && boolFor(DefaultsKey.CleanerScheduleNotify))
                || (boolFor(DefaultsKey.WhatsAppDownloadsEnabled)
                    && (boolFor(DefaultsKey.WhatsAppDownloadsAutomaticEnabled) || boolFor(DefaultsKey.WhatsAppOrganizerEnabled))
                    && boolFor(DefaultsKey.WhatsAppDownloadsNotify)),
            (AppFeature.ScreenRecorder, AppPermission.AudioCapture) => boolFor(DefaultsKey.RecorderSystemAudio),
            (AppFeature.ScreenRecorder, AppPermission.Microphone) => boolFor(DefaultsKey.RecorderMicrophone),
            _ => true,
        };
    }

    private static HashSet<string> HiddenModules(Func<string, string?> stringFor) =>
        (stringFor(DefaultsKey.NotchHiddenModules) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
}
