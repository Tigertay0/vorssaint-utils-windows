// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the kindLimits table and its enforcement loop in
// Sources/Vorssaint/Services/CommandBar/CommandBarService.swift:1262-1269, 1543-1550.

namespace Faqra.Core.CommandBar;

/// <summary>
/// How many rows each kind may contribute to a ranked result list, so one provider never floods
/// it. Actions (and every other kind not listed here - answers, links, folders, emoji,
/// kill-process, selection) have no cap: they are what the bar is for, or are already scoped by
/// construction (e.g. emoji only join search behind an explicit ":" prefix).
/// </summary>
public static class CommandBarKindLimits
{
    private static readonly IReadOnlyDictionary<CommandBarEntryKind, int> Limits =
        new Dictionary<CommandBarEntryKind, int>
        {
            [CommandBarEntryKind.App] = 5,
            [CommandBarEntryKind.Window] = 4,
            [CommandBarEntryKind.Quit] = 3,
            [CommandBarEntryKind.Menu] = 5,
            [CommandBarEntryKind.Settings] = 4,
            [CommandBarEntryKind.MacSettings] = 4,
            [CommandBarEntryKind.Clipboard] = 4,
            [CommandBarEntryKind.Snippet] = 4,
            [CommandBarEntryKind.File] = 4,
            // Every switch answers to the same verb, so searching that verb would otherwise fill
            // the list with rows that all read alike.
            [CommandBarEntryKind.Toggle] = 5,
        };

    /// <summary>The default total result count upstream stops the merged list at (CommandBarService.swift:1549).</summary>
    public const int DefaultMaxResults = 12;

    /// <summary>
    /// Caps a ranked list (best first) by kind and by total count, dropping the excess rather
    /// than reordering: ranking already decided who leads, this only decides who fits.
    /// </summary>
    public static IReadOnlyList<CommandBarEntry> Apply(
        IEnumerable<CommandBarEntry> rankedEntries, int maxResults = DefaultMaxResults)
    {
        var counts = new Dictionary<CommandBarEntryKind, int>();
        var result = new List<CommandBarEntry>();
        foreach (var entry in rankedEntries)
        {
            if (Limits.TryGetValue(entry.Kind, out var limit))
            {
                var used = counts.GetValueOrDefault(entry.Kind);
                if (used >= limit)
                {
                    continue;
                }
                counts[entry.Kind] = used + 1;
            }
            result.Add(entry);
            if (result.Count >= maxResults)
            {
                break;
            }
        }
        return result;
    }
}
