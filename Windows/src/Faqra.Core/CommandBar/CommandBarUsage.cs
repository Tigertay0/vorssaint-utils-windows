// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors CommandBarUse and CommandBarUsage in
// Sources/Vorssaint/Services/CommandBar/CommandBarSupport.swift:553-654, and the read/write
// sites in CommandBarService.swift (finish():2174-2181, resetRanking():900-904) that persist it
// under DefaultsKey.commandBarUsage. Query-habit learning (CommandBarQueryHabits, the
// HMAC/Keychain-backed digest store) is explicitly out of Stage 1's scope per the porting brief
// and is not ported here; CommandBarUsage alone is what makes "chr" favor a recently-run Chrome
// after first use.

using System.Text.Json;
using Faqra.Core.Defaults;

namespace Faqra.Core.CommandBar;

/// <summary>How often and how recently one command ran. Epoch seconds, to match upstream's Date.timeIntervalSince1970.</summary>
public sealed record CommandBarUse(int Count, double LastUsed);

/// <summary>Usage bookkeeping behind "the order privileges what the person uses most".</summary>
public static class CommandBarUsage
{
    public const int StoredIdLimit = 200;

    public static IReadOnlyDictionary<string, CommandBarUse> Decode(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return new Dictionary<string, CommandBarUse>(StringComparer.Ordinal);
        }
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, CommandBarUse>>(raw);
            return map ?? new Dictionary<string, CommandBarUse>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, CommandBarUse>(StringComparer.Ordinal);
        }
    }

    public static string Encode(IReadOnlyDictionary<string, CommandBarUse> usage) =>
        JsonSerializer.Serialize(usage);

    /// <summary>Reads and decodes the store's <see cref="DefaultsKey.CommandBarUsage"/> entry.</summary>
    public static IReadOnlyDictionary<string, CommandBarUse> Load(ISettingsStore store) =>
        Decode(store.String(DefaultsKey.CommandBarUsage));

    /// <summary>Encodes and writes <paramref name="usage"/> to the store's <see cref="DefaultsKey.CommandBarUsage"/> entry.</summary>
    public static void Save(ISettingsStore store, IReadOnlyDictionary<string, CommandBarUse> usage) =>
        store.Set(DefaultsKey.CommandBarUsage, Encode(usage));

    /// <summary>
    /// The map after one more run of <paramref name="id"/>. Oldest ids fall off past the cap so
    /// the store never grows with the years.
    /// </summary>
    public static IReadOnlyDictionary<string, CommandBarUse> Recording(
        IReadOnlyDictionary<string, CommandBarUse> usage, string id, double now)
    {
        var next = new Dictionary<string, CommandBarUse>(usage, StringComparer.Ordinal);
        var existing = next.GetValueOrDefault(id) ?? new CommandBarUse(0, now);
        next[id] = existing with { Count = Math.Min(existing.Count + 1, 999), LastUsed = now };
        if (next.Count > StoredIdLimit)
        {
            var surplus = next
                .OrderBy(pair => pair.Value.LastUsed)
                .Take(next.Count - StoredIdLimit)
                .Select(pair => pair.Key)
                .ToArray();
            foreach (var key in surplus)
            {
                next.Remove(key);
            }
        }
        return next;
    }

    /// <summary>
    /// The ranking boost habit earns. Capped well below a literal text hit so what the person
    /// actually typed always beats what they usually run.
    /// </summary>
    public static int Boost(CommandBarUse? use, double now)
    {
        if (use is null || use.Count <= 0)
        {
            return 0;
        }
        var age = Math.Max(0, now - use.LastUsed);
        var weight = age switch
        {
            < 2 * 3600 => 12,
            < 48 * 3600 => 8,
            < 14 * 86400 => 5,
            _ => 2,
        };
        return Math.Min(use.Count, 40) * weight;
    }

    /// <summary>What an empty bar offers: the most used commands first, then a few curated ones worth discovering, never more than <paramref name="limit"/>.</summary>
    public static IReadOnlyList<string> SuggestionIds(
        IReadOnlyDictionary<string, CommandBarUse> usage,
        IReadOnlyList<string> available,
        IReadOnlyList<string> curated,
        int limit)
    {
        var availableSet = new HashSet<string>(available, StringComparer.Ordinal);
        var picked = new List<string>();
        var used = usage
            .Where(pair => availableSet.Contains(pair.Key) && pair.Value.Count > 0)
            .OrderByDescending(pair => pair.Value.Count)
            .ThenByDescending(pair => pair.Value.LastUsed)
            .Select(pair => pair.Key);
        picked.AddRange(used.Take(limit));
        foreach (var id in curated)
        {
            if (picked.Count >= limit)
            {
                break;
            }
            if (availableSet.Contains(id) && !picked.Contains(id))
            {
                picked.Add(id);
            }
        }
        return picked;
    }

    /// <summary>
    /// Empty category browsers keep their useful catalog order until the person chooses
    /// something. Used rows then lead by frequency and recency, while unseen rows retain their
    /// original relative order.
    /// </summary>
    public static IReadOnlyList<string> CategoryIds(
        IReadOnlyDictionary<string, CommandBarUse> usage, IReadOnlyList<string> available)
    {
        return available
            .Select((id, offset) => (Id: id, Offset: offset, Use: usage.GetValueOrDefault(id) is { Count: > 0 } use ? use : null))
            .OrderBy(entry => entry, CategoryOrder.Instance)
            .Select(entry => entry.Id)
            .ToArray();
    }

    private sealed class CategoryOrder : IComparer<(string Id, int Offset, CommandBarUse? Use)>
    {
        public static readonly CategoryOrder Instance = new();

        public int Compare((string Id, int Offset, CommandBarUse? Use) left, (string Id, int Offset, CommandBarUse? Use) right)
        {
            if (left.Use is { } leftUse && right.Use is { } rightUse)
            {
                if (leftUse.Count != rightUse.Count)
                {
                    return rightUse.Count.CompareTo(leftUse.Count);
                }
                return leftUse.LastUsed != rightUse.LastUsed
                    ? rightUse.LastUsed.CompareTo(leftUse.LastUsed)
                    : left.Offset.CompareTo(right.Offset);
            }
            if (left.Use is not null)
            {
                return -1;
            }
            if (right.Use is not null)
            {
                return 1;
            }
            return left.Offset.CompareTo(right.Offset);
        }
    }
}
