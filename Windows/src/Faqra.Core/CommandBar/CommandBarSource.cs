// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors CommandBarSource + CommandBarPreferences.rankBias(for:) in
// Sources/Vorssaint/Services/CommandBar/CommandBarPreferences.swift (lines 8-33, 130-142).
// Only the enum and the rank bias are ported here: the rest of CommandBarPreferences
// (disabled-source storage, aliases, pins, hidden rows, panel-position clamping) is Stage 1
// out-of-scope per the porting brief and belongs to a later pass alongside the settings UI.

namespace Faqra.Core.CommandBar;

/// <summary>
/// A kind of result the bar can offer. Order and raw names matter upstream because they persist
/// in a disabled-sources list; kept in the same order here for the same reason, even though
/// Stage 1 does not yet persist a disabled-sources set.
/// </summary>
public enum CommandBarSource
{
    /// <summary>What the app itself can do. Always on: it is what the bar is for.</summary>
    Actions,
    Apps,
    Menus,
    Windows,
    QuitApps,
    SettingsPages,
    MacSettings,
    Snippets,
    Clipboard,
    Emoji,
    Folders,
    Answers,
    Calculator,
    Selection,
    Links,
    Files,
    KillProcess,
}

public static class CommandBarSourceExtensions
{
    /// <summary>
    /// What a kind of row is worth before a single letter of it is read.
    ///
    /// What this machine holds (apps, the app's own actions, Settings pages) is what people
    /// mean; menu commands borrowed from whatever app is in front, or files found by a deep
    /// search, sit under an owned row whenever the match is just as good. Small on purpose: a
    /// menu command that IS what was typed still beats an app that merely contains it, because
    /// bias only breaks ties within an equal <see cref="CommandBarSearch"/> match tier.
    /// </summary>
    public static int RankBias(this CommandBarSource source) => source switch
    {
        CommandBarSource.Menus => -80,
        // A file is the deepest and most numerous thing the bar can find, and settings pages are
        // a long list too; either has to be a plainly better match than a command to lead, never
        // merely as good.
        CommandBarSource.Files or CommandBarSource.SettingsPages => -40,
        CommandBarSource.Apps => 80,
        _ => 0,
    };
}
