// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Faqra.Core.Agents;

namespace Faqra.Core.Island;

/// <summary>A row on the island's Home. Raw values are saved in <c>faqraHomeItems</c>: add members, never rename them.</summary>
public enum HomeItem { NowPlaying, Agents, Timer, System, Battery }

/// <summary>Home's rows: their saved order, the owner's edits, and the one row that may wear the accent.</summary>
public static class HomeItems
{
    public static readonly IReadOnlyList<HomeItem> All = Enum.GetValues<HomeItem>();

    /// <summary>Home's first order. "nextEvent" arrives with the calendar and is skipped until then.</summary>
    public const string DefaultOrder = "nowPlaying,nextEvent,agents";

    public static string RawValue(this HomeItem item) => item switch
    {
        HomeItem.NowPlaying => "nowPlaying",
        HomeItem.Agents => "agents",
        HomeItem.Timer => "timer",
        HomeItem.System => "system",
        HomeItem.Battery => "battery",
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null),
    };

    public static HomeItem? FromRawValue(string? raw) =>
        All.Cast<HomeItem?>().FirstOrDefault(item => item!.Value.RawValue() == raw);

    /// <summary>The rows to show, in the saved order; unknown names, repeats and rows with nothing to show are skipped.</summary>
    public static IReadOnlyList<HomeItem> Parse(string? saved, Func<HomeItem, bool> isAvailable)
    {
        var seen = new HashSet<HomeItem>();
        var items = new List<HomeItem>();
        foreach (var raw in Names(saved))
        {
            if (FromRawValue(raw) is { } item && isAvailable(item) && seen.Add(item))
            {
                items.Add(item);
            }
        }
        return items;
    }

    /// <summary>What to save: the rows in their new order, then any names from <paramref name="previous"/> this version does not know.</summary>
    public static string Saved(IReadOnlyList<HomeItem> items, string? previous)
    {
        var unknown = Names(previous).Where(raw => FromRawValue(raw) is null).Distinct(StringComparer.Ordinal);
        return string.Join(",", items.Select(item => item.RawValue()).Concat(unknown));
    }

    /// <summary>Rows that can be added: available and not on Home yet, in canonical order.</summary>
    public static IReadOnlyList<HomeItem> Addable(IReadOnlyList<HomeItem> current, Func<HomeItem, bool> isAvailable) =>
        All.Where(item => isAvailable(item) && !current.Contains(item)).ToList();

    /// <summary>The list with <paramref name="item"/> moved by <paramref name="delta"/>; the same list when it cannot move.</summary>
    public static IReadOnlyList<HomeItem> Move(IReadOnlyList<HomeItem> items, HomeItem item, int delta)
    {
        var moved = items.ToList();
        var at = moved.IndexOf(item);
        var to = at + delta;
        if (at < 0 || to < 0 || to >= moved.Count)
        {
            return items;
        }
        moved.RemoveAt(at);
        moved.Insert(to, item);
        return moved;
    }

    public static IReadOnlyList<HomeItem> Remove(IReadOnlyList<HomeItem> items, HomeItem item) =>
        items.Where(other => other != item).ToList();

    public static IReadOnlyList<HomeItem> Add(IReadOnlyList<HomeItem> items, HomeItem item) =>
        items.Contains(item) ? items : [.. items, item];

    /// <summary>The single row that wears the accent: Agents, while an agent waits on the owner.</summary>
    public static HomeItem? Accent(IReadOnlyList<HomeItem> items, bool agentWaiting) =>
        agentWaiting && items.Contains(HomeItem.Agents) ? HomeItem.Agents : null;

    /// <summary>A session in this state waits on the owner.</summary>
    public static bool Waits(AgentState state) =>
        state is AgentState.Approval or AgentState.Question or AgentState.AwaitingReply;

    private static IEnumerable<string> Names(string? list) =>
        (list ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
