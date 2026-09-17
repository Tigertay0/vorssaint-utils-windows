// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the mixer entries of Sources/Vorssaint/Core/Localization.swift, FeatureStrings.swift
// (MixerFeatureStrings) and NotchStrings.swift (volume).

namespace Faqra.Core.Localization;

/// <summary>Every string the mixer panel section and the island's mixer and volume controls show.</summary>
public sealed partial class MixerStrings
{
    public required string Section { get; init; }
    public required string Empty { get; init; }
    public required string ResetTooltip { get; init; }
    public required string OutputCurrent { get; init; }
    public required string OutputUnavailable { get; init; }
    public required string SystemOutputTitle { get; init; }
    public required string SystemOutputNoDevices { get; init; }
    public required string SystemOutputTooltip { get; init; }
    public required string SwitchErrorFormat { get; init; }
    public required string LowerOnHeadphonesDisconnect { get; init; }
    public required string LowerOnHeadphonesDisconnectCaption { get; init; }
    public required string HeadphonesDisconnectVolume { get; init; }
    public required string VisibleApps { get; init; }
    public required string AllShown { get; init; }
    public required string HiddenCountLabel { get; init; }
    public required string HideFromList { get; init; }
    public required string HideInactiveApps { get; init; }
    public required string Options { get; init; }
    public required string Mute { get; init; }
    public required string Unmute { get; init; }
    public required string Volume { get; init; }

    public static MixerStrings For(AppLanguage language) => language switch
    {
        // Translations pending: every language resolves to English for now (see Strings.Aliases.cs).
        _ => EnUS,
    };

    public static MixerStrings EnUS { get; } = new()
    {
        Section = "Volume mixer",
        Empty = "Apps that use audio show up here",
        ResetTooltip = "Reset to 100%",
        OutputCurrent = "current",
        OutputUnavailable = "Output unavailable",
        SystemOutputTitle = "Output",
        SystemOutputNoDevices = "No outputs found",
        SystemOutputTooltip = "Choose system output",
        SwitchErrorFormat = "Could not switch: {0}",
        LowerOnHeadphonesDisconnect = "Lower volume when headphones disconnect",
        LowerOnHeadphonesDisconnectCaption = "Adjusts output when wired or Bluetooth headphones disconnect.",
        HeadphonesDisconnectVolume = "Volume after disconnect",
        VisibleApps = "Apps in the list",
        AllShown = "All",
        HiddenCountLabel = "Hidden",
        HideFromList = "Hide from the list",
        HideInactiveApps = "Hide inactive apps",
        Options = "Options",
        Mute = "Mute",
        Unmute = "Unmute",
        Volume = "Volume",
    };
}
