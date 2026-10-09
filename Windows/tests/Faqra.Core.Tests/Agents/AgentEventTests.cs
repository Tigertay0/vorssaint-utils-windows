using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class AgentEventTests
{
    [Fact]
    public void ReadsTheFieldsTheBoardUses()
    {
        var e = AgentEvent.TryParse("""
            {"hook_event_name":"Stop","session_id":"s1","cwd":"C:\\code\\faqra","faqra_agent":"claude",
             "last_assistant_message":"All done.","term_program":"vscode","claude_entrypoint":"claude-vscode"}
            """.ReplaceLineEndings(""))!;
        Assert.Equal("Stop", e.Event);
        Assert.Equal("s1", e.SessionId);
        Assert.Equal(@"C:\code\faqra", e.Cwd);
        Assert.Equal("claude", e.Agent);
        Assert.Equal("All done.", e.LastAssistantMessage);
        Assert.Equal("vscode", e.TermProgram);
        Assert.Equal("claude-vscode", e.Entrypoint);
    }

    [Fact]
    public void KeepsTheToolInput()
    {
        var e = AgentEvent.TryParse("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s1\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}")!;
        Assert.Equal("Bash", e.ToolName);
        Assert.Equal("npm test", e.ToolInput!["command"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"session_id\":\"s1\"}")]
    [InlineData("{\"hook_event_name\":42}")]
    [InlineData("nope")]
    public void RefusesALineWithoutAnEventName(string line) => Assert.Null(AgentEvent.TryParse(line));

    [Fact]
    public void DefaultsTheSessionAndAgent()
    {
        var e = AgentEvent.TryParse("{\"hook_event_name\":\"Notification\",\"message\":\"Claude needs your permission\"}")!;
        Assert.Equal("unknown", e.SessionId);
        Assert.Equal("claude", e.Agent);
        Assert.Equal("Claude needs your permission", e.Message);
    }

    [Fact]
    public void NamesThePipePerUserUnlessOverridden()
    {
        Assert.Equal("faqra-agents-S-1-5-21-1", AgentPipe.Name("S-1-5-21-1", _ => null));
        Assert.Equal("test-pipe", AgentPipe.Name("S-1-5-21-1", name => name == AgentPipe.OverrideVariable ? "test-pipe" : null));
    }
}
