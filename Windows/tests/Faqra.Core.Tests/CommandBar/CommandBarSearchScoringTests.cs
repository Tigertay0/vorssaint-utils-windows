using Faqra.Core.CommandBar;

namespace Faqra.Core.Tests;

// Tests/MetricsTests.swift:24847-24905, 22910-22917, 25247-25251 upstream match/score table,
// per digest-commandbar-logic.md section 1.7.
public class CommandBarSearchScoringTests
{
    [Theory]
    [InlineData("Reunião com João", "", "reuniao joao", true)] // accents/case ignored
    [InlineData("Brilho da tela", "", "brilho", true)]
    [InlineData("Brilho da tela", "", "Brilho", true)] // case-insensitive
    [InlineData("Brilho da tela", "", "brlho", true)] // dropped letter (subsequence)
    [InlineData("Brilho da tela", "", "birlho", true)] // transposition
    [InlineData("Zen", "", "zne", true)] // 3-letter transposition
    [InlineData("Brilho da tela", "", "volume", false)]
    [InlineData("Zen", "", "zip", false)] // short substitution not close enough
    [InlineData("Capturar tela", "screenshot print", "print", true)] // keyword match
    [InlineData("Silenciar microfone", "", "silenciar micro", true)] // tokens match as prefixes, any order
    [InlineData("Silenciar microfone", "", "silenciar tela", false)] // every token must land
    public void Matches_MatchesUpstreamTable(string title, string keywords, string query, bool expected) =>
        Assert.Equal(expected, CommandBarSearch.Matches(title, keywords, query));

    [Fact]
    public void Score_KeywordPrefixNeverBeatsKeywordExactPlusMaxSessionMemoryBoost()
    {
        // 22910-22917: keywordPrefixScore + CommandBarQueryMemory.maximumBoost < keywordExactScore -
        // a session-memory tie-breaker (cap 3) can never overtake a better keyword tier.
        var prefixScore = CommandBarSearch.ScoreRawText("Other", "primary", "pri");
        var exactScore = CommandBarSearch.ScoreRawText("Other", "pri", "pri");

        Assert.NotNull(prefixScore);
        Assert.NotNull(exactScore);
        Assert.True(prefixScore.Value + CommandBarQueryMemory.MaximumBoost < exactScore.Value);
    }

    [Fact]
    public void Score_MultiWordQueryAgainstOneWordTitleAndKeywordFails() =>
        // Every token must match something; "vorssaint"/"utils" match neither "gh" nor "Link".
        Assert.Null(CommandBarSearch.ScoreRawText("gh", "Link", "gh vorssaint utils"));

    [Fact]
    public void Score_WholeQueryBonusesAreExact()
    {
        // CommandBarSupport.swift:292-300 - exact/prefix/contains/keyword-contains bonuses.
        Assert.Equal(1200 + 140, CommandBarSearch.ScoreRawText("chrome", "", "chrome"));
        Assert.Equal(900 + 80, CommandBarSearch.ScoreRawText("chrome remote desktop", "", "chr"));
        Assert.Equal(700 + 80, CommandBarSearch.ScoreRawText("google chrome", "", "chr"));
    }

    [Theory]
    [InlineData("brilho 40", "brilho", 40)]
    [InlineData("volume 20%", "volume", 20)]
    [InlineData("brilho", "brilho", null)]
    [InlineData("40", "40", null)] // a bare number alone is a search, not a command
    [InlineData("manter acordado 30", "manter acordado", 30)]
    public void SplitTrailingNumber_MatchesUpstreamTable(string input, string expectedText, int? expectedNumber)
    {
        var split = CommandBarSearch.SplitTrailingNumber(input);
        Assert.Equal(expectedText, split.Text);
        Assert.Equal(expectedNumber, split.Number);
    }

    [Theory]
    [InlineData("40", 40)]
    [InlineData("140", 100)] // clamped
    [InlineData("35%", 35)]
    [InlineData("abc", null)]
    [InlineData("", null)]
    public void ArgumentValue_MatchesUpstreamTable(string input, int? expected) =>
        Assert.Equal(expected, CommandBarSearch.ArgumentValue(input, 0, 100));

    [Fact]
    public void FirstOccurrences_KeepsFirstOfEachId() =>
        Assert.Equal(new[] { 0, 1, 3 }, CommandBarSearch.FirstOccurrences(new[] { "a", "b", "a", "c", "b" }));
}
