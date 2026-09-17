// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/CommandBar/CommandBarMath.swift
using System.Globalization;

namespace Faqra.Core.CommandBar;

/// <summary>
/// The command bar's inline calculator: whatever looks like a sum is answered on the first row
/// instead of being searched for. Culture-invariant by default so the port's own tests are
/// deterministic, but the decimal/grouping separators and the output culture can be overridden
/// independently - exactly like upstream's own <c>Locale</c>-defaulted parameters.
///
/// Deliberately narrow: arithmetic, parentheses, powers and percentages. It refuses anything it
/// cannot fully parse, so a search like "1password" or "volume 20" never turns into a wrong answer.
/// </summary>
public static partial class CommandBarMath
{
    /// <summary>Words that read as "percent OF a number" across the languages the app speaks.</summary>
    private static readonly HashSet<string> OfWords = ["of", "de", "da", "do", "von", "di", "del", "dal"];

    private static readonly char[] DateOrTimeSeparators = ['-', '/', ':'];

    public readonly record struct Result(string Formatted, double Value);

    /// <summary>The answer for <paramref name="input"/>, or null when it is not an expression.
    /// Requires a digit and a real operation, so plain words and "verb number" commands are left
    /// to the search.</summary>
    public static Result? Evaluate(
        string input,
        string? decimalSeparator = null,
        string? groupingSeparator = null,
        CultureInfo? culture = null)
    {
        var trimmed = input.Trim();
        if (trimmed.Length > 120 || !trimmed.Any(char.IsDigit))
        {
            return null;
        }
        // A date or a clock time is written with the same characters as a subtraction or a
        // division. Someone looking for what they copied on 2026-07-27 must not be told the
        // answer is 1992.
        if (LooksLikeDateOrTime(trimmed))
        {
            return null;
        }

        var decimalChar = SeparatorChar(decimalSeparator, CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator);
        var groupingChar = SeparatorChar(groupingSeparator, CultureInfo.InvariantCulture.NumberFormat.NumberGroupSeparator);

        var tokens = Tokenize(trimmed, decimalChar, groupingChar);
        if (tokens is null)
        {
            return null;
        }
        // One lonely number is not a question; answering "5 = 5" only pushes real results down.
        // A bare "50%" is not one either.
        if (!tokens.Any(t => t.IsOperation && t.Kind != TokenKind.Percent))
        {
            return null;
        }

        var parser = new Parser(tokens);
        var operand = parser.ParseExpression();
        if (operand is null || !parser.IsAtEnd)
        {
            return null;
        }

        var value = SignificantRounded(operand.Value.Value);
        if (!double.IsFinite(value))
        {
            return null;
        }
        var formatted = Format(value, culture);
        return formatted is null ? null : new Result(formatted, value);
    }

    /// <summary>Rounds away floating point noise (0.1 + 0.2 must read as 0.3) and writes the
    /// number with the given culture's separators.</summary>
    public static string? Format(double value, CultureInfo? culture = null)
    {
        var effectiveCulture = culture ?? CultureInfo.InvariantCulture;
        var rounded = SignificantRounded(value);
        if (!double.IsFinite(rounded))
        {
            return null;
        }
        // -0 is an answer no one asked for.
        var normalizedValue = rounded == 0 ? 0 : rounded;
        var magnitude = Math.Abs(normalizedValue);
        if (normalizedValue != 0 && (magnitude >= 1e12 || magnitude < 1e-6))
        {
            // Plain digits would print a billionth as "0" and a huge product as a wall of zeros;
            // both read as a wrong answer.
            return FormatScientific(normalizedValue, effectiveCulture);
        }
        return FormatDecimal(normalizedValue, effectiveCulture);
    }

    /// <summary>True for "2026-07-27", "27/07/2026" and "10:30": groups of digits held together
    /// by a single kind of separator, with nothing else around them.</summary>
    public static bool LooksLikeDateOrTime(string input)
    {
        foreach (var separator in DateOrTimeSeparators)
        {
            var parts = input.Split(separator);
            if (parts.Length < 2 || parts.Length > 3)
            {
                continue;
            }
            var allDigitGroups = parts.All(part =>
            {
                var digits = part.Trim();
                return digits.Length > 0 && digits.Length <= 4 && digits.All(char.IsDigit);
            });
            if (!allDigitGroups)
            {
                continue;
            }
            // Two plain numbers around a minus really can be a subtraction, so only the shapes
            // that read as a date or a time are refused.
            if (separator == ':')
            {
                return true;
            }
            if (parts.Length == 3)
            {
                return true;
            }
        }
        return false;
    }

    private static char SeparatorChar(string? provided, string fallback) =>
        !string.IsNullOrEmpty(provided) ? provided[0] : fallback[0];

    private static double SignificantRounded(double value)
    {
        if (value == 0 || !double.IsFinite(value))
        {
            return value;
        }
        const double digits = 10.0;
        var magnitude = Math.Floor(Math.Log10(Math.Abs(value)));
        var factor = Math.Pow(10.0, digits - magnitude - 1);
        if (!double.IsFinite(factor) || factor == 0)
        {
            return value;
        }
        var scaled = Math.Round(value * factor, MidpointRounding.AwayFromZero);
        return double.IsFinite(scaled) ? scaled / factor : value;
    }

    private static string FormatDecimal(double value, CultureInfo culture)
    {
        var text = value.ToString("N8", culture);
        return TrimTrailingFractionZeros(text, culture.NumberFormat.NumberDecimalSeparator);
    }

    private static string FormatScientific(double value, CultureInfo culture)
    {
        const int maximumSignificantDigits = 8;
        var decimalDigits = maximumSignificantDigits - 1;
        var exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
        var mantissa = value / Math.Pow(10, exponent);
        mantissa = Math.Round(mantissa, decimalDigits, MidpointRounding.AwayFromZero);
        // Rounding the mantissa can push it to 10 (or, for a value just under a power of ten,
        // leave it under 1); re-normalize so exactly one non-zero digit precedes the point.
        if (Math.Abs(mantissa) >= 10)
        {
            mantissa /= 10;
            exponent += 1;
        }
        else if (mantissa != 0 && Math.Abs(mantissa) < 1)
        {
            mantissa *= 10;
            exponent -= 1;
        }
        var mantissaText = TrimTrailingFractionZeros(
            mantissa.ToString("F" + decimalDigits, CultureInfo.InvariantCulture), ".");
        if (culture.NumberFormat.NumberDecimalSeparator != ".")
        {
            mantissaText = mantissaText.Replace(".", culture.NumberFormat.NumberDecimalSeparator);
        }
        return $"{mantissaText}e{exponent.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string TrimTrailingFractionZeros(string text, string decimalSeparator)
    {
        if (!text.Contains(decimalSeparator, StringComparison.Ordinal))
        {
            return text;
        }
        var trimmed = text.TrimEnd('0');
        return trimmed.EndsWith(decimalSeparator, StringComparison.Ordinal)
            ? trimmed[..^decimalSeparator.Length]
            : trimmed;
    }

    // MARK: - Tokens

    private enum TokenKind { Number, Plus, Minus, Times, Divide, Power, Percent, LeftParen, RightParen, OfWord }

    private readonly record struct Token(TokenKind Kind, double Number = 0)
    {
        public bool IsOperation => Kind is TokenKind.Plus or TokenKind.Minus or TokenKind.Times
            or TokenKind.Divide or TokenKind.Power or TokenKind.Percent or TokenKind.OfWord;
    }

    private static List<Token>? Tokenize(string input, char decimalSeparator, char groupingSeparator)
    {
        var tokens = new List<Token>();
        var characters = input.ToCharArray();
        var index = 0;

        string ReadWord()
        {
            var start = index;
            while (index < characters.Length && char.IsLetter(characters[index]))
            {
                index++;
            }
            return new string(characters, start, index - start).ToLowerInvariant();
        }

        while (index < characters.Length)
        {
            var character = characters[index];
            if (char.IsWhiteSpace(character))
            {
                index++;
                continue;
            }
            if (char.IsDigit(character) || character == decimalSeparator || character == groupingSeparator)
            {
                var number = ReadNumber(characters, ref index, decimalSeparator, groupingSeparator);
                if (number is null)
                {
                    return null;
                }
                tokens.Add(new Token(TokenKind.Number, number.Value));
                continue;
            }
            if (char.IsLetter(character))
            {
                var word = ReadWord();
                // People write "3 x 4" as often as "3 * 4".
                if (word == "x" && tokens.Count > 0 && tokens[^1].Kind == TokenKind.Number)
                {
                    tokens.Add(new Token(TokenKind.Times));
                    continue;
                }
                if (!OfWords.Contains(word))
                {
                    return null;
                }
                tokens.Add(new Token(TokenKind.OfWord));
                continue;
            }
            index++;
            switch (character)
            {
                case '+': tokens.Add(new Token(TokenKind.Plus)); break;
                case '-': case '−': tokens.Add(new Token(TokenKind.Minus)); break;         // real minus sign
                case '*': case '×': tokens.Add(new Token(TokenKind.Times)); break;         // multiplication sign
                case '/': case '÷': tokens.Add(new Token(TokenKind.Divide)); break;        // division sign
                case '^': tokens.Add(new Token(TokenKind.Power)); break;
                case '%': tokens.Add(new Token(TokenKind.Percent)); break;
                case '(': case '[': tokens.Add(new Token(TokenKind.LeftParen)); break;
                case ')': case ']': tokens.Add(new Token(TokenKind.RightParen)); break;
                case '=': break;                                                                // trailing "=" is just habit
                default: return null;
            }
        }
        return tokens.Count == 0 ? null : tokens;
    }

    /// <summary>Reads one number, deciding what each separator means. When both appear, the last
    /// one is the decimal point. When only one appears, it is grouping only if it looks the part:
    /// the separator followed by exactly three digits.</summary>
    private static double? ReadNumber(char[] characters, ref int index, char decimalSeparator, char groupingSeparator)
    {
        var start = index;
        while (index < characters.Length
               && (char.IsDigit(characters[index]) || characters[index] == decimalSeparator || characters[index] == groupingSeparator))
        {
            index++;
        }
        if (index == start)
        {
            return null;
        }
        var raw = new string(characters, start, index - start);

        var hasDecimal = raw.Contains(decimalSeparator);
        var hasGrouping = decimalSeparator != groupingSeparator && raw.Contains(groupingSeparator);
        var normalized = raw;
        if (hasDecimal && hasGrouping)
        {
            var lastDecimal = raw.LastIndexOf(decimalSeparator);
            var lastGrouping = raw.LastIndexOf(groupingSeparator);
            normalized = lastGrouping > lastDecimal
                ? raw.Replace(decimalSeparator.ToString(), "").Replace(groupingSeparator.ToString(), ".")
                : raw.Replace(groupingSeparator.ToString(), "").Replace(decimalSeparator.ToString(), ".");
        }
        else if (hasGrouping)
        {
            normalized = LooksLikeGrouping(raw, groupingSeparator)
                ? raw.Replace(groupingSeparator.ToString(), "")
                : raw.Replace(groupingSeparator.ToString(), ".");
        }
        else if (hasDecimal)
        {
            normalized = raw.Replace(decimalSeparator.ToString(), ".");
        }

        if (normalized.Count(c => c == '.') > 1)
        {
            return null;
        }
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static bool LooksLikeGrouping(string raw, char separator)
    {
        var parts = raw.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts[0].Length == 0 || parts[0].Length > 3)
        {
            return false;
        }
        return parts.Skip(1).All(part => part.Length == 3);
    }

}
