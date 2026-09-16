// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/Defaults.swift (enum DefaultsKey). Key strings are kept
// identical to upstream so a macOS settings backup imports unchanged. Grows per milestone.

namespace Faqra.Core.Defaults;

public static partial class DefaultsKey
{
    // App
    public const string Language = "appLanguage";                       // AppLanguage raw value
    public const string OnboardingStep = "onboardingStep";              // resume point if onboarding is interrupted
    public const string FeaturesOnboardingVersion = "featuresOnboardingVersion"; // last feature-tour marker handled
    public const string HasOnboarded = "hasOnboarded";

    /// <summary>Availability key of a feature: "featureAvailable.&lt;raw value&gt;".</summary>
    public static string FeatureAvailable(string featureId) => $"featureAvailable.{featureId}";

    // Feature enable keys referenced by the catalog (Sources/Vorssaint/Core/FeatureCatalog.swift)
    public const string SwitcherEnabled = "switcherEnabled";
    public const string SwitcherSimpleMode = "switcherSimpleMode";
    public const string DockPreviewEnabled = "dockPreviewEnabled";
    public const string DockClickMinimize = "dockClickMinimize";
    public const string DockClickHide = "dockClickHide";
    public const string DockClickCycleWindows = "dockClickCycleWindows";
    public const string WindowMaximizeEnabled = "windowMaximizeEnabled";
    public const string AutoQuitEnabled = "autoQuitEnabled";
    public const string ScrollInverterEnabled = "scrollInverterEnabled";
    public const string ScrollInverterHorizontalEnabled = "scrollInverterHorizontalEnabled";
    public const string FocusFollowsMouseEnabled = "focusFollowsMouseEnabled";
    public const string SmoothScrollEnabled = "smoothScrollEnabled";
    public const string MouseAccelerationDisabled = "mouseAccelerationDisabled";
    public const string MouseNavigationEnabled = "mouseNavigationEnabled";
    public const string MouseButtonShortcutsEnabled = "mouseButtonShortcutsEnabled";
    public const string MouseSpacesGestureEnabled = "mouseSpacesGestureEnabled";
    public const string MiddleClickEnabled = "middleClickEnabled";
    public const string KeyboardDebounceEnabled = "keyboardDebounceEnabled";
    public const string QuitProtectionQuitEnabled = "quitProtectionQuitEnabled";
    public const string QuitProtectionCloseEnabled = "quitProtectionCloseEnabled";
    public const string TextSnippetsEnabled = "textSnippetsEnabled";
    public const string SnippetLibraryEnabled = "snippetLibraryEnabled";
    public const string SuperKeyEnabled = "superKeyEnabled";
    public const string SuperKeySource = "superKeySource";
    public const string MouseClickDebounceEnabled = "mouseClickDebounceEnabled";
    public const string ClipboardHistoryEnabled = "clipboardHistoryEnabled";
    public const string PastePlainEnabled = "pastePlainEnabled";
    public const string FinderCutPasteEnabled = "finderCutPasteEnabled";
    public const string FinderPasteImageAsFile = "finderPasteImageAsFile";
    public const string FinderRenameEnabled = "finderRenameEnabled";
    public const string ShelfEnabled = "shelfEnabled";
    public const string UrlCleanerEnabled = "urlCleanerEnabled";
    public const string SoundOutputSwitcherEnabled = "soundOutputSwitcherEnabled";
    public const string MusicBlockEnabled = "musicBlockEnabled";
    public const string BrightnessControlEnabled = "brightnessControlEnabled";
    public const string BrightnessKeysEnabled = "brightnessKeysEnabled";
    public const string BrightnessOSDEnabled = "brightnessOSDEnabled";
    public const string ExtraBrightnessEnabled = "extraBrightnessEnabled";
    public const string BluetoothSleepEnabled = "bluetoothSleepEnabled";
    public const string RadialMenuEnabled = "radialMenuEnabled";
    public const string RadialMenuMouseButton = "radialMenuMouseButton";
    public const string RadialMenuItems = "radialMenuItems";
    public const string WindowLayoutShortcutsEnabled = "windowLayoutShortcutsEnabled";
    public const string WindowEdgeSnapEnabled = "windowEdgeSnapEnabled";
    public const string WindowEdgeSnapDisabledZones = "windowEdgeSnapDisabledZones";
    public const string WindowGestureEnabled = "windowGestureEnabled";
    public const string PreciseVolumeRollerEnabled = "preciseVolumeRollerEnabled";
    public const string KeepAwakeMouseJiggleEnabled = "keepAwakeMouseJiggleEnabled";
    public const string AppUpdatesCheckFrequency = "appUpdatesCheckFrequency";   // off | daily | weekly
    public const string AppUpdatesNotify = "appUpdatesNotify";
    public const string CleanerScheduleFrequency = "cleanerScheduleFrequency";   // off | daily | weekly
    public const string CleanerScheduleNotify = "cleanerScheduleNotify";
    public const string WhatsAppDownloadsEnabled = "whatsAppDownloadsEnabled";
    public const string WhatsAppDownloadsAutomaticEnabled = "whatsAppDownloadsAutomaticEnabled";
    public const string WhatsAppDownloadsNotify = "whatsAppDownloadsNotify";
    public const string WhatsAppOrganizerEnabled = "whatsAppOrganizerEnabled";
    public const string RecorderSystemAudio = "recorderSystemAudio";
    public const string RecorderMicrophone = "recorderMicrophone";

    // Monitor alerts
    public const string MonitorAlertCPU = "monitorAlertCPU";
    public const string MonitorAlertCPUTemperature = "monitorAlertCPUTemperature";
    public const string MonitorAlertBatteryTemperature = "monitorAlertBatteryTemperature";
    public const string MonitorAlertMemory = "monitorAlertMemory";
    public const string MonitorAlertDisk = "monitorAlertDisk";
    public const string MonitorAlertBattery = "monitorAlertBattery";

    // Notch (island)
    public const string NotchEnabled = "notchEnabled";
    public const string NotchGesturesEnabled = "notchGesturesEnabled";
    public const string NotchTimerEnabled = "notchTimerEnabled";
    public const string NotchAccessoriesEnabled = "notchAccessoriesEnabled";
    public const string NotchLyricsEnabled = "notchLyricsEnabled";
    public const string NotchQueueEnabled = "notchQueueEnabled";
    public const string NotchDownloadsEnabled = "notchDownloadsEnabled";
    public const string NotchDownloadsFolderBookmark = "notchDownloadsFolderBookmark";
    public const string NotchNotificationsEnabled = "notchNotificationsEnabled";
    public const string NotchCalendarEnabled = "notchCalendarEnabled";
    public const string NotchHiddenModules = "notchHiddenModules";
    public const string NotchVolume = "notchVolume";
    public const string NotchKeyboardLight = "notchKeyboardLight";
    public const string NotchBrightness = "notchBrightness";
    public const string NotchClipboardWindow = "notchClipboardWindow";
}
