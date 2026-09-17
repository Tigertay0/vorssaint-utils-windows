// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/CommandBar/CommandBarMath.swift
using System.Globalization;
using Faqra.Core.CommandBar;

namespace Faqra.Core.Tests.CommandBar;

// Cases ported from Tests/MetricsTests.swift:22742-22803 (upstream), verified directly against
// Sources/Vorssaint/Services/CommandBar/CommandBarMath.swift where the digest and the tests disagreed.
public class CommandBarMathTests
{
    private static readonly CultureInfo Brazil = new("pt-BR");

    [Theory]
    [InlineData("2+2", 4)]
    [InlineData("10 * 4.5", 45)]
    [InlineData("(2+3)*4", 20)]
    [InlineData("2+3*4", 14)]
    [InlineData("10/4", 2.5)]
    [InlineData("2^3^2", 512)]              // right-associative power: 2^(3^2), not (2^3)^2
    [InlineData("2^10", 1024)]              // acceptance case
    [InlineData("-5+2", -3)]
    [InlineData("--5+1", 6)]
    [InlineData("1920/2", 960)]
    [InlineData("2 x 3", 6)]                // "x" after a number reads as multiplication
    [InlineData("10 ÷ 2", 5)]          // division sign
    [InlineData("2 × 3", 6)]           // multiplication sign
    [InlineData("2(3+4)", 14)]              // implicit multiplication against a parenthesis
    [InlineData("(1+2)(3+4)", 21)]
    [InlineData("-2^2", -4)]                // sign applies to the whole power: -(2^2)
    [InlineData("100-50", 50)]              // a 2-part hyphen shape is a subtraction, not a date
    [InlineData("7+3=", 10)]                // trailing "=" is dropped at tokenize time
    [InlineData("480+15%", 552)]            // percent after + is relative to the left operand
    [InlineData("480-15%", 408)]
    [InlineData("20% of 480", 96)]
    [InlineData("20% de 480", 96)]          // "de" is an of-word too
    [InlineData("200*10%", 20)]             // percent not right after +/- is just its own 0-1 value
    [InlineData("2 * -3", -6)]
    public void Evaluate_ComputesValue(string input, double expected)
    {
        var result = CommandBarMath.Evaluate(input);
        Assert.NotNull(result);
        Assert.Equal(expected, result!.Value.Value, 9);
    }

    [Fact]
    public void Evaluate_KillsFloatingPointNoise() =>
        Assert.Equal(0.3, CommandBarMath.Evaluate("0.1+0.2")!.Value.Value, 9);

    [Theory]
    [InlineData("2+2", "4")]
    [InlineData("10/4", "2.5")]
    [InlineData("1,000+1", "1,001")]
    [InlineData("7+3=", "10")]
    [InlineData("480+15%", "552")]
    public void Evaluate_Formats(string input, string expected) =>
        Assert.Equal(expected, CommandBarMath.Evaluate(input)!.Value.Formatted);

    [Fact]
    public void Evaluate_SquareRootStaysDecimal() =>
        Assert.StartsWith("1.41421", CommandBarMath.Evaluate("2^0.5")!.Value.Formatted);

    [Fact]
    public void Evaluate_TinyValueUsesScientificNotation() =>
        Assert.Equal("1e-9", CommandBarMath.Evaluate("1/1000000000")!.Value.Formatted);

    [Theory]
    [InlineData("2^100")]
    [InlineData("9999999999999999*99")]
    public void Evaluate_HugeValueUsesScientificNotation(string input) =>
        Assert.Contains("e", CommandBarMath.Evaluate(input)!.Value.Formatted);

    [Theory]
    [InlineData("2026-07-27")]         // date-shaped: 3 hyphen groups
    [InlineData("27/07/2026")]         // date-shaped: 3 slash groups
    [InlineData("10:30")]              // any colon-separated digit groups read as a time
    [InlineData("SDL_VIDEODRIVER=")]   // '_' is not a recognized character
    [InlineData("x=5")]                // "x" not preceded by a number and not an of-word
    [InlineData("50%")]                // a lone percent is not a question
    [InlineData("5")]                  // a lone number is a search, not a question
    [InlineData("hello")]
    [InlineData("1password")]
    [InlineData("volume 20")]
    [InlineData("brilho 40")]
    [InlineData("10/0")]               // division by zero has no answer
    [InlineData("2+")]
    [InlineData("(2+3")]
    [InlineData("2++")]
    [InlineData("e-mail")]
    public void Evaluate_ReturnsNull(string input) => Assert.Null(CommandBarMath.Evaluate(input));

    [Fact]
    public void Evaluate_RejectsDeepParenthesisNesting()
    {
        var input = new string('(', 60) + "1" + new string(')', 60);
        Assert.Null(CommandBarMath.Evaluate(input));
    }

    [Fact]
    public void Evaluate_BrazilianDecimalAndGroupingSeparators()
    {
        var result = CommandBarMath.Evaluate("1.234,5 + 1", decimalSeparator: ",", groupingSeparator: ".", culture: Brazil);
        Assert.NotNull(result);
        Assert.Equal("1.235,5", result!.Value.Formatted);
    }

    [Fact]
    public void Evaluate_BrazilianDecimalSeparatorMultiplication()
    {
        var result = CommandBarMath.Evaluate("1,5*2", decimalSeparator: ",", groupingSeparator: ".", culture: Brazil);
        Assert.NotNull(result);
        Assert.Equal(3, result!.Value.Value, 9);
    }

    // Upstream's evaluate(decimalSeparator:groupingSeparator:locale:) defaults each parameter
    // independently, so overriding only the tokenizer separators still formats with the default
    // culture. Ported verbatim: Tests/MetricsTests.swift's "1.500+1" row uses pt-BR-shaped input
    // separators but expects the en_US-formatted "1,501" answer.
    [Fact]
    public void Evaluate_SeparatorOverrideDoesNotChangeDefaultFormattingCulture()
    {
        var result = CommandBarMath.Evaluate("1.500+1", decimalSeparator: ",", groupingSeparator: ".");
        Assert.NotNull(result);
        Assert.Equal("1,501", result!.Value.Formatted);
    }

    [Theory]
    [InlineData("2026-07-27", true)]
    [InlineData("27/07/2026", true)]
    [InlineData("10:30", true)]
    [InlineData("100-50", false)]  // only a 3-part hyphen/slash shape counts; 2 parts is a subtraction
    [InlineData("2+2", false)]
    public void LooksLikeDateOrTime_MatchesUpstreamShapes(string input, bool expected) =>
        Assert.Equal(expected, CommandBarMath.LooksLikeDateOrTime(input));
}
