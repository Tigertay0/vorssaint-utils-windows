// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/CommandBar/CommandBarUnits.swift
using System.Globalization;
using System.Text;

namespace Faqra.Core.CommandBar;

/// <summary>
/// Unit conversion for the command bar: "100 km to mi", "20c to f", "5 gb to mb". Foundation's
/// <c>Dimension</c>/<c>Measurement</c>/<c>MeasurementFormatter</c> have no .NET equivalent, so this
/// file hand-rolls the conversion tables; only the numeric formatting is pinned exactly, not the
/// localized unit word (see upstream's own :268-273 comment - MeasurementFormatter's word choice
/// is OS/locale-version dependent, so upstream never pins it either).
///
/// Deliberately strict. A conversion needs a number, a unit it knows, one of the little words that
/// mean "to", and another unit of the SAME family. Anything else is left to the search.
/// </summary>
public static class CommandBarUnits
{
    private const string FeetSymbol = "ft";
    private const string InchSymbol = "in";

    public readonly record struct Result(string Formatted, double Value);

    private enum Family { Temperature, Length, Mass, Data, Duration, Volume }

    private sealed record UnitDefinition(Family Family, string Symbol, Func<double, double> ToBase, Func<double, double> FromBase);

    /// <summary>The words that mean "convert into", across the languages the app speaks.
    ///
    /// "in" is on this list and is also the symbol for inches. That collision is resolved
    /// structurally in <see cref="Convert"/>, not lexically: a candidate keyword position only
    /// counts when exactly one known unit follows it and a number-plus-unit precedes it.</summary>
    private static readonly HashSet<string> ConversionWords =
        ["to", "in", "into", "as", "em", "para", "pra", "en", "a", "nach", "zu", "à", "su", "->", ">", "→"];

    /// <summary>The lexicon. Symbols are matched exactly after folding, never fuzzily.</summary>
    private static readonly Dictionary<string, UnitDefinition> Lexicon = BuildLexicon();

    /// <summary>The conversion for <paramref name="input"/>, or null when it is not one.</summary>
    public static Result? Convert(
        string input,
        string? decimalSeparator = null,
        string? groupingSeparator = null,
        CultureInfo? culture = null)
    {
        var tokens = Tokenize(input);
        if (tokens.Count is < 3 or > 8)
        {
            return null;
        }

        var decimalText = decimalSeparator ?? CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator;
        var groupingText = groupingSeparator ?? CultureInfo.InvariantCulture.NumberFormat.NumberGroupSeparator;
        var effectiveCulture = culture ?? CultureInfo.InvariantCulture;

        // Try each candidate keyword from the right: in "5 in to cm" the last one is the verb and
        // the first is a unit.
        for (var position = tokens.Count - 2; position >= 1; position--)
        {
            if (!ConversionWords.Contains(tokens[position]))
            {
                continue;
            }
            var right = tokens.Skip(position + 1).ToList();
            if (right.Count != 1 || !Lexicon.TryGetValue(right[0], out var target))
            {
                continue;
            }
            var left = tokens.Take(position).ToList();
            var source = ParseValue(left, decimalText, groupingText);
            if (source is null || source.Value.Unit.Family != target.Family)
            {
                continue;
            }

            var convertedValue = target.FromBase(source.Value.Unit.ToBase(source.Value.Value));
            if (!double.IsFinite(convertedValue))
            {
                continue;
            }
            var formatted = FeetAndInches(convertedValue, target, effectiveCulture)
                ?? Format(convertedValue, target.Symbol, effectiveCulture);
            return new Result(formatted, convertedValue);
        }
        return null;
    }

    /// <summary>Splits the input into folded tokens, pulling "100km" apart into a number and a
    /// unit so both spellings work.</summary>
    public static List<string> Tokenize(string input)
    {
        var folded = Normalize(input);
        var tokens = new List<string>();
        foreach (var raw in folded.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var first = raw[0];
            if (!(char.IsDigit(first) || first is '-' or '.' or ','))
            {
                tokens.Add(raw);
                continue;
            }
            var digitCount = 0;
            while (digitCount < raw.Length && (char.IsDigit(raw[digitCount]) || raw[digitCount] is '.' or ',' or '-'))
            {
                digitCount++;
            }
            tokens.Add(raw[..digitCount]);
            if (digitCount < raw.Length)
            {
                tokens.Add(raw[digitCount..]);
            }
        }
        return tokens;
    }

    /// <summary>Case-folds, drops diacritics and collapses all Unicode whitespace to single ASCII
    /// spaces. A scoped-down stand-in for <c>CommandBarSearch.normalized</c> (out of scope for this
    /// port), sufficient for tokenizing a conversion phrase.</summary>
    private static string Normalize(string input)
    {
        var decomposed = input.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }
        var folded = builder.ToString().ToLowerInvariant();
        var words = folded.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words);
    }

    private static (double Value, UnitDefinition Unit)? ParseValue(List<string> tokens, string decimalSeparator, string groupingSeparator)
    {
        if (tokens.Count != 2 || !Lexicon.TryGetValue(tokens[1], out var unit))
        {
            return null;
        }
        var value = ParseNumber(tokens[0], decimalSeparator, groupingSeparator);
        return value is null ? null : (value.Value, unit);
    }

    /// <summary>Reads the number with the given separators; simpler than the calculator's reader,
    /// with no "does this look like real grouping" heuristic.</summary>
    private static double? ParseNumber(string token, string decimalSeparator, string groupingSeparator)
    {
        var normalized = token;
        if (decimalSeparator != groupingSeparator)
        {
            normalized = normalized.Replace(groupingSeparator, "");
        }
        normalized = normalized.Replace(decimalSeparator, ".");
        if (normalized.Length == 0 || normalized.Count(c => c == '.') > 1
            || !normalized.All(c => char.IsDigit(c) || c is '.' or '-'))
        {
            return null;
        }
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    /// <summary>Mixed units are useful once there is a whole foot to show. Smaller and negative
    /// values stay in decimal feet so the conversion keeps its meaning.</summary>
    private static string? FeetAndInches(double value, UnitDefinition target, CultureInfo culture)
    {
        if (target.Symbol != FeetSymbol || value < 1)
        {
            return null;
        }

        var wholeFeet = Math.Floor(value);
        var rawInches = (value - wholeFeet) * 12;
        var scale = Math.Pow(10, FractionDigits(rawInches));
        var inches = Math.Round(rawInches * scale, MidpointRounding.AwayFromZero) / scale;
        if (inches >= 12)
        {
            wholeFeet += 1;
            inches = 0;
        }

        var feetText = Format(wholeFeet, FeetSymbol, culture);
        if (inches == 0)
        {
            return feetText;
        }
        var inchesText = Format(inches, InchSymbol, culture);
        return $"{feetText} {inchesText}";
    }

    private static string Format(double value, string symbol, CultureInfo culture) =>
        $"{FormatNumber(value, FractionDigits(value), culture)} {symbol}";

    private static string FormatNumber(double value, int digits, CultureInfo culture)
    {
        var text = value.ToString("N" + digits, culture);
        return digits == 0 ? text : TrimTrailingFractionZeros(text, culture.NumberFormat.NumberDecimalSeparator);
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

    /// <summary>Small numbers keep their decimals, big ones lose the noise.</summary>
    private static int FractionDigits(double value)
    {
        var magnitude = Math.Abs(value);
        if (magnitude == 0)
        {
            return 0;
        }
        if (magnitude < 1)
        {
            return 4;
        }
        return magnitude < 100 ? 2 : 1;
    }

    private static Dictionary<string, UnitDefinition> BuildLexicon()
    {
        var map = new Dictionary<string, UnitDefinition>();
        void Add(IEnumerable<string> names, UnitDefinition unit)
        {
            foreach (var name in names)
            {
                map[name] = unit;
            }
        }
        UnitDefinition Linear(Family family, string symbol, double factor) =>
            new(family, symbol, v => v * factor, v => v / factor);

        // Temperature. Absolute readings, never increments: 20 degC is 68 degF.
        Add(["c", "celsius", "°c", "centigrade"], new UnitDefinition(Family.Temperature, "°C", v => v, v => v));
        Add(["f", "fahrenheit", "°f"], new UnitDefinition(Family.Temperature, "°F", v => (v - 32) * 5 / 9, v => v * 9 / 5 + 32));
        Add(["k", "kelvin"], new UnitDefinition(Family.Temperature, "K", v => v - 273.15, v => v + 273.15));

        // Length, base = meters.
        Add(["mm", "millimeter", "millimeters", "milimetro", "milimetros"], Linear(Family.Length, "mm", 0.001));
        Add(["cm", "centimeter", "centimeters", "centimetro", "centimetros"], Linear(Family.Length, "cm", 0.01));
        Add(["m", "meter", "meters", "metro", "metros", "metre", "metres"], Linear(Family.Length, "m", 1));
        Add(["km", "kilometer", "kilometers", "quilometro", "quilometros"], Linear(Family.Length, "km", 1000));
        Add(["in", "inch", "inches", "polegada", "polegadas", "\""], Linear(Family.Length, InchSymbol, 0.0254));
        Add(["ft", "foot", "feet", "pe", "pes"], Linear(Family.Length, FeetSymbol, 0.3048));
        Add(["yd", "yard", "yards", "jarda", "jardas"], Linear(Family.Length, "yd", 0.9144));
        Add(["mi", "mile", "miles", "milha", "milhas"], Linear(Family.Length, "mi", 1609.344));

        // Mass, base = kilograms.
        Add(["mg", "milligram", "milligrams"], Linear(Family.Mass, "mg", 1e-6));
        Add(["g", "gram", "grams", "grama", "gramas"], Linear(Family.Mass, "g", 0.001));
        Add(["kg", "kilogram", "kilograms", "quilo", "quilos"], Linear(Family.Mass, "kg", 1));
        Add(["t", "ton", "tons", "tonne", "tonelada", "toneladas"], Linear(Family.Mass, "t", 1000));
        Add(["oz", "ounce", "ounces", "onca", "oncas"], Linear(Family.Mass, "oz", 0.028349523125));
        Add(["lb", "lbs", "pound", "pounds", "libra", "libras"], Linear(Family.Mass, "lb", 0.45359237));

        // Data, base = bits. Decimal and binary are separate units on purpose.
        Add(["b", "byte", "bytes"], Linear(Family.Data, "B", 8));
        Add(["kb", "kilobyte", "kilobytes"], Linear(Family.Data, "KB", 8000));
        Add(["mb", "megabyte", "megabytes"], Linear(Family.Data, "MB", 8e6));
        Add(["gb", "gigabyte", "gigabytes"], Linear(Family.Data, "GB", 8e9));
        Add(["tb", "terabyte", "terabytes"], Linear(Family.Data, "TB", 8e12));
        Add(["kib", "kibibyte", "kibibytes"], Linear(Family.Data, "KiB", 8192));
        Add(["mib", "mebibyte", "mebibytes"], Linear(Family.Data, "MiB", 8 * 1024.0 * 1024));
        Add(["gib", "gibibyte", "gibibytes"], Linear(Family.Data, "GiB", 8 * 1024.0 * 1024 * 1024));
        Add(["tib", "tebibyte", "tebibytes"], Linear(Family.Data, "TiB", 8 * 1024.0 * 1024 * 1024 * 1024));
        Add(["bit", "bits"], Linear(Family.Data, "bit", 1));

        // Duration, base = seconds.
        Add(["ms", "millisecond", "milliseconds"], Linear(Family.Duration, "ms", 0.001));
        Add(["s", "sec", "secs", "second", "seconds", "segundo", "segundos"], Linear(Family.Duration, "s", 1));
        Add(["min", "mins", "minute", "minutes", "minuto", "minutos"], Linear(Family.Duration, "min", 60));
        Add(["h", "hr", "hrs", "hour", "hours", "hora", "horas"], Linear(Family.Duration, "h", 3600));

        // Volume (US liquid), base = liters.
        Add(["ml", "milliliter", "milliliters", "mililitro", "mililitros"], Linear(Family.Volume, "ml", 0.001));
        Add(["l", "liter", "liters", "litre", "litres", "litro", "litros"], Linear(Family.Volume, "l", 1));
        Add(["gal", "gallon", "gallons", "galao", "galoes"], Linear(Family.Volume, "gal", 3.785411784));
        Add(["qt", "quart", "quarts"], Linear(Family.Volume, "qt", 0.946352946));
        Add(["floz", "fluidounce", "fluidounces"], Linear(Family.Volume, "fl oz", 0.0295735295625));
        Add(["cup", "cups", "xicara", "xicaras"], Linear(Family.Volume, "cup", 0.2365882365));

        return map;
    }
}
