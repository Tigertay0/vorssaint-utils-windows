using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class MarkdownLiteTests
{
    private static string Joined(IReadOnlyList<MdInline> inlines) => string.Concat(inlines.Select(i => i.Text));

    [Fact]
    public void ReadsTheBlocksClaudeWrites()
    {
        var blocks = MarkdownLite.Parse(
            "# Fixed the tray\n" +
            "The crash came from a display wake\nwhile the icon was redrawn.\n" +
            "\n" +
            "## Changes\n" +
            "- retry the icon\n" +
            "  - after 2 s\n" +
            "3. run the tests\n" +
            "> nothing else changed\n" +
            "---\n" +
            "```csharp\n" +
            "    var x = 1;\n" +
            "```");

        Assert.Equal(
            [MdBlockKind.Heading, MdBlockKind.Paragraph, MdBlockKind.Heading, MdBlockKind.Bullet, MdBlockKind.Bullet,
             MdBlockKind.Numbered, MdBlockKind.Quote, MdBlockKind.Rule, MdBlockKind.Code],
            blocks.Select(b => b.Kind));
        Assert.Equal(1, blocks[0].Level);
        Assert.Equal("Fixed the tray", Joined(blocks[0].Inlines));
        Assert.Equal("The crash came from a display wake while the icon was redrawn.", Joined(blocks[1].Inlines));
        Assert.Equal(2, blocks[2].Level);
        Assert.Equal((0, "retry the icon"), (blocks[3].Level, Joined(blocks[3].Inlines)));
        Assert.Equal((1, "after 2 s"), (blocks[4].Level, Joined(blocks[4].Inlines)));
        Assert.Equal(("3.", "run the tests"), (blocks[5].Marker, Joined(blocks[5].Inlines)));
        Assert.Equal("nothing else changed", Joined(blocks[6].Inlines));
        Assert.Equal("    var x = 1;", blocks[8].Code);
    }

    [Fact]
    public void AnUnclosedCodeBlockRunsToTheEnd()
    {
        var blocks = MarkdownLite.Parse("Look:\n```\nline one\nline two");
        Assert.Equal(MdBlockKind.Code, blocks[^1].Kind);
        Assert.Equal("line one\nline two", blocks[^1].Code);
    }

    [Fact]
    public void BlankLinesSeparateParagraphs()
    {
        var blocks = MarkdownLite.Parse("One.\r\n\r\nTwo.");
        Assert.Equal(["One.", "Two."], blocks.Select(b => Joined(b.Inlines)));
    }

    [Fact]
    public void StylesTheRunsInsideALine()
    {
        var runs = MarkdownLite.Inlines("Run **all** the *unit* tests with `dotnet test`, see [the docs](https://example.com/a).");
        Assert.Equal(
            [("Run ", MdStyle.Plain), ("all", MdStyle.Bold), (" the ", MdStyle.Plain), ("unit", MdStyle.Italic), (" tests with ", MdStyle.Plain),
             ("dotnet test", MdStyle.Code), (", see ", MdStyle.Plain), ("the docs", MdStyle.Link), (".", MdStyle.Plain)],
            runs.Select(r => (r.Text, r.Style)));
        Assert.Equal("https://example.com/a", runs.Single(r => r.Style == MdStyle.Link).Url);
    }

    [Theory]
    [InlineData("2 * 3 = 6")]
    [InlineData("snake_case_name stays")]
    [InlineData("an unclosed **bold")]
    [InlineData("a lone ` tick")]
    [InlineData("[not a link]")]
    public void MarksWithoutAPartnerStayAsText(string text)
    {
        var runs = MarkdownLite.Inlines(text);
        Assert.Equal(text, Joined(runs));
        Assert.All(runs, r => Assert.Equal(MdStyle.Plain, r.Style));
    }

    [Fact]
    public void UnderscoresAndTripleStarsWork()
    {
        Assert.Equal([("both", MdStyle.Bold)], MarkdownLite.Inlines("***both***").Select(r => (r.Text, r.Style)));
        Assert.Equal([("strong", MdStyle.Bold)], MarkdownLite.Inlines("__strong__").Select(r => (r.Text, r.Style)));
        Assert.Equal([("soft", MdStyle.Italic)], MarkdownLite.Inlines("_soft_").Select(r => (r.Text, r.Style)));
    }

    [Fact]
    public void PlainTextDropsTheMarks()
    {
        Assert.Equal("Done\nThe tests pass.\n• one\n2. two\nx = 1",
            MarkdownLite.PlainText("# Done\n\nThe **tests** pass.\n\n- one\n2. two\n\n```\nx = 1\n```"));
        Assert.Equal(string.Empty, MarkdownLite.PlainText(null));
    }

    [Fact]
    public void NothingIsNoBlocks()
    {
        Assert.Empty(MarkdownLite.Parse(null));
        Assert.Empty(MarkdownLite.Parse(""));
        Assert.Empty(MarkdownLite.Parse("\n\n  \n"));
    }
}
