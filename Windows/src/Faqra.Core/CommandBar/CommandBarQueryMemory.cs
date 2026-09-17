// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/CommandBar/CommandBarQueryMemory.swift.

namespace Faqra.Core.CommandBar;

/// <summary>
/// What the bar noticed about this session: which row was chosen after which few letters, so
/// typing those letters again lands on it.
///
/// Ranking by usage alone cannot answer this. Someone who opens one app all day can still walk
/// past it after typing part of its name, because usage is attached to the row and not to what
/// they typed to reach it.
///
/// <b>It is never persisted.</b> The bar promises to forget everything typed into it, and that
/// promise is worth more than remembering a preference across launches: this lives in memory for
/// as long as the process runs and goes with it. Choosing the same row twice in one sitting is
/// where nearly all of the benefit is anyway. A mutable class, not an immutable record, because
/// it is deliberately session state - the same role upstream's mutating struct plays behind one
/// service-owned instance.
/// </summary>
public sealed class CommandBarQueryMemory
{
    /// <summary>Enough prefixes for a session's worth of typing, few enough that the map never becomes something to think about.</summary>
    public const int QueryLimit = 60;

    /// <summary>Two rows can honestly compete for the same letters. More than a handful is not a memory, it is a list of everything.</summary>
    public const int IdsPerQuery = 4;

    /// <summary>Past this, what was typed is a whole word and the ranking already knows what to do with it.</summary>
    public const int LongestPrefix = 12;

    /// <summary>
    /// What one remembered choice is worth, at most. Deliberately only a tie-breaker: what the
    /// bar noticed can reorder rows that match equally well, but cannot turn a weaker keyword
    /// match into a better answer.
    /// </summary>
    public const int MaximumBoost = 3;

    private sealed record Pick(int Count, int Step);

    private readonly Dictionary<string, Dictionary<string, Pick>> _picks = new(StringComparer.Ordinal);

    /// <summary>
    /// Every leading piece of what was typed, so choosing a row after three letters also answers
    /// the first one and two. Folded the same way ranking folds, so memory and search agree about
    /// accents and case.
    /// </summary>
    public static IReadOnlyList<string> Prefixes(string query)
    {
        var normalized = CommandBarSearch.Normalized(query);
        if (normalized.Length == 0)
        {
            return Array.Empty<string>();
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        var limit = Math.Min(normalized.Length, LongestPrefix);
        for (var length = 1; length <= limit; length++)
        {
            var prefix = normalized[..length].Trim();
            if (prefix.Length == 0 || !seen.Add(prefix))
            {
                continue;
            }
            result.Add(prefix);
        }
        return result;
    }

    /// <summary>Notes that this row was the answer to what was typed. <paramref name="step"/> counts choices, and only has to grow.</summary>
    public void Record(string query, string id, int step)
    {
        foreach (var prefix in Prefixes(query))
        {
            if (!_picks.TryGetValue(prefix, out var forPrefix))
            {
                forPrefix = new Dictionary<string, Pick>(StringComparer.Ordinal);
                _picks[prefix] = forPrefix;
            }
            var pick = forPrefix.GetValueOrDefault(id) ?? new Pick(0, step);
            forPrefix[id] = pick with { Count = Math.Min(pick.Count + 1, 99), Step = step };
            // The rows that answer to one prefix: the least chosen goes first, so a row picked
            // once by mistake cannot hold a place forever.
            if (forPrefix.Count > IdsPerQuery)
            {
                var surplus = forPrefix
                    .OrderBy(entry => entry.Value.Count)
                    .ThenBy(entry => entry.Value.Step)
                    .Take(forPrefix.Count - IdsPerQuery)
                    .Select(entry => entry.Key)
                    .ToArray();
                foreach (var key in surplus)
                {
                    forPrefix.Remove(key);
                }
            }
        }
        if (_picks.Count <= QueryLimit)
        {
            return;
        }
        // And the prefixes themselves: the ones not used for longest go.
        var stalePrefixes = _picks
            .OrderBy(entry => entry.Value.Values.Select(pick => pick.Step).DefaultIfEmpty(0).Max())
            .Take(_picks.Count - QueryLimit)
            .Select(entry => entry.Key)
            .ToArray();
        foreach (var prefix in stalePrefixes)
        {
            _picks.Remove(prefix);
        }
    }

    /// <summary>What this row is worth for exactly these letters. Nothing at all for a row that was never chosen after them.</summary>
    public int Boost(string query, string id) => BoostNormalized(CommandBarSearch.Normalized(query), id);

    /// <summary>
    /// The same answer for letters the caller has already folded, so a pass over the pool folds
    /// the query once instead of once per row. This is a read of the exact string handed to it -
    /// no prefix/substring matching happens here, only on the write side in <see cref="Record"/>.
    /// </summary>
    public int BoostNormalized(string normalizedQuery, string id)
    {
        if (normalizedQuery.Length == 0)
        {
            return 0;
        }
        if (!_picks.TryGetValue(normalizedQuery, out var forPrefix) || !forPrefix.TryGetValue(id, out var pick))
        {
            return 0;
        }
        return Math.Min(pick.Count, 3) * (MaximumBoost / 3);
    }

    /// <summary>Forgets one row everywhere, for the person who asks the bar to stop putting it first.</summary>
    public void Forget(string id)
    {
        var emptiedPrefixes = new List<string>();
        foreach (var (prefix, forPrefix) in _picks)
        {
            if (forPrefix.Remove(id) && forPrefix.Count == 0)
            {
                emptiedPrefixes.Add(prefix);
            }
        }
        foreach (var prefix in emptiedPrefixes)
        {
            _picks.Remove(prefix);
        }
    }

    public void Clear() => _picks.Clear();

    public bool IsEmpty => _picks.Count == 0;
}
