using Faqra.Core.CommandBar;

namespace Faqra.Core.Tests;

// Tests/MetricsTests.swift assertions for CommandBarSearch.normalized and matchesVerb, per
// digest-commandbar-logic.md sections 1.2-1.3 and 1.6.
public class CommandBarSearchNormalizationTests
{
    [Theory]
    [InlineData("Reunião", "reuniao")]
    [InlineData("BRILHO", "brilho")]
    [InlineData("  Résumé ", "resume")]
    public void Normalized_FoldsCaseAndDiacritics(string input, string expected) =>
        Assert.Equal(expected, CommandBarSearch.Normalized(input));

    [Fact]
    public void Normalized_CollapsesAnyWhitespaceIncludingIdeographicSpace() =>
        Assert.Equal("bd 123", CommandBarSearch.Normalized("bd　123"));

    [Fact]
    public void Normalized_StripsLeadingInvisibleMark() =>
        // A left-to-right mark in front of a title must not stop it from equaling its own name.
        Assert.Equal("whatsapp", CommandBarSearch.Normalized("‎WhatsApp"));

    [Fact]
    public void Normalized_EmptyStaysEmpty() =>
        Assert.Equal("", CommandBarSearch.Normalized("   "));

    // CommandBarSupport.swift:1.6 upstream test table (digest section 1.6, line 68).
    [Theory]
    [InlineData("quit saf", "Quit {0}", true)]
    [InlineData("encerrar", "Encerrar {0}", true)]
    [InlineData("safari", "Quit {0}", false)] // the app name alone must not drag quit rows in
    [InlineData("", "Quit {0}", false)]
    public void MatchesVerb_RequiresTheVerbNotJustTheName(string query, string format, bool expected) =>
        Assert.Equal(expected, CommandBarSearch.MatchesVerb(query, format));

    [Fact]
    public void MatchesVerb_MatchesByContainmentForSpacelessLanguages() =>
        // "終了" in "{0}を終了" (Japanese quit verb) has no space to tokenize the query against.
        Assert.True(CommandBarSearch.MatchesVerb("終了", "{0}を終了"));

    [Fact]
    public void EmojiQuery_StripsLeadingColon()
    {
        Assert.Equal("fire", CommandBarSearch.EmojiQuery(":fire"));
        Assert.Equal("", CommandBarSearch.EmojiQuery(":"));
        Assert.Null(CommandBarSearch.EmojiQuery("fire"));
    }
}
