// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/FeatureCatalog.swift (extension AppFeature: identity, group, symbol, keys)

using Faqra.Core.Defaults;

namespace Faqra.Core.Features;

/// <summary>Pure, dependency-free description of every feature. Live state lives in the runtime.</summary>
public static partial class AppFeatures
{
    public static readonly IReadOnlyList<AppFeature> All = Enum.GetValues<AppFeature>();

    private static readonly Dictionary<string, AppFeature> ByRawValue =
        All.ToDictionary(feature => feature.RawValue(), StringComparer.Ordinal);

    /// <summary>The persisted identity: upstream's Swift case name, lowerCamelCase.</summary>
    public static string RawValue(this AppFeature feature)
    {
        var name = feature.ToString();
        return feature switch
        {
            AppFeature.MonitorCPU => "monitorCPU",
            AppFeature.MonitorGPU => "monitorGPU",
            AppFeature.ScreenOCR => "screenOCR",
            AppFeature.UrlCleaner => "urlCleaner",
            _ => char.ToLowerInvariant(name[0]) + name[1..],
        };
    }

    public static AppFeature? FromRawValue(string? raw) =>
        raw is not null && ByRawValue.TryGetValue(raw, out var feature) ? feature : null;

    public static string AvailabilityKey(this AppFeature feature) => DefaultsKey.FeatureAvailable(feature.RawValue());

    public static bool IsBeta(this AppFeature feature) => feature is AppFeature.FanControl or AppFeature.KillProcess;

    public static FeatureGroup Group(this AppFeature feature) => feature switch
    {
        AppFeature.Switcher or AppFeature.DockPreview or AppFeature.DockClick or AppFeature.WindowMaximizer
            or AppFeature.WindowLayout or AppFeature.AutoQuit => FeatureGroup.WindowsDock,
        AppFeature.ScrollInverter or AppFeature.FocusFollowsMouse or AppFeature.SmoothScroll or AppFeature.MouseAcceleration
            or AppFeature.MouseNavigation or AppFeature.MouseButtonShortcuts or AppFeature.MiddleClick
            or AppFeature.KeyboardDebounce or AppFeature.TextSnippets or AppFeature.SuperKey
            or AppFeature.QuitWindowProtection or AppFeature.MouseClickDebounce => FeatureGroup.MouseKeyboard,
        AppFeature.ClipboardHistory or AppFeature.PastePlain or AppFeature.FinderCutPaste or AppFeature.FinderRename
            or AppFeature.Shelf or AppFeature.UrlCleaner or AppFeature.DiskImageInstaller => FeatureGroup.ClipboardFiles,
        AppFeature.Mixer or AppFeature.SoundOutputSwitcher or AppFeature.MicMute or AppFeature.MusicBlock => FeatureGroup.Sound,
        AppFeature.KeepAwake or AppFeature.Brightness or AppFeature.ExtraBrightness or AppFeature.BluetoothSleep => FeatureGroup.EnergyDisplay,
        AppFeature.MonitorCPU or AppFeature.MonitorGPU or AppFeature.MonitorMemory or AppFeature.MonitorNetwork
            or AppFeature.MonitorDisk or AppFeature.MonitorPower or AppFeature.FanControl => FeatureGroup.Monitor,
        _ => FeatureGroup.Tools,
    };

    public static IReadOnlyList<AppFeature> FeaturesIn(FeatureGroup group) =>
        All.Where(feature => feature.Group() == group).ToList();

    /// <summary>Upstream's SF Symbol name; the Windows UI maps these to Segoe Fluent glyphs.</summary>
    public static string SymbolName(this AppFeature feature) => feature switch
    {
        AppFeature.Switcher => "rectangle.on.rectangle",
        AppFeature.DockPreview => "dock.rectangle",
        AppFeature.DockClick => "dock.arrow.down.rectangle",
        AppFeature.WindowMaximizer => "arrow.up.left.and.arrow.down.right",
        AppFeature.WindowLayout => "rectangle.3.group",
        AppFeature.AutoQuit => "xmark.rectangle",
        AppFeature.ScrollInverter => "arrow.up.arrow.down",
        AppFeature.FocusFollowsMouse => "cursorarrow.and.square.on.square.dashed",
        AppFeature.SmoothScroll => "cursorarrow.motionlines",
        AppFeature.MouseAcceleration => "cursorarrow.rays",
        AppFeature.MouseNavigation => "arrow.left.arrow.right",
        AppFeature.MouseButtonShortcuts => "button.programmable",
        AppFeature.MiddleClick => "computermouse",
        AppFeature.KeyboardDebounce => "keyboard",
        AppFeature.TextSnippets => "text.append",
        AppFeature.SuperKey => "capslock", // upstream derives this from SuperKeySource; Caps Lock is its default
        AppFeature.MouseClickDebounce => "cursorarrow.click",
        AppFeature.QuitWindowProtection => "shield.lefthalf.filled",
        AppFeature.ClipboardHistory => "doc.on.clipboard",
        AppFeature.PastePlain => "doc.plaintext",
        AppFeature.FinderCutPaste => "scissors",
        AppFeature.FinderRename => "pencil",
        AppFeature.Shelf => "tray.full",
        AppFeature.UrlCleaner => "link",
        AppFeature.DiskImageInstaller => "externaldrive.badge.plus",
        AppFeature.Mixer => "slider.horizontal.3",
        AppFeature.SoundOutputSwitcher => "hifispeaker",
        AppFeature.MicMute => "mic.slash",
        AppFeature.MusicBlock => "music.note",
        AppFeature.KeepAwake => "moon.zzz.fill",
        AppFeature.Brightness => "display.2",
        AppFeature.ExtraBrightness => "sun.max.fill",
        AppFeature.BluetoothSleep => "wave.3.right.circle",
        AppFeature.QuickLauncher => "wand.and.rays",
        AppFeature.QuickToggles => "togglepower",
        AppFeature.ColorPicker => "eyedropper",
        AppFeature.ScreenOCR => "text.viewfinder",
        AppFeature.CleaningMode => "bubbles.and.sparkles",
        AppFeature.MediaTools => "photo.on.rectangle.angled",
        AppFeature.Cleaner => "sparkles",
        AppFeature.Uninstaller => "trash",
        AppFeature.Homebrew => "shippingbox",
        AppFeature.AppUpdates => "arrow.down.app",
        AppFeature.Screenshot => "camera.viewfinder",
        AppFeature.ScreenRecorder => "record.circle",
        AppFeature.CameraPreview => "web.camera",
        AppFeature.NotchGestures => "hand.draw",
        AppFeature.NotchTimer => "timer",
        AppFeature.NotchAccessories => "battery.25percent",
        AppFeature.NotchLyrics => "quote.bubble",
        AppFeature.NotchQueue => "list.bullet",
        AppFeature.NotchDownloads => "arrow.down.circle",
        AppFeature.NotchNotifications => "bell",
        AppFeature.NotchCalendar => "calendar",
        AppFeature.Notch => "macbook",
        AppFeature.RadialMenu => "circle.grid.cross",
        AppFeature.Scratchpad => "note.text",
        AppFeature.CommandBar => "command",
        AppFeature.KillProcess => "xmark.octagon",
        AppFeature.MonitorCPU => "cpu",
        AppFeature.MonitorGPU => "rectangle.connected.to.line.below",
        AppFeature.MonitorMemory => "memorychip",
        AppFeature.MonitorNetwork => "network",
        AppFeature.MonitorDisk => "internaldrive",
        AppFeature.MonitorPower => "bolt.fill",
        AppFeature.FanControl => "fanblades.fill",
        _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, null),
    };

    /// <summary>
    /// The feature's own enable keys; any one being true means the feature is engaged. Empty
    /// means the feature works on demand, so being available already counts as engaged.
    /// </summary>
    public static IReadOnlyList<string> EnabledKeys(this AppFeature feature) => feature switch
    {
        AppFeature.Switcher => [DefaultsKey.SwitcherEnabled],
        AppFeature.DockPreview => [DefaultsKey.DockPreviewEnabled],
        AppFeature.DockClick => [DefaultsKey.DockClickMinimize, DefaultsKey.DockClickHide, DefaultsKey.DockClickCycleWindows],
        AppFeature.WindowMaximizer => [DefaultsKey.WindowMaximizeEnabled],
        AppFeature.AutoQuit => [DefaultsKey.AutoQuitEnabled],
        AppFeature.ScrollInverter => [DefaultsKey.ScrollInverterEnabled, DefaultsKey.ScrollInverterHorizontalEnabled],
        AppFeature.FocusFollowsMouse => [DefaultsKey.FocusFollowsMouseEnabled],
        AppFeature.SmoothScroll => [DefaultsKey.SmoothScrollEnabled],
        AppFeature.MouseAcceleration => [DefaultsKey.MouseAccelerationDisabled],
        AppFeature.MouseNavigation => [DefaultsKey.MouseNavigationEnabled],
        AppFeature.MouseButtonShortcuts => [DefaultsKey.MouseButtonShortcutsEnabled, DefaultsKey.MouseSpacesGestureEnabled],
        AppFeature.MiddleClick => [DefaultsKey.MiddleClickEnabled],
        AppFeature.KeyboardDebounce => [DefaultsKey.KeyboardDebounceEnabled],
        AppFeature.QuitWindowProtection => [DefaultsKey.QuitProtectionQuitEnabled, DefaultsKey.QuitProtectionCloseEnabled],
        AppFeature.TextSnippets => [DefaultsKey.TextSnippetsEnabled, DefaultsKey.SnippetLibraryEnabled],
        AppFeature.SuperKey => [DefaultsKey.SuperKeyEnabled],
        AppFeature.MouseClickDebounce => [DefaultsKey.MouseClickDebounceEnabled],
        AppFeature.NotchGestures => [DefaultsKey.NotchGesturesEnabled],
        AppFeature.NotchTimer => [DefaultsKey.NotchTimerEnabled],
        AppFeature.NotchAccessories => [DefaultsKey.NotchAccessoriesEnabled],
        AppFeature.NotchLyrics => [DefaultsKey.NotchLyricsEnabled],
        AppFeature.NotchQueue => [DefaultsKey.NotchQueueEnabled],
        AppFeature.NotchDownloads => [DefaultsKey.NotchDownloadsEnabled],
        AppFeature.NotchNotifications => [DefaultsKey.NotchNotificationsEnabled],
        AppFeature.NotchCalendar => [DefaultsKey.NotchCalendarEnabled],
        AppFeature.Notch => [DefaultsKey.NotchEnabled],
        AppFeature.RadialMenu => [DefaultsKey.RadialMenuEnabled],
        AppFeature.ClipboardHistory => [DefaultsKey.ClipboardHistoryEnabled],
        AppFeature.PastePlain => [DefaultsKey.PastePlainEnabled],
        AppFeature.FinderCutPaste => [DefaultsKey.FinderCutPasteEnabled, DefaultsKey.FinderPasteImageAsFile],
        AppFeature.FinderRename => [DefaultsKey.FinderRenameEnabled],
        AppFeature.Shelf => [DefaultsKey.ShelfEnabled],
        AppFeature.UrlCleaner => [DefaultsKey.UrlCleanerEnabled],
        AppFeature.SoundOutputSwitcher => [DefaultsKey.SoundOutputSwitcherEnabled],
        AppFeature.MusicBlock => [DefaultsKey.MusicBlockEnabled],
        AppFeature.Brightness => [DefaultsKey.BrightnessControlEnabled],
        AppFeature.ExtraBrightness => [DefaultsKey.ExtraBrightnessEnabled],
        AppFeature.BluetoothSleep => [DefaultsKey.BluetoothSleepEnabled],
        _ => [],
    };

    /// <summary>Registered defaults: existing features stay on across updates; opt-in betas ship uninstalled.</summary>
    public static IReadOnlyDictionary<string, object> AvailabilityDefaults { get; } =
        All.ToDictionary(
            feature => feature.AvailabilityKey(),
            feature => (object)(feature is not (AppFeature.FocusFollowsMouse or AppFeature.FanControl
                or AppFeature.DiskImageInstaller or AppFeature.KillProcess)),
            StringComparer.Ordinal);
}
