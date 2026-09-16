// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/FeaturePresets.swift (enum FeaturePreset)

using Faqra.Core.Defaults;

namespace Faqra.Core.Features;

/// <summary>
/// One-click starting points for the Features hub. Applying a preset installs and engages its
/// features and uninstalls the rest; nothing is deleted, every feature keeps its settings.
/// </summary>
public enum FeaturePreset
{
    Essential, Windows, Battery,
}

public static class FeaturePresets
{
    public static readonly IReadOnlyList<FeaturePreset> All = Enum.GetValues<FeaturePreset>();

    public static string RawValue(this FeaturePreset preset) => preset switch
    {
        FeaturePreset.Essential => "essential",
        FeaturePreset.Windows => "windows",
        FeaturePreset.Battery => "battery",
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
    };

    /// <summary>
    /// A clean install starts from the small Essential set before any feature binding runs.
    /// Updates keep every existing availability choice, and an interrupted setup keeps the
    /// selection already applied on its purpose step.
    /// </summary>
    public static void PrepareFirstRunAvailability(ISettingsStore store)
    {
        if (store.Bool(DefaultsKey.HasOnboarded) || store.Int(DefaultsKey.OnboardingStep) != 0)
        {
            return;
        }
        var selected = FeaturePreset.Essential.Features();
        foreach (var feature in AppFeatures.All)
        {
            store.Set(feature.AvailabilityKey(), selected.Contains(feature));
        }
    }

    /// <summary>The features the preset keeps installed.</summary>
    public static IReadOnlySet<AppFeature> Features(this FeaturePreset preset) => preset switch
    {
        FeaturePreset.Essential => new HashSet<AppFeature>
        {
            AppFeature.Mixer, AppFeature.KeepAwake,
            AppFeature.MonitorCPU, AppFeature.MonitorGPU, AppFeature.MonitorMemory,
            AppFeature.MonitorNetwork, AppFeature.MonitorDisk, AppFeature.MonitorPower,
        },
        FeaturePreset.Windows => new HashSet<AppFeature>
        {
            AppFeature.Switcher, AppFeature.WindowLayout, AppFeature.DockPreview, AppFeature.DockClick, AppFeature.WindowMaximizer,
        },
        FeaturePreset.Battery => new HashSet<AppFeature>
        {
            AppFeature.MonitorCPU, AppFeature.MonitorMemory, AppFeature.MonitorPower,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
    };

    /// <summary>Enable keys switched on along with the install so the preset's features work right away.</summary>
    public static IReadOnlyList<string> EnableKeys(this FeaturePreset preset) => preset switch
    {
        FeaturePreset.Windows =>
        [
            DefaultsKey.SwitcherEnabled, DefaultsKey.DockPreviewEnabled, DefaultsKey.DockClickMinimize, DefaultsKey.WindowMaximizeEnabled,
        ],
        _ => [],
    };

    public static string SymbolName(this FeaturePreset preset) => preset switch
    {
        FeaturePreset.Essential => "star.fill",
        FeaturePreset.Windows => "macwindow.on.rectangle",
        FeaturePreset.Battery => "battery.75percent",
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
    };
}
