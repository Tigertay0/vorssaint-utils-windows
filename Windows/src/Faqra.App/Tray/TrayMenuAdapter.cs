// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Faqra.Core.Tray;
using Faqra.Win32.Menus;

namespace Faqra.App.Tray;

/// <summary>
/// Turns the platform-neutral menu model into native popup entries with sequential ids, and
/// greys out actions that no milestone has wired yet so the menu shape still matches upstream.
/// </summary>
public static class TrayMenuAdapter
{
    public static (IReadOnlyList<PopupMenuEntry> Entries, IReadOnlyDictionary<int, TrayMenuItem> Lookup) ToEntries(
        IReadOnlyList<TrayMenuItem> items,
        ISet<TrayMenuAction> implementedActions)
    {
        var lookup = new Dictionary<int, TrayMenuItem>();
        var nextId = 1;
        var entries = Convert(items, implementedActions, lookup, ref nextId);
        return (entries, lookup);
    }

    private static List<PopupMenuEntry> Convert(
        IReadOnlyList<TrayMenuItem> items,
        ISet<TrayMenuAction> implemented,
        Dictionary<int, TrayMenuItem> lookup,
        ref int nextId)
    {
        var entries = new List<PopupMenuEntry>(items.Count);
        foreach (var item in items)
        {
            if (item.IsSeparator)
            {
                entries.Add(new PopupMenuEntry(string.Empty, IsSeparator: true));
                continue;
            }
            if (item.HasChildren)
            {
                var children = Convert(item.Children!, implemented, lookup, ref nextId);
                entries.Add(new PopupMenuEntry(item.Title, Enabled: children.Any(c => c.Enabled), Children: children));
                continue;
            }
            var id = nextId++;
            lookup[id] = item;
            var enabled = item.Action is { } action && implemented.Contains(action);
            entries.Add(new PopupMenuEntry(item.Title, id, enabled));
        }
        return entries;
    }
}
