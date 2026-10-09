// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/UI/Settings/FeatureVisibilitySupport.swift (SettingsPage, page gating)
// and UI/Settings/SettingsDirectory.swift (the sidebar map).

using Faqra.Core.Features;

namespace Faqra.Core.Settings;

/// <summary>Every Settings page upstream has. Pages whose features are not ported stay hidden.</summary>
public enum SettingsPage
{
    General, Features, Energy, Monitor,
    Mouse, Switcher, KeyDebounce, SuperKey, CutPaste, AutoQuit, QuitProtection, Cleaner, Uninstaller,
    UrlCleaner, Homebrew, AppUpdates, Media, Clipboard, WindowLayout, Shelf, QuickTools, TextSnippets,
    Screenshot, RadialMenu, CommandBar, KillProcess, Notch, Agents,
    Shortcuts, Advanced, About,
}

/// <summary>Sidebar groups, in display order.</summary>
public enum SettingsSection
{
    Essentials, WindowControls, Files, AppManagement, Utilities, App,
}

/// <summary>One sidebar row.</summary>
public sealed record SettingsPageInfo(SettingsPage Page, SettingsSection Section, string Glyph);

public static class SettingsDirectory
{
    /// <summary>
    /// Every page with its section and Segoe Fluent glyph, in upstream's order. The glyphs replace
    /// upstream's SF Symbols; the sections and order are upstream's.
    /// </summary>
    public static readonly IReadOnlyList<SettingsPageInfo> AllPages =
    [
        new(SettingsPage.General, SettingsSection.Essentials, ""),        // Settings
        new(SettingsPage.Features, SettingsSection.Essentials, ""),       // AllApps
        new(SettingsPage.Energy, SettingsSection.Essentials, ""),         // LightningBolt
        new(SettingsPage.Monitor, SettingsSection.Essentials, ""),        // DataSense
        new(SettingsPage.Mouse, SettingsSection.WindowControls, ""),      // Mouse
        new(SettingsPage.Switcher, SettingsSection.WindowControls, ""),   // SelectAll
        new(SettingsPage.WindowLayout, SettingsSection.WindowControls, ""), // Tiles
        new(SettingsPage.AutoQuit, SettingsSection.WindowControls, ""),   // ChromeClose
        new(SettingsPage.QuitProtection, SettingsSection.WindowControls, ""), // Lock
        new(SettingsPage.Clipboard, SettingsSection.Files, ""),           // Copy
        new(SettingsPage.CutPaste, SettingsSection.Files, ""),            // Cut
        new(SettingsPage.Shelf, SettingsSection.Files, ""),               // Tray
        new(SettingsPage.Media, SettingsSection.Files, ""),               // Photo
        new(SettingsPage.AppUpdates, SettingsSection.AppManagement, ""),  // Download
        new(SettingsPage.Cleaner, SettingsSection.AppManagement, ""),     // Sweep
        new(SettingsPage.Homebrew, SettingsSection.AppManagement, ""),
        new(SettingsPage.Uninstaller, SettingsSection.AppManagement, ""), // Delete
        new(SettingsPage.KillProcess, SettingsSection.AppManagement, ""), // Cancel
        new(SettingsPage.Notch, SettingsSection.Utilities, ""),           // TVMonitor
        new(SettingsPage.CommandBar, SettingsSection.Utilities, ""),      // CommandPrompt
        new(SettingsPage.Agents, SettingsSection.Utilities, "\uE99A"),      // Robot
        new(SettingsPage.QuickTools, SettingsSection.Utilities, ""),
        new(SettingsPage.Screenshot, SettingsSection.Utilities, ""),      // Camera
        new(SettingsPage.UrlCleaner, SettingsSection.Utilities, ""),      // Link
        new(SettingsPage.KeyDebounce, SettingsSection.Utilities, ""),     // KeyboardClassic
        new(SettingsPage.SuperKey, SettingsSection.Utilities, ""),        // Up
        new(SettingsPage.TextSnippets, SettingsSection.Utilities, ""),    // Document
        new(SettingsPage.RadialMenu, SettingsSection.Utilities, ""),      // CircleRing
        new(SettingsPage.Shortcuts, SettingsSection.App, ""),             // KeyboardShortcut
        new(SettingsPage.Advanced, SettingsSection.App, ""),              // Repair
        new(SettingsPage.About, SettingsSection.App, ""),                 // Info
    ];

    /// <summary>
    /// Which features keep a page alive. A page with an empty gate is always visible; otherwise it
    /// shows while at least one gating feature is installed.
    /// </summary>
    public static IReadOnlyList<AppFeature> Gate(SettingsPage page) => page switch
    {
        SettingsPage.Energy => [AppFeature.KeepAwake, AppFeature.Brightness, AppFeature.ExtraBrightness, AppFeature.BluetoothSleep],
        SettingsPage.Monitor =>
        [
            AppFeature.MonitorCPU, AppFeature.MonitorGPU, AppFeature.MonitorMemory, AppFeature.MonitorNetwork,
            AppFeature.MonitorDisk, AppFeature.MonitorPower, AppFeature.FanControl,
        ],
        SettingsPage.Mouse =>
        [
            AppFeature.ScrollInverter, AppFeature.FocusFollowsMouse, AppFeature.SmoothScroll, AppFeature.MouseAcceleration,
            AppFeature.MouseNavigation, AppFeature.MouseButtonShortcuts, AppFeature.MiddleClick, AppFeature.MouseClickDebounce,
        ],
        SettingsPage.Switcher => [AppFeature.Switcher, AppFeature.DockPreview, AppFeature.DockClick],
        SettingsPage.WindowLayout => [AppFeature.WindowLayout],
        SettingsPage.AutoQuit => [AppFeature.AutoQuit],
        SettingsPage.QuitProtection => [AppFeature.QuitWindowProtection],
        SettingsPage.Clipboard => [AppFeature.ClipboardHistory, AppFeature.PastePlain, AppFeature.FinderCutPaste],
        SettingsPage.CutPaste => [AppFeature.FinderCutPaste, AppFeature.FinderRename],
        SettingsPage.Shelf => [AppFeature.Shelf],
        SettingsPage.Media => [AppFeature.MediaTools],
        SettingsPage.QuickTools =>
        [
            AppFeature.QuickLauncher, AppFeature.QuickToggles, AppFeature.MicMute, AppFeature.CameraPreview,
            AppFeature.Scratchpad, AppFeature.CleaningMode,
        ],
        SettingsPage.Screenshot => [AppFeature.Screenshot, AppFeature.ScreenRecorder, AppFeature.ScreenOCR, AppFeature.ColorPicker],
        SettingsPage.Notch =>
        [
            AppFeature.Notch, AppFeature.NotchCalendar, AppFeature.NotchNotifications, AppFeature.NotchGestures,
            AppFeature.NotchTimer, AppFeature.NotchAccessories, AppFeature.NotchLyrics, AppFeature.NotchQueue,
            AppFeature.NotchDownloads,
        ],
        SettingsPage.UrlCleaner => [AppFeature.UrlCleaner],
        SettingsPage.Cleaner => [AppFeature.Cleaner],
        SettingsPage.Homebrew => [AppFeature.Homebrew],
        SettingsPage.AppUpdates => [AppFeature.AppUpdates],
        SettingsPage.Uninstaller => [AppFeature.Uninstaller],
        SettingsPage.KillProcess => [AppFeature.KillProcess],
        SettingsPage.KeyDebounce => [AppFeature.KeyboardDebounce],
        SettingsPage.SuperKey => [AppFeature.SuperKey],
        SettingsPage.TextSnippets => [AppFeature.TextSnippets],
        SettingsPage.RadialMenu => [AppFeature.RadialMenu],
        SettingsPage.CommandBar => [AppFeature.CommandBar],
        SettingsPage.Agents => [AppFeature.FaqraAgents],
        _ => [],
    };

    /// <summary>A page shows when its gate is empty or any gating feature is installed.</summary>
    public static bool IsVisible(SettingsPage page, Func<AppFeature, bool> isAvailable)
    {
        var gate = Gate(page);
        return gate.Count == 0 || gate.Any(isAvailable);
    }

    /// <summary>The visible pages, grouped in sidebar order. Empty sections are omitted.</summary>
    public static IReadOnlyList<(SettingsSection Section, IReadOnlyList<SettingsPageInfo> Pages)> VisibleSections(
        Func<AppFeature, bool> isAvailable)
    {
        return AllPages
            .Where(info => IsVisible(info.Page, isAvailable))
            .GroupBy(info => info.Section)
            .OrderBy(group => (int)group.Key)
            .Select(group => (group.Key, (IReadOnlyList<SettingsPageInfo>)group.ToList()))
            .ToList();
    }

    /// <summary>Where a feature row in the hub links to; the hub itself is the honest fallback.</summary>
    public static SettingsPage Destination(AppFeature feature) => feature switch
    {
        AppFeature.Switcher or AppFeature.DockPreview or AppFeature.DockClick => SettingsPage.Switcher,
        AppFeature.WindowMaximizer or AppFeature.Mixer or AppFeature.MusicBlock => SettingsPage.General,
        AppFeature.WindowLayout => SettingsPage.WindowLayout,
        AppFeature.AutoQuit => SettingsPage.AutoQuit,
        AppFeature.QuitWindowProtection => SettingsPage.QuitProtection,
        AppFeature.ScrollInverter or AppFeature.FocusFollowsMouse or AppFeature.SmoothScroll or AppFeature.MouseAcceleration
            or AppFeature.MouseNavigation or AppFeature.MouseButtonShortcuts or AppFeature.MiddleClick
            or AppFeature.MouseClickDebounce => SettingsPage.Mouse,
        AppFeature.KeyboardDebounce => SettingsPage.KeyDebounce,
        AppFeature.TextSnippets => SettingsPage.TextSnippets,
        AppFeature.SuperKey => SettingsPage.SuperKey,
        AppFeature.ClipboardHistory or AppFeature.PastePlain => SettingsPage.Clipboard,
        AppFeature.FinderCutPaste or AppFeature.FinderRename => SettingsPage.CutPaste,
        AppFeature.Shelf => SettingsPage.Shelf,
        AppFeature.UrlCleaner => SettingsPage.UrlCleaner,
        AppFeature.SoundOutputSwitcher => SettingsPage.Shortcuts,
        AppFeature.MicMute or AppFeature.QuickLauncher or AppFeature.QuickToggles or AppFeature.CleaningMode
            or AppFeature.CameraPreview or AppFeature.Scratchpad => SettingsPage.QuickTools,
        AppFeature.KeepAwake or AppFeature.Brightness or AppFeature.ExtraBrightness or AppFeature.BluetoothSleep => SettingsPage.Energy,
        AppFeature.ColorPicker or AppFeature.ScreenOCR or AppFeature.Screenshot or AppFeature.ScreenRecorder => SettingsPage.Screenshot,
        AppFeature.MediaTools => SettingsPage.Media,
        AppFeature.Cleaner => SettingsPage.Cleaner,
        AppFeature.Uninstaller => SettingsPage.Uninstaller,
        AppFeature.KillProcess => SettingsPage.KillProcess,
        AppFeature.Homebrew => SettingsPage.Homebrew,
        AppFeature.AppUpdates => SettingsPage.AppUpdates,
        AppFeature.Notch or AppFeature.NotchCalendar or AppFeature.NotchNotifications or AppFeature.NotchGestures
            or AppFeature.NotchTimer or AppFeature.NotchAccessories or AppFeature.NotchLyrics or AppFeature.NotchQueue
            or AppFeature.NotchDownloads => SettingsPage.Notch,
        AppFeature.RadialMenu => SettingsPage.RadialMenu,
        AppFeature.CommandBar => SettingsPage.CommandBar,
        AppFeature.FaqraAgents => SettingsPage.Agents,
        AppFeature.MonitorCPU or AppFeature.MonitorGPU or AppFeature.MonitorMemory or AppFeature.MonitorNetwork
            or AppFeature.MonitorDisk or AppFeature.MonitorPower or AppFeature.FanControl => SettingsPage.Monitor,
        _ => SettingsPage.Features,
    };

    /// <summary>False when the destination is the hub itself, so no chevron promises a jump that does nothing.</summary>
    public static bool HasNavigableDestination(AppFeature feature) => Destination(feature) != SettingsPage.Features;
}
