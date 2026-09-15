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
