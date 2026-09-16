// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/Defaults.swift (enum DefaultsKey): every key the Stage 1 surfaces read.
// Constant names follow upstream; where upstream's key string differs from its constant name the
// string is kept (e.g. DefaultDuration = "defaultDurationMinutes") so backups stay compatible.

namespace Faqra.Core.Defaults;

public static partial class DefaultsKey
{
    // Appearance
    public const string Appearance = "appAppearance";                    // system | light | dark
    public const string LiquidGlassEnabled = "liquidGlassEnabled";       // macOS 26 styling; no Windows meaning

    // Settings window (machine state, never exported)
    public const string SettingsWindowWidth = "settingsWindowWidth";     // last user-chosen content size (0 = unset)
    public const string SettingsWindowHeight = "settingsWindowHeight";

    // Launch at login
    public const string LaunchAtLoginWanted = "launchAtLoginWanted";     // the user's stored wish; the OS record is not trusted

    // Panel layout
    public const string PanelSectionOrder = "panelSectionOrder";         // comma-joined PanelSectionId order
    public const string PanelUtilityOrder = "panelUtilityOrder";
    public const string PanelControlOrder = "panelControlOrder";
    public const string PanelToggleOrder = "panelToggleOrder";
    public const string PanelSystemOrder = "panelSystemOrder";
    public const string PanelNetworkOrder = "panelNetworkOrder";
    public const string PanelDiskOrder = "panelDiskOrder";
    public const string PanelPowerOrder = "panelPowerOrder";
    public const string PanelCollapsedSections = "panelCollapsedSections";
    public const string PanelCollapsedResetVersion = "panelCollapsedResetVersion";
    public const string PanelNavigationEnabled = "panelNavigationEnabled";
    public const string PanelShowKeepAwake = "panelShowKeepAwake";
    public const string PanelShowBrightness = "panelShowBrightness";
    public const string PanelShowUtilities = "panelShowUtilities";
    public const string PanelShowControls = "panelShowControls";
    public const string PanelShowToggles = "panelShowToggles";
    public const string PanelShowFanControl = "panelShowFanControl";
    public const string MonitorShowFanControlBeta = "monitorShowFanControlBeta"; // legacy, migrated then deleted
    public const string MonitorShowSystem = "monitorShowSystem";
    public const string MonitorShowNetwork = "monitorShowNetwork";
    public const string MonitorShowDisk = "monitorShowDisk";
    public const string MonitorShowPower = "monitorShowPower";
    public const string MonitorShowMixer = "monitorShowMixer";
    public const string PanelControlNotch = "panelControlNotch";
    public const string PanelToggleMicMute = "panelUtilityMicMute";     // deliberate legacy spelling
    public const string PanelUtilityCommandBar = "panelUtilityCommandBar";

    // Menu bar (tray) metrics
    public const string MenuBarCPU = "menuBarCPU";
    public const string MenuBarGPU = "menuBarGPU";
    public const string MenuBarMemory = "menuBarMemory";
    public const string MenuBarNetwork = "menuBarNetwork";
    public const string MenuBarBattery = "menuBarBattery";
    public const string MenuBarPower = "menuBarPower";
    public const string MenuBarCPUTemperature = "menuBarCPUTemperature";
    public const string MenuBarGPUTemperature = "menuBarGPUTemperature";
    public const string MenuBarBatteryTemperature = "menuBarBatteryTemperature";
    public const string MenuBarTemperature = "menuBarTemperature";       // legacy, migrated then removed
    public const string MenuBarDiskUsage = "menuBarDiskUsage";
    public const string MenuBarDiskActivity = "menuBarDiskActivity";
    public const string MenuBarBatteryTime = "menuBarBatteryTime";
    public const string MenuBarPeripheralBattery = "menuBarPeripheralBattery";
    public const string MenuBarFanSpeed = "menuBarFanSpeed";
    public const string MenuBarPreset = "menuBarPreset";                 // dense
    public const string MenuBarMetricSpacing = "menuBarMetricSpacing";   // standard | compact
    public const string MenuBarMetricAppearance = "menuBarMetricAppearance"; // values | bars
    public const string MenuBarUsageBarNormalColor = "menuBarUsageBarNormalColor";
    public const string MenuBarUsageBarElevatedColor = "menuBarUsageBarElevatedColor";
    public const string MenuBarUsageBarCriticalColor = "menuBarUsageBarCriticalColor";
    public const string MenuBarUsageBarMediumThreshold = "menuBarUsageBarMediumThreshold";
    public const string MenuBarUsageBarHighThreshold = "menuBarUsageBarHighThreshold";
    public const string MenuBarHideIconWithMetrics = "menuBarHideIconWithMetrics";
    public const string MenuBarMetricOrder = "menuBarMetricOrder";
    public const string MenuBarCombineTemperatures = "menuBarCombineTemperatures";
    public const string MenuBarSeparateMetrics = "menuBarSeparateMetrics";
    public const string MenuBarNetworkUploadFirst = "menuBarNetworkUploadFirst";
    public const string MenuBarLabelStyle = "menuBarLabelStyle";         // compact | classic
    public const string MenuBarMemoryStyle = "menuBarMemoryStyle";       // dot | percent | both
    public const string MonitorMemoryMetric = "monitorMemoryMetric";     // used | app
    public const string MonitorInterval = "monitorIntervalSeconds";      // 1 | 2 | 5
    public const string TemperatureUnit = "temperatureUnit";             // celsius | fahrenheit
    public const string ShowCountdown = "showCountdownInMenuBar";
    public const string StatusItemPlacementGeneration = "statusItemPlacementGeneration";

    // Keep awake
    public const string DefaultDuration = "defaultDurationMinutes";      // 0 = indefinite
    public const string BatteryLimit = "batteryLimitPercent";            // 0 = never
    public const string KeepAwakeAutoStart = "keepAwakeAutoStart";
    public const string KeepAwakeRightClickToggle = "keepAwakeRightClickToggle";
    public const string KeepAwakeAllowDisplaySleep = "keepAwakeAllowDisplaySleep";
    public const string KeepAwakeExternalDisplay = "keepAwakeExternalDisplay";
    public const string KeepAwakeConnectedToPower = "keepAwakeConnectedToPower";
    public const string KeepAwakeRunningApps = "keepAwakeRunningApps";
    public const string KeepAwakeRunningAppBundleIDs = "keepAwakeRunningAppBundleIDs";
    public const string KeepAwakePauseWhenLocked = "keepAwakePauseWhenLocked";
    public const string KeepAwakeMouseJiggleInterval = "keepAwakeMouseJiggleIntervalMinutes";
    public const string HotkeyEnabled = "hotkeyEnabled";
    public const string KeepAwakeShortcut = "keepAwakeShortcut";
    public const string KeepAwakeIconTint = "keepAwakeIconTint";
    public const string KeepAwakeActiveIcon = "keepAwakeActiveIcon";
    public const string ClamshellPreferred = "clamshellPreferred";       // macOS closed-lid rule; no Windows meaning

    // Mixer / audio
    public const string AppVolumes = "appVolumes";                       // app id -> 0…2
    public const string AppOutputDevices = "appOutputDevices";           // app id -> device id
    public const string MixerShowFinder = "mixerShowFinder";
    public const string MixerHideInactiveApps = "mixerHideInactiveApps";
    public const string MixerHiddenApps = "mixerHiddenApps";             // persistence id -> display name
    public const string MixerLowerVolumeOnHeadphonesDisconnect = "mixerLowerVolumeOnHeadphonesDisconnect";
    public const string MixerHeadphonesDisconnectVolumePercent = "mixerHeadphonesDisconnectVolumePercent";
    public const string SoundOutputSwitcherShortcut = "soundOutputSwitcherShortcut";
    public const string SoundOutputSwitcherDeviceUIDs = "soundOutputSwitcherDeviceUIDs";
    public const string PreferredInputDevice = "preferredInputDevice";
    public const string MicMuteShortcutEnabled = "micMuteShortcutEnabled";
    public const string MicMuteShortcut = "micMuteShortcut";
    public const string MicMuteActive = "micMuteActive";
    public const string MicMuteMenuBarIndicator = "micMuteMenuBarIndicator";
    public const string MusicBlockReplacementPath = "musicBlockReplacementPath";

    // Monitor panel blocks
    public const string MonitorGraphCPU = "monitorGraphCPU";
    public const string MonitorGraphGPU = "monitorGraphGPU";
    public const string MonitorGraphMemory = "monitorGraphMemory";
    public const string MonitorGraphNetwork = "monitorGraphNetwork";
    public const string MonitorGraphDisk = "monitorGraphDisk";
    public const string MonitorGraphPower = "monitorGraphPower";
    public const string MonitorGraphBattery = "monitorGraphBattery";
    public const string MonitorSysTemps = "monitorSysTemps";
    public const string MonitorSysCPU = "monitorSysCPU";
    public const string MonitorSysGPU = "monitorSysGPU";
    public const string MonitorSysBattery = "monitorSysBattery";
    public const string MonitorSysMemory = "monitorSysMemory";
    public const string MonitorSysAlerts = "monitorSysAlerts";
    public const string MonitorSysUptime = "monitorSysUptime";
    public const string MonitorNetSpeed = "monitorNetSpeed";
    public const string MonitorNetApps = "monitorNetApps";
    public const string MonitorNetTotals = "monitorNetTotals";
    public const string MonitorNetTest = "monitorNetTest";
    public const string MonitorDiskUsage = "monitorDiskUsage";
    public const string MonitorDiskActivity = "monitorDiskActivity";
    public const string MonitorDiskSMART = "monitorDiskSMART";
    public const string MonitorDiskProtection = "monitorDiskProtection";
    public const string MonitorDiskTools = "monitorDiskTools";
    public const string MonitorPwrTemperature = "monitorPwrTemperature";
    public const string MonitorPwrSystem = "monitorPwrSystem";
    public const string MonitorPwrAdapter = "monitorPwrAdapter";
    public const string MonitorPwrBattery = "monitorPwrBattery";
    public const string MonitorPwrTimeRemaining = "monitorPwrTimeRemaining";
    public const string MonitorPwrHealth = "monitorPwrHealth";
    public const string MonitorAlertCPUThreshold = "monitorAlertCPUThreshold";
    public const string MonitorAlertCPUTemperatureThreshold = "monitorAlertCPUTemperatureThreshold";
    public const string MonitorAlertBatteryTemperatureThreshold = "monitorAlertBatteryTemperatureThreshold";
    public const string MonitorAlertDiskFreePercent = "monitorAlertDiskFreePercent";
    public const string MonitorAlertBatteryPercent = "monitorAlertBatteryPercent";
    public const string MonitorAlertCooldownMinutes = "monitorAlertCooldownMinutes";

    // Notch (island)
    public const string NotchDisplay = "notchDisplay";

    /// <summary>
    /// Faqra-only: which screen edge the island is attached to. Upstream has no equivalent because a
    /// Mac's notch is always at the top, so the key carries the Faqra prefix to avoid ever colliding
    /// with an upstream key of the same meaning.
    /// </summary>
    public const string IslandEdge = "faqraIslandEdge";               // top | left | right
    public const string NotchSize = "notchSize";
    public const string NotchCustomWidth = "notchCustomWidth";
    public const string NotchCustomHeight = "notchCustomHeight";
    public const string NotchShowPlayingMusic = "notchShowPlayingMusic";
    public const string NotchIdleContent = "notchIdleContent";
    public const string NotchHiddenControls = "notchHiddenControls";
    public const string NotchControlOrder = "notchControlOrder";
    public const string NotchHapticFeedback = "notchHapticFeedback";
    public const string NotchShelf = "notchShelf";
    public const string NotchDragReveal = "notchDragReveal";
    public const string NotchCaptureControls = "notchCaptureControls";
    public const string NotchQuickPanel = "notchQuickPanel";
    public const string NotchAppPanel = "notchAppPanel";
    public const string NotchHoverExpands = "notchHoverExpands";
    public const string NotchOpenOnHover = "notchOpenOnHover";
    public const string NotchDismissNativeNotifications = "notchDismissNativeNotifications";
    public const string NotchTimerMode = "notchTimerMode";
    public const string NotchTimerSoundEnabled = "notchTimerSoundEnabled";
    public const string NotchPomodoroFocusMinutes = "notchPomodoroFocusMinutes";
    public const string NotchPomodoroShortBreakMinutes = "notchPomodoroShortBreakMinutes";
    public const string NotchPomodoroLongBreakMinutes = "notchPomodoroLongBreakMinutes";
    public const string NotchPomodoroLongBreakInterval = "notchPomodoroLongBreakInterval";
    public const string NotchPomodoroTotalSessions = "notchPomodoroTotalSessions";
    public const string NotchCameraEnabled = "notchCameraEnabled";
    public const string NotchLyricsOnline = "notchLyricsOnline";
    public const string NotchModuleOrder = "notchModuleOrder";
    public const string NotchQuickAccessLayout = "notchQuickAccessLayout";
    public const string NotchQuickAccessSide = "notchQuickAccessSide";
    public const string NotchQuickAccessSecond = "notchQuickAccessSecond";
    public const string NotchQuickAccessThird = "notchQuickAccessThird";
    public const string NotchBattery = "notchBattery";
    public const string NotchClipboard = "notchClipboard";
    public const string NotchCapture = "notchCapture";
    public const string NotchMusicActivity = "notchMusicActivity";       // legacy
    public const string NotchShowInCaptures = "notchShowInCaptures";
    public const string NotchHideInCaptures = "notchHideInCaptures";     // legacy

    // Command bar
    public const string CommandBarShortcutEnabled = "commandBarShortcutEnabled";
    public const string CommandBarShortcut = "commandBarShortcut";
    public const string CommandBarCompactMode = "commandBarCompactMode";
    public const string CommandBarUsage = "commandBarUsage";
    public const string CommandBarQueryHabits = "commandBarQueryHabits";
    public const string CommandBarDisabledSources = "commandBarDisabledSources";
    public const string CommandBarAliases = "commandBarAliases";
    public const string CommandBarPins = "commandBarPins";
    public const string CommandBarHidden = "commandBarHidden";
    public const string CommandBarLinks = "commandBarLinks";
    public const string CommandBarRowShortcuts = "commandBarRowShortcuts";
    public const string CommandBarPositionOffset = "commandBarPositionOffset";
    public const string CommandBarFileScopes = "commandBarFileScopes";
    public const string CommandBarFileIgnores = "commandBarFileIgnores";
    public const string KillProcessCommandBarEnabled = "killProcessCommandBarEnabled";

    // Updates
    public const string AutoCheckUpdates = "autoCheckUpdates";
    public const string IncludeBetaUpdates = "includeBetaUpdates";
    public const string ReleaseNotesOnUpdate = "releaseNotesOnUpdate";
    public const string UpdateLastInstallFailure = "updateLastInstallFailure";
    public const string LastUpdateIntroVersion = "lastUpdateIntroVersion";
    public const string SupportUpdateIntroVersion = "supportUpdateIntroVersion";
    public const string UpdateHighlightsSeenVersion = "updateHighlightsSeenVersion";
    public const string UpdateShowcaseIntroVersion = "updateShowcaseIntroVersion";
    public const string UpdateShowcaseMediaOverride = "updateShowcaseMediaOverride";
    public const string SimulateUpdate = "simulateUpdate";
    public const string SimulateBetaUI = "simulateBetaUI";

    /// <summary>One-shot marker that the beta channel was switched on for a pre-release build.</summary>
    public static string BetaChannelActivatedFor(string version) => $"betaChannelActivatedFor.{version}";
}
