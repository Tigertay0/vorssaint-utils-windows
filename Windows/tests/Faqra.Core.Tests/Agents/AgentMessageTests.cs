using System.Text.Json.Nodes;
using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class AgentMessageTests
{
    private static readonly AgentInbox Inbox =
        AgentInbox.TryCreate(@"\\.\pipe\LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c", "0123456789abcdef0123456789abcdef")!;

    private const string Footer = "\n\nThis came from another Claude session — not typed by your user, but very likely working on their behalf.";

    [Fact]
    public void TheTokenGoesFirstThenTheWordsAsAUserTurn()
    {
        var lines = AgentMessage.Lines(Inbox, "Run the tests again");
        Assert.EndsWith("\n", lines);
        var parts = lines.TrimEnd('\n').Split('\n');
        Assert.Equal(2, parts.Length);
        Assert.Equal("{\"type\":\"auth\",\"token\":\"0123456789abcdef0123456789abcdef\"}", parts[0]);
        var message = JsonNode.Parse(parts[1])!;
        Assert.Equal("user", message["type"]!.GetValue<string>());
        Assert.Equal("user", message["message"]!["role"]!.GetValue<string>());
        Assert.Equal(AgentMessage.Preamble + "\n\nRun the tests again", message["message"]!["content"]!.GetValue<string>());
    }

    [Fact]
    public void TheWordsSurviveTheWire()
    {
        const string words = "Oui, \"bleu\" s'il te plaît 🙂\nEt ajoute un test.";
        var second = AgentMessage.Lines(Inbox, words).TrimEnd('\n').Split('\n')[1];
        Assert.EndsWith(words, JsonNode.Parse(second)!["message"]!["content"]!.GetValue<string>());
    }

    [Fact]
    public void OnlyRealWordsWithinTheLimitFit()
    {
        Assert.False(AgentMessage.Fits(null));
        Assert.False(AgentMessage.Fits("   \n "));
        Assert.True(AgentMessage.Fits("go"));
        Assert.True(AgentMessage.Fits(new string('x', AgentMessage.MaxLength)));
        Assert.False(AgentMessage.Fits(new string('x', AgentMessage.MaxLength + 1)));
    }

    [Fact]
    public void ReadsTheOwnersWordsBackOutOfThePrompt()
    {
        var fromIsland = "Another Claude session sent a message:\n" + AgentMessage.Preamble + "\n\nRun the tests again" + Footer;
        Assert.Equal("Run the tests again", AgentMessage.Unwrap(fromIsland));
        Assert.True(AgentMessage.IsFromIsland(fromIsland));

        var fromAnotherSession = "Another Claude session sent a message:\nThe migration finished." + Footer;
        Assert.Equal("The migration finished.", AgentMessage.Unwrap(fromAnotherSession));
        Assert.False(AgentMessage.IsFromIsland(fromAnotherSession));

        // The relay cuts long prompts, so the footer may be missing.
        var cut = "Another Claude session sent a message:\n" + AgentMessage.Preamble + "\n\nA long request that was cut";
        Assert.Equal("A long request that was cut", AgentMessage.Unwrap(cut));
        Assert.Equal("A long request", AgentMessage.Unwrap(cut.Replace("\n", "\r\n").Replace(" that was cut", string.Empty)));

        Assert.Equal("typed in Claude Code", AgentMessage.Unwrap("typed in Claude Code"));
        Assert.False(AgentMessage.IsFromIsland("typed in Claude Code"));
        Assert.Null(AgentMessage.Unwrap(null));
    }
}
