using Faqra.Core.CommandBar;

namespace Faqra.Core.Tests;

// Tests/MetricsTests.swift:24894-24905 upstream table, per digest-commandbar-logic.md section 1.7.
public class CommandBarFuzzyMatchTests
{
    [Fact]
    public void IsSubsequence_DroppedLetterMatches() =>
        Assert.True(CommandBarFuzzyMatch.IsSubsequence("brlho", "brilho"));

    [Fact]
    public void IsSubsequence_NeedleLongerThanWordFails() =>
        Assert.False(CommandBarFuzzyMatch.IsSubsequence("brilhoo", "brilho"));

    [Theory]
    [InlineData("birlho", "brilho", true)] // transposition
    [InlineData("brilo", "brilho", true)] // deletion
    [InlineData("brilyo", "brilho", true)] // substitution
    [InlineData("brolyo", "brilho", false)] // two edits
    public void WithinOneEdit_MatchesUpstreamTable(string a, string b, bool expected) =>
        Assert.Equal(expected, CommandBarFuzzyMatch.WithinOneEdit(a, b));

    [Theory]
    [InlineData("zne", "zen", true)]
    [InlineData("zne", "zone", false)] // different length
    [InlineData("zip", "zen", false)]
    public void IsAdjacentTransposition_MatchesUpstreamTable(string a, string b, bool expected) =>
        Assert.Equal(expected, CommandBarFuzzyMatch.IsAdjacentTransposition(a, b));
}
