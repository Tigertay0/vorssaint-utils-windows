using System.Text.Json.Nodes;
using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class HookPayloadTests
{
    private static readonly Func<string, string?> NoEnv = _ => null;

    private static JsonObject Line(string stdin, string eventArg = "PreToolUse", Func<string, string?>? env = null) =>
        (JsonObject)JsonNode.Parse(HookPayload.ToLine(stdin, eventArg, "claude", env ?? NoEnv, @"C:\work")!)!;

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("42")]
    public void IgnoresAnythingButAnObject(string stdin) =>
        Assert.Null(HookPayload.ToLine(stdin, "Stop", "claude", NoEnv, @"C:\work"));

    [Fact]
    public void ReadsThroughAByteOrderMark()
    {
        var line = Line("\uFEFF{\"session_id\":\"s1\",\"hook_event_name\":\"Stop\"}");
        Assert.Equal("Stop", line["hook_event_name"]!.GetValue<string>());
    }

    [Fact]
    public void NamesTheEventFromTheArgumentWhenThePayloadDoesNot()
    {
        var line = Line("{\"session_id\":\"s1\"}", "SessionStart");
        Assert.Equal("SessionStart", line["hook_event_name"]!.GetValue<string>());
    }

    [Fact]
    public void TagsTheAgentTheTerminalAndAMissingFolder()
    {
        var env = new Dictionary<string, string?> { ["TERM_PROGRAM"] = "vscode", ["WT_SESSION"] = "abc", ["CLAUDE_CODE_ENTRYPOINT"] = "claude-vscode" };
        var line = Line("{\"session_id\":\"s1\"}", env: name => env.GetValueOrDefault(name));
        Assert.Equal("claude", line["faqra_agent"]!.GetValue<string>());
        Assert.Equal("vscode", line["term_program"]!.GetValue<string>());
        Assert.Equal("abc", line["wt_session"]!.GetValue<string>());
        Assert.Equal("claude-vscode", line["claude_entrypoint"]!.GetValue<string>());
        Assert.Equal(@"C:\work", line["cwd"]!.GetValue<string>());
    }

    [Fact]
    public void CutsLongStringsWithoutSplittingACharacter()
    {
        var command = new string('a', HookPayload.MaxString - 1) + "\U0001F600" + "tail";
        var line = Line($"{{\"session_id\":\"s1\",\"tool_name\":\"Bash\",\"tool_input\":{{\"command\":\"{command}\"}}}}");
        var cut = line["tool_input"]!["command"]!.GetValue<string>();
        Assert.Equal(new string('a', HookPayload.MaxString - 1) + "…", cut);
    }

    [Fact]
    public void KeepsEditBodiesUpToTheirOwnLimitAndFlagsACut()
    {
        var small = new string('x', 5000);
        var huge = new string('y', HookPayload.MaxEditString + 10);
        var line = Line($"{{\"hook_event_name\":\"PostToolUse\",\"session_id\":\"s1\",\"tool_name\":\"Edit\",\"tool_input\":{{\"file_path\":\"a.cs\",\"old_string\":\"{small}\",\"new_string\":\"{huge}\"}}}}");
        Assert.Equal(small, line["tool_input"]!["old_string"]!.GetValue<string>());
        Assert.Equal(HookPayload.MaxEditString + 1, line["tool_input"]!["new_string"]!.GetValue<string>().Length);
        Assert.True(line["faqra_diff_truncated"]!.GetValue<bool>());
    }

    [Fact]
    public void KeepsAQuestionWhole()
    {
        var question = new string('q', HookPayload.MaxString + 500);
        var line = Line($"{{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"tool_name\":\"AskUserQuestion\",\"tool_input\":{{\"questions\":[{{\"question\":\"{question}\"}}]}}}}");
        Assert.Equal(question, line["tool_input"]!["questions"]![0]!["question"]!.GetValue<string>());
    }

    [Fact]
    public void WritesOneLine()
    {
        var text = HookPayload.ToLine("{\"session_id\":\"s1\",\"prompt\":\"two\\nlines\"}", "UserPromptSubmit", "claude", NoEnv, @"C:\w")!;
        Assert.DoesNotContain('\n', text);
        Assert.Contains("two\\nlines", text);
    }
}
