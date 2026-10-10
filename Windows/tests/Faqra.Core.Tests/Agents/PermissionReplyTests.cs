using System.Text.Json.Nodes;
using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

// Ported from the tests in Coucou's windows/hook/src/reply.rs.
public class PermissionReplyTests
{
    private const string AllowJson = "{\"hookSpecificOutput\":{\"hookEventName\":\"PermissionRequest\",\"decision\":{\"behavior\":\"allow\"}}}";
    private const string DenyJson = "{\"hookSpecificOutput\":{\"hookEventName\":\"PermissionRequest\",\"decision\":{\"behavior\":\"deny\",\"message\":\"Denied from Faqra\"}}}";

    private static JsonObject Request(string json) => (JsonObject)JsonNode.Parse(json)!;

    private static JsonObject Bash(string suggestions = "[]") => Request(
        "{\"hook_event_name\":\"PermissionRequest\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"},\"permission_suggestions\":" + suggestions + "}");

    private static JsonObject Question() => Request(
        "{\"hook_event_name\":\"PermissionRequest\",\"tool_name\":\"AskUserQuestion\",\"tool_input\":{\"questions\":[" +
        "{\"question\":\"Which one?\",\"header\":\"Pick\",\"options\":[{\"label\":\"A\"},{\"label\":\"B\"}],\"multiSelect\":false}," +
        "{\"question\":\"Extras?\",\"multiSelect\":true,\"options\":[{\"label\":\"Tests\"},{\"label\":\"Docs\"}]}]}}");

    private static Dictionary<string, string> Answers(params (string Question, string Answer)[] pairs) =>
        pairs.ToDictionary(p => p.Question, p => p.Answer);

    [Fact]
    public void NothingIsPrintedWithoutADecision()
    {
        Assert.Null(PermissionReply.Stdout(Bash(), null));
        Assert.Null(PermissionReply.Stdout(Question(), null));
    }

    [Fact]
    public void AllowAndDenyMatchTheDocumentedShape()
    {
        Assert.Equal(AllowJson, PermissionReply.Stdout(Bash(), AgentDecision.Allow));
        Assert.Equal(DenyJson, PermissionReply.Stdout(Bash(), AgentDecision.Deny()));
        Assert.Equal(DenyJson, PermissionReply.Stdout(Bash(), AgentDecision.Deny("  ")));
        Assert.Contains("\"message\":\"Not on main\"", PermissionReply.Stdout(Bash(), AgentDecision.Deny("Not on main")));
    }

    [Fact]
    public void AlwaysWithoutSuggestionsIsAPlainAllow() =>
        Assert.Equal(AllowJson, PermissionReply.Stdout(Bash(), AgentDecision.Always));

    [Fact]
    public void AlwaysKeepsOnlyAllowRulesAndAcceptEdits()
    {
        var suggestions = "[" +
            "{\"type\":\"addRules\",\"rules\":[{\"toolName\":\"Bash\",\"ruleContent\":\"npm test:*\"}],\"behavior\":\"allow\",\"destination\":\"userSettings\"}," +
            "{\"type\":\"addRules\",\"rules\":[{\"toolName\":\"Bash\"}],\"behavior\":\"deny\",\"destination\":\"localSettings\"}," +
            "{\"type\":\"setMode\",\"mode\":\"bypassPermissions\",\"destination\":\"session\"}," +
            "{\"type\":\"setMode\",\"mode\":\"acceptEdits\",\"destination\":\"userSettings\"}," +
            "{\"type\":\"addDirectories\",\"directories\":[\"C:\\\\\"],\"destination\":\"session\"}," +
            "{\"type\":\"addRules\",\"rules\":[],\"behavior\":\"allow\",\"destination\":\"localSettings\"}," +
            "{\"type\":\"addRules\",\"rules\":[{\"ruleContent\":\"x\"}],\"behavior\":\"allow\",\"destination\":\"localSettings\"}]";
        var reply = JsonNode.Parse(PermissionReply.Stdout(Bash(suggestions), AgentDecision.Always)!)!;
        var decision = reply["hookSpecificOutput"]!["decision"]!;
        Assert.Equal("allow", decision["behavior"]!.GetValue<string>());
        var updates = decision["updatedPermissions"]!.AsArray();
        Assert.Equal(2, updates.Count);
        Assert.Equal("addRules", updates[0]!["type"]!.GetValue<string>());
        Assert.Equal("allow", updates[0]!["behavior"]!.GetValue<string>());
        Assert.Equal("localSettings", updates[0]!["destination"]!.GetValue<string>());
        Assert.Equal("npm test:*", updates[0]!["rules"]![0]!["ruleContent"]!.GetValue<string>());
        Assert.Equal("setMode", updates[1]!["type"]!.GetValue<string>());
        Assert.Equal("acceptEdits", updates[1]!["mode"]!.GetValue<string>());
        Assert.Equal("session", updates[1]!["destination"]!.GetValue<string>());
        Assert.DoesNotContain("bypassPermissions", reply.ToJsonString());
        Assert.DoesNotContain("userSettings", reply.ToJsonString());
    }

    [Fact]
    public void AlwaysLabelsNameTheRules()
    {
        var suggestions = JsonNode.Parse(
            "[{\"type\":\"addRules\",\"rules\":[{\"toolName\":\"Bash\",\"ruleContent\":\"npm test:*\"},{\"toolName\":\"WebFetch\"}],\"behavior\":\"allow\",\"destination\":\"localSettings\"}," +
            "{\"type\":\"setMode\",\"mode\":\"acceptEdits\",\"destination\":\"session\"}]")!.AsArray();
        Assert.Equal(["Bash(npm test:*)", "WebFetch"], PermissionReply.AlwaysRuleLabels(suggestions));
        Assert.True(PermissionReply.AlwaysAcceptsEdits(suggestions));
        Assert.Empty(PermissionReply.AlwaysRuleLabels(null));
        Assert.False(PermissionReply.AlwaysAcceptsEdits(null));
    }

    [Fact]
    public void AnAnsweredQuestionGoesBackAsTheSameInputPlusAnswers()
    {
        var reply = JsonNode.Parse(PermissionReply.Stdout(Question(), AgentDecision.Answer(Answers(("Which one?", "B"), ("Extras?", "Tests, Docs"))))!)!;
        var decision = reply["hookSpecificOutput"]!["decision"]!;
        Assert.Equal("allow", decision["behavior"]!.GetValue<string>());
        Assert.Equal(Question()["tool_input"]!["questions"]!.ToJsonString(), decision["updatedInput"]!["questions"]!.ToJsonString());
        Assert.Equal("B", decision["updatedInput"]!["answers"]!["Which one?"]!.GetValue<string>());
        Assert.Equal("Tests, Docs", decision["updatedInput"]!["answers"]!["Extras?"]!.GetValue<string>());
    }

    [Fact]
    public void OwnWordsAndAccentsGoBackAsWritten()
    {
        var text = PermissionReply.Stdout(Question(), AgentDecision.Answer(Answers(("Which one?", "Ni l'un ni l'autre, désolé 🙂"), ("Extras?", "Docs"))))!;
        // Accents stay as written; an emoji may be written as a \u surrogate pair, which is the same JSON string.
        Assert.Contains("Ni l'un ni l'autre, désolé ", text);
        var back = JsonNode.Parse(text)!["hookSpecificOutput"]!["decision"]!["updatedInput"]!["answers"]!["Which one?"]!.GetValue<string>();
        Assert.Equal("Ni l'un ni l'autre, désolé 🙂", back);
    }

    [Fact]
    public void AQuestionNeedsAnswersNotAPlainAllow()
    {
        Assert.Null(PermissionReply.Stdout(Question(), AgentDecision.Allow));
        Assert.Null(PermissionReply.Stdout(Question(), AgentDecision.Always));
        Assert.Equal(DenyJson, PermissionReply.Stdout(Question(), AgentDecision.Deny()));
        Assert.Null(PermissionReply.Stdout(Bash(), AgentDecision.Answer(Answers(("q", "a")))));
    }

    [Theory]
    [InlineData("Which one?", "A", null, null)]                    // a question left out
    [InlineData("Which one?", "A", "Other?", "x")]                 // one nobody asked, and Extras? missing
    [InlineData("Which one?", "A", "Extras?", " ")]                // a blank answer
    [InlineData("Which?", "A", "Extras?", "Docs")]                 // the wrong text
    public void AnswersMustMatchTheQuestionsAsked(string q1, string a1, string? q2, string? a2)
    {
        var answers = new Dictionary<string, string> { [q1] = a1 };
        if (q2 is not null)
        {
            answers[q2] = a2!;
        }
        Assert.Null(PermissionReply.Stdout(Question(), AgentDecision.Answer(answers)));
    }

    [Fact]
    public void AnExtraAnswerIsRefused() =>
        Assert.Null(PermissionReply.Stdout(Question(), AgentDecision.Answer(Answers(("Which one?", "A"), ("Extras?", "Docs"), ("More?", "x")))));

    [Fact]
    public void AnAnswerLongerThanTheCapIsRefused() =>
        Assert.Null(PermissionReply.Stdout(Question(), AgentDecision.Answer(Answers(("Which one?", new string('x', PermissionReply.MaxAnswerLength + 1)), ("Extras?", "Docs")))));

    [Fact]
    public void RepeatedQuestionTextsCannotBeAnswered()
    {
        var request = Request("{\"tool_name\":\"AskUserQuestion\",\"tool_input\":{\"questions\":[{\"question\":\"Same?\"},{\"question\":\"Same?\"}]}}");
        Assert.Null(PermissionReply.Stdout(request, AgentDecision.Answer(Answers(("Same?", "yes")))));
    }
}
