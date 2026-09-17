// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors highlightOffsets/foldedCharacters/firstIndex in
// Sources/Vorssaint/Services/CommandBar/CommandBarSupport.swift:385-421. Split from
// CommandBarSearch.cs (same partial type) to keep each file under the 400-line guideline.

using System.Globalization;
using System.Text;

namespace Faqra.Core.CommandBar;

public static partial class CommandBarSearch
{
    /// <summary>
    /// Character positions of the title the query literally matched, so the row can show why it
    /// is there. Only literal hits are marked: a fuzzy rescue has no honest range to point at,
    /// and highlighting a guess reads worse than highlighting nothing.
    /// </summary>
    public static IReadOnlySet<int> HighlightOffsets(string title, string query)
    {
        var folded = FoldedCharacters(title);
        if (folded.Length == 0)
        {
            return new HashSet<int>();
        }
        var offsets = new HashSet<int>();
        foreach (var rawToken in Normalized(query).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = rawToken.ToCharArray();
            if (token.Length == 0)
            {
                continue;
            }
            var start = FirstIndex(token, folded);
            if (start is null)
            {
                continue;
            }
            for (var position = start.Value; position < start.Value + token.Length; position++)
            {
                offsets.Add(position);
            }
        }
        return offsets;
    }

    /// <summary>
    /// Folds each character on its own so the folded array stays aligned with the original, one
    /// position per character. Assumes folding never changes character count, which holds for
    /// NFC Latin/CJK but is a known simplification for some multi-codepoint folds.
    /// </summary>
    private static char[] FoldedCharacters(string value)
    {
        var result = new char[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            var decomposed = value[i].ToString().Normalize(NormalizationForm.FormKD);
            char? folded = null;
            foreach (var ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }
                folded = char.ToLowerInvariant(ch);
                break;
            }
            result[i] = folded ?? char.ToLowerInvariant(value[i]);
        }
        return result;
    }

    private static int? FirstIndex(char[] token, char[] haystack)
    {
        if (token.Length > haystack.Length)
        {
            return null;
        }
        // Word starts first: highlighting "set" inside "Reset" when the title also begins with
        // "Settings" would point at the wrong place.
        for (var start = 0; start <= haystack.Length - token.Length; start++)
        {
            var isWordStart = start == 0 || haystack[start - 1] == ' ';
            if (isWordStart && SpanMatches(haystack, token, start))
            {
                return start;
            }
        }
        for (var start = 0; start <= haystack.Length - token.Length; start++)
        {
            if (SpanMatches(haystack, token, start))
            {
                return start;
            }
        }
        return null;
    }

    private static bool SpanMatches(char[] haystack, char[] token, int start)
    {
        for (var i = 0; i < token.Length; i++)
        {
            if (haystack[start + i] != token[i])
            {
                return false;
            }
        }
        return true;
    }
}
