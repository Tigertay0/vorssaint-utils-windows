using System.Collections.Immutable;
using Faqra.Core.Agents;
using Faqra.Core.Localization;

namespace Faqra.Core.Tests.Agents;

public class AgentsTextTests
{
    private static readonly AgentsStrings S = AgentsStrings.EnUS;
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private static AgentSession Session(AgentState state, int subagents = 0, params AgentStep[] steps) =>
        new("s1", "claude", @"C:\code\faqra", state, steps.ToImmutableList(), null, subagents, T0, T0, null);

    [Theory]
    [InlineData(AgentStepKind.Read, "Tray.cs", "Reads Tray.cs")]
    [InlineData(AgentStepKind.Edit, "Tray.cs", "Edits Tray.cs")]
    [InlineData(AgentStepKind.Run, "npm test", "Runs npm test")]
    [InlineData(AgentStepKind.Search, "TrayIcon", "Searches for TrayIcon")]
    [InlineData(AgentStepKind.WebSearch, "velopack", "Searches the web for velopack")]
    [InlineData(AgentStepKind.WebFetch, "docs.velopack.io", "Opens docs.velopack.io")]
    [InlineData(AgentStepKind.Subagent, "Review the diff", "Starts a subagent: Review the diff")]
    [InlineData(AgentStepKind.Subagent, "", "Starts a subagent")]
    [InlineData(AgentStepKind.SubagentDone, "", "A subagent finished")]
    [InlineData(AgentStepKind.Plan, "", "Updates its plan")]
    [InlineData(AgentStepKind.Tool, "mcp__x__y", "Uses mcp__x__y")]
    [InlineData(AgentStepKind.Failed, "Bash", "Bash failed")]
    [InlineData(AgentStepKind.Prompt, "fix the tray", "Asked: fix the tray")]
    public void StepsReadAsShortSentences(AgentStepKind kind, string detail, string text) =>
        Assert.Equal(text, AgentsText.Step(new AgentStep(T0, kind, detail), S));

    [Fact]
    public void TheStatusShowsTheLatestStepWhileWorking()
    {
        Assert.Equal("Runs npm test", AgentsText.Status(Session(AgentState.Working, 0, new AgentStep(T0, AgentStepKind.Run, "npm test")), S));
        Assert.Equal("Needs your OK", AgentsText.Status(Session(AgentState.Approval, 0, new AgentStep(T0, AgentStepKind.Run, "rm x")), S));
        Assert.Equal("Working", AgentsText.Status(Session(AgentState.Working), S));
        Assert.Equal("Subagents running: 2", AgentsText.Status(Session(AgentState.Background, 2), S));
    }

    [Theory]
    [InlineData(20, "now")]
    [InlineData(150, "2 min")]
    [InlineData(7300, "2 h")]
    public void ElapsedIsCoarse(int seconds, string text) =>
        Assert.Equal(text, AgentsText.Elapsed(TimeSpan.FromSeconds(seconds), S));

    [Fact]
    public void NoStringUsesADash() =>
        Assert.All(typeof(AgentsStrings).GetProperties().Where(p => p.PropertyType == typeof(string)),
            p => Assert.DoesNotMatch("[\u2013\u2014]", (string)p.GetValue(S)!));

    [Theory]
    [InlineData("Bash", "Wants to run a command")]
    [InlineData("PowerShell", "Wants to run a command")]
    [InlineData("Edit", "Wants to edit a file")]
    [InlineData("Write", "Wants to edit a file")]
    [InlineData("WebFetch", "Wants to open a web page")]
    [InlineData("mcp__github__create_issue", "Wants to use mcp__github__create_issue")]
    public void ARequestSaysWhatClaudeWants(string tool, string title)
    {
        var request = new AgentRequest("r", "s", AgentRequestKind.Approval, tool, "", [], [], false, DateTimeOffset.Now);
        Assert.Equal(title, AgentsText.RequestTitle(request, Faqra.Core.Localization.AgentsStrings.EnUS));
    }

    [Fact]
    public void AQuestionSaysSo()
    {
        var request = new AgentRequest("r", "s", AgentRequestKind.Question, "AskUserQuestion", "", [], [], false, DateTimeOffset.Now);
        Assert.Equal("Has a question", AgentsText.RequestTitle(request, Faqra.Core.Localization.AgentsStrings.EnUS));
    }
}
