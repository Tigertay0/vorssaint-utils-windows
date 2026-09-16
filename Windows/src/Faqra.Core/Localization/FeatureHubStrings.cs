// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/FeatureHubStrings.swift plus the per-feature hubTitle/hubDescription
// resolution in UI/Settings/FeatureHubSettings.swift (lines 702-860)

using Faqra.Core.Features;

namespace Faqra.Core.Localization;

/// <summary>
/// Every string the Features hub shows. Per-feature tables are dictionaries rather than one
/// property per feature; a unit test asserts each language covers every enum member.
/// </summary>
public sealed partial class FeatureHubStrings
{
    public required IReadOnlyDictionary<AppFeature, string> FeatureTitles { get; init; }
    public required IReadOnlyDictionary<AppFeature, string> FeatureDescriptions { get; init; }
    public required IReadOnlyDictionary<FeatureGroup, string> GroupTitles { get; init; }
    public required IReadOnlyDictionary<FeaturePreset, string> PresetNames { get; init; }
    public required IReadOnlyDictionary<FeaturePreset, string> PresetDescriptions { get; init; }
    public required IReadOnlyDictionary<FeatureEnergyProfile, string> EnergyLabels { get; init; }
    public required IReadOnlyDictionary<AppPermission, string> PermissionNames { get; init; }
    public required IReadOnlyDictionary<AppPermission, string> PermissionExplainers { get; init; }

    // Tabs and header
    public required string TabFeatures { get; init; }
    public required string TabPermissions { get; init; }
    public required string Intro { get; init; }
    public required string FooterNote { get; init; }
    public required string MonitorAllOffNote { get; init; }
    public required string InstalledCountFormat { get; init; }
    public required string InstallAll { get; init; }
    public required string UninstallAll { get; init; }
    public required string Install { get; init; }
    public required string Uninstall { get; init; }
    public required string BetaBadge { get; init; }
    public required string EnergyHelp { get; init; }

    // Restart banner
    public required string RestartBannerText { get; init; }
    public required string RestartBannerButton { get; init; }

    // Presets
    public required string PresetsTitle { get; init; }
    public required string PresetsCaption { get; init; }
    public required string PresetApplyButton { get; init; }
    public required string PresetConfirmFormat { get; init; }
    public required string PresetConfirmApply { get; init; }
    public required string PresetConfirmCancel { get; init; }

    // Permissions tab chrome
    public required string PermissionsIntro { get; init; }
    public required string UsedByFormat { get; init; }
    public required string UsedByNone { get; init; }
    public required string StatusGranted { get; init; }
    public required string StatusMissing { get; init; }
    public required string StatusUnknown { get; init; }
    public required string WindowsPermissionNote { get; init; }

    public static FeatureHubStrings For(AppLanguage language) => language switch
    {
        // Translations pending: every language resolves to English for now (see Strings.Aliases.cs).
        _ => EnUS,
    };
}
