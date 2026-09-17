// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors splitTrailingNumber/argumentValue in
// Sources/Vorssaint/Services/CommandBar/CommandBarSupport.swift:521-548. Split from
// CommandBarSearch.cs (same partial type) to keep each file under the 400-line guideline.

using System.Globalization;

namespace Faqra.Core.CommandBar;

/// <summary>
/// The result of <see cref="CommandBarSearch.SplitTrailingNumber"/>: the text before a trailing
/// bare number, and the number itself when one was found.
/// </summary>
public sealed record CommandBarNumberSplit(string Text, int? Number);

public static partial class CommandBarSearch
{
    /// <summary>
    /// The split of "brilho 40" into the verb and its value. Only a trailing bare number counts,
    /// and only after some text: a number alone is a search, not a command.
    /// </summary>
    public static CommandBarNumberSplit SplitTrailingNumber(string query)
    {
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
        {
            return new CommandBarNumberSplit(query.Trim(), null);
        }
        var digits = tokens[^1];
        if (digits.EndsWith('%'))
        {
            digits = digits[..^1];
        }
        if (digits.Length == 0 || !digits.All(char.IsDigit)
            || !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            return new CommandBarNumberSplit(query.Trim(), null);
        }
        return new CommandBarNumberSplit(string.Join(' ', tokens[..^1]), value);
    }

    /// <summary>
    /// The value typed while the bar asks for a number in place. Accepts an optional %, rejects
    /// everything else, clamps into the command's range.
    /// </summary>
    public static int? ArgumentValue(string text, int minimum, int maximum)
    {
        var digits = text.Trim();
        if (digits.EndsWith('%'))
        {
            digits = digits[..^1];
        }
        if (digits.Length == 0 || digits.Length > 4 || !digits.All(char.IsDigit)
            || !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }
        return Math.Min(Math.Max(value, minimum), maximum);
    }
}
