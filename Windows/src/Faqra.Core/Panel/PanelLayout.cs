// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors PanelLayout in Sources/Vorssaint/UI/MenuPanel/PanelLayout.swift (lines 113-160) and the
// section visibility rules of MenuPanelView.swift (lines 249-255, 329-346).

using Faqra.Core.Features;

namespace Faqra.Core.Panel;

public static class PanelLayout
{
    /// <summary>
    /// The saved section order, repaired: unknown and repeated ids are dropped, then every missing
    /// section is added in canonical order, except that disk lands right after network, controls
    /// after utilities and brightness after keep awake, so a section introduced by an update
    /// appears next to its natural neighbour instead of at the bottom.
    /// </summary>
    public static IReadOnlyList<PanelSectionId> Order(string? saved)
    {
        var seen = new HashSet<PanelSectionId>();
        var result = new List<PanelSectionId>();
        // Swift's split drops empty pieces and never trims, so " power" is not a section.
        foreach (var raw in (saved ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (PanelSections.FromRawValue(raw) is { } id && seen.Add(id))
            {
                result.Add(id);
            }
        }
        foreach (var id in PanelSections.All)
        {
            if (!seen.Add(id))
            {
                continue;
            }
            var anchor = id switch
            {
                PanelSectionId.Disk => PanelSectionId.Network,
                PanelSectionId.Controls => PanelSectionId.Utilities,
                PanelSectionId.Brightness => PanelSectionId.KeepAwake,
                _ => (PanelSectionId?)null,
            };
            var anchorIndex = anchor is { } a ? result.IndexOf(a) : -1;
            if (anchorIndex >= 0)
            {
                result.Insert(anchorIndex + 1, id);
            }
            else
            {
                result.Add(id);
            }
        }
        return result;
    }

    /// <summary>
    /// The order with two sections exchanged. Settings moves a section past its visible neighbour this
    /// way, so hidden or unbuilt sections in between keep their saved places.
    /// </summary>
    public static IReadOnlyList<PanelSectionId> Swap(IReadOnlyList<PanelSectionId> order, PanelSectionId a, PanelSectionId b)
    {
        var result = order.ToList();
        var i = result.IndexOf(a);
        var j = result.IndexOf(b);
        if (i >= 0 && j >= 0)
        {
            (result[i], result[j]) = (result[j], result[i]);
        }
        return result;
    }

    public static string Serialize(IEnumerable<PanelSectionId> ids) => string.Join(",", ids.Select(id => id.RawValue()));

    /// <summary>
    /// The sections the panel offers as tabs, in order: available through a feature, switched on
    /// by the user, and built. <paramref name="isBuilt"/> is the Windows port's addition, so a
    /// section whose feature is not ported yet never becomes an empty tab.
    /// </summary>
    public static IReadOnlyList<PanelSectionId> Visible(
        IReadOnlyList<PanelSectionId> order,
        Func<PanelSectionId, bool> isShown,
        Func<AppFeature, bool> isAvailable,
        bool brightnessControlEnabled,
        Func<PanelSectionId, bool> isBuilt) =>
        order.Where(id => id.IsAvailable(isAvailable)
                && isShown(id)
                && (id != PanelSectionId.Brightness || brightnessControlEnabled)
                && isBuilt(id))
            .ToList();

    /// <summary>The section to render: the selection while it is still visible, else the first visible one.</summary>
    public static PanelSectionId Active(PanelSectionId? selected, IReadOnlyList<PanelSectionId> visible) =>
        selected is { } s && visible.Contains(s) ? s : visible.Count > 0 ? visible[0] : PanelSectionId.KeepAwake;
}
