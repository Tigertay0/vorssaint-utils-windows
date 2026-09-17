// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the fuzzy-typo helpers in Sources/Vorssaint/Services/CommandBar/CommandBarSupport.swift:
// isSubsequence (423-431), isAdjacentTransposition (434-450), withinOneEdit (454-483).

namespace Faqra.Core.CommandBar;

/// <summary>
/// The typing-mistake rescues <see cref="CommandBarSearch"/> falls back on once an exact word,
/// prefix or substring match fails. Kept apart from the scoring itself so each rule reads and
/// tests on its own.
/// </summary>
public static class CommandBarFuzzyMatch
{
    /// <summary>
    /// A classic in-order subsequence check, no skip penalty tracked: every character of
    /// <paramref name="needle"/> must appear in <paramref name="word"/> in order, not necessarily
    /// adjacent. Catches a dropped letter ("brlho" finds "brilho").
    /// </summary>
    public static bool IsSubsequence(string needle, string word)
    {
        if (needle.Length > word.Length)
        {
            return false;
        }
        var position = 0;
        foreach (var character in needle)
        {
            var found = word.IndexOf(character, position);
            if (found < 0)
            {
                return false;
            }
            position = found + 1;
        }
        return true;
    }

    /// <summary>
    /// True only when exactly one neighboring pair was typed in reverse order. Requires equal
    /// length; a length mismatch is never a transposition. Catches "zne" for "zen".
    /// </summary>
    public static bool IsAdjacentTransposition(string first, string second)
    {
        if (first.Length != second.Length)
        {
            return false;
        }
        int? firstMismatch = null;
        var swapped = false;
        for (var index = 0; index < first.Length; index++)
        {
            if (first[index] == second[index])
            {
                continue;
            }
            if (swapped)
            {
                return false;
            }
            if (firstMismatch is null)
            {
                firstMismatch = index;
                continue;
            }
            var previous = firstMismatch.Value;
            if (index != previous + 1 || first[previous] != second[index] || first[index] != second[previous])
            {
                return false;
            }
            swapped = true;
        }
        return swapped;
    }

    /// <summary>
    /// True when the strings are at most one substitution, insertion, deletion or adjacent swap
    /// apart. Length difference must be at most one. Catches "birlho" (transposition), "brilo"
    /// (deletion) and "brilyo" (substitution) for "brilho".
    /// </summary>
    public static bool WithinOneEdit(string first, string second)
    {
        if (Math.Abs(first.Length - second.Length) > 1)
        {
            return false;
        }
        if (first == second)
        {
            return true;
        }
        var i = 0;
        var j = 0;
        var edited = false;
        while (i < first.Length && j < second.Length)
        {
            if (first[i] == second[j])
            {
                i++;
                j++;
                continue;
            }
            if (edited)
            {
                return false;
            }
            edited = true;
            if (first.Length == second.Length)
            {
                if (i + 1 < first.Length && j + 1 < second.Length
                    && first[i] == second[j + 1] && first[i + 1] == second[j])
                {
                    i += 2;
                    j += 2;
                }
                else
                {
                    i++;
                    j++;
                }
            }
            else if (first.Length > second.Length)
            {
                i++;
            }
            else
            {
                j++;
            }
        }
        return true;
    }
}
