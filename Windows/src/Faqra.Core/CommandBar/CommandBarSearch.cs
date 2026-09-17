// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors CommandBarSearch in Sources/Vorssaint/Services/CommandBar/CommandBarSupport.swift:
// normalized (182-191), strippingInvisibles/isInvisible (193-207), matchesVerb (243-258),
// score (267-302), rankedIndexes/matchTier (307-339), bestTokenScore (341-370),
// firstOccurrences (376-379), emojiQuery (172-176). Highlighting lives in
// CommandBarSearch.Highlighting.cs and the numeric-argument split in
// CommandBarSearch.Arguments.cs - same partial type, split for the 400-line file guideline.
//
// Pinyin keyword generation (pinyinKeywords, applicationKeywords) is explicitly out of Stage 1's
// scope per the porting brief - it needs a bundled Han-to-Pinyin table with no .NET equivalent -
// and is not ported here.

using System.Globalization;
using System.Text;

namespace Faqra.Core.CommandBar;

/// <summary>
/// Pure text matching and ranking for the command bar. The shape follows the upstream Swift:
/// fold, tokenize, score, stable ties - but typing mistakes must not break it: a token also
/// matches as an in-order subsequence of a word ("brlho" finds "brilho") and, as a last resort,
/// within one edit of a word ("birlho" too).
/// </summary>
public static partial class CommandBarSearch
{
    /// <summary>
    /// A leading colon scopes the global search to emoji. The marker is not part of the text
    /// being matched, so ":fire" finds the same emoji as "fire" inside the Emoji category.
    /// </summary>
    public static string? EmojiQuery(string query)
    {
        var trimmed = query.Trim();
        return trimmed.StartsWith(':') ? trimmed[1..].Trim() : null;
    }

    /// <summary>
    /// Case, accent and width differences never matter. Folded under the invariant culture on
    /// purpose: a culture-aware fold (e.g. Turkish lowercasing capital I to a dotless one) would
    /// stop "insta" from finding a title that begins with that letter under that culture.
    /// </summary>
    public static string Normalized(string value)
    {
        var stripped = StripInvisibles(value);
        var decomposed = stripped.Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            builder.Append(ch);
        }
        var folded = builder.ToString().ToLowerInvariant();
        // Splitting on any whitespace char (not just ASCII space) treats the full-width space
        // (U+3000) some input methods produce the same as the half-width one.
        var parts = folded.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }

    /// <summary>
    /// Drops the characters that take up no space and that nobody can type: direction marks,
    /// zero-width joiners, soft hyphens. One widely installed app carries a left-to-right mark in
    /// front of its name, and a single invisible character was enough to stop it from being an
    /// exact match for its own name.
    /// </summary>
    private static string StripInvisibles(string value)
    {
        var hasInvisible = false;
        foreach (var rune in value.EnumerateRunes())
        {
            if (IsInvisible(rune))
            {
                hasInvisible = true;
                break;
            }
        }
        if (!hasInvisible)
        {
            return value;
        }
        var builder = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            if (!IsInvisible(rune))
            {
                builder.Append(rune.ToString());
            }
        }
        return builder.ToString();
    }

    /// <summary>Every format character sits above this point, so plain text never pays for the lookup.</summary>
    private static bool IsInvisible(Rune rune) =>
        rune.Value >= 0x00AD && Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format;

    /// <summary>
    /// Whether the query names the verb of a format like "Quit {0}". Used to keep a heavy, rare
    /// command out of the list until it is asked for, in every language: the verb is whatever the
    /// format says around the name, and languages written without spaces match by containment.
    /// The upstream placeholder is Swift's "%@"; the Windows format string uses "{0}" instead.
    /// </summary>
    public static bool MatchesVerb(string query, string format)
    {
        var verb = Normalized(format.Replace("{0}", " ")).Trim();
        if (verb.Length == 0)
        {
            return false;
        }
        var normalizedQuery = Normalized(query);
        if (normalizedQuery.Length == 0)
        {
            return false;
        }
        var verbWords = verb.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length < 2)
            {
                continue;
            }
            foreach (var word in verbWords)
            {
                if (word.StartsWith(token, StringComparison.Ordinal) || token.StartsWith(word, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            // Japanese, Chinese and Korean write the verb against the name with no space to split on.
            if (verb.Contains(token, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    public static bool Matches(string title, string keywords, string query) =>
        ScoreRawText(title, keywords, query) is not null;

    /// <summary>
    /// Null when the query does not match; otherwise a comparable score. Folds
    /// <paramref name="title"/>/<paramref name="keywords"/>/<paramref name="query"/> first; the
    /// ranking pass itself calls the already-normalized overload below so a long list is not
    /// re-folded on every keystroke.
    /// </summary>
    public static int? ScoreRawText(string title, string keywords, string query) =>
        Score(Normalized(title), Normalized(keywords), Normalized(query));

    /// <summary>The scoring itself, over text that is already folded. Everything the bar ranks per keystroke comes through here.</summary>
    public static int? Score(string normalizedTitle, string normalizedKeywords, string normalizedQuery)
    {
        if (normalizedQuery.Length == 0)
        {
            return null;
        }
        var haystack = normalizedKeywords.Length == 0
            ? normalizedTitle
            : normalizedTitle + " " + normalizedKeywords;
        var words = haystack.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var score = 0;
        foreach (var token in normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var tokenScore = BestTokenScore(token, words, haystack);
            if (tokenScore is null)
            {
                return null;
            }
            score += tokenScore.Value;
        }

        if (normalizedTitle == normalizedQuery)
        {
            score += 1200;
        }
        else if (normalizedTitle.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            score += 900;
        }
        else if (normalizedTitle.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            score += 700;
        }
        else if (normalizedKeywords.Length != 0 && normalizedKeywords.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            score += 350;
        }
        return score;
    }

    /// <summary>
    /// Indexes of the matching candidates, best first. Deliberate preferences (<see
    /// cref="CommandBarCandidate.Priority"/>) lead match quality; ties keep the caller's order so
    /// equally good rows stay where the catalog put them.
    /// </summary>
    public static IReadOnlyList<int> RankedIndexes(IReadOnlyList<CommandBarCandidate> candidates, string query)
    {
        var normalizedQuery = Normalized(query);
        var scored = new List<(int Index, int Priority, int Tier, int Score, int Position)>(candidates.Count);
        for (var position = 0; position < candidates.Count; position++)
        {
            var candidate = candidates[position];
            var baseScore = Score(candidate.NormalizedTitle, candidate.NormalizedKeywords, normalizedQuery);
            if (baseScore is null)
            {
                continue;
            }
            var tier = MatchTier(candidate.NormalizedTitle, candidate.NormalizedKeywords, normalizedQuery);
            scored.Add((candidate.Index, candidate.Priority, tier, baseScore.Value + candidate.Boost, position));
        }
        scored.Sort((a, b) =>
        {
            if (a.Priority != b.Priority)
            {
                return b.Priority.CompareTo(a.Priority);
            }
            if (a.Tier != b.Tier)
            {
                return b.Tier.CompareTo(a.Tier);
            }
            if (a.Score != b.Score)
            {
                return b.Score.CompareTo(a.Score);
            }
            return a.Position.CompareTo(b.Position);
        });
        return scored.ConvertAll(entry => entry.Index);
    }

    /// <summary>
    /// Broad text quality is compared before passive signals such as usage and source preference.
    /// Explicit aliases and learned query choices arrive as priority instead, because they record
    /// what the person actually meant.
    /// </summary>
    private static int MatchTier(string title, string keywords, string query)
    {
        if (title == query)
        {
            return 5;
        }
        if (title.StartsWith(query, StringComparison.Ordinal))
        {
            return 4;
        }
        if (title.Contains(query, StringComparison.Ordinal))
        {
            return 3;
        }
        if (keywords.Length != 0 && keywords.Contains(query, StringComparison.Ordinal))
        {
            return 2;
        }
        return 1;
    }

    private static int? BestTokenScore(string token, IReadOnlyList<string> words, string haystack)
    {
        int? best = null;
        foreach (var word in words)
        {
            if (word == token)
            {
                return 140;
            }
            if (word.StartsWith(token, StringComparison.Ordinal))
            {
                best = Math.Max(best ?? 0, 80);
            }
        }
        if (best is null && haystack.Contains(token, StringComparison.Ordinal))
        {
            best = 44;
        }
        // Fuzzy passes only rescue typing mistakes, so they need some length to bite on; two
        // letters matching half the catalog helps no one. Digit tokens are values, never typos.
        var isNumeric = token.Length > 0 && token.All(char.IsDigit);
        if (best is null && token.Length >= 3 && !isNumeric)
        {
            foreach (var word in words)
            {
                if (CommandBarFuzzyMatch.IsSubsequence(token, word))
                {
                    best = 24;
                    break;
                }
            }
        }
        if (best is null && token.Length >= 3 && !isNumeric)
        {
            foreach (var word in words)
            {
                if (CommandBarFuzzyMatch.IsAdjacentTransposition(token, word))
                {
                    best = 16;
                    break;
                }
            }
        }
        if (best is null && token.Length >= 4 && !isNumeric)
        {
            foreach (var word in words)
            {
                if (CommandBarFuzzyMatch.WithinOneEdit(token, word))
                {
                    best = 16;
                    break;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// The positions to keep when ids repeat: the first of each, in order. Two rows sharing one
    /// id is undefined behaviour in a list bound to it, and the list is stitched together from
    /// several providers plus whatever the person saved.
    /// </summary>
    public static IReadOnlyList<int> FirstOccurrences(IReadOnlyList<string> ids)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<int>();
        for (var index = 0; index < ids.Count; index++)
        {
            if (seen.Add(ids[index]))
            {
                result.Add(index);
            }
        }
        return result;
    }

}
