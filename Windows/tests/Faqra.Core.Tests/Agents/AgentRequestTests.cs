using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class AgentRequestTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private static AgentRequest From(string line) => AgentRequest.From("r1", AgentEvent.TryParse(line)!, T0);

    [Fact]
    public void ACommandShowsItselfAndWhatAlwaysWouldSave()
    {
        var request = From("{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}," +
            "\"permission_suggestions\":[{\"type\":\"addRules\",\"rules\":[{\"toolName\":\"Bash\",\"ruleContent\":\"npm test:*\"}],\"behavior\":\"allow\",\"destination\":\"localSettings\"}]}");
        Assert.Equal(("r1", "s1", AgentRequestKind.Approval, "Bash", "npm test"), (request.Id, request.SessionId, request.Kind, request.ToolName, request.Detail));
        Assert.Equal(["Bash(npm test:*)"], request.AlwaysRules);
        Assert.True(request.CanAlways);
        Assert.Empty(request.Questions);
        Assert.Equal(T0, request.ReceivedAt);
    }

    [Fact]
    public void AnEditShowsItsFileAndCanAcceptEdits()
    {
        var request = From("{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"C:\\\\code\\\\a.ts\",\"content\":\"x\"}," +
            "\"permission_suggestions\":[{\"type\":\"setMode\",\"mode\":\"acceptEdits\",\"destination\":\"session\"}]}");
        Assert.Equal(@"C:\code\a.ts", request.Detail);
        Assert.Empty(request.AlwaysRules);
        Assert.True(request.AlwaysAcceptsEdits);
        Assert.True(request.CanAlways);
    }

    [Fact]
    public void WithoutSuggestionsThereIsNoAlways() =>
        Assert.False(From("{\"hook_event_name\":\"PermissionRequest\",\"tool_name\":\"WebFetch\",\"tool_input\":{\"url\":\"https://example.com\"}}").CanAlways);

    [Fact]
    public void AQuestionCarriesItsQuestions()
    {
        var request = From("{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"tool_name\":\"AskUserQuestion\",\"tool_input\":{\"questions\":[{\"question\":\"Which one?\",\"options\":[{\"label\":\"A\"}]}]}," +
            "\"permission_suggestions\":[{\"type\":\"setMode\",\"mode\":\"acceptEdits\",\"destination\":\"session\"}]}");
        Assert.Equal(AgentRequestKind.Question, request.Kind);
        Assert.Equal("Which one?", request.Questions.Single().Text);
        Assert.Equal(string.Empty, request.Detail);
        Assert.False(request.CanAlways);
    }

    [Theory]
    [InlineData("WebFetch", "{\"url\":\"https://example.com/a\"}", "https://example.com/a")]
    [InlineData("WebSearch", "{\"query\":\"wpf toggle\"}", "wpf toggle")]
    [InlineData("Grep", "{\"pattern\":\"TODO\"}", "TODO")]
    [InlineData("NotebookEdit", "{\"notebook_path\":\"C:\\\\n.ipynb\"}", @"C:\n.ipynb")]
    [InlineData("mcp__github__create_issue", "{\"description\":\"Open an issue\"}", "Open an issue")]
    [InlineData("Task", "{}", "")]
    public void EachToolShowsWhatItActsOn(string tool, string input, string detail) =>
        Assert.Equal(detail, From($"{{\"hook_event_name\":\"PermissionRequest\",\"tool_name\":\"{tool}\",\"tool_input\":{input}}}").Detail);

    [Fact]
    public void ATruncatedInputFlowsToTheRequest()
    {
        Assert.True(From("{\"hook_event_name\":\"PermissionRequest\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"x\"},\"faqra_input_truncated\":true}").InputTruncated);
        Assert.False(From("{\"hook_event_name\":\"PermissionRequest\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"x\"}}").InputTruncated);
    }

    [Fact]
    public void AnUnknownToolShowsItsWholeInput()
    {
        var detail = From("{\"hook_event_name\":\"PermissionRequest\",\"tool_name\":\"mcp__github__create_issue\",\"tool_input\":{\"title\":\"Bug\",\"body\":\"Steps\"}}").Detail;
        Assert.Contains("\"title\": \"Bug\"", detail);
        Assert.Contains("\"body\": \"Steps\"", detail);
    }

    [Fact]
    public void ATaskWithoutADescriptionShowsItsPrompt() =>
        Assert.Contains("Do X", From("{\"hook_event_name\":\"PermissionRequest\",\"tool_name\":\"Task\",\"tool_input\":{\"description\":\"\",\"prompt\":\"Do X\"}}").Detail);

    [Fact]
    public void AnEmptyInputKeepsTheDetailEmpty() =>
        Assert.Equal(string.Empty, From("{\"hook_event_name\":\"PermissionRequest\",\"tool_name\":\"mcp__x__y\",\"tool_input\":{}}").Detail);
}
