// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the row shape described by CommandBarCatalog.swift's CommandBarEntry (fields referenced
// in docs/superpowers/specs digest-commandbar-service.md section 10) trimmed to what Stage 1's
// ranking pipeline needs. The full upstream struct also carries icon, shortcut, trouble state,
// numeric-argument and run-closure fields that belong to the catalog/service layer, not to the
// pure ranking core ported here.

namespace Faqra.Core.CommandBar;

/// <summary>
/// The kind of row, used to cap how many of one kind can crowd a result list
/// (<see cref="CommandBarKindLimits"/>). Mirrors the id-prefix families CommandBarService.swift's
/// kindLimits table checks by string (CommandBarService.swift:1262-1269): app./window./quit./
/// menu./settings./macsettings./clipboard./snippet./file./toggle. Kinds with no upstream limit
/// (actions, answers, links, folders, emoji, kill-process, selection) are uncapped.
/// </summary>
public enum CommandBarEntryKind
{
    Action,
    App,
    Window,
    Quit,
    Menu,
    Settings,
    MacSettings,
    Snippet,
    Clipboard,
    File,
    Toggle,
    Emoji,
    Folder,
    Answer,
    Link,
    Selection,
    KillProcess,
}

/// <summary>
/// One searchable row offered to the command bar. Immutable: ranking is a pure function of a
/// snapshot of these, never of something the ranking pass itself could still be mutating.
/// </summary>
/// <param name="Id">
/// Stable within one build, kind-prefixed the way upstream ids are ("app.chrome",
/// "window.42", ...) so <see cref="CommandBarKindLimits"/> and list diffing can key off it.
/// </param>
/// <param name="Title">The row's display name; also the primary ranking text.</param>
/// <param name="Subtitle">Secondary text shown under the title, not searched.</param>
/// <param name="Keywords">Extra searchable text not shown, e.g. an app's alternate names.</param>
/// <param name="Kind">Which result family this row belongs to, for <see cref="CommandBarKindLimits"/>.</param>
/// <param name="Source">Which provider this row came from, for <see cref="CommandBarSourceExtensions.RankBias"/>.</param>
/// <param name="Priority">
/// An explicit preference (an alias or a learned query choice, both out of Stage 1's scope)
/// that should outrank ordinary text quality. Zero for every Stage 1 row.
/// </param>
/// <param name="CountsUsage">
/// False for rows a run should never teach the ranking from (a found file, a menu command, a
/// clipboard entry) - mirrors upstream's CommandBarEntry.countsUsage.
/// </param>
public sealed record CommandBarEntry(
    string Id,
    string Title,
    string Subtitle,
    string Keywords,
    CommandBarEntryKind Kind,
    CommandBarSource Source,
    int Priority = 0,
    bool CountsUsage = true);
