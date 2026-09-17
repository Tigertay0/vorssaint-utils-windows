using Faqra.Core.CommandBar;

namespace Faqra.Core.Tests;

// Tests/MetricsTests.swift:22883-22933, 26525-26540 upstream table, per
// digest-commandbar-logic.md section 4.
public class CommandBarQueryMemoryTests
{
    [Fact]
    public void Prefixes_BuildsEveryLeadingSubstring() =>
        Assert.Equal(new[] { "w", "wh", "wha" }, CommandBarQueryMemory.Prefixes("wha"));

    [Fact]
    public void Prefixes_TrimsAndFoldsEachPrefix() =>
        // Folded per-prefix, so "é" -> "e" applies before trimming shortens the run, not after.
        Assert.Equal(
            new[] { "r", "re", "res", "resu", "resum", "resume" },
            CommandBarQueryMemory.Prefixes("  Résumé "));

    [Fact]
    public void Prefixes_WhitespaceOnlyIsEmpty() =>
        Assert.Empty(CommandBarQueryMemory.Prefixes("   "));

    [Fact]
    public void Prefixes_CapsAtLongestPrefix() =>
        Assert.Equal(CommandBarQueryMemory.LongestPrefix, CommandBarQueryMemory.Prefixes(new string('a', 40)).Count);

    [Fact]
    public void Boost_FreshMemoryIsAlwaysZero() =>
        Assert.Equal(0, new CommandBarQueryMemory().Boost("anything", "id"));

    [Fact]
    public void Boost_GrowsWithRepeatedChoicesAndCapsAtMaximumBoost()
    {
        var memory = new CommandBarQueryMemory();
        memory.Record("primary", "app.primary", step: 1);

        // "pri" is a recorded prefix of "primary".
        Assert.True(memory.Boost("pri", "app.primary") > 0);

        for (var step = 2; step <= 4; step++)
        {
            memory.Record("primary", "app.primary", step);
        }

        Assert.Equal(CommandBarQueryMemory.MaximumBoost, memory.Boost("primary", "app.primary"));
    }

    [Fact]
    public void Boost_OnlyExactNormalizedStringIsRead()
    {
        var memory = new CommandBarQueryMemory();
        memory.Record("primary", "app.primary", step: 1);

        // "primary extra" was never itself recorded - boost never fans out on the read side.
        Assert.Equal(0, memory.Boost("primary extra", "app.primary"));
    }

    [Fact]
    public void Boost_NormalizedOverloadAgreesWithTheFoldingOverload()
    {
        var memory = new CommandBarQueryMemory();
        memory.Record("PRÍ", "app.primary", step: 1);

        Assert.Equal(
            memory.Boost("PRÍ", "app.primary"),
            memory.BoostNormalized(CommandBarSearch.Normalized("PRÍ"), "app.primary"));
    }

    [Fact]
    public void Record_CrowdingEvictsTheLeastChosenRowPastIdsPerQuery()
    {
        var memory = new CommandBarQueryMemory();
        // row.0 is recorded once; the other four are recorded four times each against the same
        // one-token query, so row.0 is the least-chosen once the 4-id cap evicts one.
        memory.Record("x", "row.0", step: 1);
        for (var step = 2; step <= 5; step++)
        {
            memory.Record("x", "row.1", step);
            memory.Record("x", "row.2", step);
            memory.Record("x", "row.3", step);
            memory.Record("x", "row.4", step);
        }

        Assert.Equal(0, memory.Boost("x", "row.0"));
        Assert.True(memory.Boost("x", "row.1") > 0);
    }

    [Fact]
    public void Forget_RemovesTheRowFromEveryPrefix()
    {
        var memory = new CommandBarQueryMemory();
        memory.Record("primary", "app.primary", step: 1);

        memory.Forget("app.primary");

        Assert.Equal(0, memory.Boost("primary", "app.primary"));
        Assert.True(memory.IsEmpty);
    }

    [Fact]
    public void Clear_WipesEverything()
    {
        var memory = new CommandBarQueryMemory();
        memory.Record("primary", "app.primary", step: 1);

        memory.Clear();

        Assert.True(memory.IsEmpty);
    }
}
