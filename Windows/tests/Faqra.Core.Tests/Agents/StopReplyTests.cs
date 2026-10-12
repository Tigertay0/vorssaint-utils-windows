using System.Text.Json.Nodes;
using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class StopReplyTests
{
    [Fact]
    public void AReplyGoesBackAsStopContextWithThePreamble()
    {
        var printed = StopReply.Stdout(AgentDecision.Reply("Use blue, not red"))!;
        var output = JsonNode.Parse(printed)!["hookSpecificOutput"]!;
        Assert.Equal("Stop", output["hookEventName"]!.GetValue<string>());
        Assert.Equal(StopReply.Preamble + "\n\nUse blue, not red", output["additionalContext"]!.GetValue<string>());
        Assert.DoesNotContain('\n', printed); // one line for Claude Code to read
    }

    [Fact]
    public void TheReplyKeepsItsLinesQuotesAndAccents()
    {
        const string words = "Oui, en \"bleu\" s'il te plaît 🙂\nEt ajoute un test.";
        var said = JsonNode.Parse(StopReply.Stdout(AgentDecision.Reply(words))!)!["hookSpecificOutput"]!["additionalContext"]!.GetValue<string>();
        Assert.EndsWith(words, said);
    }

    [Fact]
    public void NothingButANonBlankReplyPrints()
    {
        Assert.Null(StopReply.Stdout(null));
        Assert.Null(StopReply.Stdout(AgentDecision.Allow));
        Assert.Null(StopReply.Stdout(AgentDecision.Deny("no")));
        Assert.Null(StopReply.Stdout(AgentDecision.Reply("   ")));
        Assert.Null(StopReply.Stdout(AgentDecision.Reply(new string('x', StopReply.MaxReplyLength + 1))));
        Assert.NotNull(StopReply.Stdout(AgentDecision.Reply(new string('x', StopReply.MaxReplyLength))));
    }

    [Fact]
    public void TheHoldLineIsTheDocumentedOne() => Assert.Equal("{\"hold\":true}", StopReply.HoldLine);
}
