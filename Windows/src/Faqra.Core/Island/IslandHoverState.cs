// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors NotchHoverState and the hover timing in Sources/Vorssaint/Services/Notch/NotchService.swift
// (lines 392-443) and NotchQuickAccessLayout.hoverExitDelay (line 344).

namespace Faqra.Core.Island;

/// <summary>What the island is showing.</summary>
public enum IslandPresentation
{
    /// <summary>The resting pill.</summary>
    Collapsed,
    /// <summary>The small hover state used when full expansion is switched off.</summary>
    Peek,
    /// <summary>A module is open.</summary>
    Expanded,
}

/// <summary>
/// The hover timings. A resize can send an exit and an entry without the pointer moving, so an
/// expansion that happens under a still pointer suppresses the next auto-close until the pointer
/// actually leaves.
/// </summary>
public sealed class IslandHoverState
{
    /// <summary>How long the pointer must rest on the pill before it opens.</summary>
    public static readonly TimeSpan OpenDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>How long after the pointer leaves an expanded island before it collapses.</summary>
    public static readonly TimeSpan ExpandedExitDelay = TimeSpan.FromMilliseconds(180);

    /// <summary>The same, from the peek state.</summary>
    public static readonly TimeSpan PeekExitDelay = TimeSpan.FromMilliseconds(120);

    /// <summary>True while an auto-close is suppressed because the pointer never left.</summary>
    public bool Suppressed { get; private set; }

    /// <summary>Called when the island closes. Staying under the pointer suppresses re-opening.</summary>
    public void Close(bool pointerInside) => Suppressed = pointerInside;

    public void Open() => Suppressed = false;

    /// <summary>Called on every pointer update; leaving the silhouette clears the suppression.</summary>
    public void Update(bool pointerInside)
    {
        if (!pointerInside)
        {
            Suppressed = false;
        }
    }

    /// <summary>The delay before collapsing from a presentation, or null when it should not auto-close.</summary>
    public static TimeSpan? ExitDelay(IslandPresentation presentation, bool openedByHover, bool pinned)
    {
        if (pinned)
        {
            return null;
        }
        return presentation switch
        {
            IslandPresentation.Expanded when openedByHover => ExpandedExitDelay,
            IslandPresentation.Peek => PeekExitDelay,
            _ => null,
        };
    }

    /// <summary>
    /// What a hover should open. Null means stay collapsed: hover opening is switched off, or the
    /// last close happened under a still pointer.
    /// </summary>
    public IslandPresentation? PresentationForHover(bool openOnHover, bool hoverExpands)
    {
        if (!openOnHover || Suppressed)
        {
            return null;
        }
        return hoverExpands ? IslandPresentation.Expanded : IslandPresentation.Peek;
    }
}
