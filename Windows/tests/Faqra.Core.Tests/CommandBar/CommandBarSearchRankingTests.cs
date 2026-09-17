using Faqra.Core.CommandBar;

namespace Faqra.Core.Tests;

// Tests/MetricsTests.swift:24907-25011 upstream ranking table, per digest-commandbar-logic.md
// section 1.8, plus the CommandBarPreferences.rankBias assertions the same section borrows from
// CommandBarPreferences.swift:130-142 (read directly, since it is cited but not itself digested).
public class CommandBarSearchRankingTests
{
    private static CommandBarCandidate Candidate(int index, string title, string keywords = "", int priority = 0, int boost = 0) =>
        CommandBarCandidate.FromRawText(index, title, keywords, priority, boost);

    [Fact]
    public void RankedIndexes_EqualTierAndScoreKeepsCatalogOrder()
    {
        var candidates = new[]
        {
            Candidate(0, "Ajustar tela"),
            Candidate(1, "Limpar tela"),
            Candidate(2, "Girar tela"),
            Candidate(3, "Manter acordado"), // does not contain "tela" - excluded
        };

        Assert.Equal(new[] { 0, 1, 2 }, CommandBarSearch.RankedIndexes(candidates, "tela"));
    }

    [Fact]
    public void RankedIndexes_ExactTitlePrefersTier5()
    {
        var candidates = new[]
        {
            Candidate(0, "Capturar tela"),
            Candidate(1, "Capturar tela inteira"),
        };

        Assert.Equal(0, CommandBarSearch.RankedIndexes(candidates, "capturar tela")[0]);
    }

    [Fact]
    public void RankedIndexes_BoostFlipsOrderWithinTheSameTier()
    {
        var candidates = new[]
        {
            Candidate(0, "Limpar tela"),
            Candidate(1, "Ajustar tela", boost: 300),
        };

        Assert.Equal(new[] { 1, 0 }, CommandBarSearch.RankedIndexes(candidates, "tela"));
    }

    [Fact]
    public void RankedIndexes_BoostNeverResurrectsANonMatch()
    {
        var candidates = new[]
        {
            Candidate(0, "Capturar tela"),
            Candidate(1, "Ajustar tela", boost: 300),
        };

        // Only index 0 contains "capturar" at all; index 1's boost cannot make it match.
        Assert.Equal(new[] { 0 }, CommandBarSearch.RankedIndexes(candidates, "capturar"));
    }

    [Fact]
    public void RankedIndexes_WhitespaceOnlyQueryMatchesNothing()
    {
        var candidates = new[] { Candidate(0, "Anything") };
        Assert.Empty(CommandBarSearch.RankedIndexes(candidates, " "));
    }

    [Fact]
    public void RankedIndexes_TypoRescueRanksBelowLiteralTokenMatch()
    {
        var candidates = new[]
        {
            Candidate(0, "Zebra"),
            Candidate(1, "Zen"), // transposition-rescued
            Candidate(2, "Zne Tools"), // literal token match
        };

        Assert.Equal(new[] { 2, 1 }, CommandBarSearch.RankedIndexes(candidates, "zne"));
    }

    [Fact]
    public void RankedIndexes_InvisibleMarkStillWinsAnExactTitleMatch()
    {
        var candidates = new[]
        {
            Candidate(0, "WhatsApp Business (and 1 more tab)", "safari history"),
            Candidate(1, "‎WhatsApp"),
        };

        Assert.Equal(new[] { 1, 0 }, CommandBarSearch.RankedIndexes(candidates, "whatsapp"));
    }

    [Fact]
    public void RankedIndexes_PriorityBeatsBetterTextTierQuality()
    {
        // An explicit alias or learned query choice (both plumbed through CommandBarCandidate's
        // priority, out of Stage 1's scope) outranks a strictly better-tiered ordinary match.
        var candidates = new[]
        {
            Candidate(0, "Passwords"),
            Candidate(1, "Secure Pass", priority: 1),
        };

        Assert.Equal(new[] { 1, 0 }, CommandBarSearch.RankedIndexes(candidates, "pass"));
    }

    [Fact]
    public void RankedIndexes_ExplicitPriorityBeatsTheHabitStoreHardCap()
    {
        // 1100 (an explicit alias) beats 720 (CommandBarQueryHabits' hard cap - out of Stage 1's
        // scope, but the priority channel it would use must compare directly like this).
        var candidates = new[]
        {
            Candidate(0, "Learned Favorite", priority: 720),
            Candidate(1, "Aliased Favorite", priority: 1100),
        };

        Assert.Equal(new[] { 1, 0 }, CommandBarSearch.RankedIndexes(candidates, "favorite"));
    }

    [Theory]
    [InlineData("Screen brightness", "bright", 7, 6)]
    [InlineData("Reunião com João", "reuniao", 0, 7)]
    [InlineData("Empty the Trash", "trash", 10, 5)]
    public void HighlightOffsets_MatchesUpstreamRanges(string title, string query, int start, int length)
    {
        var expected = Enumerable.Range(start, length).ToHashSet();
        Assert.Equal(expected, CommandBarSearch.HighlightOffsets(title, query));
    }

    [Fact]
    public void HighlightOffsets_MultiTokenHighlightsEachSpan()
    {
        var expected = new HashSet<int> { 0, 1, 2, 3, 4, 5, 10, 11, 12, 13 };
        Assert.Equal(expected, CommandBarSearch.HighlightOffsets("Brilho da tela", "brilho tela"));
    }

    [Fact]
    public void HighlightOffsets_TypoRescueIsNeverHighlighted() =>
        Assert.Empty(CommandBarSearch.HighlightOffsets("Settings", "brlho"));

    [Fact]
    public void HighlightOffsets_EmptyQueryHighlightsNothing() =>
        Assert.Empty(CommandBarSearch.HighlightOffsets("Anything", ""));

    [Theory]
    [InlineData(CommandBarSource.Menus, -80)]
    [InlineData(CommandBarSource.Files, -40)]
    [InlineData(CommandBarSource.SettingsPages, -40)]
    [InlineData(CommandBarSource.Apps, 80)]
    [InlineData(CommandBarSource.Actions, 0)]
    [InlineData(CommandBarSource.Windows, 0)]
    public void RankBias_MatchesUpstreamTable(CommandBarSource source, int expected) =>
        Assert.Equal(expected, source.RankBias());

    [Fact]
    public void RankBias_AppsOutranksActionsAndMenusSitsBelowEverything()
    {
        Assert.True(CommandBarSource.Apps.RankBias() > CommandBarSource.Actions.RankBias());
        Assert.True(CommandBarSource.Menus.RankBias() < 0);
        Assert.Equal(0, CommandBarSource.Actions.RankBias());
    }
}
