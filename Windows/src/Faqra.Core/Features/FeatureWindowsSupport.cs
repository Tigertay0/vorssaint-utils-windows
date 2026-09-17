// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of hardwareUnsupportedReason in Sources/Vorssaint/App/FeatureRuntime.swift: on Windows
// the question is not "does this Mac have the hardware" but "has this feature been ported / can it be".

namespace Faqra.Core.Features;

public enum WindowsSupport
{
    /// <summary>Ported with a direct Windows equivalent.</summary>
    Supported,
    /// <summary>Ported with an approximation (documented in the feature's settings page).</summary>
    Approximate,
    /// <summary>No Windows meaning, or not yet ported; the hub greys the row and refuses installs.</summary>
    NotApplicable,
}

public static class FeatureWindowsSupport
{
    /// <summary>
    /// Stage 1 classification. Each later milestone flips the features it ports; features with no
    /// Windows counterpart (Dock, Finder, Homebrew, notch hardware, Apple fan control) stay NotApplicable.
    /// </summary>
    public static WindowsSupport Classify(AppFeature feature) => feature switch
    {
        AppFeature.Mixer or AppFeature.KeepAwake or AppFeature.CommandBar
            or AppFeature.Notch or AppFeature.NotchTimer
            or AppFeature.MonitorCPU or AppFeature.MonitorGPU or AppFeature.MonitorMemory
            or AppFeature.MonitorNetwork or AppFeature.MonitorDisk or AppFeature.MonitorPower => WindowsSupport.Supported,
        AppFeature.SoundOutputSwitcher => WindowsSupport.Approximate,
        _ => WindowsSupport.NotApplicable,
    };

    public static bool IsSupported(this AppFeature feature) => Classify(feature) != WindowsSupport.NotApplicable;

    /// <summary>
    /// Whether the feature actually works in this build. Supported answers "can Windows run it"; a
    /// supported feature still waits for its milestone, and the hub says so instead of looking broken.
    /// </summary>
    public static bool IsBuilt(AppFeature feature) => feature
        is AppFeature.Notch or AppFeature.NotchTimer or AppFeature.Mixer or AppFeature.KeepAwake
        or AppFeature.MonitorCPU or AppFeature.MonitorGPU or AppFeature.MonitorMemory
        or AppFeature.MonitorNetwork or AppFeature.MonitorDisk or AppFeature.MonitorPower
        or AppFeature.CommandBar;

    /// <summary>The hub's note for a feature Windows can run that this build does not have yet, else null.</summary>
    public static string? PendingNote(AppFeature feature, Localization.FeatureHubStrings hub) =>
        feature.IsSupported() && !IsBuilt(feature) ? hub.NotBuiltYet : null;

    /// <summary>Why the hub refuses to install this feature, or null when it may be installed.</summary>
    public static string? UnsupportedReason(AppFeature feature, Localization.Strings s) =>
        Classify(feature) == WindowsSupport.NotApplicable ? s.FeatureNotAvailableOnWindows : null;
}
