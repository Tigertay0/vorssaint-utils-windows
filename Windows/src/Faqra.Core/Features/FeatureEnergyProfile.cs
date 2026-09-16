// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/FeaturePresets.swift (enum FeatureEnergyProfile, energyProfile)

using Faqra.Core.Defaults;

namespace Faqra.Core.Features;

/// <summary>
/// The honest, curated cost label each feature earns in the hub: what the feature keeps alive
/// while it is on. Static by design; uninstalled features load nothing at all.
/// </summary>
public enum FeatureEnergyProfile
{
    /// <summary>Nothing at rest: on-demand tools, shortcut-driven actions, notification listeners.</summary>
    Idle,
    /// <summary>A mouse event hook (scrolls, clicks or pointer moves).</summary>
    Mouse,
    /// <summary>A pointer gesture that works with either a trackpad or mouse.</summary>
    Pointer,
    /// <summary>A keyboard event hook.</summary>
    Keyboard,
    /// <summary>Both input hooks.</summary>
    Inputs,
    /// <summary>Samples or polls on an interval while active or visible.</summary>
    Periodic,
}

public static class FeatureEnergyProfiles
{
    public static string RawValue(this FeatureEnergyProfile profile) => profile.ToString().ToLowerInvariant();

    /// <summary>The cost label; a few features depend on their own settings, read through the injected readers.</summary>
    public static FeatureEnergyProfile EnergyProfile(this AppFeature feature, Func<string, bool> boolFor, Func<string, string?> stringFor) =>
        feature switch
        {
            AppFeature.ScrollInverter or AppFeature.FocusFollowsMouse or AppFeature.SmoothScroll or AppFeature.WindowMaximizer
                or AppFeature.MiddleClick or AppFeature.MouseNavigation or AppFeature.MouseButtonShortcuts
                or AppFeature.MouseClickDebounce or AppFeature.DockPreview or AppFeature.DockClick or AppFeature.Shelf
                => FeatureEnergyProfile.Mouse,
            AppFeature.Switcher or AppFeature.KeyboardDebounce or AppFeature.FinderCutPaste or AppFeature.FinderRename
                or AppFeature.SuperKey or AppFeature.QuitWindowProtection => FeatureEnergyProfile.Keyboard,
            AppFeature.TextSnippets or AppFeature.AutoQuit => FeatureEnergyProfile.Inputs,
            // Upstream also requires at least one enabled edge-snap zone; the zone table is ported with the feature.
            AppFeature.WindowLayout => boolFor(DefaultsKey.WindowGestureEnabled) || boolFor(DefaultsKey.WindowEdgeSnapEnabled)
                ? FeatureEnergyProfile.Pointer : FeatureEnergyProfile.Idle,
            AppFeature.RadialMenu => (stringFor(DefaultsKey.RadialMenuMouseButton) ?? "off") == "off"
                ? FeatureEnergyProfile.Idle : FeatureEnergyProfile.Mouse,
            AppFeature.NotchNotifications or AppFeature.NotchGestures or AppFeature.NotchTimer or AppFeature.NotchQueue
                or AppFeature.NotchDownloads => FeatureEnergyProfile.Idle,
            AppFeature.NotchAccessories or AppFeature.Notch or AppFeature.NotchCalendar or AppFeature.NotchLyrics
                or AppFeature.ClipboardHistory or AppFeature.UrlCleaner or AppFeature.ExtraBrightness
                or AppFeature.MonitorCPU or AppFeature.MonitorGPU or AppFeature.MonitorMemory
                or AppFeature.MonitorNetwork or AppFeature.MonitorDisk or AppFeature.MonitorPower => FeatureEnergyProfile.Periodic,
            AppFeature.Mixer => boolFor(DefaultsKey.PreciseVolumeRollerEnabled) ? FeatureEnergyProfile.Keyboard : FeatureEnergyProfile.Idle,
            AppFeature.AppUpdates => (stringFor(DefaultsKey.AppUpdatesCheckFrequency) ?? "off") == "off"
                ? FeatureEnergyProfile.Idle : FeatureEnergyProfile.Periodic,
            _ => FeatureEnergyProfile.Idle,
        };

    public static FeatureEnergyProfile EnergyProfile(this AppFeature feature, ISettingsStore store) =>
        feature.EnergyProfile(store.Bool, store.String);
}
