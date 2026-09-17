// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Composes the Stage 1 ranking inputs the way
// Sources/Vorssaint/Services/CommandBar/CommandBarService.swift's searchRows(for:) does
// (CommandBarService.swift:1486-1526, 1543-1550): usage boost + source rank bias + session query
// memory feed CommandBarCandidate.boost, CommandBarSearch.RankedIndexes orders the pool, and
// CommandBarKindLimits caps the result. Pool assembly across providers (selection, apps, windows,
// menus, clipboard, ...), aliases and learned query-habit priority are the service layer's job -
// out of Stage 1's ranking-core scope - so this facade takes one flat pool and no alias/habit
// input; a caller wanting those folds them into CommandBarEntry.Priority before calling.
//
// This is the one call the Windows command bar service is expected to use once it exists.

namespace Faqra.Core.CommandBar;

public static class CommandBarRanking
{
    /// <summary>
    /// Ranks <paramref name="entries"/> against <paramref name="query"/> and caps the result by
    /// kind, mirroring the boost composition in CommandBarService.swift:1498-1524: persisted
    /// usage (only for entries with <see cref="CommandBarEntry.CountsUsage"/>), this session's
    /// query memory (same condition) and the entry's source rank bias. Priority (alias/learned
    /// choice) is read straight from each entry - Stage 1 has no alias or query-habit store, so
    /// every entry's own <see cref="CommandBarEntry.Priority"/> is normally zero.
    /// </summary>
    public static IReadOnlyList<CommandBarEntry> Rank(
        IReadOnlyList<CommandBarEntry> entries,
        string query,
        IReadOnlyDictionary<string, CommandBarUse>? usage = null,
        CommandBarQueryMemory? sessionMemory = null,
        double now = 0,
        int maxResults = CommandBarKindLimits.DefaultMaxResults)
    {
        usage ??= new Dictionary<string, CommandBarUse>(StringComparer.Ordinal);
        var foldedQuery = CommandBarSearch.Normalized(query);
        var candidates = new List<CommandBarCandidate>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var boost = 0;
            if (entry.CountsUsage)
            {
                boost += CommandBarUsage.Boost(usage.GetValueOrDefault(entry.Id), now);
                if (sessionMemory is not null)
                {
                    boost += sessionMemory.BoostNormalized(foldedQuery, entry.Id);
                }
            }
            boost += entry.Source.RankBias();
            candidates.Add(CommandBarCandidate.FromRawText(
                index, entry.Title, entry.Keywords, entry.Priority, boost));
        }
        var ranked = CommandBarSearch.RankedIndexes(candidates, query);
        return CommandBarKindLimits.Apply(ranked.Select(index => entries[index]), maxResults);
    }
}
