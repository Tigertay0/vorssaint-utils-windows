// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors PanelSectionID in Sources/Vorssaint/UI/MenuPanel/PanelLayout.swift (lines 12-101)

using Faqra.Core.Defaults;
using Faqra.Core.Features;

namespace Faqra.Core.Panel;

/// <summary>
/// The popover panel's sections, in canonical order. Raw values are persisted in
/// <see cref="DefaultsKey.PanelSectionOrder"/>, so members can be added but never renamed.
/// </summary>
public enum PanelSectionId
{
    KeepAwake, Brightness, Mixer, System, Network, Disk, Power, FanControl, Utilities, Controls, Toggles,
}

public static class PanelSections
{
    public static readonly IReadOnlyList<PanelSectionId> All = Enum.GetValues<PanelSectionId>();

    public static string RawValue(this PanelSectionId id) => id switch
    {
        PanelSectionId.KeepAwake => "keepAwake",
        PanelSectionId.Brightness => "brightness",
        PanelSectionId.Mixer => "mixer",
        PanelSectionId.System => "system",
        PanelSectionId.Network => "network",
        PanelSectionId.Disk => "disk",
        PanelSectionId.Power => "power",
        PanelSectionId.FanControl => "fanControl",
        PanelSectionId.Utilities => "utilities",
        PanelSectionId.Controls => "controls",
        PanelSectionId.Toggles => "toggles",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null),
    };

    /// <summary>Exact, case-sensitive match on the raw value, like Swift's <c>init(rawValue:)</c>.</summary>
    public static PanelSectionId? FromRawValue(string? raw) =>
        All.Cast<PanelSectionId?>().FirstOrDefault(id => id!.Value.RawValue() == raw);

    /// <summary>The boolean preference that shows or hides the section.</summary>
    public static string VisibilityKey(this PanelSectionId id) => id switch
    {
        PanelSectionId.KeepAwake => DefaultsKey.PanelShowKeepAwake,
        PanelSectionId.Brightness => DefaultsKey.PanelShowBrightness,
        PanelSectionId.Mixer => DefaultsKey.MonitorShowMixer,
        PanelSectionId.System => DefaultsKey.MonitorShowSystem,
        PanelSectionId.Network => DefaultsKey.MonitorShowNetwork,
        PanelSectionId.Disk => DefaultsKey.MonitorShowDisk,
        PanelSectionId.Power => DefaultsKey.MonitorShowPower,
        PanelSectionId.FanControl => DefaultsKey.PanelShowFanControl,
        PanelSectionId.Utilities => DefaultsKey.PanelShowUtilities,
        PanelSectionId.Controls => DefaultsKey.PanelShowControls,
        PanelSectionId.Toggles => DefaultsKey.PanelShowToggles,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null),
    };

    /// <summary>Segoe Fluent Icons glyph, replacing upstream's SF Symbol.</summary>
    public static string Glyph(this PanelSectionId id) => id switch
    {
        PanelSectionId.KeepAwake => "",  // QuietHours (moon.zzz.fill)
        PanelSectionId.Brightness => "", // TVMonitor (display.2)
        PanelSectionId.Mixer => "",      // Volume (slider.horizontal.3)
        PanelSectionId.System => "",     // Processing (cpu)
        PanelSectionId.Network => "",    // MyNetwork (network)
        PanelSectionId.Disk => "",       // HardDrive (internaldrive)
        PanelSectionId.Power => "",      // LightningBolt (bolt.fill)
        PanelSectionId.FanControl => "", // Frigid (fanblades.fill)
        PanelSectionId.Utilities => "",  // Repair (wrench.and.screwdriver.fill)
        PanelSectionId.Controls => "",   // Equalizer (switch.2)
        PanelSectionId.Toggles => "",    // PowerButton (togglepower)
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null),
    };

    /// <summary>The features that fill the section. It is available when any one of them is.</summary>
    public static IReadOnlyList<AppFeature> FeatureGate(this PanelSectionId id) => id switch
    {
        PanelSectionId.KeepAwake => [AppFeature.KeepAwake],
        PanelSectionId.Brightness => [AppFeature.Brightness],
        PanelSectionId.Mixer => [AppFeature.Mixer],
        PanelSectionId.System => [AppFeature.MonitorCPU, AppFeature.MonitorGPU, AppFeature.MonitorMemory],
        PanelSectionId.Network => [AppFeature.MonitorNetwork],
        PanelSectionId.Disk => [AppFeature.MonitorDisk],
        PanelSectionId.Power => [AppFeature.MonitorPower],
        PanelSectionId.FanControl => [AppFeature.FanControl],
        PanelSectionId.Utilities =>
        [
            AppFeature.QuickLauncher, AppFeature.Cleaner, AppFeature.Homebrew, AppFeature.AppUpdates, AppFeature.MediaTools,
            AppFeature.ClipboardHistory, AppFeature.WindowLayout, AppFeature.Uninstaller, AppFeature.UrlCleaner,
            AppFeature.CleaningMode, AppFeature.ScreenOCR, AppFeature.ColorPicker, AppFeature.Screenshot,
            AppFeature.ScreenRecorder, AppFeature.CameraPreview, AppFeature.Scratchpad, AppFeature.CommandBar,
        ],
        PanelSectionId.Controls =>
        [
            AppFeature.ScrollInverter, AppFeature.MouseAcceleration, AppFeature.MouseNavigation, AppFeature.MouseButtonShortcuts,
            AppFeature.Switcher, AppFeature.FinderCutPaste, AppFeature.AutoQuit, AppFeature.Shelf, AppFeature.WindowMaximizer,
            AppFeature.DockPreview, AppFeature.KeyboardDebounce, AppFeature.DockClick, AppFeature.MiddleClick,
            AppFeature.TextSnippets, AppFeature.SuperKey, AppFeature.RadialMenu, AppFeature.MouseClickDebounce, AppFeature.Notch,
        ],
        PanelSectionId.Toggles => [AppFeature.QuickToggles, AppFeature.MicMute],
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null),
    };

    public static bool IsAvailable(this PanelSectionId id, Func<AppFeature, bool> isAvailable) =>
        id.FeatureGate().Any(isAvailable);
}
