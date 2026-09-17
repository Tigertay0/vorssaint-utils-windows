// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/CommandBar/CommandBarUnits.swift
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Faqra.Core.CommandBar;

namespace Faqra.Core.Tests.CommandBar;

// Cases ported from Tests/MetricsTests.swift:23233-23298 (upstream), verified directly against
// Sources/Vorssaint/Services/CommandBar/CommandBarUnits.swift. Upstream never pins the localized
// unit word/abbreviation (MeasurementFormatter's word choice is OS/locale-version dependent -
// see the file's own :268-273 comment) - only the numeric portion is asserted for the
// feet-and-inches rows, exactly as upstream's own "unitNumbers" test helper does.
public class CommandBarUnitsTests
{
    private static readonly CultureInfo Brazil = new("pt-BR");

    [Theory]
    [InlineData("100 km to mi", 62.1371, 1e-3)]
    [InlineData("20 c to f", 68, 1e-9)]
    [InlineData("0 c to f", 32, 1e-9)]
    [InlineData("5 gb to mb", 5000, 1e-9)]
    [InlineData("1 gib to mib", 1024, 1e-9)]
    [InlineData("100km to mi", 62.1371, 1e-3)]     // no space between number and unit
    [InlineData("5 in to cm", 12.7, 1e-9)]
    [InlineData("2 h to min", 120, 1e-9)]
    [InlineData("1 kg to lb", 2.20462, 1e-4)]
    [InlineData("1.5 l to ml", 1500, 1e-9)]
    public void Convert_ComputesValue(string input, double expected, double tolerance)
    {
        var result = CommandBarUnits.Convert(input);
        Assert.NotNull(result);
        Assert.True(Math.Abs(expected - result!.Value.Value) <= tolerance,
            $"expected {expected} +/- {tolerance}, got {result.Value.Value}");
    }

    [Fact]
    public void Convert_AcceptanceCaseKilometersToMiles()
    {
        var result = CommandBarUnits.Convert("10 km in mi");
        Assert.NotNull(result);
        Assert.Equal(10000.0 / 1609.344, result!.Value.Value, 9);
        Assert.Equal("6.21 mi", result.Value.Formatted);
    }

    [Theory]
    [InlineData("100 km to kg")]                       // family mismatch: length vs mass
    [InlineData("100 km")]                             // no conversion keyword at all
    [InlineData("to")]
    [InlineData("in")]
    [InlineData("safari to dock")]                     // neither side is a known unit
    [InlineData("5 xyz to cm")]                        // unknown source unit
    [InlineData("minutes to read the article")]        // a sentence, not a conversion
    public void Convert_ReturnsNull(string input) => Assert.Null(CommandBarUnits.Convert(input));

    [Fact]
    public void Convert_BrazilianDecimalSeparator()
    {
        var result = CommandBarUnits.Convert("1,5 m to cm", decimalSeparator: ",", groupingSeparator: ".", culture: Brazil);
        Assert.NotNull(result);
        Assert.Equal(150, result!.Value.Value, 6);
    }

    [Theory]
    [InlineData("180 cm to ft", new[] { "5", "10.87" })]
    [InlineData("1.75 m to ft", new[] { "5", "8.9" })]
    [InlineData("6 ft to ft", new[] { "6" })]
    [InlineData("5.9999 ft to ft", new[] { "6" })]      // inches round up to 12 and carry to the next foot
    [InlineData("2 cm to ft", new[] { "0.0656" })]      // under 1 foot stays plain decimal
    [InlineData("-180 cm to ft", new[] { "-5.91" })]    // negative stays plain decimal too
    [InlineData("180 cm to in", new[] { "70.87" })]     // converting straight to inches is unaffected
    public void Convert_FeetAndInchesNumericParts(string input, string[] expectedNumbers)
    {
        var result = CommandBarUnits.Convert(input);
        Assert.NotNull(result);
        Assert.Equal(expectedNumbers, NumericParts(result!.Value.Formatted));
    }

    [Fact]
    public void Convert_FeetAndInchesFollowsThePersonsDecimalSeparator()
    {
        var result = CommandBarUnits.Convert("180 cm para pes", decimalSeparator: ",", groupingSeparator: ".", culture: Brazil);
        Assert.NotNull(result);
        Assert.Equal(new[] { "5", "10,87" }, NumericParts(result!.Value.Formatted));
    }

    [Fact]
    public void Tokenize_SplitsANumberFromAnAttachedUnit() =>
        Assert.Equal(new[] { "100", "km", "to", "mi" }, CommandBarUnits.Tokenize("100km to mi"));

    /// <summary>Strips everything but digits/./,/- , the same way upstream's own tests compare
    /// only the numeric portion of a MeasurementFormatter string.</summary>
    private static string[] NumericParts(string formatted) =>
        Regex.Matches(formatted, @"-?\d+(?:[.,]\d+)?").Select(m => m.Value).ToArray();
}
