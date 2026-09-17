// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors CommandBarCandidate in Sources/Vorssaint/Services/CommandBar/CommandBarSupport.swift:128-161.

namespace Faqra.Core.CommandBar;

/// <summary>
/// One row's ranking inputs. <see cref="Boost"/> carries whatever the caller wants to privilege
/// (usage, session memory, source bias) so <see cref="CommandBarSearch"/> itself stays a pure
/// function of text; <see cref="Priority"/> carries a deliberate override (alias, learned choice)
/// that beats ordinary text quality outright instead of merely nudging its score.
/// </summary>
/// <param name="Index">Stable identity into the caller's original list.</param>
/// <param name="NormalizedTitle">Already folded via <see cref="CommandBarSearch.Normalized"/>, so a long list is not re-folded on every keystroke.</param>
/// <param name="NormalizedKeywords">Already folded, same reasoning as <paramref name="NormalizedTitle"/>.</param>
/// <param name="Priority">Beats tier and score outright; zero unless the caller supplies an alias or learned-choice override.</param>
/// <param name="Boost">Added into the text score after matching succeeds; never resurrects a non-match.</param>
public sealed record CommandBarCandidate(
    int Index,
    string NormalizedTitle,
    string NormalizedKeywords,
    int Priority = 0,
    int Boost = 0)
{
    /// <summary>Folds raw <paramref name="title"/>/<paramref name="keywords"/> once, for callers building a candidate ad hoc.</summary>
    public static CommandBarCandidate FromRawText(
        int index, string title, string keywords = "", int priority = 0, int boost = 0) =>
        new(index, CommandBarSearch.Normalized(title), CommandBarSearch.Normalized(keywords), priority, boost);
}
