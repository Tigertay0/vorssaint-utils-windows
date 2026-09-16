// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/FeatureCatalog.swift (enum AppFeature, FeatureGroup, AppPermission)

namespace Faqra.Core.Features;

/// <summary>
/// Every feature the Features hub can switch off entirely. The raw value (see
/// <see cref="AppFeatures.RawValue"/>) is the stable identity persisted inside the availability
/// key, so members can be added but never renamed. All 66 upstream members are kept, including
/// the ones that have no Windows meaning, so settings stay compatible with upstream backups.
/// </summary>
public enum AppFeature
{
    // Windows and Dock
    Switcher, DockPreview, DockClick, WindowMaximizer, WindowLayout, AutoQuit,
    // Mouse and keyboard
    ScrollInverter, FocusFollowsMouse, SmoothScroll, MouseAcceleration, MouseNavigation, MouseButtonShortcuts, MiddleClick,
    MouseClickDebounce, KeyboardDebounce, TextSnippets, SuperKey, QuitWindowProtection,
    // Clipboard and files
    ClipboardHistory, PastePlain, FinderCutPaste, FinderRename, Shelf, UrlCleaner, DiskImageInstaller,
    // Sound
    Mixer, SoundOutputSwitcher, MicMute, MusicBlock,
    // Energy and display
    KeepAwake, Brightness, ExtraBrightness, BluetoothSleep,
    // Tools
    QuickLauncher, QuickToggles, ColorPicker, ScreenOCR, CleaningMode, MediaTools,
    Cleaner, Uninstaller, Homebrew, AppUpdates, Screenshot, CameraPreview, RadialMenu, Scratchpad,
    CommandBar, ScreenRecorder, KillProcess, Notch, NotchCalendar, NotchNotifications, NotchGestures, NotchTimer,
    NotchAccessories, NotchLyrics, NotchQueue, NotchDownloads,
    // System monitor, one entry per metric family
    MonitorCPU, MonitorGPU, MonitorMemory, MonitorNetwork, MonitorDisk, MonitorPower, FanControl,
}

/// <summary>Hub sections, in display order.</summary>
public enum FeatureGroup
{
    WindowsDock, MouseKeyboard, ClipboardFiles, Sound, EnergyDisplay, Tools, Monitor,
}

/// <summary>System permissions surfaced by the hub's transparency portal (macOS grants; informational on Windows).</summary>
public enum AppPermission
{
    Accessibility, ScreenRecording, FullDiskAccess, FilesAndFolders, Notifications,
    AutomationFinder, AutomationTerminal, AutomationPlayback, AudioCapture, Microphone, Camera, AppManagement, Calendar,
}

public static class FeatureGroups
{
    public static readonly IReadOnlyList<FeatureGroup> All = Enum.GetValues<FeatureGroup>();

    public static string RawValue(this FeatureGroup group) => group switch
    {
        FeatureGroup.WindowsDock => "windowsDock",
        FeatureGroup.MouseKeyboard => "mouseKeyboard",
        FeatureGroup.ClipboardFiles => "clipboardFiles",
        FeatureGroup.Sound => "sound",
        FeatureGroup.EnergyDisplay => "energyDisplay",
        FeatureGroup.Tools => "tools",
        FeatureGroup.Monitor => "monitor",
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, null),
    };
}

public static class AppPermissions
{
    public static readonly IReadOnlyList<AppPermission> All = Enum.GetValues<AppPermission>();

    public static string RawValue(this AppPermission permission) => permission switch
    {
        AppPermission.Accessibility => "accessibility",
        AppPermission.ScreenRecording => "screenRecording",
        AppPermission.FullDiskAccess => "fullDiskAccess",
        AppPermission.FilesAndFolders => "filesAndFolders",
        AppPermission.Notifications => "notifications",
        AppPermission.AutomationFinder => "automationFinder",
        AppPermission.AutomationTerminal => "automationTerminal",
        AppPermission.AutomationPlayback => "automationPlayback",
        AppPermission.AudioCapture => "audioCapture",
        AppPermission.Microphone => "microphone",
        AppPermission.Camera => "camera",
        AppPermission.AppManagement => "appManagement",
        AppPermission.Calendar => "calendar",
        _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, null),
    };

    public static string SymbolName(this AppPermission permission) => permission switch
    {
        AppPermission.Accessibility => "accessibility",
        AppPermission.ScreenRecording => "rectangle.dashed.badge.record",
        AppPermission.FullDiskAccess => "externaldrive.badge.person.crop",
        AppPermission.FilesAndFolders => "folder.badge.person.crop",
        AppPermission.Notifications => "bell.badge",
        AppPermission.AutomationFinder or AppPermission.AutomationTerminal or AppPermission.AutomationPlayback => "gearshape.2",
        AppPermission.AudioCapture => "waveform",
        AppPermission.Microphone => "mic",
        AppPermission.Calendar => "calendar",
        AppPermission.Camera => "camera",
        AppPermission.AppManagement => "app.badge",
        _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, null),
    };
}
