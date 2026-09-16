// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/Localization.swift (struct Strings)

namespace Faqra.Core.Localization;

/// <summary>
/// Flat, compiler-checked catalog of UI strings. Every member is <c>required</c>, so a
/// translation that omits a field fails to compile, the same guarantee upstream gets from
/// Swift's memberwise initializer. Grows milestone by milestone.
/// </summary>
public sealed partial class Strings
{
    // Status item tooltip
    public required string StatusIdleTooltip { get; init; }
    public required string StatusActiveUntil { get; init; }
    public required string StatusActiveIndefinite { get; init; }

    // Status item context menu
    public required string MenuEnableAwake { get; init; }
    public required string MenuDisableAwake { get; init; }
    public required string MenuActivateFor { get; init; }
    public required string MenuSettings { get; init; }
    public required string MenuAbout { get; init; }
    public required string MenuQuit { get; init; }
    public required string MenuCheckUpdates { get; init; }
    public required string CleaningMenuItem { get; init; }
    public required string UninstallerMenuItem { get; init; }
    public required string ShelfMenuItem { get; init; }

    // Keep awake durations
    public required string Minutes15 { get; init; }
    public required string Minutes30 { get; init; }
    public required string Hour1 { get; init; }
    public required string Hours2 { get; init; }
    public required string Hours4 { get; init; }
    public required string Hours8 { get; init; }
    public required string Indefinitely { get; init; }
    public required string KeepAwakeTitle { get; init; }

    // Feature hub (platform)
    public required string FeatureNotAvailableOnWindows { get; init; }

    // Settings window
    public required string SettingsTitle { get; init; }
    public required string SettingsSearchPlaceholder { get; init; }
    public required IReadOnlyDictionary<Settings.SettingsSection, string> SettingsSectionTitles { get; init; }
    public required IReadOnlyDictionary<Settings.SettingsPage, string> SettingsPageTitles { get; init; }

    // General page
    public required string LaunchAtLogin { get; init; }
    public required string LaunchAtLoginNeedsApproval { get; init; }
    public required string LaunchAtLoginUnstableLocation { get; init; }
    public required string LanguageLabel { get; init; }
    public required string AppearanceLabel { get; init; }
    public required string AppearanceSystem { get; init; }
    public required string AppearanceLight { get; init; }
    public required string AppearanceDark { get; init; }
    public required string TraySection { get; init; }
    public required string ShowTrayIcon { get; init; }
    public required string ShowTrayIconHint { get; init; }
    public required string ShowTrayIconAction { get; init; }

    // Island page
    public required string IslandShow { get; init; }
    public required string IslandShowHint { get; init; }
    public required string IslandPosition { get; init; }
    public required string IslandPositionTop { get; init; }
    public required string IslandPositionLeft { get; init; }
    public required string IslandPositionRight { get; init; }
    public required string IslandDisplayLabel { get; init; }
    public required string IslandDisplayFollow { get; init; }
    public required string IslandDisplayPrimary { get; init; }
    public required string IslandOpenOnHover { get; init; }
    public required string IslandHoverExpands { get; init; }
    public required string IslandHoverExpandsHint { get; init; }
    public required string IslandSizeLabel { get; init; }
    public required string IslandSizeCompact { get; init; }
    public required string IslandSizeSpacious { get; init; }
    public required string IslandIdleLabel { get; init; }
    public required string IslandIdleMusic { get; init; }
    public required string IslandIdleBattery { get; init; }
    public required string IslandIdleNone { get; init; }

    // Advanced page
    public required string BackupTitle { get; init; }
    public required string BackupDescription { get; init; }
    public required string BackupExportButton { get; init; }
    public required string BackupImportButton { get; init; }
    public required string BackupExported { get; init; }
    public required string BackupInvalidFile { get; init; }
    public required string BackupImportConfirmTitle { get; init; }
    public required string BackupImportConfirmBody { get; init; }
    public required string BackupImportAction { get; init; }
    public required string Cancel { get; init; }

    // Onboarding
    public required string OnboardingWelcomeTitle { get; init; }
    public required string OnboardingWelcomeBody { get; init; }
    public required string OnboardingBullet1Title { get; init; }
    public required string OnboardingBullet1Body { get; init; }
    public required string OnboardingBullet2Title { get; init; }
    public required string OnboardingBullet2Body { get; init; }
    public required string OnboardingBullet3Title { get; init; }
    public required string OnboardingBullet3Body { get; init; }
    public required string OnboardingPurposeTitle { get; init; }
    public required string OnboardingPurposeBody { get; init; }
    public required string OnboardingPurposeSkip { get; init; }
    public required string OnboardingDoneTitle { get; init; }
    public required string OnboardingDoneBody { get; init; }
    public required string OnboardingDoneHint { get; init; }
    public required string OnboardingBack { get; init; }
    public required string OnboardingContinue { get; init; }
    public required string OnboardingStart { get; init; }

    // About
    public required string AboutDescription { get; init; }
    public required string VersionPrefix { get; init; }
    public required string ViewOnGitHub { get; init; }
    public required string BetaBadgeLabel { get; init; }
    public required string ReviewIntro { get; init; }
    public required string ReviewHighlights { get; init; }

    public static Strings For(AppLanguage language) => language switch
    {
        AppLanguage.EnUS => EnUS,
        AppLanguage.PtBR => PtBR,
        AppLanguage.Tr => Tr,
        AppLanguage.Ru => Ru,
        AppLanguage.Es => Es,
        AppLanguage.De => De,
        AppLanguage.Fr => Fr,
        AppLanguage.It => It,
        AppLanguage.Ja => Ja,
        AppLanguage.Ko => Ko,
        AppLanguage.ZhHans => ZhHans,
        AppLanguage.ZhTW => ZhTW,
        AppLanguage.ZhHK => ZhHK,
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
    };
}
