# Faqra Agents A2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the owner approve Claude Code's tool requests and answer its questions from the island, open the island (without stealing focus) when an agent needs them or has answered, chime for both, show each session by its conversation name with the owner's last prompt, bring a session's window forward, and reach all of it from global shortcuts.

**Architecture:** A1's relay now waits on `PermissionRequest`: it sends its line, reads back one decision line from `AgentHub` (110 s budget), and turns it into Claude Code's documented `hookSpecificOutput` reply with `PermissionReply` (the only place that knows Claude's wire format). `AgentHub` keeps the connection open while a card shows the request, lets it go at once when no card can show (Claude Code then asks in its own UI), and releases it when the relay hangs up, the turn moves on, or 108 s pass. The hub also reads each session's name from its transcript and remembers which window the session runs in (process-tree walk from the relay's pipe client). The island's Agents module gains approval, question and answered cards; `IslandController` opens the island for them without activating it and takes the keyboard only when the owner clicks into a text box or uses the jump shortcut.

**Tech Stack:** C# / .NET 8, WPF + WPF UI 4.3, System.Text.Json (Nodes), System.IO.Pipes, System.Collections.Immutable, Win32 (Toolhelp32, `GetNamedPipeClientProcessId`, `PlaySound`), xunit. No new NuGet packages.

**Spec:** `docs/superpowers/specs/2026-10-09-faqra-agents-design.md`: the A2 row of the milestone table, "How it works" (waiting events, free-text answers), "What the owner sees" (the island, shortcuts, sounds), "Coucou's leftover hooks" (watch-only rule) and "Owner's additions (2026-10-10)" (session names, Prompted, sounds and the answered alert).

## Global Constraints

- Solution lives in `Windows/`; run every `dotnet` command from `C:\Users\Tigre\vorssaint-utils-windows\Windows`.
- Every new source file under `src/` starts with `// SPDX-License-Identifier: GPL-3.0-or-later` and `// Copyright (C) 2026 Faqra contributors`. Ported files add a third line naming the source and its MIT notice. Test files follow the existing test convention (no header).
- Nullable warnings are errors (`Directory.Build.props`). Package versions live only in `Directory.Packages.props`; this plan adds none.
- Pipe name and options are A1's: `faqra-agents-<user SID>`, `PipeOptions.CurrentUserOnly` on both ends, `FAQRA_AGENTS_PIPE` overrides it in tests.
- Timings: the relay waits at most `Relay.DecisionBudgetMs = 110_000` for a decision; the hub gives up after `AgentHub.DecisionTimeout = 108 s`; Claude Code's `PermissionRequest` hook timeout stays 120 s (A1's `ClaudeHookConfig`). Fire-and-forget events keep the whole-run budget of 2 s, and a missing pipe still returns at once.
- Wire format from the hub back to the relay: one JSON line, `{"decision":"allow"}`, `{"decision":"always"}`, `{"decision":"deny","message":"…"}` (message optional) or `{"decision":"answer","answers":{"<question text>":"<answer>"}}`. Nothing, an empty line, or anything else means no decision.
- Nothing allows anything without a click. The relay prints only for a decision; `AskUserQuestion` is answered only with answers (a plain Allow prints nothing); Always applies only `addRules` allow suggestions (always written to `localSettings`) and `setMode` `acceptEdits` (always `session`), never any other suggestion.
- The relay writes stdout as UTF-8 without a byte-order mark.
- The island opens for a request or an answered turn with `Expand(takeFocus: false, …)`: it never activates on its own. It takes the keyboard only when the owner clicks into a text box on a card, clicks the pill, or presses the jump shortcut.
- Faqra answers only when no `coucou-hook` entry is in `~/.claude/settings.json`; otherwise it watches only (no cards, requests let go at once).
- Session names: only files under `%USERPROFILE%\.claude\projects` ending in `.jsonl` are read; the newest `custom-title` line wins over the newest `ai-title` line; names are one line, at most 120 characters.
- Faqra-only settings keys (all prefixed `faqraAgents`): `faqraAgentsNeedsYouSound` (true), `faqraAgentsAnsweredSound` (true), `faqraAgentsOpenOnAnswer` (true), `faqraAgentsShortcutsEnabled` (true), `faqraAgentsJumpShortcut` (`control+option+command:65`, Ctrl+Alt+Win+A), `faqraAgentsWindowShortcut` (`control+option+command:71`, Ctrl+Alt+Win+G), `faqraAgentsNextShortcut` (`control+option+command:40`, Ctrl+Alt+Win+Down), `faqraAgentsPreviousShortcut` (`control+option+command:38`, Ctrl+Alt+Win+Up).
- `~/.claude/settings.json` is never written by A2. When the owner clicks Always, Claude Code itself writes the rule to the project's `.claude/settings.local.json`, exactly as its own "don't ask again" does.
- User-visible copy: sentence case, no em or en dashes, no exclamation marks, never the words Coucou or Mochi except where the text names Coucou's leftover hooks.
- Commits: conventional prefixes (`feat(windows):`, `test(windows):`, `docs(windows):`), and no `Co-Authored-By` or any Claude attribution line.
- WPF tests run inside `StaThread.Run(...)` (tests/Faqra.App.Tests/StaThread.cs).
- Subagents dispatched for this plan run on Sonnet (`model: "sonnet"`).

## Review Focus

1. Faqra cannot show a card (island off, Agents section hidden, a fullscreen app, Coucou's hooks present, or the hub stopped): Claude Code must get its own prompt at once, never after a 110 s wait. Pinned in Task 3 (`PrintsNothingWhenFaqraLetsGo`) and Task 4 (`LetsGoAtOnceWhenNoCardCanShow`).
2. The owner answers in the terminal, or the turn moves on, while a card is up: the card disappears and nothing is printed late. Pinned in Task 4 (`ACardGoesWhenTheRelayGoesAway`, `ACardGoesWhenTheTurnMovesOn`).
3. Accents, emoji and long question texts: echoed whole and written as UTF-8, so Claude reads exactly what it asked and what the owner typed. Pinned in Task 3 (`PrintsAnswersAsUtf8WithTheQuestionWhole`).
4. Events arriving while the owner picks options or types an answer: the card, its picks and the typed text survive every refresh. Pinned in Task 7 (`TheCardKeepsWhatTheOwnerTypedWhileEventsArrive`).
5. A malformed or hostile request (repeated JSON keys, a question without text, a suggestion that would switch to bypassPermissions or write the owner's user settings): no crash, no card or no Always, never a broader rule. Pinned in Task 1 (`AlwaysKeepsOnlyAllowRulesAndAcceptEdits`) and Task 4 (`ARepeatedKeyInAQuestionNeverMakesACard`).

---

## File structure

| File | Responsibility |
|---|---|
| `src/Faqra.Core/Agents/AgentDecision.cs` (new) | The owner's choice and its one-line wire form |
| `src/Faqra.Core/Agents/PermissionReply.cs` (new) | Claude Code's PermissionRequest reply; answer validation; Always filtering |
| `src/Faqra.Core/Agents/AgentQuestions.cs` (new) | AskUserQuestion parsing and answer composition |
| `src/Faqra.Core/Agents/AgentRequest.cs` (new) | A request a card shows |
| `src/Faqra.Core/Agents/ProcessTree.cs` (new) | Ancestor walk and window choice (pure) |
| `src/Faqra.Core/Agents/AgentEvent.cs` (modify) | Permission suggestions, transcript path |
| `src/Faqra.Core/Agents/AgentBoard.cs` (modify) | Title, last prompt, unread answer; `Answered`, `Titled`, `MarkRead` |
| `src/Faqra.Core/Agents/AgentsText.cs` (modify) | Request titles |
| `src/Faqra.Core/Localization/AgentsStrings*.cs` (modify) | New strings |
| `src/Faqra.Core/Shortcuts/*` and `src/Faqra.Core/Defaults/*` (modify) | Four shortcut roles, Faqra-only keys |
| `src/Faqra.Hook/Relay.cs`, `Program.cs` (modify) | Wait for a decision, print the reply as UTF-8 |
| `src/Faqra.Services/Agents/AgentHub.cs` (modify) | Requests, titles, windows, finished turns |
| `src/Faqra.Services/Agents/SessionTitles.cs` (new) | Reads a conversation's name from its transcript |
| `src/Faqra.Services/Agents/SessionWindows.cs` (new) | Locates and brings forward a session's window |
| `src/Faqra.Services/Agents/SessionWindowCache.cs` (new) | Session to window-owner memory |
| `src/Faqra.Win32/Agents/ProcessSnapshot.cs`, `PipeClient.cs`, `AlertSound.cs` (new) | Toolhelp32, pipe client PID, Windows sounds |
| `src/Faqra.App/Agents/ClaudeHooks.cs` (new) | Reads hook status for the island |
| `src/Faqra.App/Island/Modules/AgentCards.cs` (new) | Approval, question and answered cards |
| `src/Faqra.App/Island/Modules/AgentsModule.cs` (modify) | Cards, names, Prompted, go to window, focus API |
| `src/Faqra.App/Island/IslandController.cs` (modify) | Opens for requests and answers, sounds, keyboard, shortcuts |
| `src/Faqra.App/AppServices.cs` (modify) | Hub options, shortcut bindings |
| `src/Faqra.App/Settings/Pages/AgentsPage.cs`, `ShortcutsPage.cs` (modify) | Alerts section, shortcut titles |

---

### Task 1: The owner's decision and Claude Code's reply

**Files:**
- Create: `src/Faqra.Core/Agents/AgentDecision.cs`
- Create: `src/Faqra.Core/Agents/PermissionReply.cs`
- Modify: `src/Faqra.Core/Agents/AgentEvent.cs`
- Test: `tests/Faqra.Core.Tests/Agents/AgentDecisionTests.cs`, `tests/Faqra.Core.Tests/Agents/PermissionReplyTests.cs`, `tests/Faqra.Core.Tests/Agents/AgentEventTests.cs` (add)

**Interfaces:**
- Consumes: `AgentJson.Compact` (A1).
- Produces: `enum AgentDecisionKind { Allow, Always, Deny, Answer }`; `sealed class AgentDecision` with `Kind`, `string? Message`, `IReadOnlyDictionary<string,string>? Answers`, static `Allow`, `Always`, `Deny(string? message = null)`, `Answer(IReadOnlyDictionary<string,string>)`, `string ToLine()`, `static AgentDecision? TryParse(string? line)`. `static class PermissionReply` with `const string DefaultDenyMessage = "Denied from Faqra"`, `const int MaxAnswerLength = 2000`, `string? Stdout(JsonObject request, AgentDecision? decision)`, `bool AnswersFit(JsonObject toolInput, IReadOnlyDictionary<string,string> answers)`, `JsonArray AlwaysRules(JsonArray? suggestions)`, `IReadOnlyList<string> AlwaysRuleLabels(JsonArray? suggestions)`, `bool AlwaysAcceptsEdits(JsonArray? suggestions)`. `AgentEvent` gains init properties `JsonArray? PermissionSuggestions` and `string? TranscriptPath`.

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.Core.Tests/Agents/AgentDecisionTests.cs`:

```csharp
using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class AgentDecisionTests
{
    [Fact]
    public void EveryKindSurvivesTheWire()
    {
        Assert.Equal(AgentDecisionKind.Allow, AgentDecision.TryParse(AgentDecision.Allow.ToLine())!.Kind);
        Assert.Equal(AgentDecisionKind.Always, AgentDecision.TryParse(AgentDecision.Always.ToLine())!.Kind);

        var deny = AgentDecision.TryParse(AgentDecision.Deny("Not now").ToLine())!;
        Assert.Equal(AgentDecisionKind.Deny, deny.Kind);
        Assert.Equal("Not now", deny.Message);
        Assert.Null(AgentDecision.TryParse(AgentDecision.Deny().ToLine())!.Message);

        var answer = AgentDecision.TryParse(AgentDecision.Answer(new Dictionary<string, string> { ["Café?"] = "Oui" }).ToLine())!;
        Assert.Equal(AgentDecisionKind.Answer, answer.Kind);
        Assert.Equal("Oui", answer.Answers!["Café?"]);
    }

    [Fact]
    public void TheWireFormIsOneLine() =>
        Assert.DoesNotContain('\n', AgentDecision.Answer(new Dictionary<string, string> { ["a\nb"] = "c\nd" }).ToLine());

    [Fact]
    public void TheWireFormIsTheDocumentedOne()
    {
        Assert.Equal("{\"decision\":\"allow\"}", AgentDecision.Allow.ToLine());
        Assert.Equal("{\"decision\":\"always\"}", AgentDecision.Always.ToLine());
        Assert.Equal("{\"decision\":\"deny\",\"message\":\"No\"}", AgentDecision.Deny("No").ToLine());
        Assert.Equal("{\"decision\":\"answer\",\"answers\":{\"Q?\":\"A\"}}", AgentDecision.Answer(new Dictionary<string, string> { ["Q?"] = "A" }).ToLine());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("allow")]
    [InlineData("[1]")]
    [InlineData("{\"decision\":\"maybe\"}")]
    [InlineData("{\"permissionDecision\":\"allow\"}")]
    [InlineData("{\"decision\":\"answer\"}")]
    [InlineData("{\"decision\":\"answer\",\"answers\":{\"q\":1}}")]
    [InlineData("{\"decision\":\"allow\",\"decision\":\"deny\"}")]
    public void AnythingElseIsNoDecision(string? line) => Assert.Null(AgentDecision.TryParse(line));
}
```

`tests/Faqra.Core.Tests/Agents/PermissionReplyTests.cs`:

```csharp
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
        Assert.Contains("Ni l'un ni l'autre, désolé 🙂", text);
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
```

Add to `tests/Faqra.Core.Tests/Agents/AgentEventTests.cs` (inside the existing class):

```csharp
    [Fact]
    public void ReadsSuggestionsAndTheTranscript()
    {
        var e = AgentEvent.TryParse("{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s\",\"transcript_path\":\"C:\\\\Users\\\\me\\\\.claude\\\\projects\\\\p\\\\s.jsonl\",\"permission_suggestions\":[{\"type\":\"setMode\",\"mode\":\"acceptEdits\",\"destination\":\"session\"}]}")!;
        Assert.Single(e.PermissionSuggestions!);
        Assert.Equal(@"C:\Users\me\.claude\projects\p\s.jsonl", e.TranscriptPath);
        var bare = AgentEvent.TryParse("{\"hook_event_name\":\"Stop\"}")!;
        Assert.Null(bare.PermissionSuggestions);
        Assert.Null(bare.TranscriptPath);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~AgentDecisionTests|FullyQualifiedName~PermissionReplyTests|FullyQualifiedName~AgentEventTests"`
Expected: build FAILS with "The type or namespace name 'AgentDecision' could not be found" and "'AgentEvent' does not contain a definition for 'PermissionSuggestions'".

- [ ] **Step 3: Write `AgentDecision`**

`src/Faqra.Core/Agents/AgentDecision.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

public enum AgentDecisionKind { Allow, Always, Deny, Answer }

/// <summary>
/// What the owner chose on a card, as it travels from Faqra back to the waiting relay. Only the relay turns it into
/// Claude Code's reply (<see cref="PermissionReply"/>), so Claude's wire format lives in one place.
/// </summary>
public sealed class AgentDecision
{
    private AgentDecision(AgentDecisionKind kind, string? message, IReadOnlyDictionary<string, string>? answers)
    {
        Kind = kind;
        Message = message;
        Answers = answers;
    }

    public AgentDecisionKind Kind { get; }

    /// <summary>For Deny: what Claude is told. Null means <see cref="PermissionReply.DefaultDenyMessage"/>.</summary>
    public string? Message { get; }

    /// <summary>For Answer: each question's text and the answer to it.</summary>
    public IReadOnlyDictionary<string, string>? Answers { get; }

    public static AgentDecision Allow { get; } = new(AgentDecisionKind.Allow, null, null);

    public static AgentDecision Always { get; } = new(AgentDecisionKind.Always, null, null);

    public static AgentDecision Deny(string? message = null) => new(AgentDecisionKind.Deny, message, null);

    public static AgentDecision Answer(IReadOnlyDictionary<string, string> answers) => new(AgentDecisionKind.Answer, null, answers);

    public string ToLine()
    {
        var line = new JsonObject { ["decision"] = Word(Kind) };
        if (Message is not null)
        {
            line["message"] = Message;
        }
        if (Answers is not null)
        {
            var answers = new JsonObject();
            foreach (var (question, answer) in Answers)
            {
                answers[question] = answer;
            }
            line["answers"] = answers;
        }
        return line.ToJsonString(AgentJson.Compact);
    }

    /// <summary>The decision on a line, or null for anything that is not exactly one.</summary>
    public static AgentDecision? TryParse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }
        try
        {
            if (JsonNode.Parse(line) is not JsonObject obj || Text(obj["decision"]) is not { } word)
            {
                return null;
            }
            return word switch
            {
                "allow" => Allow,
                "always" => Always,
                "deny" => Deny(Text(obj["message"])),
                "answer" when obj["answers"] is JsonObject answers && ReadAnswers(answers) is { } read => Answer(read),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            // ArgumentException: .NET 8 reports repeated property names this way, when the object is first read.
            return null;
        }
    }

    private static Dictionary<string, string>? ReadAnswers(JsonObject answers)
    {
        var read = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (question, value) in answers)
        {
            if (Text(value) is not { } answer)
            {
                return null;
            }
            read[question] = answer;
        }
        return read;
    }

    private static string Word(AgentDecisionKind kind) => kind switch
    {
        AgentDecisionKind.Allow => "allow",
        AgentDecisionKind.Always => "always",
        AgentDecisionKind.Deny => "deny",
        _ => "answer",
    };

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
```

- [ ] **Step 4: Write `PermissionReply`**

`src/Faqra.Core/Agents/PermissionReply.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Ported from Coucou's relay replies (windows/hook/src/reply.rs), https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé).

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

/// <summary>
/// What the relay prints back to Claude Code for a PermissionRequest, from Claude's documented hook output
/// (https://code.claude.com/docs/en/hooks). The rule that matters: nothing that allows anything is printed without a
/// decision a person made. With no decision the relay prints nothing and Claude Code asks in its own UI.
/// </summary>
public static class PermissionReply
{
    /// <summary>What Claude reads when the owner denies without saying why.</summary>
    public const string DefaultDenyMessage = "Denied from Faqra";

    /// <summary>Longest answer a question takes back, the relay's usual string cap.</summary>
    public const int MaxAnswerLength = 2000;

    private const string AskUserQuestion = "AskUserQuestion";

    /// <summary>The line to print for <paramref name="request"/>, or null to print nothing.</summary>
    public static string? Stdout(JsonObject request, AgentDecision? decision)
    {
        if (decision is null)
        {
            return null;
        }
        var isQuestion = Text(request["tool_name"]) == AskUserQuestion;
        var body = decision.Kind switch
        {
            AgentDecisionKind.Deny => Deny(decision.Message),
            AgentDecisionKind.Allow when !isQuestion => new JsonObject { ["behavior"] = "allow" },
            AgentDecisionKind.Always when !isQuestion => AllowAlways(request),
            AgentDecisionKind.Answer when isQuestion => Answered(request, decision.Answers!),
            _ => null,
        };
        if (body is null)
        {
            return null;
        }
        return new JsonObject
        {
            ["hookSpecificOutput"] = new JsonObject { ["hookEventName"] = "PermissionRequest", ["decision"] = body },
        }.ToJsonString(AgentJson.Compact);
    }

    /// <summary>True when the answers answer exactly the questions asked: one non-empty answer per question text, no extras.</summary>
    public static bool AnswersFit(JsonObject toolInput, IReadOnlyDictionary<string, string> answers)
    {
        if (toolInput["questions"] is not JsonArray questions || questions.Count == 0 || questions.Count != answers.Count)
        {
            return false;
        }
        var asked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in questions)
        {
            if (item is not JsonObject question || Text(question["question"]) is not { Length: > 0 } text || !asked.Add(text))
            {
                return false;
            }
            if (!answers.TryGetValue(text, out var answer) || string.IsNullOrWhiteSpace(answer) || answer.Length > MaxAnswerLength)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// The suggestions an Always click may apply: allow rules, always saved to this project's local settings, and
    /// accept-edits for this session only. Everything else Claude Code suggests (other modes, directories, deny or ask
    /// rules, the owner's user settings) is dropped.
    /// </summary>
    public static JsonArray AlwaysRules(JsonArray? suggestions)
    {
        var kept = new JsonArray();
        foreach (var entry in suggestions?.OfType<JsonObject>() ?? [])
        {
            var type = Text(entry["type"]);
            if (type == "addRules" && Text(entry["behavior"]) == "allow" && entry["rules"] is JsonArray rules && rules.Count > 0
                && rules.All(rule => rule is JsonObject r && Text(r["toolName"]) is { Length: > 0 }))
            {
                kept.Add(new JsonObject
                {
                    ["type"] = "addRules",
                    ["rules"] = rules.DeepClone(),
                    ["behavior"] = "allow",
                    ["destination"] = "localSettings",
                });
            }
            else if (type == "setMode" && Text(entry["mode"]) == "acceptEdits")
            {
                kept.Add(new JsonObject { ["type"] = "setMode", ["mode"] = "acceptEdits", ["destination"] = "session" });
            }
        }
        return kept;
    }

    /// <summary>The rules an Always click saves, as Claude Code writes them: "Bash(npm test:*)", "WebFetch".</summary>
    public static IReadOnlyList<string> AlwaysRuleLabels(JsonArray? suggestions) => AlwaysRules(suggestions)
        .OfType<JsonObject>()
        .Where(entry => Text(entry["type"]) == "addRules")
        .SelectMany(entry => ((JsonArray)entry["rules"]!).OfType<JsonObject>())
        .Select(rule => Text(rule["ruleContent"]) is { Length: > 0 } content ? $"{Text(rule["toolName"])}({content})" : Text(rule["toolName"])!)
        .ToList();

    /// <summary>True when an Always click also accepts edits for the rest of the session.</summary>
    public static bool AlwaysAcceptsEdits(JsonArray? suggestions) =>
        AlwaysRules(suggestions).OfType<JsonObject>().Any(entry => Text(entry["type"]) == "setMode");

    private static JsonObject Deny(string? message) => new()
    {
        ["behavior"] = "deny",
        ["message"] = string.IsNullOrWhiteSpace(message) ? DefaultDenyMessage : message,
    };

    private static JsonObject AllowAlways(JsonObject request)
    {
        var body = new JsonObject { ["behavior"] = "allow" };
        var rules = AlwaysRules(request["permission_suggestions"] as JsonArray);
        if (rules.Count > 0)
        {
            body["updatedPermissions"] = rules;
        }
        return body;
    }

    /// <summary>Claude Code takes an answered question as its own input with an answers map added; nothing else changes.</summary>
    private static JsonObject? Answered(JsonObject request, IReadOnlyDictionary<string, string> answers)
    {
        if (request["tool_input"] is not JsonObject input || !AnswersFit(input, answers))
        {
            return null;
        }
        var updated = (JsonObject)input.DeepClone();
        var map = new JsonObject();
        foreach (var (question, answer) in answers)
        {
            map[question] = answer;
        }
        updated["answers"] = map;
        return new JsonObject { ["behavior"] = "allow", ["updatedInput"] = updated };
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
```

- [ ] **Step 5: Add the two fields to `AgentEvent`**

In `src/Faqra.Core/Agents/AgentEvent.cs`, replace the `return new AgentEvent(` statement's closing `Text(obj, "claude_entrypoint"));` with an object initializer, and add the properties after the record's parameter list body opens:

```csharp
public sealed record AgentEvent(
    string Event,
    string SessionId,
    string Agent,
    string Cwd,
    string? ToolName,
    JsonObject? ToolInput,
    string? Prompt,
    string? Message,
    string? LastAssistantMessage,
    string? NotificationType,
    string? TermProgram,
    string? Entrypoint)
{
    /// <summary>What Claude Code offers to remember on a PermissionRequest ("don't ask again" rules and modes).</summary>
    public JsonArray? PermissionSuggestions { get; init; }

    /// <summary>The session's transcript, where Claude Code keeps the conversation's name.</summary>
    public string? TranscriptPath { get; init; }

    public static AgentEvent? TryParse(string line)
    {
        try
        {
            if (JsonNode.Parse(line) is not JsonObject obj || Text(obj, "hook_event_name") is not { Length: > 0 } name)
            {
                return null;
            }
            return new AgentEvent(
                name,
                Text(obj, "session_id") ?? "unknown",
                Text(obj, "faqra_agent") ?? "claude",
                Text(obj, "cwd") ?? string.Empty,
                Text(obj, "tool_name"),
                obj["tool_input"] as JsonObject,
                Text(obj, "prompt"),
                Text(obj, "message"),
                Text(obj, "last_assistant_message"),
                Text(obj, "notification_type"),
                Text(obj, "term_program"),
                Text(obj, "claude_entrypoint"))
            {
                PermissionSuggestions = obj["permission_suggestions"] as JsonArray,
                TranscriptPath = Text(obj, "transcript_path"),
            };
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            // ArgumentException: .NET 8 reports duplicate property names this way, and only when
            // the object is first read, so the reads stay inside the try.
            return null;
        }
    }

    private static string? Text(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~AgentDecisionTests|FullyQualifiedName~PermissionReplyTests|FullyQualifiedName~AgentEventTests"`
Expected: PASS (all tests in the three classes).

- [ ] **Step 7: Commit**

```bash
git add src/Faqra.Core/Agents/AgentDecision.cs src/Faqra.Core/Agents/PermissionReply.cs src/Faqra.Core/Agents/AgentEvent.cs tests/Faqra.Core.Tests/Agents/AgentDecisionTests.cs tests/Faqra.Core.Tests/Agents/PermissionReplyTests.cs tests/Faqra.Core.Tests/Agents/AgentEventTests.cs
git commit -m "feat(windows): agent decisions and Claude Code's permission replies"
```

---

### Task 2: Questions, requests, names and prompts on the board

**Files:**
- Create: `src/Faqra.Core/Agents/AgentQuestions.cs`
- Create: `src/Faqra.Core/Agents/AgentRequest.cs`
- Modify: `src/Faqra.Core/Agents/AgentBoard.cs`
- Modify: `src/Faqra.Core/Agents/AgentsText.cs`
- Modify: `src/Faqra.Core/Localization/AgentsStrings.cs`, `src/Faqra.Core/Localization/AgentsStrings.EnUS.cs`
- Test: `tests/Faqra.Core.Tests/Agents/AgentQuestionsTests.cs`, `tests/Faqra.Core.Tests/Agents/AgentRequestTests.cs`, `tests/Faqra.Core.Tests/Agents/AgentBoardTests.cs` (add), `tests/Faqra.Core.Tests/Agents/AgentsTextTests.cs` (add)

**Interfaces:**
- Consumes: `AgentEvent.PermissionSuggestions`, `PermissionReply.AlwaysRuleLabels`, `PermissionReply.AlwaysAcceptsEdits` (Task 1).
- Produces: `sealed record AgentOption(string Label, string Description)`; `sealed record AgentQuestion(string Text, string Header, bool MultiSelect, IReadOnlyList<AgentOption> Options)`; `static class AgentQuestions` with `const string Separator = ", "`, `bool OwnTextInAnswers { get; }` (true), `IReadOnlyList<AgentQuestion> Parse(JsonObject? toolInput)`, `string? Compose(AgentQuestion question, IReadOnlyCollection<string> picked, string? ownText)`, `IReadOnlyDictionary<string,string>? Answers(IReadOnlyList<AgentQuestion> questions, IReadOnlyList<string?> composed)`, `string AsDenialMessage(IReadOnlyDictionary<string,string> answers)`. `enum AgentRequestKind { Approval, Question }`; `sealed record AgentRequest(string Id, string SessionId, AgentRequestKind Kind, string ToolName, string Detail, IReadOnlyList<AgentQuestion> Questions, IReadOnlyList<string> AlwaysRules, bool AlwaysAcceptsEdits, DateTimeOffset ReceivedAt)` with `bool CanAlways` and `static AgentRequest From(string id, AgentEvent e, DateTimeOffset now)`. `AgentSession` gains init properties `string? Title`, `string? LastPrompt`, `bool Unread` and the computed `string Name`. `AgentBoard` gains `AgentBoard Answered(string sessionId, bool allowed, DateTimeOffset now)`, `AgentBoard Titled(string sessionId, string title)`, `AgentBoard MarkRead(string sessionId)`. `AgentsText.RequestTitle(AgentRequest, AgentsStrings)`. New strings `ApprovalRun`, `ApprovalEdit`, `ApprovalFetch`, `ApprovalToolFormat`.

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.Core.Tests/Agents/AgentQuestionsTests.cs`:

```csharp
using System.Text.Json.Nodes;
using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class AgentQuestionsTests
{
    private static JsonObject Input(string json) => (JsonObject)JsonNode.Parse(json)!;

    private static JsonObject Two() => Input("{\"questions\":[" +
        "{\"question\":\"Which framework?\",\"header\":\"Framework\",\"options\":[{\"label\":\"React\",\"description\":\"Component library\"},{\"label\":\"Vue\",\"description\":\"Progressive framework\"}],\"multiSelect\":false}," +
        "{\"question\":\"Which extras?\",\"header\":\"Extras\",\"options\":[{\"label\":\"Tests\"},{\"label\":\"Docs\"},{\"label\":\"Lint\"}],\"multiSelect\":true}]}");

    [Fact]
    public void ParsesEveryQuestionAndOption()
    {
        var questions = AgentQuestions.Parse(Two());
        Assert.Equal(2, questions.Count);
        Assert.Equal("Which framework?", questions[0].Text);
        Assert.Equal("Framework", questions[0].Header);
        Assert.False(questions[0].MultiSelect);
        Assert.Equal(new AgentOption("Vue", "Progressive framework"), questions[0].Options[1]);
        Assert.True(questions[1].MultiSelect);
        Assert.Equal(["Tests", "Docs", "Lint"], questions[1].Options.Select(o => o.Label));
        Assert.Equal(string.Empty, questions[1].Options[0].Description);
    }

    [Theory]
    [InlineData("{\"questions\":[{\"question\":\"Same?\"},{\"question\":\"Same?\"}]}")]
    [InlineData("{\"questions\":[{\"header\":\"No text\"}]}")]
    [InlineData("{\"questions\":[]}")]
    [InlineData("{\"questions\":\"nope\"}")]
    [InlineData("{}")]
    public void AQuestionNobodyCanAnswerMeansNoneAre(string json) => Assert.Empty(AgentQuestions.Parse(Input(json)));

    [Fact]
    public void NoInputMeansNoQuestions() => Assert.Empty(AgentQuestions.Parse(null));

    [Fact]
    public void OptionsWithoutALabelAreSkipped()
    {
        var questions = AgentQuestions.Parse(Input("{\"questions\":[{\"question\":\"Q?\",\"options\":[{\"description\":\"no label\"},{\"label\":\"Yes\"}]}]}"));
        Assert.Equal(["Yes"], questions[0].Options.Select(o => o.Label));
    }

    [Fact]
    public void ASingleChoiceTakesThePickOrTheOwnersWords()
    {
        var question = AgentQuestions.Parse(Two())[0];
        Assert.Equal("Vue", AgentQuestions.Compose(question, ["Vue"], null));
        Assert.Equal("Svelte", AgentQuestions.Compose(question, ["Vue"], "  Svelte "));
        Assert.Null(AgentQuestions.Compose(question, [], "   "));
        Assert.Null(AgentQuestions.Compose(question, ["Angular"], null));
    }

    [Fact]
    public void AMultiChoiceKeepsClaudesOrderThenTheOwnersWords()
    {
        var question = AgentQuestions.Parse(Two())[1];
        Assert.Equal("Tests, Lint", AgentQuestions.Compose(question, ["Lint", "Tests"], null));
        Assert.Equal("Tests, Lint, a changelog", AgentQuestions.Compose(question, ["Lint", "Tests", "Bogus"], "a changelog"));
        Assert.Equal("a changelog", AgentQuestions.Compose(question, [], "a changelog"));
        Assert.Null(AgentQuestions.Compose(question, [], null));
    }

    [Fact]
    public void AnswersOnlyOnceEveryQuestionHasOne()
    {
        var questions = AgentQuestions.Parse(Two());
        Assert.Null(AgentQuestions.Answers(questions, ["Vue", null]));
        Assert.Null(AgentQuestions.Answers(questions, ["Vue"]));
        Assert.Null(AgentQuestions.Answers([], []));
        var answers = AgentQuestions.Answers(questions, ["Vue", "Tests, Docs"])!;
        Assert.Equal("Vue", answers["Which framework?"]);
        Assert.Equal("Tests, Docs", answers["Which extras?"]);
    }

    [Fact]
    public void TheDenialFallbackNamesEachQuestionAndAnswer()
    {
        var message = AgentQuestions.AsDenialMessage(new Dictionary<string, string> { ["Which framework?"] = "Svelte" });
        Assert.Contains("Which framework?", message);
        Assert.Contains("Svelte", message);
    }
}
```

`tests/Faqra.Core.Tests/Agents/AgentRequestTests.cs`:

```csharp
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
}
```

Add to `tests/Faqra.Core.Tests/Agents/AgentBoardTests.cs` (inside the existing class; it already has a `T0` field and an event helper; if its helper is named differently, parse lines with `AgentEvent.TryParse(...)!` as below):

```csharp
    private static AgentBoard With(params string[] lines)
    {
        var board = AgentBoard.Empty;
        foreach (var line in lines)
        {
            board = board.Apply(AgentEvent.TryParse(line)!, new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero));
        }
        return board;
    }

    [Fact]
    public void AnAnsweredRequestMovesTheSessionOn()
    {
        var now = new DateTimeOffset(2026, 10, 10, 9, 1, 0, TimeSpan.Zero);
        var waiting = With("{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s\",\"tool_name\":\"Bash\"}");
        Assert.Equal(AgentState.Working, waiting.Answered("s", allowed: true, now).Sessions["s"].State);
        Assert.Equal(AgentState.Thinking, waiting.Answered("s", allowed: false, now).Sessions["s"].State);
        Assert.Equal(now, waiting.Answered("s", allowed: true, now).Sessions["s"].UpdatedAt);

        var busy = With("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s\",\"tool_name\":\"Bash\"}");
        Assert.Same(busy, busy.Answered("s", allowed: true, now));
        Assert.Same(busy, busy.Answered("other", allowed: true, now));
    }

    [Fact]
    public void ASessionRemembersTheLastPromptInFull()
    {
        var prompt = "Please refactor the relay so it waits for a decision on permission requests, then run the tests";
        var board = With($"{{\"hook_event_name\":\"UserPromptSubmit\",\"session_id\":\"s\",\"prompt\":\"{prompt}\"}}");
        Assert.Equal(prompt, board.Sessions["s"].LastPrompt);
    }

    [Fact]
    public void AFinishedTurnIsUnreadUntilReadOrANewPrompt()
    {
        var finished = With(
            "{\"hook_event_name\":\"UserPromptSubmit\",\"session_id\":\"s\",\"prompt\":\"go\"}",
            "{\"hook_event_name\":\"Stop\",\"session_id\":\"s\",\"last_assistant_message\":\"All done.\"}");
        Assert.True(finished.Sessions["s"].Unread);
        Assert.False(finished.MarkRead("s").Sessions["s"].Unread);
        Assert.Same(finished, finished.MarkRead("other"));
        var next = finished.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"UserPromptSubmit\",\"session_id\":\"s\",\"prompt\":\"more\"}")!, DateTimeOffset.Now);
        Assert.False(next.Sessions["s"].Unread);

        var silent = With("{\"hook_event_name\":\"Stop\",\"session_id\":\"q\"}");
        Assert.False(silent.Sessions["q"].Unread);
    }

    [Fact]
    public void ASessionIsNamedAfterItsConversation()
    {
        var board = With("{\"hook_event_name\":\"SessionStart\",\"session_id\":\"s\",\"cwd\":\"C:\\\\code\\\\Windows\"}");
        Assert.Equal("Windows", board.Sessions["s"].Name);
        var titled = board.Titled("s", "Faqra milestones 5 and 6");
        Assert.Equal("Faqra milestones 5 and 6", titled.Sessions["s"].Name);
        Assert.Same(titled, titled.Titled("s", "Faqra milestones 5 and 6"));
        Assert.Same(board, board.Titled("other", "x"));
        var later = titled.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s\",\"tool_name\":\"Read\"}")!, DateTimeOffset.Now);
        Assert.Equal("Faqra milestones 5 and 6", later.Sessions["s"].Name);
    }
```

Add to `tests/Faqra.Core.Tests/Agents/AgentsTextTests.cs` (inside the existing class):

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~AgentQuestionsTests|FullyQualifiedName~AgentRequestTests|FullyQualifiedName~AgentBoardTests|FullyQualifiedName~AgentsTextTests"`
Expected: build FAILS with "The type or namespace name 'AgentQuestions' could not be found" and "'AgentBoard' does not contain a definition for 'Answered'".

- [ ] **Step 3: Write `AgentQuestions`**

`src/Faqra.Core/Agents/AgentQuestions.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

public sealed record AgentOption(string Label, string Description);

public sealed record AgentQuestion(string Text, string Header, bool MultiSelect, IReadOnlyList<AgentOption> Options);

/// <summary>Claude Code's AskUserQuestion, read for a card, and the owner's picks turned into its answers.</summary>
public static class AgentQuestions
{
    /// <summary>How Claude Code joins several picked labels (hooks reference, AskUserQuestion answers).</summary>
    public const string Separator = ", ";

    /// <summary>
    /// True: the owner's own words go back as the answer itself. False: they go back as a denial Claude reads (the
    /// spec's fallback, "Free-text answers"). Settled by A2's live check; see Task 9.
    /// </summary>
    public static bool OwnTextInAnswers { get; } = true;

    /// <summary>
    /// Every question with its options. Empty when any question lacks its text or repeats another's: answers are
    /// keyed by that text, so such a question can only be answered in Claude Code.
    /// </summary>
    public static IReadOnlyList<AgentQuestion> Parse(JsonObject? toolInput)
    {
        if (toolInput?["questions"] is not JsonArray items || items.Count == 0)
        {
            return [];
        }
        var questions = new List<AgentQuestion>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item is not JsonObject question || Text(question["question"]) is not { Length: > 0 } text || !seen.Add(text))
            {
                return [];
            }
            var options = (question["options"] as JsonArray)?
                .OfType<JsonObject>()
                .Select(option => (Label: Text(option["label"]), Description: Text(option["description"]) ?? string.Empty))
                .Where(option => option.Label is { Length: > 0 })
                .Select(option => new AgentOption(option.Label!, option.Description))
                .ToList() ?? [];
            var multi = question["multiSelect"] is JsonValue flag && flag.GetValueKind() == JsonValueKind.True;
            questions.Add(new AgentQuestion(text, Text(question["header"]) ?? string.Empty, multi, options));
        }
        return questions;
    }

    /// <summary>
    /// One question's answer: the picked labels in the order Claude listed them, then the owner's own words. A
    /// single-choice question takes the owner's words over a pick. Null when nothing is chosen.
    /// </summary>
    public static string? Compose(AgentQuestion question, IReadOnlyCollection<string> picked, string? ownText)
    {
        var own = ownText?.Trim() ?? string.Empty;
        var labels = question.Options.Select(option => option.Label).Where(picked.Contains).ToList();
        if (!question.MultiSelect)
        {
            return own.Length > 0 ? own : labels.FirstOrDefault();
        }
        if (own.Length > 0)
        {
            labels.Add(own);
        }
        return labels.Count > 0 ? string.Join(Separator, labels) : null;
    }

    /// <summary>Each question's answer keyed by its text, or null until every question has one.</summary>
    public static IReadOnlyDictionary<string, string>? Answers(IReadOnlyList<AgentQuestion> questions, IReadOnlyList<string?> composed)
    {
        if (questions.Count == 0 || composed.Count != questions.Count || composed.Any(string.IsNullOrWhiteSpace))
        {
            return null;
        }
        return questions.Zip(composed).ToDictionary(pair => pair.First.Text, pair => pair.Second!, StringComparer.Ordinal);
    }

    /// <summary>What Claude reads when the owner's words cannot go back as answers: each question and its reply.</summary>
    public static string AsDenialMessage(IReadOnlyDictionary<string, string> answers) =>
        "The user answered in Faqra instead of picking an option:\n"
        + string.Join("\n", answers.Select(pair => $"Q: {pair.Key}\nA: {pair.Value}"));

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
```

- [ ] **Step 4: Write `AgentRequest`**

`src/Faqra.Core/Agents/AgentRequest.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

public enum AgentRequestKind { Approval, Question }

/// <summary>A permission request a card shows: what Claude wants, and what an Always click would save.</summary>
public sealed record AgentRequest(
    string Id,
    string SessionId,
    AgentRequestKind Kind,
    string ToolName,
    string Detail,
    IReadOnlyList<AgentQuestion> Questions,
    IReadOnlyList<string> AlwaysRules,
    bool AlwaysAcceptsEdits,
    DateTimeOffset ReceivedAt)
{
    public bool CanAlways => Kind == AgentRequestKind.Approval && (AlwaysRules.Count > 0 || AlwaysAcceptsEdits);

    /// <summary>The card for a PermissionRequest event. Reads the tool input, so a payload with repeated keys throws ArgumentException.</summary>
    public static AgentRequest From(string id, AgentEvent e, DateTimeOffset now)
    {
        var tool = e.ToolName ?? string.Empty;
        if (tool == "AskUserQuestion")
        {
            return new AgentRequest(id, e.SessionId, AgentRequestKind.Question, tool, string.Empty,
                AgentQuestions.Parse(e.ToolInput), [], false, now);
        }
        return new AgentRequest(id, e.SessionId, AgentRequestKind.Approval, tool, Detail(tool, e.ToolInput), [],
            PermissionReply.AlwaysRuleLabels(e.PermissionSuggestions), PermissionReply.AlwaysAcceptsEdits(e.PermissionSuggestions), now);
    }

    private static string Detail(string tool, JsonObject? input) => tool switch
    {
        "Bash" or "PowerShell" => Field(input, "command"),
        "Read" or "Edit" or "MultiEdit" or "Write" => Field(input, "file_path"),
        "NotebookEdit" => Field(input, "notebook_path"),
        "WebFetch" => Field(input, "url"),
        "WebSearch" => Field(input, "query"),
        "Grep" or "Glob" => Field(input, "pattern"),
        _ => Field(input, "description"),
    };

    private static string Field(JsonObject? input, string key) =>
        input?[key] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : string.Empty;
}
```

- [ ] **Step 5: Extend the board**

In `src/Faqra.Core/Agents/AgentBoard.cs`, replace the `AgentSession` record with:

```csharp
public sealed record AgentSession(
    string Id,
    string Agent,
    string Cwd,
    AgentState State,
    ImmutableList<AgentStep> Steps,
    string? LastMessage,
    int Subagents,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? FinishedAt)
{
    /// <summary>The conversation's name in Claude Code (the owner's rename, else Claude's title), once known.</summary>
    public string? Title { get; init; }

    /// <summary>The owner's last prompt, as the relay forwarded it (up to 2,000 characters).</summary>
    public string? LastPrompt { get; init; }

    /// <summary>Claude finished a turn with something to say and the owner has not opened it yet.</summary>
    public bool Unread { get; init; }

    /// <summary>The folder the session runs in, as the owner calls the project.</summary>
    public string Project => Cwd.Length == 0 ? Id : Path.GetFileName(Cwd.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : Cwd;

    /// <summary>What the island calls the session: its conversation's name, else its folder.</summary>
    public string Name => Title is { Length: > 0 } title ? title : Project;
}
```

Replace the `"UserPromptSubmit"` and `"Stop"` arms of `Next` with:

```csharp
        "UserPromptSubmit" => Step(s with { State = AgentState.Thinking, FinishedAt = null, LastPrompt = e.Prompt ?? s.LastPrompt, Unread = false }, now, AgentStepKind.Prompt, OneLine(e.Prompt ?? string.Empty, PromptChars)),
```

```csharp
        "Stop" => Finished(s, e, now),
```

and add these members to `AgentBoard` (after `Tick`):

```csharp
    /// <summary>The owner answered a waiting request: the tool runs (allowed) or Claude reads the refusal (denied).</summary>
    public AgentBoard Answered(string sessionId, bool allowed, DateTimeOffset now) =>
        Sessions.TryGetValue(sessionId, out var s) && s.State is AgentState.Approval or AgentState.Question
            ? this with { Sessions = Sessions.SetItem(sessionId, s with { State = allowed ? AgentState.Working : AgentState.Thinking, UpdatedAt = now }) }
            : this;

    /// <summary>Names a session after its conversation; the same board when nothing changes.</summary>
    public AgentBoard Titled(string sessionId, string title) =>
        Sessions.TryGetValue(sessionId, out var s) && s.Title != title
            ? this with { Sessions = Sessions.SetItem(sessionId, s with { Title = title }) }
            : this;

    /// <summary>The owner has seen the session's latest answer.</summary>
    public AgentBoard MarkRead(string sessionId) =>
        Sessions.TryGetValue(sessionId, out var s) && s.Unread
            ? this with { Sessions = Sessions.SetItem(sessionId, s with { Unread = false }) }
            : this;

    private static AgentSession Finished(AgentSession s, AgentEvent e, DateTimeOffset now)
    {
        var said = e.LastAssistantMessage ?? e.Message;
        return s with
        {
            State = AgentState.Finished,
            FinishedAt = now,
            LastMessage = said ?? s.LastMessage,
            Unread = said is { Length: > 0 },
        };
    }
```

- [ ] **Step 6: Add the request titles and their strings**

In `src/Faqra.Core/Localization/AgentsStrings.cs`, add under `// States`:

```csharp
    // Requests
    public required string ApprovalRun { get; init; }
    public required string ApprovalEdit { get; init; }
    public required string ApprovalFetch { get; init; }
    public required string ApprovalToolFormat { get; init; }
```

In `src/Faqra.Core/Localization/AgentsStrings.EnUS.cs`, add after `StateFinished = "Done",`:

```csharp
        ApprovalRun = "Wants to run a command",
        ApprovalEdit = "Wants to edit a file",
        ApprovalFetch = "Wants to open a web page",
        ApprovalToolFormat = "Wants to use {0}",
```

In `src/Faqra.Core/Agents/AgentsText.cs`, add after `Status`:

```csharp
    /// <summary>A card's headline: what Claude wants, in the owner's words.</summary>
    public static string RequestTitle(AgentRequest request, AgentsStrings s) => request.Kind == AgentRequestKind.Question
        ? s.StateQuestion
        : request.ToolName switch
        {
            "Bash" or "PowerShell" => s.ApprovalRun,
            "Edit" or "MultiEdit" or "Write" or "NotebookEdit" => s.ApprovalEdit,
            "WebFetch" => s.ApprovalFetch,
            _ => Format(s.ApprovalToolFormat, request.ToolName),
        };
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.Core.Tests`
Expected: PASS (every Core test, including A1's board tests).

- [ ] **Step 8: Commit**

```bash
git add src/Faqra.Core/Agents src/Faqra.Core/Localization tests/Faqra.Core.Tests/Agents
git commit -m "feat(windows): agent questions, requests, names and prompts on the board"
```

---
### Task 3: The relay waits for a decision and prints Claude Code's reply

**Files:**
- Modify: `src/Faqra.Hook/Relay.cs` (full replacement below)
- Modify: `src/Faqra.Hook/Program.cs`
- Test: `tests/Faqra.Services.Tests/Agents/RelayTests.cs` (full replacement below; A1's five tests keep their names and meaning, with the new `stdout` argument)

**Interfaces:**
- Consumes: `HookPayload.ToLine` (A1), `AgentDecision.TryParse`, `PermissionReply.Stdout` (Task 1).
- Produces: `Relay.Run(string[] args, Stream stdin, Stream stdout, Func<string, string?> env, string cwd, string pipeName)` returning 0; constants `Relay.ConnectBudgetMs = 300`, `Relay.RunBudgetMs = 2000`, `Relay.DecisionBudgetMs = 110_000`. Protocol: for `PermissionRequest` only, after writing its line the relay reads one line back (up to the first `\n` or the server hanging up, at most 1 MB) and prints `PermissionReply.Stdout(...)` followed by `\n` as UTF-8 without a byte-order mark.

- [ ] **Step 1: Write the failing tests**

Replace `tests/Faqra.Services.Tests/Agents/RelayTests.cs` with:

```csharp
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using Faqra.Hook;

namespace Faqra.Services.Tests.Agents;

public class RelayTests
{
    private static readonly Func<string, string?> NoEnv = _ => null;

    private const string BashRequest = "{\"session_id\":\"s1\",\"hook_event_name\":\"PermissionRequest\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}";
    private const string AllowJson = "{\"hookSpecificOutput\":{\"hookEventName\":\"PermissionRequest\",\"decision\":{\"behavior\":\"allow\"}}}";

    private static string PipeName() => "faqra-test-" + Guid.NewGuid().ToString("N");

    private static Stream Stdin(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    private static NamedPipeServerStream Server(string name, int inBuffer = 0) =>
        new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, inBuffer, 0);

    /// <summary>Plays Faqra for one connection: reads the relay's line, waits, writes <paramref name="reply"/> (or nothing) and hangs up.</summary>
    private static Task<string?> FaqraAnswers(NamedPipeServerStream server, string? reply, int delayMs = 0) => Task.Run(async () =>
    {
        string? line = null;
        try
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
            line = await reader.ReadLineAsync();
            await Task.Delay(delayMs);
            if (reply is not null)
            {
                await server.WriteAsync(Encoding.UTF8.GetBytes(reply + "\n"));
                await server.FlushAsync();
            }
        }
        catch (IOException)
        {
            // The relay already left, which some tests expect.
        }
        server.Dispose();
        return line;
    });

    private static string Printed(MemoryStream stdout) => Encoding.UTF8.GetString(stdout.ToArray());

    [Fact]
    public async Task SendsOneTrimmedLine()
    {
        var name = PipeName();
        using var server = Server(name);
        var received = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            return await reader.ReadLineAsync();
        });

        var exit = Relay.Run(["PreToolUse"], Stdin("{\"session_id\":\"s1\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}"), Stream.Null, NoEnv, @"C:\work", name);

        Assert.Equal(0, exit);
        var line = JsonNode.Parse((await received.WaitAsync(TimeSpan.FromSeconds(5)))!)!;
        Assert.Equal("PreToolUse", line["hook_event_name"]!.GetValue<string>());
        Assert.Equal("claude", line["faqra_agent"]!.GetValue<string>());
        Assert.Equal("npm test", line["tool_input"]!["command"]!.GetValue<string>());
    }

    [Fact]
    public void ExitsAtOnceWhenFaqraIsClosed()
    {
        Relay.Run(["Stop"], Stdin("{}"), Stream.Null, NoEnv, @"C:\work", PipeName()); // warm up the JIT
        var clock = Stopwatch.StartNew();
        Assert.Equal(0, Relay.Run(["PermissionRequest"], Stdin(BashRequest), Stream.Null, NoEnv, @"C:\work", PipeName()));
        // No pipe means no Faqra: even a permission request never waits out the 300 ms connect budget.
        Assert.InRange(clock.ElapsedMilliseconds, 0, 250);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1]")]
    public async Task SendsNothingForGarbage(string stdin)
    {
        var name = PipeName();
        using var server = Server(name);
        var connected = server.WaitForConnectionAsync();
        Assert.Equal(0, Relay.Run(["PermissionRequest"], Stdin(stdin), Stream.Null, NoEnv, @"C:\work", name));
        var winner = await Task.WhenAny(connected, Task.Delay(500));
        Assert.NotSame(connected, winner);
    }

    [Fact]
    public async Task GivesUpOnAServerThatNeverReads()
    {
        var name = PipeName();
        using var server = Server(name, inBuffer: 4096);
        var connected = server.WaitForConnectionAsync();
        // Every field stays under the 2,000-character cut, so the line stays far larger than the pipe's buffer.
        var many = "{\"session_id\":\"s1\"," + string.Join(",", Enumerable.Range(0, 2000).Select(i => $"\"f{i}\":\"{new string('x', 1500)}\"")) + "}";
        var clock = Stopwatch.StartNew();
        Assert.Equal(0, Relay.Run(["UserPromptSubmit"], Stdin(many), Stream.Null, NoEnv, @"C:\work", name));
        Assert.InRange(clock.ElapsedMilliseconds, Relay.RunBudgetMs - 100, Relay.RunBudgetMs + 1500);
        await connected;
    }

    [Fact]
    public void GivesUpOnAStdinThatNeverCloses()
    {
        using var stdin = new BlockingStream();
        try
        {
            var clock = Stopwatch.StartNew();
            Assert.Equal(0, Relay.Run(["PermissionRequest"], stdin, Stream.Null, NoEnv, @"C:\work", PipeName()));
            Assert.InRange(clock.ElapsedMilliseconds, 0, Relay.RunBudgetMs + 1500);
        }
        finally
        {
            stdin.Release();
        }
    }

    [Fact]
    public async Task PrintsTheOwnersAllow()
    {
        var name = PipeName();
        var faqra = FaqraAnswers(Server(name), "{\"decision\":\"allow\"}");
        using var stdout = new MemoryStream();
        Assert.Equal(0, Relay.Run(["PermissionRequest"], Stdin(BashRequest), stdout, NoEnv, @"C:\work", name));
        Assert.Equal(AllowJson + "\n", Printed(stdout));
        Assert.Contains("\"tool_name\":\"Bash\"", await faqra);
    }

    [Fact]
    public async Task PrintsNothingWhenFaqraLetsGo()
    {
        var name = PipeName();
        var faqra = FaqraAnswers(Server(name), reply: null);
        using var stdout = new MemoryStream();
        var clock = Stopwatch.StartNew();
        Assert.Equal(0, Relay.Run(["PermissionRequest"], Stdin(BashRequest), stdout, NoEnv, @"C:\work", name));
        Assert.InRange(clock.ElapsedMilliseconds, 0, 1000);
        Assert.Empty(stdout.ToArray());
        await faqra;
    }

    [Fact]
    public async Task WaitsBeyondTheRunBudgetForTheOwner()
    {
        var name = PipeName();
        var faqra = FaqraAnswers(Server(name), "{\"decision\":\"deny\"}", delayMs: Relay.RunBudgetMs + 600);
        using var stdout = new MemoryStream();
        Relay.Run(["PermissionRequest"], Stdin(BashRequest), stdout, NoEnv, @"C:\work", name);
        Assert.Contains("\"behavior\":\"deny\"", Printed(stdout));
        await faqra;
    }

    [Fact]
    public async Task AnEventNobodyWaitsOnNeverPrints()
    {
        var name = PipeName();
        var faqra = FaqraAnswers(Server(name), "{\"decision\":\"allow\"}", delayMs: 200);
        using var stdout = new MemoryStream();
        var clock = Stopwatch.StartNew();
        Relay.Run(["PreToolUse"], Stdin("{\"session_id\":\"s1\",\"tool_name\":\"Bash\"}"), stdout, NoEnv, @"C:\work", name);
        Assert.InRange(clock.ElapsedMilliseconds, 0, 1000);
        Assert.Empty(stdout.ToArray());
        await faqra;
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"decision\":\"maybe\"}")]
    [InlineData("{\"permissionDecision\":\"allow\"}")]
    public async Task AnythingButADecisionPrintsNothing(string reply)
    {
        var name = PipeName();
        var faqra = FaqraAnswers(Server(name), reply);
        using var stdout = new MemoryStream();
        Relay.Run(["PermissionRequest"], Stdin(BashRequest), stdout, NoEnv, @"C:\work", name);
        Assert.Empty(stdout.ToArray());
        await faqra;
    }

    [Fact]
    public async Task AQuestionTakesNoPlainAllow()
    {
        var name = PipeName();
        var faqra = FaqraAnswers(Server(name), "{\"decision\":\"allow\"}");
        using var stdout = new MemoryStream();
        Relay.Run(["PermissionRequest"], Stdin("{\"session_id\":\"s1\",\"hook_event_name\":\"PermissionRequest\",\"tool_name\":\"AskUserQuestion\",\"tool_input\":{\"questions\":[{\"question\":\"Q?\"}]}}"), stdout, NoEnv, @"C:\work", name);
        Assert.Empty(stdout.ToArray());
        await faqra;
    }

    [Fact]
    public async Task TheEventNameMayComeFromTheCommandLine()
    {
        var name = PipeName();
        var faqra = FaqraAnswers(Server(name), "{\"decision\":\"allow\"}");
        using var stdout = new MemoryStream();
        Relay.Run(["PermissionRequest"], Stdin("{\"session_id\":\"s1\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"ls\"}}"), stdout, NoEnv, @"C:\work", name);
        Assert.Equal(AllowJson + "\n", Printed(stdout));
        await faqra;
    }

    [Fact]
    public async Task PrintsAnswersAsUtf8WithTheQuestionWhole()
    {
        var question = "Quelle option préférez-vous ? " + new string('é', 3000);
        var request = new JsonObject
        {
            ["session_id"] = "s1",
            ["hook_event_name"] = "PermissionRequest",
            ["tool_name"] = "AskUserQuestion",
            ["tool_input"] = new JsonObject
            {
                ["questions"] = new JsonArray(new JsonObject { ["question"] = question, ["options"] = new JsonArray(new JsonObject { ["label"] = "Oui" }) }),
            },
        }.ToJsonString();
        var reply = new JsonObject { ["decision"] = "answer", ["answers"] = new JsonObject { [question] = "Ni l'un ni l'autre 🙂" } }.ToJsonString();
        var name = PipeName();
        var faqra = FaqraAnswers(Server(name), reply);
        using var stdout = new MemoryStream();

        Relay.Run(["PermissionRequest"], Stdin(request), stdout, NoEnv, @"C:\work", name);

        var bytes = stdout.ToArray();
        Assert.Equal((byte)'{', bytes[0]); // no byte-order mark
        var input = JsonNode.Parse(Encoding.UTF8.GetString(bytes))!["hookSpecificOutput"]!["decision"]!["updatedInput"]!;
        Assert.Equal(question, input["questions"]![0]!["question"]!.GetValue<string>());
        Assert.Equal("Ni l'un ni l'autre 🙂", input["answers"]![question]!.GetValue<string>());
        await faqra;
    }

    private sealed class BlockingStream : Stream
    {
        private readonly ManualResetEventSlim _gate = new(false);

        public void Release() => _gate.Set();

        public override int Read(byte[] buffer, int offset, int count)
        {
            _gate.Wait();
            return 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Services.Tests --filter "FullyQualifiedName~RelayTests"`
Expected: build FAILS with "No overload for method 'Run' takes 6 arguments".

- [ ] **Step 3: Replace the relay**

`src/Faqra.Hook/Relay.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of coucou-hook (windows/hook/src/main.rs) in Coucou, https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé).

using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Faqra.Core.Agents;

namespace Faqra.Hook;

/// <summary>
/// Claude Code runs this once per hook event. It hands the event to Faqra and gets out of the way: it always exits 0, a
/// session never waits on a Faqra that is closed or slow, and only a permission request the owner decided on the island
/// gets a reply on stdout. Everything else prints nothing, exactly as if Faqra did not exist.
/// </summary>
public static partial class Relay
{
    public const int ConnectBudgetMs = 300;

    /// <summary>The whole run for an event nobody waits on: read stdin, connect and send.</summary>
    public const int RunBudgetMs = 2000;

    /// <summary>How long a permission request waits for the owner. Faqra lets go at 108 s; Claude Code's hook timeout is 120 s.</summary>
    public const int DecisionBudgetMs = 110_000;

    /// <summary>Larger than any hook payload; anything bigger is not read whole.</summary>
    private const int MaxStdinBytes = 32 * 1024 * 1024;

    private const int MaxReplyBytes = 1024 * 1024;

    public static int Run(string[] args, Stream stdin, Stream stdout, Func<string, string?> env, string cwd, string pipeName)
    {
        var clock = Stopwatch.StartNew();
        // The work runs on pool (background) threads, so a blocked read cannot keep the process alive once Main returns.
        var prepared = Task.Run(() => Prepare(args.Length > 0 ? args[0] : string.Empty, stdin, env, cwd));
        if (!prepared.Wait(RunBudgetMs) || prepared.Result is not { } outgoing)
        {
            return 0;
        }
        var talk = Task.Run(() => Talk(pipeName, outgoing.Line, outgoing.WaitsForAnswer));
        var budget = outgoing.WaitsForAnswer ? DecisionBudgetMs : Math.Max(0, RunBudgetMs - (int)clock.ElapsedMilliseconds);
        if (!talk.Wait(budget) || !outgoing.WaitsForAnswer)
        {
            return 0;
        }
        if (PermissionReply.Stdout(outgoing.Request, AgentDecision.TryParse(talk.Result)) is { } reply)
        {
            stdout.Write(Encoding.UTF8.GetBytes(reply + "\n"));
            stdout.Flush();
        }
        return 0;
    }

    /// <summary>The line for Faqra and, for a permission request, the untrimmed payload its reply is built from.</summary>
    private sealed record Outgoing(string Line, JsonObject Request, bool WaitsForAnswer);

    private static Outgoing? Prepare(string eventArg, Stream stdin, Func<string, string?> env, string cwd)
    {
        try
        {
            if (ReadAll(stdin) is not { } text || HookPayload.ToLine(text, eventArg, "claude", env, cwd) is not { } line)
            {
                return null;
            }
            // Only a permission request needs the payload again, whole: Claude reads its own question back.
            var maybe = eventArg == "PermissionRequest" || line.Contains("\"hook_event_name\":\"PermissionRequest\"", StringComparison.Ordinal);
            if (!maybe || JsonNode.Parse(text.TrimStart('\uFEFF')) is not JsonObject request)
            {
                return new Outgoing(line, new JsonObject(), false);
            }
            var name = request["hook_event_name"] is JsonValue value && value.GetValueKind() == JsonValueKind.String
                ? value.GetValue<string>()
                : eventArg;
            return new Outgoing(line, request, name == "PermissionRequest");
        }
        catch (Exception)
        {
            // A hook must never fail the session: whatever goes wrong, it carries on as if Faqra did not exist.
            return null;
        }
    }

    /// <summary>Sends the line and, when asked to, reads Faqra's one-line answer. Null for no answer, whatever the reason.</summary>
    private static string? Talk(string pipeName, string line, bool waitsForAnswer)
    {
        try
        {
            // NamedPipeClientStream.Connect keeps retrying a pipe that does not exist until its timeout, and
            // Faqra being closed is the common case. WaitNamedPipe returns at once when there is no pipe and
            // waits only while every instance is busy, so a false answer means: give up now.
            if (!WaitNamedPipe(@"\\.\pipe\" + pipeName, ConnectBudgetMs))
            {
                return null;
            }
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
            pipe.Connect(ConnectBudgetMs);
            pipe.Write(Encoding.UTF8.GetBytes(line + "\n"));
            pipe.Flush();
            return waitsForAnswer ? ReadReply(pipe) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Up to the first newline, or until Faqra hangs up; nothing when it hung up without a word.</summary>
    private static string? ReadReply(Stream pipe)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = pipe.Read(chunk, 0, chunk.Length)) > 0)
        {
            var newline = Array.IndexOf(chunk, (byte)'\n', 0, read);
            buffer.Write(chunk, 0, newline >= 0 ? newline : read);
            if (newline >= 0 || buffer.Length > MaxReplyBytes)
            {
                break;
            }
        }
        return buffer.Length == 0 || buffer.Length > MaxReplyBytes
            ? null
            : Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    [LibraryImport("kernel32.dll", EntryPoint = "WaitNamedPipeW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WaitNamedPipe(string name, uint timeoutMs);

    private static string? ReadAll(Stream stdin)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int read;
        while ((read = stdin.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > MaxStdinBytes)
            {
                return null;
            }
            buffer.Write(chunk, 0, read);
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
```

- [ ] **Step 4: Pass the raw stdout stream from `Program`**

In `src/Faqra.Hook/Program.cs`, replace the `return Relay.Run(...)` line with:

```csharp
            // The raw stream, not Console.Out: Console.Out encodes with the console's code page, and Claude Code
            // reads the reply as UTF-8 (a question or an answer may carry any character).
            using var stdout = Console.OpenStandardOutput();
            return Relay.Run(args, Console.OpenStandardInput(), stdout, env, Environment.CurrentDirectory, AgentPipe.Name(sid, env));
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.Services.Tests --filter "FullyQualifiedName~RelayTests"`
Expected: PASS (17 test cases).

- [ ] **Step 6: Re-measure the relay's start-up**

The relay now parses a permission request twice and references more of `Faqra.Core`; check it still starts inside the spec's 80 ms budget. Run from `Windows/` (PowerShell):

```powershell
dotnet publish src/Faqra.Hook/Faqra.Hook.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:PublishReadyToRun=true -o $env:TEMP\faqra-hook-measure
$exe = "$env:TEMP\faqra-hook-measure\faqra-hook.exe"
$runs = 1..15 | ForEach-Object { (Measure-Command { '{"session_id":"m","hook_event_name":"Stop"}' | & $exe Stop }).TotalMilliseconds }
"median {0:N0} ms" -f (($runs | Sort-Object)[7])
```

Expected: a median within 10 ms of A1's (about 87 ms through PowerShell, which adds about 13 ms of its own). Record the number in the task report.

- [ ] **Step 7: Commit**

```bash
git add src/Faqra.Hook tests/Faqra.Services.Tests/Agents/RelayTests.cs
git commit -m "feat(windows): relay waits for the owner's decision on permission requests"
```

---

### Task 4: The hub holds requests until the owner decides

**Files:**
- Modify: `src/Faqra.Services/Agents/AgentHub.cs` (full replacement below)
- Create: `tests/Faqra.Services.Tests/Agents/HubTestKit.cs`
- Create: `tests/Faqra.Services.Tests/Agents/AgentHubRequestTests.cs`

**Interfaces:**
- Consumes: `AgentDecision`, `AgentRequest.From`, `AgentBoard.Answered`, `AgentBoard.MarkRead`, `AgentSession.Unread` (Tasks 1 and 2).
- Produces on `AgentHub`: `ImmutableList<AgentRequest> Requests`; `event Action<AgentRequest>? RequestArrived`; `event Action<AgentSession>? TurnFinished` (on `Stop` when the session is `Unread`); `Func<bool> CanAsk { get; set; }` (default `() => false`); `bool Answer(string requestId, AgentDecision decision)`; `void Release(string requestId)`; `void MarkRead(string sessionId)`; `void ReplaceRequestsForTests(params AgentRequest[] requests)`; `static readonly TimeSpan DecisionTimeout` (108 s); `internal TimeSpan DecisionWait { get; set; }`. All of `Answer`, `Release`, `MarkRead`, `CanAsk` run on the hub's context thread; `Changed`, `RequestArrived` and `TurnFinished` are raised there. Test kit: `FakeRelay.SendAsync(pipe, line)`, `FakeRelay.ReplyAsync(within)`, `HubTestKit.NewPipe()`, `HubTestKit.OnContext(context, func)`, `HubTestKit.Eventually(condition)`.

- [ ] **Step 1: Write the test kit**

`tests/Faqra.Services.Tests/Agents/HubTestKit.cs`:

```csharp
using System.IO.Pipes;
using System.Text;

namespace Faqra.Services.Tests.Agents;

/// <summary>Plays the relay against a hub: sends one line, then reads the one line Faqra answers (null when it hangs up).</summary>
internal sealed class FakeRelay : IDisposable
{
    private readonly NamedPipeClientStream _pipe;

    private FakeRelay(NamedPipeClientStream pipe) => _pipe = pipe;

    public static async Task<FakeRelay> SendAsync(string pipeName, string line)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
        await pipe.ConnectAsync(2000);
        await pipe.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"));
        await pipe.FlushAsync();
        return new FakeRelay(pipe);
    }

    public async Task<string?> ReplyAsync(TimeSpan within)
    {
        using var reader = new StreamReader(_pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
        return await reader.ReadLineAsync().WaitAsync(within);
    }

    public void Dispose() => _pipe.Dispose();
}

internal static class HubTestKit
{
    public static string NewPipe() => "faqra-test-" + Guid.NewGuid().ToString("N");

    /// <summary>Runs <paramref name="action"/> on the hub's thread, as the island would.</summary>
    public static Task<T> OnContext<T>(SynchronizationContext context, Func<T> action)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Post(_ =>
        {
            try
            {
                done.SetResult(action());
            }
            catch (Exception ex)
            {
                done.SetException(ex);
            }
        }, null);
        return done.Task;
    }

    public static async Task<bool> Eventually(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(50);
        }
        return condition();
    }
}
```

- [ ] **Step 2: Write the failing tests**

`tests/Faqra.Services.Tests/Agents/AgentHubRequestTests.cs`:

```csharp
using System.Diagnostics;
using Faqra.Core.Agents;
using Faqra.Services.Agents;
using static Faqra.Services.Tests.Agents.HubTestKit;

namespace Faqra.Services.Tests.Agents;

public class AgentHubRequestTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(5);

    private const string BashRequest = "{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}";
    private const string Question = "{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"tool_name\":\"AskUserQuestion\",\"tool_input\":{\"questions\":[{\"question\":\"Which one?\",\"options\":[{\"label\":\"A\"},{\"label\":\"B\"}]}]}}";

    private static AgentHub Hub(SerialContext context, string pipe, bool canAsk = true, string? log = null)
    {
        var hub = new AgentHub(pipe, context, () => T0, log) { CanAsk = () => canAsk };
        hub.SetRunning(true);
        return hub;
    }

    [Fact]
    public async Task HoldsARequestUntilTheOwnerAnswers()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        AgentRequest? arrived = null;
        hub.RequestArrived += request => arrived = request;
        using var relay = await FakeRelay.SendAsync(pipe, BashRequest);

        Assert.True(await Eventually(() => hub.Requests.Count == 1));
        var request = hub.Requests[0];
        Assert.Equal("npm test", request.Detail);
        Assert.Same(request, await OnContext(context, () => arrived));
        Assert.Equal(AgentState.Approval, (await OnContext(context, () => hub.Board)).Sessions["s1"].State);

        Assert.True(await OnContext(context, () => hub.Answer(request.Id, AgentDecision.Allow)));
        Assert.Equal("{\"decision\":\"allow\"}", await relay.ReplyAsync(Within));
        Assert.Empty(hub.Requests);
        Assert.Equal(AgentState.Working, (await OnContext(context, () => hub.Board)).Sessions["s1"].State);
        Assert.False(await OnContext(context, () => hub.Answer(request.Id, AgentDecision.Deny())));
    }

    [Fact]
    public async Task LetsGoAtOnceWhenNoCardCanShow()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe, canAsk: false);
        using var relay = await FakeRelay.SendAsync(pipe, BashRequest);
        var clock = Stopwatch.StartNew();

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.InRange(clock.ElapsedMilliseconds, 0, 1000);
        Assert.Empty(hub.Requests);
        // Watching still works: the session shows it needs the owner's OK in Claude Code.
        Assert.Equal(AgentState.Approval, (await OnContext(context, () => hub.Board)).Sessions["s1"].State);
    }

    [Fact]
    public async Task ReleasingSendsNothing()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        using var relay = await FakeRelay.SendAsync(pipe, BashRequest);
        Assert.True(await Eventually(() => hub.Requests.Count == 1));

        await OnContext(context, () => { hub.Release(hub.Requests[0].Id); return 0; });

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.Empty(hub.Requests);
        Assert.Equal(AgentState.Approval, (await OnContext(context, () => hub.Board)).Sessions["s1"].State);
    }

    [Fact]
    public async Task ACardGoesWhenTheRelayGoesAway()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        var relay = await FakeRelay.SendAsync(pipe, BashRequest);
        Assert.True(await Eventually(() => hub.Requests.Count == 1));

        relay.Dispose();

        Assert.True(await Eventually(() => hub.Requests.Count == 0));
    }

    [Fact]
    public async Task ACardGoesWhenTheTurnMovesOn()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        using var relay = await FakeRelay.SendAsync(pipe, BashRequest);
        Assert.True(await Eventually(() => hub.Requests.Count == 1));

        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"Stop\",\"session_id\":\"s1\"}"))
        {
        }

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.Empty(hub.Requests);
    }

    [Fact]
    public async Task AnotherSessionsTurnLeavesTheCardAlone()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        using var relay = await FakeRelay.SendAsync(pipe, BashRequest);
        Assert.True(await Eventually(() => hub.Requests.Count == 1));

        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"Stop\",\"session_id\":\"s2\"}"))
        {
        }

        Assert.True(await Eventually(() => (hub.Board.Sessions.ContainsKey("s2"))));
        Assert.Single(hub.Requests);
    }

    [Fact]
    public async Task ACardTimesOut()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        hub.DecisionWait = TimeSpan.FromMilliseconds(300);
        using var relay = await FakeRelay.SendAsync(pipe, BashRequest);

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.True(await Eventually(() => hub.Requests.Count == 0));
    }

    [Fact]
    public async Task AQuestionArrivesWithItsOptionsAndTakesAnAnswer()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        using var relay = await FakeRelay.SendAsync(pipe, Question);
        Assert.True(await Eventually(() => hub.Requests.Count == 1));
        var request = hub.Requests[0];
        Assert.Equal(AgentRequestKind.Question, request.Kind);
        Assert.Equal(["A", "B"], request.Questions[0].Options.Select(o => o.Label));

        await OnContext(context, () => hub.Answer(request.Id, AgentDecision.Answer(new Dictionary<string, string> { ["Which one?"] = "B" })));

        var reply = AgentDecision.TryParse(await relay.ReplyAsync(Within))!;
        Assert.Equal(AgentDecisionKind.Answer, reply.Kind);
        Assert.Equal("B", reply.Answers!["Which one?"]);
    }

    [Fact]
    public async Task ARepeatedKeyInAQuestionNeverMakesACard()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        using var relay = await FakeRelay.SendAsync(pipe,
            "{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"tool_name\":\"AskUserQuestion\",\"tool_input\":{\"questions\":[{\"question\":\"A?\",\"question\":\"B?\"}]}}");

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.Empty(hub.Requests);
        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"s2\"}"))
        {
        }
        Assert.True(await Eventually(() => hub.Board.Sessions.ContainsKey("s2")));
    }

    [Fact]
    public async Task StoppingLetsEveryRequestGo()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        using var relay = await FakeRelay.SendAsync(pipe, BashRequest);
        Assert.True(await Eventually(() => hub.Requests.Count == 1));

        hub.SetRunning(false);

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.True(await Eventually(() => hub.Requests.Count == 0));
    }

    [Fact]
    public async Task AFinishedTurnWithWordsIsAnnouncedOnce()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        var finished = new List<AgentSession>();
        hub.TurnFinished += finished.Add;

        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"Stop\",\"session_id\":\"s1\",\"last_assistant_message\":\"All done.\"}"))
        {
        }
        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"Stop\",\"session_id\":\"s2\"}"))
        {
        }

        Assert.True(await Eventually(() => hub.Board.Sessions.Count == 2));
        var announced = await OnContext(context, () => finished.ToList());
        Assert.Equal("s1", Assert.Single(announced).Id);
        Assert.True(announced[0].Unread);

        await OnContext(context, () => { hub.MarkRead("s1"); return 0; });
        Assert.False(hub.Board.Sessions["s1"].Unread);
    }

    [Fact]
    public async Task TheLogNamesTheOutcomeButNeverTheAnswer()
    {
        var pipe = NewPipe();
        var log = Path.Combine(Path.GetTempPath(), $"faqra-agents-{Guid.NewGuid():N}.log");
        using var context = new SerialContext();
        using (var hub = Hub(context, pipe, log: log))
        using (var relay = await FakeRelay.SendAsync(pipe, Question))
        {
            Assert.True(await Eventually(() => hub.Requests.Count == 1));
            await OnContext(context, () => hub.Answer(hub.Requests[0].Id, AgentDecision.Answer(new Dictionary<string, string> { ["Which one?"] = "secret answer" })));
            await relay.ReplyAsync(Within);
            await Task.Delay(200);
        }
        var text = File.ReadAllText(log);
        Assert.Contains("Decision s1 AskUserQuestion answered", text);
        Assert.DoesNotContain("secret answer", text);
        Assert.DoesNotContain("Which one?", text);
        File.Delete(log);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Services.Tests --filter "FullyQualifiedName~AgentHubRequestTests"`
Expected: build FAILS with "'AgentHub' does not contain a definition for 'CanAsk'".

- [ ] **Step 4: Replace the hub**

`src/Faqra.Services/Agents/AgentHub.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of Coucou's pipe server (windows/src-tauri/src/pipe.rs), https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé).

using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using Faqra.Core.Agents;

namespace Faqra.Services.Agents;

/// <summary>
/// Listens on the agents pipe and folds every event into <see cref="Board"/>. A permission request keeps its
/// connection open while a card shows it, and the owner's choice goes back on that same connection. The board and the
/// requests are read and replaced only on the given context's thread (the UI thread in the app), and every event the
/// hub raises is raised there.
/// </summary>
public sealed class AgentHub : IDisposable
{
    public const int MaxLineBytes = 1024 * 1024;
    private const long MaxLogBytes = 1024 * 1024;
    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    /// <summary>How long a card waits for the owner: under the relay's 110 s, so Faqra always answers or lets go first.</summary>
    public static readonly TimeSpan DecisionTimeout = TimeSpan.FromSeconds(108);

    /// <summary>Events after which a waiting request no longer matters: the turn moved on without it.</summary>
    private static readonly HashSet<string> TurnEnds = new(StringComparer.Ordinal) { "UserPromptSubmit", "Stop", "StopFailure", "SessionEnd" };

    private readonly string _pipeName;
    private readonly SynchronizationContext _context;
    private readonly Func<DateTimeOffset> _now;
    private readonly string? _logPath;
    private readonly Dictionary<string, TaskCompletionSource<AgentDecision?>> _waiting = new(StringComparer.Ordinal);
    private CancellationTokenSource? _running;
    private Timer? _tick;
    private bool _disposed;

    public AgentHub(string pipeName, SynchronizationContext context, Func<DateTimeOffset> now, string? logPath = null)
    {
        _pipeName = pipeName;
        _context = context;
        _now = now;
        _logPath = logPath;
    }

    public AgentBoard Board { get; private set; } = AgentBoard.Empty;

    /// <summary>The requests cards are showing, oldest first.</summary>
    public ImmutableList<AgentRequest> Requests { get; private set; } = ImmutableList<AgentRequest>.Empty;

    public bool IsRunning => _running is not null;

    public event Action? Changed;

    /// <summary>A request now waits on a card. Raised after <see cref="Changed"/>.</summary>
    public event Action<AgentRequest>? RequestArrived;

    /// <summary>A session finished a turn with something to say. Raised after <see cref="Changed"/>.</summary>
    public event Action<AgentSession>? TurnFinished;

    /// <summary>
    /// Whether a card can show a request right now, asked on the context's thread as each one arrives. When it says no,
    /// the request is let go at once and Claude Code asks in its own UI. Nothing is held until the island sets it.
    /// </summary>
    public Func<bool> CanAsk { get; set; } = () => false;

    /// <summary>How long a card waits; tests shorten it.</summary>
    internal TimeSpan DecisionWait { get; set; } = DecisionTimeout;

    /// <summary>For render tests: shows a prepared board without a pipe. Never called by the app.</summary>
    public void ReplaceBoardForTests(AgentBoard board)
    {
        Board = board;
        Changed?.Invoke();
    }

    /// <summary>For render tests: shows prepared requests without a pipe; nothing waits on them. Never called by the app.</summary>
    public void ReplaceRequestsForTests(params AgentRequest[] requests)
    {
        Requests = ImmutableList.Create(requests);
        Changed?.Invoke();
    }

    /// <summary>Sends the owner's choice to the waiting relay; false when the request no longer waits. Call on the context's thread.</summary>
    public bool Answer(string requestId, AgentDecision decision) => Settle(requestId, decision);

    /// <summary>Lets a request go without a choice, so Claude Code asks in its own UI. Call on the context's thread.</summary>
    public void Release(string requestId) => Settle(requestId, null);

    /// <summary>The owner has seen a session's latest answer. Call on the context's thread.</summary>
    public void MarkRead(string sessionId)
    {
        var next = Board.MarkRead(sessionId);
        if (!ReferenceEquals(next, Board))
        {
            Board = next;
            Changed?.Invoke();
        }
    }

    /// <summary>Starts or stops listening; stopping forgets every session and lets every request go. Call on the context's thread.</summary>
    public void SetRunning(bool running)
    {
        if (running)
        {
            Start();
        }
        else
        {
            Stop();
        }
    }

    private void Start()
    {
        if (_running is not null || _disposed)
        {
            return;
        }
        _running = new CancellationTokenSource();
        var token = _running.Token;
        _ = Task.Run(() => AcceptLoop(token));
        _tick = new Timer(_ => Post(TickBoard), null, TickInterval, TickInterval);
    }

    private void Stop()
    {
        if (_running is null)
        {
            return;
        }
        _running.Cancel();
        _running.Dispose();
        _running = null;
        _tick?.Dispose();
        _tick = null;
        Post(() =>
        {
            foreach (var waiting in _waiting.Values)
            {
                waiting.TrySetResult(null);
            }
            _waiting.Clear();
            Requests = Requests.Clear();
            Board = AgentBoard.Empty;
            Changed?.Invoke();
        });
    }

    private async Task AcceptLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                server?.Dispose();
                return;
            }
            catch (Exception ex)
            {
                // Another process holds the name, a burst used every instance, or something unexpected: wait and try again.
                server?.Dispose();
                Trace.TraceWarning($"Faqra agents pipe: {ex.Message}");
                try
                {
                    await Task.Delay(RetryDelay, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                continue;
            }
            var connection = server;
            _ = Task.Run(() => Serve(connection, token), CancellationToken.None);
        }
    }

    /// <summary>Test seam: runs after a line parsed and before it is posted to the context.</summary>
    internal Action? BeforePost { get; set; }

    private async Task Serve(NamedPipeServerStream connection, CancellationToken token)
    {
        using (connection)
        {
            string? line;
            try
            {
                line = await ReadLine(connection, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            {
                return;
            }
            if (line is null || AgentEvent.TryParse(line) is not { } e)
            {
                return;
            }
            BeforePost?.Invoke();
            if (e.Event == "PermissionRequest")
            {
                await ServeRequest(connection, e, token).ConfigureAwait(false);
                return;
            }
            Post(() =>
            {
                // A run that was stopped meanwhile must not put sessions back on the emptied board.
                if (token.IsCancellationRequested)
                {
                    return;
                }
                Board = Board.Apply(e, _now());
                Log(e);
                if (TurnEnds.Contains(e.Event))
                {
                    ReleaseSession(e.SessionId);
                }
                Changed?.Invoke();
                if (e.Event == "Stop" && Board.Sessions.TryGetValue(e.SessionId, out var session) && session.Unread)
                {
                    TurnFinished?.Invoke(session);
                }
            });
        }
    }

    /// <summary>
    /// Shows the request on a card and waits for the owner, the relay hanging up (Claude Code took the answer in its own
    /// UI, or ended the hook), the turn moving on, the timeout, or a stop. Only a choice is written back; anything else
    /// closes the connection without a word, and Claude Code asks in its own UI.
    /// </summary>
    private async Task ServeRequest(NamedPipeServerStream connection, AgentEvent e, CancellationToken token)
    {
        var id = Guid.NewGuid().ToString("N");
        var decided = new TaskCompletionSource<AgentDecision?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var shown = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() => shown.TrySetResult(!token.IsCancellationRequested && Open(id, e, decided)));
        bool isShown;
        try
        {
            isShown = await shown.Task.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (!isShown)
        {
            return;
        }

        AgentDecision? decision;
        using (var waiting = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            var hangUp = HangUp(connection, waiting.Token);
            var timeout = Task.Delay(DecisionWait, waiting.Token);
            var first = await Task.WhenAny(decided.Task, hangUp, timeout).ConfigureAwait(false);
            decision = first == decided.Task ? await decided.Task.ConfigureAwait(false) : null;
            // Stop watching for the hang-up before writing on the same pipe.
            waiting.Cancel();
            await hangUp.ConfigureAwait(false);
        }
        if (decision is null)
        {
            Post(() => Settle(id, null));
            return;
        }
        try
        {
            await connection.WriteAsync(Encoding.UTF8.GetBytes(decision.ToLine() + "\n")).ConfigureAwait(false);
            await connection.FlushAsync().ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The relay left between the click and the write: Claude Code asks in its own UI.
        }
    }

    /// <summary>Folds the request into the board and, when a card can show it, starts waiting on it. Runs on the context's thread.</summary>
    private bool Open(string id, AgentEvent e, TaskCompletionSource<AgentDecision?> decided)
    {
        var now = _now();
        Board = Board.Apply(e, now);
        Log(e);
        AgentRequest? request = null;
        if (CanAsk())
        {
            try
            {
                request = AgentRequest.From(id, e, now);
            }
            catch (ArgumentException)
            {
                // Repeated keys in the tool input: no card for a request Faqra cannot read whole.
            }
        }
        if (request is not null)
        {
            Requests = Requests.Add(request);
            _waiting[id] = decided;
        }
        Changed?.Invoke();
        if (request is not null)
        {
            RequestArrived?.Invoke(request);
        }
        return request is not null;
    }

    /// <summary>Ends a waiting request with the owner's choice, or none. Runs on the context's thread.</summary>
    private bool Settle(string requestId, AgentDecision? decision)
    {
        if (!_waiting.Remove(requestId, out var waiting))
        {
            return false;
        }
        var request = Requests.Find(r => r.Id == requestId);
        Requests = Requests.RemoveAll(r => r.Id == requestId);
        if (decision is not null && request is not null)
        {
            Board = Board.Answered(request.SessionId, allowed: decision.Kind != AgentDecisionKind.Deny, _now());
        }
        waiting.TrySetResult(decision);
        if (request is not null)
        {
            LogDecision(request, decision);
        }
        Changed?.Invoke();
        return true;
    }

    private void ReleaseSession(string sessionId)
    {
        foreach (var request in Requests.Where(r => r.SessionId == sessionId).ToList())
        {
            Settle(request.Id, null);
        }
    }

    /// <summary>Completes when the relay closes its end: it exited, or Claude Code ended the hook.</summary>
    private static async Task HangUp(Stream connection, CancellationToken token)
    {
        var one = new byte[1];
        try
        {
            while (await connection.ReadAsync(one, token).ConfigureAwait(false) > 0)
            {
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // Cancelled because the wait is over, or the pipe broke because the relay left: either way, done.
        }
    }

    private static async Task<string?> ReadLine(Stream stream, CancellationToken token)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(ReadBudget);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, budget.Token).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            var newline = Array.IndexOf(chunk, (byte)'\n', 0, read);
            buffer.Write(chunk, 0, newline >= 0 ? newline : read);
            if (newline >= 0)
            {
                break;
            }
            if (buffer.Length > MaxLineBytes)
            {
                return null;
            }
        }
        return buffer.Length == 0 ? null : Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private void TickBoard()
    {
        var next = Board.Tick(_now());
        if (!ReferenceEquals(next, Board))
        {
            Board = next;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// One line per event: time, event, session, tool and the state it led to. Never the prompt, command, path,
    /// answer or Claude's words, so the log can be shared when something goes wrong.
    /// </summary>
    private void Log(AgentEvent e)
    {
        var state = Board.Sessions.TryGetValue(e.SessionId, out var s) ? s.State.ToString() : "gone";
        WriteLog($"{_now():O} {e.Event} {Short(e.SessionId)} {e.ToolName ?? "-"} {state}");
    }

    /// <summary>What became of a request: allow, always, deny, answered or released. Never the answer itself.</summary>
    private void LogDecision(AgentRequest request, AgentDecision? decision)
    {
        var outcome = decision?.Kind switch
        {
            AgentDecisionKind.Allow => "allow",
            AgentDecisionKind.Always => "always",
            AgentDecisionKind.Deny => "deny",
            AgentDecisionKind.Answer => "answered",
            _ => "released",
        };
        WriteLog($"{_now():O} Decision {Short(request.SessionId)} {request.ToolName} {outcome}");
    }

    private static string Short(string sessionId) => sessionId.Length > 8 ? sessionId[..8] : sessionId;

    private void WriteLog(string line)
    {
        if (_logPath is null)
        {
            return;
        }
        try
        {
            if (File.Exists(_logPath) && new FileInfo(_logPath).Length > MaxLogBytes)
            {
                File.Move(_logPath, _logPath + ".1", overwrite: true);
            }
            File.AppendAllText(_logPath, line + "\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Faqra agents log: {ex.Message}");
        }
    }

    private void Post(Action action) => _context.Post(_ =>
    {
        if (!_disposed)
        {
            action();
        }
    }, null);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        Stop();
        _disposed = true;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.Services.Tests`
Expected: PASS (every Services test, including A1's `AgentHubTests`).

- [ ] **Step 6: Commit**

```bash
git add src/Faqra.Services/Agents/AgentHub.cs tests/Faqra.Services.Tests/Agents/HubTestKit.cs tests/Faqra.Services.Tests/Agents/AgentHubRequestTests.cs
git commit -m "feat(windows): agent hub holds permission requests until the owner decides"
```

---
### Task 5: Session names and windows

**Files:**
- Create: `src/Faqra.Core/Agents/ProcessTree.cs`
- Create: `src/Faqra.Win32/Agents/ProcessSnapshot.cs`
- Create: `src/Faqra.Win32/Agents/PipeClient.cs`
- Create: `src/Faqra.Services/Agents/SessionTitles.cs`
- Create: `src/Faqra.Services/Agents/SessionWindows.cs`
- Create: `src/Faqra.Services/Agents/SessionWindowCache.cs`
- Modify: `src/Faqra.Services/Agents/AgentHub.cs`
- Test: `tests/Faqra.Core.Tests/Agents/ProcessTreeTests.cs`, `tests/Faqra.Services.Tests/Agents/SessionTitlesTests.cs`, `tests/Faqra.Services.Tests/Agents/SessionWindowsTests.cs`, `tests/Faqra.Services.Tests/Agents/AgentHubSessionTests.cs`

**Interfaces:**
- Consumes: `AgentEvent.TranscriptPath` (Task 1), `AgentBoard.Titled` (Task 2), the Task 4 hub, A1/M6's `OpenWindows.Enumerate()` and `OpenWindows.Activate(IntPtr)` (`src/Faqra.Win32/Windows/OpenWindows.cs`), `FakeRelay`/`HubTestKit` (Task 4).
- Produces: `readonly record struct ProcessEntry(int ParentId, string Executable)`; `static class ProcessTree` with `const int MaxDepth = 16`, `IReadOnlyList<int> Ancestors(IReadOnlyDictionary<int, ProcessEntry> table, int start)`, `int? FirstOwner(IReadOnlyList<int> ancestors, Func<int, bool> ownsWindow)`, `int PickWindow(IReadOnlyList<string> titles, string folder)` (-1 when none). Win32: `readonly record struct ProcessRecord(int Id, int ParentId, string Executable)`, `ProcessSnapshot.Take()`, `PipeClient.ProcessId(SafeHandle pipe)`. Services: `static class SessionTitles` with `const int MaxTitleLength = 120`, `string DefaultProjectsRoot`, `string? Read(string? transcriptPath, string projectsRoot)`; `readonly record struct WindowLookup(bool Settled, int? Owner)`; `static class SessionWindows` with `WindowLookup Locate(int relayPid)` and `bool BringForward(int ownerPid, string project)`. `AgentHub` constructor becomes `AgentHub(string pipeName, SynchronizationContext context, Func<DateTimeOffset> now, string? logPath = null, Func<int, WindowLookup>? locateWindow = null, string? projectsRoot = null)` and gains `int? WindowOwner(string sessionId)` and `void RememberWindowForTests(string sessionId, int owner)`.

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.Core.Tests/Agents/ProcessTreeTests.cs`:

```csharp
using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

// Ported from the tests in Coucou's windows/src-tauri/src/session_window.rs.
public class ProcessTreeTests
{
    private static Dictionary<int, ProcessEntry> Tree(params (int Id, int Parent, string Exe)[] entries) =>
        entries.ToDictionary(entry => entry.Id, entry => new ProcessEntry(entry.Parent, entry.Exe));

    [Fact]
    public void WindowsTerminalIsFoundAboveTheShell()
    {
        var procs = Tree((4, 0, "System"), (100, 4, "explorer.exe"), (200, 100, "WindowsTerminal.exe"), (300, 200, "pwsh.exe"), (400, 300, "claude.exe"), (600, 400, "faqra-hook.exe"));
        var ancestors = ProcessTree.Ancestors(procs, 600);
        Assert.Equal([400, 300, 200], ancestors);
        Assert.Equal(200, ProcessTree.FirstOwner(ancestors, pid => pid is 200 or 100));
    }

    [Fact]
    public void AntigravityIsFoundThroughItsExtensionHost()
    {
        var procs = Tree((100, 1, "explorer.exe"), (210, 100, "Antigravity.exe"), (220, 210, "Antigravity.exe"), (400, 220, "claude.exe"), (600, 400, "faqra-hook.exe"));
        Assert.Equal(210, ProcessTree.FirstOwner(ProcessTree.Ancestors(procs, 600), pid => pid == 210));
    }

    [Fact]
    public void AClassicConsoleFindsNothingRatherThanTheDesktop()
    {
        // cmd.exe's window belongs to conhost, which is no ancestor: the walk stops at explorer instead of the taskbar.
        var procs = Tree((100, 1, "explorer.exe"), (300, 100, "cmd.exe"), (400, 300, "claude.exe"), (600, 400, "faqra-hook.exe"));
        Assert.Null(ProcessTree.FirstOwner(ProcessTree.Ancestors(procs, 600), pid => pid == 100));
    }

    [Fact]
    public void AGoneRelayALoopOrADeepChainEndsTheWalk()
    {
        Assert.Empty(ProcessTree.Ancestors(Tree(), 600));
        Assert.Equal([1, 2], ProcessTree.Ancestors(Tree((1, 2, "a.exe"), (2, 1, "b.exe"), (3, 1, "faqra-hook.exe")), 3));
        var deep = Enumerable.Range(1, 99).Select(i => (i, i + 1, "x.exe")).ToArray();
        Assert.Equal(ProcessTree.MaxDepth, ProcessTree.Ancestors(Tree(deep), 1).Count);
    }

    [Fact]
    public void TreeTopsMatchWhateverTheCase() =>
        Assert.Empty(ProcessTree.Ancestors(Tree((100, 1, "EXPLORER.EXE"), (600, 100, "faqra-hook.exe")), 600));

    [Fact]
    public void TheWindowNamedAfterTheProjectWins()
    {
        string[] titles = ["notes - Visual Studio Code", "app.ts - Faqra - Antigravity"];
        Assert.Equal(1, ProcessTree.PickWindow(titles, "faqra"));
        Assert.Equal(0, ProcessTree.PickWindow(titles, "other"));
        Assert.Equal(0, ProcessTree.PickWindow(titles, ""));
        Assert.Equal(-1, ProcessTree.PickWindow([], "x"));
    }
}
```

`tests/Faqra.Services.Tests/Agents/SessionTitlesTests.cs`:

```csharp
using Faqra.Services.Agents;

namespace Faqra.Services.Tests.Agents;

public class SessionTitlesTests
{
    private static string Root() => Directory.CreateTempSubdirectory("faqra-projects-").FullName;

    private static string Transcript(string root, params string[] lines)
    {
        var folder = Path.Combine(root, "C--code-faqra");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".jsonl");
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void TheOwnersNameBeatsClaudesTitle()
    {
        var root = Root();
        var path = Transcript(root,
            "{\"type\":\"custom-title\",\"customTitle\":\"Instagram transcripts\",\"sessionId\":\"s\"}",
            "{\"type\":\"user\",\"message\":{\"content\":\"hi\"}}",
            "{\"type\":\"ai-title\",\"aiTitle\":\"Extracting a video transcript\",\"sessionId\":\"s\"}");
        Assert.Equal("Instagram transcripts", SessionTitles.Read(path, root));
    }

    [Fact]
    public void TheNewestTitleWins()
    {
        var root = Root();
        var path = Transcript(root,
            "{\"type\":\"ai-title\",\"aiTitle\":\"Faqra milestone 5\",\"sessionId\":\"s\"}",
            "{\"type\":\"ai-title\",\"aiTitle\":\"Faqra milestones 5 and 6\",\"sessionId\":\"s\"}");
        Assert.Equal("Faqra milestones 5 and 6", SessionTitles.Read(path, root));
    }

    [Fact]
    public void ATitleFarBeforeTheTailIsStillFound()
    {
        var root = Root();
        var filler = Enumerable.Range(0, 3000).Select(_ => $"{{\"type\":\"assistant\",\"text\":\"{new string('x', 200)}\"}}");
        var path = Transcript(root, ["{\"type\":\"ai-title\",\"aiTitle\":\"Early title\",\"sessionId\":\"s\"}", .. filler]);
        Assert.Equal("Early title", SessionTitles.Read(path, root));
    }

    [Fact]
    public void ANameIsOneShortLine()
    {
        var root = Root();
        var path = Transcript(root, $"{{\"type\":\"ai-title\",\"aiTitle\":\"Two\\nlines {new string('w', 300)}\"}}");
        var name = SessionTitles.Read(path, root)!;
        Assert.DoesNotContain('\n', name);
        Assert.StartsWith("Two lines", name);
        Assert.True(name.Length <= SessionTitles.MaxTitleLength);
        Assert.EndsWith("…", name);
    }

    [Fact]
    public void NoTitleYetMeansNone()
    {
        var root = Root();
        Assert.Null(SessionTitles.Read(Transcript(root, "{\"type\":\"user\",\"message\":{\"content\":\"hi\"}}"), root));
        Assert.Null(SessionTitles.Read(Transcript(root, "{\"type\":\"ai-title\",\"aiTitle\":\"   \"}"), root));
    }

    [Fact]
    public void OnlyTranscriptsUnderTheProjectsFolderAreRead()
    {
        var root = Root();
        var outside = Path.Combine(Path.GetTempPath(), $"faqra-outside-{Guid.NewGuid():N}.jsonl");
        File.WriteAllText(outside, "{\"type\":\"ai-title\",\"aiTitle\":\"x\"}\n");
        var notJsonl = Path.Combine(root, "a.txt");
        File.WriteAllText(notJsonl, "{\"type\":\"ai-title\",\"aiTitle\":\"x\"}\n");

        Assert.Null(SessionTitles.Read(outside, root));
        Assert.Null(SessionTitles.Read(Path.Combine(root, "..", Path.GetFileName(outside)), root));
        Assert.Null(SessionTitles.Read(notJsonl, root));
        Assert.Null(SessionTitles.Read(null, root));
        Assert.Null(SessionTitles.Read(Path.Combine(root, "missing.jsonl"), root));
        File.Delete(outside);
    }

    [Fact]
    public void ATranscriptClaudeIsWritingCanBeRead()
    {
        var root = Root();
        var path = Transcript(root, "{\"type\":\"ai-title\",\"aiTitle\":\"Busy\"}");
        // Claude Code keeps its transcript open while it appends.
        using var writer = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        Assert.Equal("Busy", SessionTitles.Read(path, root));
    }
}
```

`tests/Faqra.Services.Tests/Agents/SessionWindowsTests.cs`:

```csharp
using System.Diagnostics;
using Faqra.Services.Agents;
using Faqra.Win32.Agents;

namespace Faqra.Services.Tests.Agents;

public class SessionWindowsTests
{
    [Fact]
    public void TheSnapshotHoldsThisProcessAndItsParent()
    {
        var self = ProcessSnapshot.Take().Single(process => process.Id == Environment.ProcessId);
        Assert.Equal(Process.GetCurrentProcess().ProcessName + ".exe", self.Executable, ignoreCase: true);
        Assert.NotEqual(0, self.ParentId);
    }

    [Fact]
    public void LookingFromThisProcessSettles() => Assert.True(SessionWindows.Locate(Environment.ProcessId).Settled);

    [Fact]
    public void AProcessThatIsGoneIsNotSettled() => Assert.False(SessionWindows.Locate(int.MaxValue - 1).Settled);
}
```

`tests/Faqra.Services.Tests/Agents/AgentHubSessionTests.cs`:

```csharp
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Faqra.Core.Agents;
using Faqra.Services.Agents;
using static Faqra.Services.Tests.Agents.HubTestKit;

namespace Faqra.Services.Tests.Agents;

public class AgentHubSessionTests
{
    [Fact]
    public async Task RemembersWhichWindowASessionRunsIn()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        var asked = new ConcurrentQueue<int>();
        using var hub = new AgentHub(pipe, context, () => DateTimeOffset.Now, locateWindow: pid =>
        {
            asked.Enqueue(pid);
            return new WindowLookup(true, 4242);
        });
        hub.SetRunning(true);

        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"s1\"}"))
        {
            Assert.True(await Eventually(() => hub.WindowOwner("s1") == 4242));
        }
        Assert.Equal(Environment.ProcessId, Assert.Single(asked));

        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"UserPromptSubmit\",\"session_id\":\"s1\",\"prompt\":\"go\"}"))
        {
            Assert.True(await Eventually(() => hub.Board.Sessions.TryGetValue("s1", out var s) && s.State == AgentState.Thinking));
        }
        Assert.Single(asked); // looked for once per session

        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"SessionEnd\",\"session_id\":\"s1\"}"))
        {
            Assert.True(await Eventually(() => hub.WindowOwner("s1") is null && !hub.Board.Sessions.ContainsKey("s1")));
        }
    }

    [Fact]
    public async Task ARelayAlreadyGoneIsLookedForAgainNextTime()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        var lookups = 0;
        using var hub = new AgentHub(pipe, context, () => DateTimeOffset.Now,
            locateWindow: _ => Interlocked.Increment(ref lookups) == 1 ? new WindowLookup(false, null) : new WindowLookup(true, null));
        hub.SetRunning(true);

        foreach (var name in new[] { "SessionStart", "UserPromptSubmit", "PreToolUse" })
        {
            using (await FakeRelay.SendAsync(pipe, $"{{\"hook_event_name\":\"{name}\",\"session_id\":\"s1\",\"tool_name\":\"Read\"}}"))
            {
                await Task.Delay(150);
            }
        }

        Assert.True(await Eventually(() => Volatile.Read(ref lookups) == 2));
        await Task.Delay(200);
        Assert.Equal(2, Volatile.Read(ref lookups)); // settled on the second look, with no window
        Assert.Null(hub.WindowOwner("s1"));
    }

    [Fact]
    public async Task ImplausibleSessionIdsAreNeverLookedUp()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        var lookups = 0;
        using var hub = new AgentHub(pipe, context, () => DateTimeOffset.Now, locateWindow: _ =>
        {
            Interlocked.Increment(ref lookups);
            return new WindowLookup(true, 1);
        });
        hub.SetRunning(true);
        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"../x\"}"))
        {
            Assert.True(await Eventually(() => hub.Board.Sessions.ContainsKey("../x")));
        }
        Assert.Equal(0, lookups);
    }

    [Fact]
    public async Task NamesASessionAfterItsConversation()
    {
        var root = Directory.CreateTempSubdirectory("faqra-projects-").FullName;
        var transcript = Path.Combine(root, "C--code-faqra", "s1.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);
        File.WriteAllText(transcript, "{\"type\":\"ai-title\",\"aiTitle\":\"Faqra milestones 5 and 6\",\"sessionId\":\"s1\"}\n");
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = new AgentHub(pipe, context, () => DateTimeOffset.Now, projectsRoot: root);
        hub.SetRunning(true);

        var stop = new JsonObject { ["hook_event_name"] = "Stop", ["session_id"] = "s1", ["cwd"] = @"C:\code\Windows", ["transcript_path"] = transcript }.ToJsonString();
        using (await FakeRelay.SendAsync(pipe, stop))
        {
            Assert.True(await Eventually(() => hub.Board.Sessions.TryGetValue("s1", out var s) && s.Name == "Faqra milestones 5 and 6"));
        }
        Directory.Delete(root, recursive: true);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~ProcessTreeTests"` then `dotnet test tests/Faqra.Services.Tests --filter "FullyQualifiedName~SessionTitlesTests|FullyQualifiedName~SessionWindowsTests|FullyQualifiedName~AgentHubSessionTests"`
Expected: both builds FAIL ("The type or namespace name 'ProcessTree' could not be found", "'SessionTitles' could not be found").

- [ ] **Step 3: Write `ProcessTree`**

`src/Faqra.Core/Agents/ProcessTree.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Ported from Coucou's session_window.rs (windows/src-tauri/src), https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé).

namespace Faqra.Core.Agents;

/// <summary>One process as a snapshot lists it: its parent and its executable's file name.</summary>
public readonly record struct ProcessEntry(int ParentId, string Executable);

/// <summary>
/// Where a session's window is. The relay is a child of Claude Code, which runs in the shell, terminal or editor whose
/// window the owner wants; walking up from the relay while it still runs finds that window's process.
/// </summary>
public static class ProcessTree
{
    /// <summary>How far up to look. A session sits a handful of levels below its window.</summary>
    public const int MaxDepth = 16;

    /// <summary>
    /// Never a session's window: the desktop shell and the services at the top of every tree. Reaching one means the
    /// terminal window was not an ancestor (a classic console window belongs to conhost).
    /// </summary>
    private static readonly HashSet<string> TreeTops = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "services.exe", "wininit.exe", "winlogon.exe", "svchost.exe",
        "smss.exe", "csrss.exe", "system", "sihost.exe", "userinit.exe",
    };

    /// <summary>
    /// The ancestors of <paramref name="start"/>, nearest first, stopping below the top of the tree. The start itself is
    /// left out. A loop in the parent links (a parent ID reused by a newer process) ends the walk.
    /// </summary>
    public static IReadOnlyList<int> Ancestors(IReadOnlyDictionary<int, ProcessEntry> table, int start)
    {
        var found = new List<int>();
        var seen = new HashSet<int> { start };
        var current = start;
        while (found.Count < MaxDepth && table.TryGetValue(current, out var entry))
        {
            var parent = entry.ParentId;
            if (parent == 0 || !seen.Add(parent) || !table.TryGetValue(parent, out var up) || TreeTops.Contains(up.Executable))
            {
                break;
            }
            found.Add(parent);
            current = parent;
        }
        return found;
    }

    /// <summary>The nearest ancestor that owns a window: the terminal or the editor.</summary>
    public static int? FirstOwner(IReadOnlyList<int> ancestors, Func<int, bool> ownsWindow)
    {
        foreach (var pid in ancestors)
        {
            if (ownsWindow(pid))
            {
                return pid;
            }
        }
        return null;
    }

    /// <summary>Which of a process's windows to bring forward: the one titled after the project, else the first; -1 for none.</summary>
    public static int PickWindow(IReadOnlyList<string> titles, string folder)
    {
        if (titles.Count == 0)
        {
            return -1;
        }
        if (folder.Length > 0)
        {
            for (var i = 0; i < titles.Count; i++)
            {
                if (titles[i].Contains(folder, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        return 0;
    }
}
```

- [ ] **Step 4: Write the Win32 pieces**

`src/Faqra.Win32/Agents/ProcessSnapshot.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Runtime.InteropServices;

namespace Faqra.Win32.Agents;

public readonly record struct ProcessRecord(int Id, int ParentId, string Executable);

/// <summary>Every running process with its parent, from one Toolhelp32 snapshot.</summary>
public static class ProcessSnapshot
{
    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private static readonly IntPtr InvalidHandle = new(-1);

    public static IReadOnlyList<ProcessRecord> Take()
    {
        var processes = new List<ProcessRecord>();
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == InvalidHandle)
        {
            return processes;
        }
        try
        {
            var entry = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            for (var more = Process32FirstW(snapshot, ref entry); more; more = Process32NextW(snapshot, ref entry))
            {
                processes.Add(new ProcessRecord((int)entry.th32ProcessID, (int)entry.th32ParentProcessID, entry.szExeFile ?? string.Empty));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }
        return processes;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string? szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(IntPtr snapshot, ref PROCESSENTRY32W entry);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(IntPtr snapshot, ref PROCESSENTRY32W entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
```

`src/Faqra.Win32/Agents/PipeClient.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Runtime.InteropServices;

namespace Faqra.Win32.Agents;

public static class PipeClient
{
    /// <summary>The process on the other end of a server pipe, as Windows recorded it when it connected; null if unknown.</summary>
    public static int? ProcessId(SafeHandle pipe) =>
        GetNamedPipeClientProcessId(pipe, out var id) && id != 0 ? (int)id : null;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafeHandle pipe, out uint clientProcessId);
}
```

- [ ] **Step 5: Write `SessionTitles`, `SessionWindows` and the cache**

`src/Faqra.Services/Agents/SessionTitles.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Services.Agents;

/// <summary>
/// The name Claude Code shows for a conversation, read from the session's transcript: the owner's own name (/rename,
/// a "custom-title" line) when there is one, else Claude's title (an "ai-title" line). Claude Code appends both again as
/// the conversation goes on, so the newest wins and the end of the file nearly always holds them.
/// </summary>
public static class SessionTitles
{
    public const int MaxTitleLength = 120;
    private const int TailBytes = 256 * 1024;
    private const long MaxScanBytes = 8L * 1024 * 1024;

    /// <summary>Where Claude Code keeps transcripts: %USERPROFILE%\.claude\projects.</summary>
    public static string DefaultProjectsRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    /// <summary>The conversation's name, or null when it has none yet or the path is not a transcript under <paramref name="projectsRoot"/>.</summary>
    public static string? Read(string? transcriptPath, string projectsRoot)
    {
        if (string.IsNullOrEmpty(transcriptPath) || !IsTranscript(transcriptPath, projectsRoot))
        {
            return null;
        }
        try
        {
            using var file = new FileStream(transcriptPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var tail = Math.Max(0, file.Length - TailBytes);
            if (NewestFrom(file, tail) is { } title)
            {
                return title;
            }
            return tail > 0 && file.Length <= MaxScanBytes ? NewestFrom(file, 0) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsTranscript(string path, string root)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var under = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(under, StringComparison.OrdinalIgnoreCase) && full.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>The newest name from <paramref name="start"/> on. A line cut by the start position simply fails to parse.</summary>
    private static string? NewestFrom(FileStream file, long start)
    {
        file.Seek(start, SeekOrigin.Begin);
        using var reader = new StreamReader(file, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 64 * 1024, leaveOpen: true);
        string? custom = null;
        string? ai = null;
        while (reader.ReadLine() is { } line)
        {
            if (line.Contains("\"custom-title\"", StringComparison.Ordinal) && Field(line, "custom-title", "customTitle") is { } named)
            {
                custom = named;
            }
            else if (line.Contains("\"ai-title\"", StringComparison.Ordinal) && Field(line, "ai-title", "aiTitle") is { } titled)
            {
                ai = titled;
            }
        }
        return Clean(custom) ?? Clean(ai);
    }

    private static string? Field(string line, string type, string key)
    {
        try
        {
            return JsonNode.Parse(line) is JsonObject obj && Text(obj["type"]) == type ? Text(obj[key]) : null;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>One line, at most <see cref="MaxTitleLength"/> characters.</summary>
    private static string? Clean(string? title)
    {
        if (title is null)
        {
            return null;
        }
        var line = string.Join(' ', title.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (line.Length == 0)
        {
            return null;
        }
        return line.Length <= MaxTitleLength ? line : string.Concat(line.AsSpan(0, MaxTitleLength - 1).TrimEnd(), "…");
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
```

`src/Faqra.Services/Agents/SessionWindows.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the window lookup in Coucou's platform/windows.rs, https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé).

using Faqra.Core.Agents;
using Faqra.Win32.Agents;
using Faqra.Win32.Windows;

namespace Faqra.Services.Agents;

/// <summary>What looking for a session's window found. Not settled when the relay had already exited: look again on its next event.</summary>
public readonly record struct WindowLookup(bool Settled, int? Owner);

/// <summary>"Go to window": finds the process that owns a session's window, and brings that window forward.</summary>
public static class SessionWindows
{
    /// <summary>The nearest ancestor of the relay that owns a window. Settled with no owner for a classic console.</summary>
    public static WindowLookup Locate(int relayPid)
    {
        var table = new Dictionary<int, ProcessEntry>();
        foreach (var process in ProcessSnapshot.Take())
        {
            table[process.Id] = new ProcessEntry(process.ParentId, process.Executable);
        }
        var ancestors = ProcessTree.Ancestors(table, relayPid);
        if (ancestors.Count == 0)
        {
            return new WindowLookup(false, null);
        }
        var owners = OpenWindows.Enumerate().Select(window => window.ProcessId).ToHashSet();
        return new WindowLookup(true, ProcessTree.FirstOwner(ancestors, owners.Contains));
    }

    /// <summary>
    /// Brings forward the owner's window titled after the project, else its first. Windows allows it because the owner's
    /// click on the island, or the shortcut, was the last input.
    /// </summary>
    public static bool BringForward(int ownerPid, string project)
    {
        var windows = OpenWindows.Enumerate().Where(window => window.ProcessId == ownerPid).ToList();
        var index = ProcessTree.PickWindow(windows.Select(window => window.Title).ToList(), project);
        return index >= 0 && OpenWindows.Activate(windows[index].Handle);
    }
}
```

`src/Faqra.Services/Agents/SessionWindowCache.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

namespace Faqra.Services.Agents;

/// <summary>
/// Which window owner each session was found under, in memory only. A session looked at but without a window is kept
/// too (no owner), so the process table is not walked on every event. The oldest go once there are more than 64.
/// Thread-safe: the hub writes from pipe threads and the island reads on the UI thread.
/// </summary>
internal sealed class SessionWindowCache
{
    public const int Capacity = 64;
    private readonly List<(string Session, int? Owner)> _sessions = [];
    private readonly object _gate = new();

    /// <summary>Only what Claude Code sends as a session ID: letters, digits, '-' and '_', at most 128 characters.</summary>
    public static bool IsPlausible(string id) =>
        id.Length is > 0 and <= 128 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public bool Knows(string session)
    {
        lock (_gate)
        {
            return _sessions.Exists(entry => entry.Session == session);
        }
    }

    public int? Owner(string session)
    {
        lock (_gate)
        {
            return _sessions.Find(entry => entry.Session == session).Owner;
        }
    }

    public void Remember(string session, int? owner)
    {
        if (!IsPlausible(session))
        {
            return;
        }
        lock (_gate)
        {
            _sessions.RemoveAll(entry => entry.Session == session);
            _sessions.Add((session, owner));
            if (_sessions.Count > Capacity)
            {
                _sessions.RemoveRange(0, _sessions.Count - Capacity);
            }
        }
    }

    public void Forget(string session)
    {
        lock (_gate)
        {
            _sessions.RemoveAll(entry => entry.Session == session);
        }
    }
}
```

- [ ] **Step 6: Teach the hub names and windows**

In `src/Faqra.Services/Agents/AgentHub.cs`:

1. Add `using Faqra.Win32.Agents;` to the usings.
2. Add these fields after `private readonly string? _logPath;`:

```csharp
    private readonly Func<int, WindowLookup>? _locateWindow;
    private readonly string? _projectsRoot;
    private readonly SessionWindowCache _windows = new();
```

3. Replace the constructor with:

```csharp
    /// <param name="locateWindow">Finds the window owner above a relay's process (<see cref="SessionWindows.Locate"/>); null finds none.</param>
    /// <param name="projectsRoot">Where Claude Code's transcripts live (<see cref="SessionTitles.DefaultProjectsRoot"/>); null reads no names.</param>
    public AgentHub(string pipeName, SynchronizationContext context, Func<DateTimeOffset> now, string? logPath = null,
        Func<int, WindowLookup>? locateWindow = null, string? projectsRoot = null)
    {
        _pipeName = pipeName;
        _context = context;
        _now = now;
        _logPath = logPath;
        _locateWindow = locateWindow;
        _projectsRoot = projectsRoot;
    }
```

4. Add these public members after `MarkRead`:

```csharp
    /// <summary>The process that owns the window a session runs in, once found. Safe from any thread.</summary>
    public int? WindowOwner(string sessionId) => _windows.Owner(sessionId);

    /// <summary>For render tests: pretends a session's window was found. Never called by the app.</summary>
    public void RememberWindowForTests(string sessionId, int owner) => _windows.Remember(sessionId, owner);
```

5. In `Serve`, replace everything from `BeforePost?.Invoke();` to the end of the `using (connection)` block with:

```csharp
            NoteWindow(connection, e);
            var title = TitleFor(e);
            BeforePost?.Invoke();
            if (e.Event == "PermissionRequest")
            {
                await ServeRequest(connection, e, title, token).ConfigureAwait(false);
                return;
            }
            Post(() =>
            {
                // A run that was stopped meanwhile must not put sessions back on the emptied board.
                if (token.IsCancellationRequested)
                {
                    return;
                }
                Board = Fold(e, title, _now());
                Log(e);
                if (TurnEnds.Contains(e.Event))
                {
                    ReleaseSession(e.SessionId);
                }
                Changed?.Invoke();
                if (e.Event == "Stop" && Board.Sessions.TryGetValue(e.SessionId, out var session) && session.Unread)
                {
                    TurnFinished?.Invoke(session);
                }
            });
```

6. Change `ServeRequest`'s signature to `private async Task ServeRequest(NamedPipeServerStream connection, AgentEvent e, string? title, CancellationToken token)`, change its `Open(id, e, decided)` call to `Open(id, e, title, decided)`, change `Open`'s signature to `private bool Open(string id, AgentEvent e, string? title, TaskCompletionSource<AgentDecision?> decided)`, and in `Open` replace `Board = Board.Apply(e, now);` with `Board = Fold(e, title, now);`.

7. Add these private members before `HangUp`:

```csharp
    /// <summary>The board after an event, named after the session's conversation when its name was read.</summary>
    private AgentBoard Fold(AgentEvent e, string? title, DateTimeOffset now)
    {
        var board = Board.Apply(e, now);
        return title is null ? board : board.Titled(e.SessionId, title);
    }

    /// <summary>The conversation's name, for the events after which Claude Code may have named or renamed it. Off the context's thread.</summary>
    private string? TitleFor(AgentEvent e) =>
        _projectsRoot is not null && e.Event is "SessionStart" or "UserPromptSubmit" or "Stop" or "PermissionRequest"
            ? SessionTitles.Read(e.TranscriptPath, _projectsRoot)
            : null;

    /// <summary>
    /// Remembers, once per session, which window the session runs in. Runs on the pipe thread while the relay is still
    /// connected, because the walk starts from the relay's own process.
    /// </summary>
    private void NoteWindow(NamedPipeServerStream connection, AgentEvent e)
    {
        if (e.Event == "SessionEnd")
        {
            _windows.Forget(e.SessionId);
            return;
        }
        if (_locateWindow is null || !SessionWindowCache.IsPlausible(e.SessionId) || _windows.Knows(e.SessionId)
            || PipeClient.ProcessId(connection.SafePipeHandle) is not { } relay)
        {
            return;
        }
        try
        {
            var found = _locateWindow(relay);
            if (found.Settled)
            {
                _windows.Remember(e.SessionId, found.Owner);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A failed lookup only means no "Go to window" for this session; the event itself still counts.
            Trace.TraceWarning($"Faqra agents window lookup: {ex.Message}");
        }
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.Core.Tests` then `dotnet test tests/Faqra.Services.Tests`
Expected: PASS (every Core and Services test).

- [ ] **Step 8: Commit**

```bash
git add src/Faqra.Core/Agents/ProcessTree.cs src/Faqra.Win32/Agents src/Faqra.Services/Agents tests/Faqra.Core.Tests/Agents/ProcessTreeTests.cs tests/Faqra.Services.Tests/Agents
git commit -m "feat(windows): agent sessions take their conversation's name and remember their window"
```

---

### Task 6: Alert switches, shortcut roles and their pages

**Files:**
- Modify: `src/Faqra.Core/Defaults/DefaultsKey.Stage1.cs`, `src/Faqra.Core/Defaults/RegisteredDefaults.Stage1.cs`
- Modify: `src/Faqra.Core/Shortcuts/GlobalShortcut.cs`, `src/Faqra.Core/Shortcuts/GlobalShortcutRole.cs` (full replacement below)
- Modify: `src/Faqra.Core/Localization/AgentsStrings.cs`, `src/Faqra.Core/Localization/AgentsStrings.EnUS.cs`
- Modify: `src/Faqra.App/Settings/Pages/ShortcutsPage.cs`, `src/Faqra.App/Settings/Pages/AgentsPage.cs`
- Modify: `tests/Faqra.Services.Tests/HotKeyRegistryTests.cs`, `tests/Faqra.App.Tests/AgentsRenderTests.cs` (make `AllText` and `HubWith` internal)
- Test: `tests/Faqra.Core.Tests/ShortcutRoleTests.cs` (add), `tests/Faqra.App.Tests/AgentSettingsRenderTests.cs` (new)

**Interfaces:**
- Consumes: `HookStatus.CoucouEvents` (A1), `AppServices.Current.Store`.
- Produces: `DefaultsKey.FaqraAgentsNeedsYouSound`, `FaqraAgentsAnsweredSound`, `FaqraAgentsOpenOnAnswer`, `FaqraAgentsShortcutsEnabled`, `FaqraAgentsJumpShortcut`, `FaqraAgentsWindowShortcut`, `FaqraAgentsNextShortcut`, `FaqraAgentsPreviousShortcut` (values in Global Constraints); `GlobalShortcut.AgentsJumpDefault`, `AgentsWindowDefault`, `AgentsNextDefault`, `AgentsPreviousDefault`; `GlobalShortcutRole.AgentsJumpToWaiting`, `AgentsGoToWindow`, `AgentsNextSession`, `AgentsPreviousSession` (appended, so `HotKeyRegistry.HotKeyId` of the existing roles stays 1, 2, 3). Strings `AlertsSection`, `NeedsYouSound`, `NeedsYouSoundCaption`, `AnsweredSound`, `AnsweredSoundCaption`, `OpenOnAnswer`, `OpenOnAnswerCaption`, `ShortcutsToggle`, `ShortcutsCaption`, `WatchOnly`, `ShortcutJump`, `ShortcutWindow`, `ShortcutNext`, `ShortcutPrevious`.

- [ ] **Step 1: Write the failing tests**

Add to `tests/Faqra.Core.Tests/ShortcutRoleTests.cs` (inside the existing class):

```csharp
    [Theory]
    [InlineData(GlobalShortcutRole.AgentsJumpToWaiting, DefaultsKey.FaqraAgentsJumpShortcut, 0x41)]
    [InlineData(GlobalShortcutRole.AgentsGoToWindow, DefaultsKey.FaqraAgentsWindowShortcut, 0x47)]
    [InlineData(GlobalShortcutRole.AgentsNextSession, DefaultsKey.FaqraAgentsNextShortcut, 0x28)]
    [InlineData(GlobalShortcutRole.AgentsPreviousSession, DefaultsKey.FaqraAgentsPreviousShortcut, 0x26)]
    public void AgentRolesUseCtrlAltWinUnderOneSwitch(GlobalShortcutRole role, string key, int virtualKey)
    {
        Assert.Equal(key, role.StorageKey());
        Assert.Equal(new GlobalShortcut(virtualKey, ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Win), role.DefaultShortcut());
        Assert.Equal(AppFeature.FaqraAgents, role.Feature());
        Assert.Equal([DefaultsKey.FaqraAgentsShortcutsEnabled], role.RequiredEnableKeys());
    }

    [Fact]
    public void NoTwoRolesShareADefault()
    {
        var defaults = Enum.GetValues<GlobalShortcutRole>().Select(role => role.DefaultShortcut()).ToList();
        Assert.Equal(defaults.Count, defaults.Distinct().Count());
    }

    [Fact]
    public void TheAgentSwitchesStartOn()
    {
        var store = DefaultsStore.InMemory();
        Assert.True(store.Bool(DefaultsKey.FaqraAgentsNeedsYouSound));
        Assert.True(store.Bool(DefaultsKey.FaqraAgentsAnsweredSound));
        Assert.True(store.Bool(DefaultsKey.FaqraAgentsOpenOnAnswer));
        Assert.True(store.Bool(DefaultsKey.FaqraAgentsShortcutsEnabled));
    }
```

In `tests/Faqra.Services.Tests/HotKeyRegistryTests.cs`, in `RegistersOnlyActiveRolesOfInstalledFeatures`, add `store.Set(DefaultsKey.FaqraAgentsShortcutsEnabled, false);` right after `store.Set(DefaultsKey.CommandBarShortcutEnabled, false);` (the test is about keep awake alone; the agents' switch is on by default). Then add to the class:

```csharp
    [Fact]
    public void TheExistingRolesKeepTheirHotKeyIds()
    {
        Assert.Equal(1, HotKeyRegistry.HotKeyId(GlobalShortcutRole.KeepAwake));
        Assert.Equal(2, HotKeyRegistry.HotKeyId(GlobalShortcutRole.SoundOutputSwitcher));
        Assert.Equal(3, HotKeyRegistry.HotKeyId(GlobalShortcutRole.CommandBar));
    }

    [Fact]
    public void TheAgentShortcutsRegisterTogetherUnderOneSwitch()
    {
        var (registry, host, store) = Create(f => f == AppFeature.FaqraAgents);
        foreach (var role in Enum.GetValues<GlobalShortcutRole>())
        {
            registry.Bind(role, () => { });
        }
        registry.Sync();
        Assert.Equal(4, host.Registered.Count);

        store.Set(DefaultsKey.FaqraAgentsShortcutsEnabled, false);
        Assert.Empty(host.Registered);
    }
```

(If `Create`'s fake host exposes its registrations under another name than `Registered`, use that name; the A1-era test above already relies on `host.Registered`.)

In `tests/Faqra.App.Tests/AgentsRenderTests.cs`, change `private static AgentHub HubWith(` to `internal static AgentHub HubWith(` and `private static string AllText(` to `internal static string AllText(`.

Create `tests/Faqra.App.Tests/AgentSettingsRenderTests.cs`:

```csharp
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using Faqra.App.Settings.Pages;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;

namespace Faqra.App.Tests;

public class AgentSettingsRenderTests
{
    private static readonly AgentsStrings S = AgentsStrings.EnUS;

    private static List<Wpf.Ui.Controls.ToggleSwitch> Toggles(DependencyObject root)
    {
        var found = new List<Wpf.Ui.Controls.ToggleSwitch>();
        void Walk(DependencyObject node)
        {
            if (node is Wpf.Ui.Controls.ToggleSwitch toggle)
            {
                found.Add(toggle);
            }
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            {
                Walk(child);
            }
        }
        Walk(root);
        return found;
    }

    [Fact]
    public void TheAgentsPageHasTheAlertSwitches() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        var dir = Directory.CreateTempSubdirectory("faqra-page-").FullName;
        var page = new AgentsPage(Path.Combine(dir, "settings.json"), Path.Combine(dir, "faqra-hook.exe"));
        AgentsRenderTests.RenderInk(page, "settings-agents-alerts", 840, 900);

        var texts = AgentsRenderTests.AllText(page);
        Assert.Contains(S.AlertsSection, texts);
        var toggles = Toggles(page);
        Assert.Equal(4, toggles.Count);
        Assert.All(toggles, toggle => Assert.True(toggle.IsChecked));

        var sound = toggles.Single(toggle => AutomationProperties.GetName(toggle) == S.NeedsYouSound);
        sound.IsChecked = false;
        sound.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.False(services.Store.Bool(DefaultsKey.FaqraAgentsNeedsYouSound));
        Directory.Delete(dir, recursive: true);
    });

    [Fact]
    public void CoucousHooksMeanFaqraOnlyWatches() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        var dir = Directory.CreateTempSubdirectory("faqra-page-").FullName;
        var settings = Path.Combine(dir, "settings.json");
        File.WriteAllText(settings, "{\n  \"hooks\": {\n    \"Stop\": [\n      {\n        \"hooks\": [\n          {\n            \"type\": \"command\",\n            \"command\": \"\\\"C:/x/coucou-hook.exe\\\" Stop\"\n          }\n        ]\n      }\n    ]\n  }\n}\n");
        var page = new AgentsPage(settings, Path.Combine(dir, "faqra-hook.exe"));
        Assert.Contains(S.WatchOnly, AgentsRenderTests.AllText(page));
        Directory.Delete(dir, recursive: true);
    });

    [Fact]
    public void TheShortcutsPageListsTheFourAgentShortcuts() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        var page = new ShortcutsPage();
        var texts = AgentsRenderTests.AllText(page);
        foreach (var title in new[] { S.ShortcutJump, S.ShortcutWindow, S.ShortcutNext, S.ShortcutPrevious })
        {
            Assert.Contains(title, texts);
        }
    });
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~ShortcutRoleTests"`
Expected: build FAILS with "'GlobalShortcutRole' does not contain a definition for 'AgentsJumpToWaiting'".

- [ ] **Step 3: Add the keys and their defaults**

In `src/Faqra.Core/Defaults/DefaultsKey.Stage1.cs`, add after the Command bar block's last constant:

```csharp

    // Faqra Agents (Faqra-only; every key carries the faqraAgents prefix so it never meets an upstream key)
    public const string FaqraAgentsNeedsYouSound = "faqraAgentsNeedsYouSound";
    public const string FaqraAgentsAnsweredSound = "faqraAgentsAnsweredSound";
    public const string FaqraAgentsOpenOnAnswer = "faqraAgentsOpenOnAnswer";
    public const string FaqraAgentsShortcutsEnabled = "faqraAgentsShortcutsEnabled";
    public const string FaqraAgentsJumpShortcut = "faqraAgentsJumpShortcut";
    public const string FaqraAgentsWindowShortcut = "faqraAgentsWindowShortcut";
    public const string FaqraAgentsNextShortcut = "faqraAgentsNextShortcut";
    public const string FaqraAgentsPreviousShortcut = "faqraAgentsPreviousShortcut";
```

In `src/Faqra.Core/Defaults/RegisteredDefaults.Stage1.cs`, add after `[DefaultsKey.KillProcessCommandBarEnabled] = true,`:

```csharp

        // Faqra Agents (Faqra-only)
        [DefaultsKey.FaqraAgentsNeedsYouSound] = true,
        [DefaultsKey.FaqraAgentsAnsweredSound] = true,
        [DefaultsKey.FaqraAgentsOpenOnAnswer] = true,
        [DefaultsKey.FaqraAgentsShortcutsEnabled] = true,
        [DefaultsKey.FaqraAgentsJumpShortcut] = "control+option+command:65",      // Ctrl+Alt+Win+A (Windows VK)
        [DefaultsKey.FaqraAgentsWindowShortcut] = "control+option+command:71",    // Ctrl+Alt+Win+G (Windows VK)
        [DefaultsKey.FaqraAgentsNextShortcut] = "control+option+command:40",      // Ctrl+Alt+Win+Down (Windows VK)
        [DefaultsKey.FaqraAgentsPreviousShortcut] = "control+option+command:38",  // Ctrl+Alt+Win+Up (Windows VK)
```

In `src/Faqra.Core/Shortcuts/GlobalShortcut.cs`, add after `SoundOutputSwitcherDefault`:

```csharp

    /// <summary>Faqra Agents: open the island on the waiting card, Ctrl+Alt+Win+A.</summary>
    public static readonly GlobalShortcut AgentsJumpDefault = new(0x41, ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Win);

    /// <summary>Faqra Agents: bring the session's window forward, Ctrl+Alt+Win+G.</summary>
    public static readonly GlobalShortcut AgentsWindowDefault = new(0x47, ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Win);

    /// <summary>Faqra Agents: next session, Ctrl+Alt+Win+Down.</summary>
    public static readonly GlobalShortcut AgentsNextDefault = new(0x28, ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Win);

    /// <summary>Faqra Agents: previous session, Ctrl+Alt+Win+Up.</summary>
    public static readonly GlobalShortcut AgentsPreviousDefault = new(0x26, ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Win);
```

- [ ] **Step 4: Add the four roles**

Replace `src/Faqra.Core/Shortcuts/GlobalShortcutRole.cs` with:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors GlobalShortcutRole in Sources/Vorssaint/Core/GlobalShortcut.swift (lines 687-1003). Only the
// roles whose features exist on Windows in Stage 1; the others join as their features are ported. The four
// Agents roles are Faqra's own, appended so the existing roles keep their hot key IDs.

using Faqra.Core.Defaults;
using Faqra.Core.Features;

namespace Faqra.Core.Shortcuts;

public enum GlobalShortcutRole
{
    KeepAwake,
    SoundOutputSwitcher,
    CommandBar,
    AgentsJumpToWaiting,
    AgentsGoToWindow,
    AgentsNextSession,
    AgentsPreviousSession,
}

public static class GlobalShortcutRoleExtensions
{
    public static string StorageKey(this GlobalShortcutRole role) => role switch
    {
        GlobalShortcutRole.KeepAwake => DefaultsKey.KeepAwakeShortcut,
        GlobalShortcutRole.SoundOutputSwitcher => DefaultsKey.SoundOutputSwitcherShortcut,
        GlobalShortcutRole.CommandBar => DefaultsKey.CommandBarShortcut,
        GlobalShortcutRole.AgentsJumpToWaiting => DefaultsKey.FaqraAgentsJumpShortcut,
        GlobalShortcutRole.AgentsGoToWindow => DefaultsKey.FaqraAgentsWindowShortcut,
        GlobalShortcutRole.AgentsNextSession => DefaultsKey.FaqraAgentsNextShortcut,
        GlobalShortcutRole.AgentsPreviousSession => DefaultsKey.FaqraAgentsPreviousShortcut,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    public static GlobalShortcut DefaultShortcut(this GlobalShortcutRole role) => role switch
    {
        GlobalShortcutRole.KeepAwake => GlobalShortcut.KeepAwakeDefault,
        GlobalShortcutRole.SoundOutputSwitcher => GlobalShortcut.SoundOutputSwitcherDefault,
        GlobalShortcutRole.CommandBar => GlobalShortcut.CommandBarDefault,
        GlobalShortcutRole.AgentsJumpToWaiting => GlobalShortcut.AgentsJumpDefault,
        GlobalShortcutRole.AgentsGoToWindow => GlobalShortcut.AgentsWindowDefault,
        GlobalShortcutRole.AgentsNextSession => GlobalShortcut.AgentsNextDefault,
        GlobalShortcutRole.AgentsPreviousSession => GlobalShortcut.AgentsPreviousDefault,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    public static AppFeature Feature(this GlobalShortcutRole role) => role switch
    {
        GlobalShortcutRole.KeepAwake => AppFeature.KeepAwake,
        GlobalShortcutRole.SoundOutputSwitcher => AppFeature.SoundOutputSwitcher,
        GlobalShortcutRole.CommandBar => AppFeature.CommandBar,
        GlobalShortcutRole.AgentsJumpToWaiting or GlobalShortcutRole.AgentsGoToWindow
            or GlobalShortcutRole.AgentsNextSession or GlobalShortcutRole.AgentsPreviousSession => AppFeature.FaqraAgents,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    /// <summary>Every key must be on for the shortcut to run.</summary>
    public static IReadOnlyList<string> RequiredEnableKeys(this GlobalShortcutRole role) => role switch
    {
        GlobalShortcutRole.KeepAwake => [DefaultsKey.HotkeyEnabled],
        GlobalShortcutRole.SoundOutputSwitcher => [DefaultsKey.SoundOutputSwitcherEnabled],
        GlobalShortcutRole.CommandBar => [DefaultsKey.CommandBarShortcutEnabled],
        GlobalShortcutRole.AgentsJumpToWaiting or GlobalShortcutRole.AgentsGoToWindow
            or GlobalShortcutRole.AgentsNextSession or GlobalShortcutRole.AgentsPreviousSession => [DefaultsKey.FaqraAgentsShortcutsEnabled],
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    /// <summary>The shortcut in effect: the stored one, or the default when the stored text is not valid.</summary>
    public static GlobalShortcut Saved(this GlobalShortcutRole role, ISettingsStore store) =>
        GlobalShortcut.Parse(store.String(role.StorageKey())) ?? role.DefaultShortcut();

    public static bool IsActive(this GlobalShortcutRole role, ISettingsStore store) =>
        role.RequiredEnableKeys().All(store.Bool);

    /// <summary>
    /// Another role of an installed feature already using the shortcut; inactive roles count when
    /// <paramref name="includeInactive"/> (the Shortcuts page passes true so a switched-off shortcut still blocks reuse).
    /// </summary>
    public static GlobalShortcutRole? Conflict(this GlobalShortcutRole role, GlobalShortcut shortcut, ISettingsStore store,
        Func<AppFeature, bool> isInstalled, bool includeInactive)
    {
        foreach (var other in Enum.GetValues<GlobalShortcutRole>())
        {
            if (other != role
                && isInstalled(other.Feature())
                && (includeInactive || other.IsActive(store))
                && other.Saved(store) == shortcut)
            {
                return other;
            }
        }
        return null;
    }
}
```

- [ ] **Step 5: Add the strings**

In `src/Faqra.Core/Localization/AgentsStrings.cs`, add after `public required string Removed { get; init; }`:

```csharp
    public required string AlertsSection { get; init; }
    public required string NeedsYouSound { get; init; }
    public required string NeedsYouSoundCaption { get; init; }
    public required string AnsweredSound { get; init; }
    public required string AnsweredSoundCaption { get; init; }
    public required string OpenOnAnswer { get; init; }
    public required string OpenOnAnswerCaption { get; init; }
    public required string ShortcutsToggle { get; init; }
    public required string ShortcutsCaption { get; init; }
    public required string WatchOnly { get; init; }

    // Shortcuts page
    public required string ShortcutJump { get; init; }
    public required string ShortcutWindow { get; init; }
    public required string ShortcutNext { get; init; }
    public required string ShortcutPrevious { get; init; }
```

In `src/Faqra.Core/Localization/AgentsStrings.EnUS.cs`, add after `Removed = "Hooks removed.",`:

```csharp
        AlertsSection = "Alerts",
        NeedsYouSound = "Sound when an agent needs you",
        NeedsYouSoundCaption = "Plays the Windows notification sound when the island opens for an approval or a question.",
        AnsweredSound = "Sound when an agent answers",
        AnsweredSoundCaption = "Plays the Windows message sound when Claude finishes a turn.",
        OpenOnAnswer = "Open the island when an agent answers",
        OpenOnAnswerCaption = "Shows what Claude said without taking the keyboard from what you're doing.",
        ShortcutsToggle = "Keyboard shortcuts",
        ShortcutsCaption = "Open the waiting card, go to a session's window and switch sessions from anywhere. Change the keys on the Shortcuts page.",
        WatchOnly = "While Coucou's hooks are installed, Faqra only watches: approvals and questions stay in Claude Code.",
        ShortcutJump = "Open the waiting agent",
        ShortcutWindow = "Go to the agent's window",
        ShortcutNext = "Next agent session",
        ShortcutPrevious = "Previous agent session",
```

- [ ] **Step 6: Title the roles on the Shortcuts page**

In `src/Faqra.App/Settings/Pages/ShortcutsPage.cs`, add the field `private readonly AgentsStrings _agents = AgentsStrings.For(L10n.Shared.Language);` after `_s`, and replace `RoleTitle` and `Icon` with:

```csharp
    private string RoleTitle(GlobalShortcutRole role) => role switch
    {
        GlobalShortcutRole.AgentsJumpToWaiting => _agents.ShortcutJump,
        GlobalShortcutRole.AgentsGoToWindow => _agents.ShortcutWindow,
        GlobalShortcutRole.AgentsNextSession => _agents.ShortcutNext,
        GlobalShortcutRole.AgentsPreviousSession => _agents.ShortcutPrevious,
        _ => _hub.FeatureTitles[role.Feature()],
    };
```

```csharp
    private static SymbolRegular Icon(GlobalShortcutRole role) => role switch
    {
        GlobalShortcutRole.KeepAwake => SymbolRegular.WeatherMoon24,
        GlobalShortcutRole.SoundOutputSwitcher => SymbolRegular.Speaker224,
        GlobalShortcutRole.AgentsJumpToWaiting => SymbolRegular.Alert24,
        GlobalShortcutRole.AgentsGoToWindow => SymbolRegular.Open24,
        GlobalShortcutRole.AgentsNextSession => SymbolRegular.ArrowDown24,
        GlobalShortcutRole.AgentsPreviousSession => SymbolRegular.ArrowUp24,
        _ => SymbolRegular.Search24,
    };
```

- [ ] **Step 7: Add the Alerts section to the Agents page**

In `src/Faqra.App/Settings/Pages/AgentsPage.cs`, add `using Faqra.Core.Defaults;`. In `Build`, add `status.CoucouEvents > 0 ? _s.WatchOnly : null,` to the array right after the `CoucouLeftoversFormat` line, and add after the `foreach` loop that writes those lines:

```csharp
        _page.Children.Add(Text(_s.AlertsSection, "SectionHeader"));
        _page.Children.Add(Toggle(SymbolRegular.Alert24, _s.NeedsYouSound, _s.NeedsYouSoundCaption, DefaultsKey.FaqraAgentsNeedsYouSound, top: 0));
        _page.Children.Add(Toggle(SymbolRegular.CheckmarkCircle24, _s.AnsweredSound, _s.AnsweredSoundCaption, DefaultsKey.FaqraAgentsAnsweredSound, top: 6));
        _page.Children.Add(Toggle(SymbolRegular.Chat24, _s.OpenOnAnswer, _s.OpenOnAnswerCaption, DefaultsKey.FaqraAgentsOpenOnAnswer, top: 6));
        _page.Children.Add(Toggle(SymbolRegular.Keyboard24, _s.ShortcutsToggle, _s.ShortcutsCaption, DefaultsKey.FaqraAgentsShortcutsEnabled, top: 6));
```

and add this method before `Format`:

```csharp
    /// <summary>A switch bound to one setting, written the moment it flips.</summary>
    private static CardControl Toggle(SymbolRegular icon, string title, string caption, string key, double top)
    {
        var store = AppServices.Current.Store;
        var toggle = new ToggleSwitch { IsChecked = store.Bool(key) };
        System.Windows.Automation.AutomationProperties.SetName(toggle, title);
        toggle.Click += (_, _) => store.Set(key, toggle.IsChecked == true);
        var header = new StackPanel();
        header.Children.Add(Text(title, "Body"));
        var note = Text(caption, "Caption");
        note.TextWrapping = TextWrapping.Wrap;
        header.Children.Add(note);
        return new CardControl { Icon = new SymbolIcon(icon), Header = header, Content = toggle, Margin = new Thickness(0, top, 0, 0) };
    }
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test Faqra.sln`
Expected: PASS (every test in the three projects).

- [ ] **Step 9: Commit**

```bash
git add src/Faqra.Core src/Faqra.App/Settings tests
git commit -m "feat(windows): agent alert switches and four global shortcuts"
```

---
### Task 7: Approval, question and answered cards in the island

**Files:**
- Create: `src/Faqra.App/Island/Modules/AgentCards.cs`
- Modify: `src/Faqra.App/Island/Modules/AgentsModule.cs` (full replacement below)
- Modify: `src/Faqra.Core/Localization/AgentsStrings.cs`, `src/Faqra.Core/Localization/AgentsStrings.EnUS.cs`
- Test: `tests/Faqra.App.Tests/AgentCardRenderTests.cs`

**Interfaces:**
- Consumes: `AgentRequest`, `AgentQuestions`, `AgentDecision`, `AgentsText.RequestTitle`, `AgentSession.Name`/`LastPrompt`/`Unread` (Tasks 1 and 2); `AgentHub.Requests`, `Answer`, `Release`, `MarkRead`, `ReplaceRequestsForTests` (Task 4); `AgentHub.WindowOwner`, `RememberWindowForTests` (Task 5); `AgentsRenderTests.RenderInk`, `AllText`, `HubWith` (internal since Task 6); `IslandPalette` (A1).
- Produces: `internal sealed class ApprovalCard(AgentRequest request, string name, AgentsStrings s, Action<AgentDecision> decide, Action release)`; `internal sealed class QuestionCard(AgentRequest request, string name, AgentsStrings s, Action<AgentDecision> decide, Action release, Action wantKeyboard)` with `IReadOnlyList<ToggleButton> ChoicesFor(int question)`, `TextBox OwnAnswerFor(int question)`, `Button? SendButton`, `IReadOnlyDictionary<string,string>? Answers()`; `internal sealed class AnsweredCard(AgentSession session, AgentsStrings s, Action dismiss)`. On `AgentsModule`: `event Action? KeyboardWanted`, `event Action<AgentSession>? GoToWindowRequested`, `AgentSession? FocusedSession`, `void FocusSession(string sessionId)`, `void OfferFocus(string sessionId)`, `void MoveFocus(int delta)`. Strings `CardTitleFormat`, `Allow`, `Deny`, `AlwaysAllow`, `AlwaysSavesFormat`, `AlwaysAcceptEdits`, `AnswerInClaude`, `QuestionOwnAnswer`, `Send`, `QuestionUnreadable`, `AnsweredTitle`, `Dismiss`, `GoToWindow`, `PromptedHeader`, `InFolderFormat`.

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.App.Tests/AgentCardRenderTests.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Faqra.App.Island.Modules;
using Faqra.Core.Agents;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;

namespace Faqra.App.Tests;

public class AgentCardRenderTests
{
    private static readonly AgentsStrings S = AgentsStrings.EnUS;
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private const string WaitingLine = "{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\site\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}";
    private const string AskingLine = "{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\site\",\"tool_name\":\"AskUserQuestion\"}";
    private const string WorkingLine = "{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s2\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Read\",\"tool_input\":{\"file_path\":\"C:\\\\code\\\\faqra\\\\a.cs\"}}";

    private static AgentRequest Approval(bool always = true, string session = "s1") => new("r1", session, AgentRequestKind.Approval, "Bash",
        "npm test -- --watch=false", [], always ? ["Bash(npm test:*)"] : [], false, T0);

    private static AgentRequest TwoQuestions() => new("r2", "s1", AgentRequestKind.Question, "AskUserQuestion", "",
        [
            new AgentQuestion("Which color?", "Color", false, [new AgentOption("Red", "Warm"), new AgentOption("Blue", "Cool")]),
            new AgentQuestion("Which extras?", "Extras", true, [new AgentOption("Tests", ""), new AgentOption("Docs", ""), new AgentOption("Lint", "")]),
        ],
        [], false, T0);

    private static IEnumerable<T> All<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            foreach (var nested in All<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static Button ButtonNamed(DependencyObject root, string text) => All<Button>(root).Single(button => button.Content as string == text);

    private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static void Render(FrameworkElement element, string name, double height) =>
        AgentsRenderTests.RenderInk(new Border { Background = IslandPalette.Surface, Child = element, Width = 412, Height = height }, name, 412, height);

    [Fact]
    public void AnApprovalShowsTheCommandAndAllThreeChoices() => StaThread.Run(() =>
    {
        AgentDecision? chosen = null;
        var card = new ApprovalCard(Approval(), "Faqra milestones", S, decision => chosen = decision, () => { });
        Render(card, "island-card-approval", 220);
        var text = AgentsRenderTests.AllText(card);
        Assert.Contains("Faqra milestones · Wants to run a command", text);
        Assert.Contains("npm test -- --watch=false", text);
        Assert.Contains("Always allow also saves: Bash(npm test:*)", text);

        Click(ButtonNamed(card, "Allow"));
        Assert.Equal(AgentDecisionKind.Allow, chosen!.Kind);
        Click(ButtonNamed(card, "Always allow"));
        Assert.Equal(AgentDecisionKind.Always, chosen!.Kind);
        Click(ButtonNamed(card, "Deny"));
        Assert.Equal(AgentDecisionKind.Deny, chosen!.Kind);
    });

    [Fact]
    public void WithoutSuggestionsThereIsNoAlwaysButton() => StaThread.Run(() =>
    {
        var card = new ApprovalCard(Approval(always: false), "x", S, _ => { }, () => { });
        Assert.DoesNotContain(All<Button>(card), button => button.Content as string == "Always allow");
    });

    [Fact]
    public void AnswerInClaudeCodeLetsTheRequestGo() => StaThread.Run(() =>
    {
        var released = false;
        var card = new ApprovalCard(Approval(), "x", S, _ => { }, () => released = true);
        Click(ButtonNamed(card, "Answer in Claude Code"));
        Assert.True(released);
    });

    [Fact]
    public void AQuestionShowsEveryOptionAndSendsTheAnswers() => StaThread.Run(() =>
    {
        AgentDecision? chosen = null;
        var card = new QuestionCard(TwoQuestions(), "Faqra", S, decision => chosen = decision, () => { }, () => { });
        Render(card, "island-card-question", 520);
        var text = AgentsRenderTests.AllText(card);
        foreach (var label in new[] { "Faqra · Has a question", "Which color?", "Red", "Warm", "Blue", "Cool", "Which extras?", "Tests", "Docs", "Lint" })
        {
            Assert.Contains(label, text);
        }

        Assert.False(card.SendButton!.IsEnabled);
        card.ChoicesFor(0)[1].IsChecked = true;   // Blue
        Assert.False(card.SendButton.IsEnabled);  // the second question has no answer yet
        card.ChoicesFor(1)[0].IsChecked = true;   // Tests
        card.ChoicesFor(1)[2].IsChecked = true;   // Lint
        card.OwnAnswerFor(1).Text = "a changelog";
        Assert.True(card.SendButton.IsEnabled);

        Click(card.SendButton);
        Assert.Equal(AgentDecisionKind.Answer, chosen!.Kind);
        Assert.Equal("Blue", chosen.Answers!["Which color?"]);
        Assert.Equal("Tests, Lint, a changelog", chosen.Answers["Which extras?"]);
    });

    [Fact]
    public void OwnWordsReplaceASingleChoiceAndBack() => StaThread.Run(() =>
    {
        var card = new QuestionCard(TwoQuestions(), "Faqra", S, _ => { }, () => { }, () => { });
        card.ChoicesFor(0)[0].IsChecked = true;
        card.OwnAnswerFor(0).Text = "Green";
        Assert.False(card.ChoicesFor(0)[0].IsChecked);
        card.ChoicesFor(1)[1].IsChecked = true;
        Assert.Equal("Green", card.Answers()!["Which color?"]);

        card.ChoicesFor(0)[1].IsChecked = true;
        Assert.Equal(string.Empty, card.OwnAnswerFor(0).Text);
        Assert.Equal("Blue", card.Answers()!["Which color?"]);
    });

    [Fact]
    public void ClickingIntoTheOwnAnswerAsksForTheKeyboard() => StaThread.Run(() =>
    {
        var asked = 0;
        var card = new QuestionCard(TwoQuestions(), "Faqra", S, _ => { }, () => { }, () => asked++);
        card.OwnAnswerFor(0).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent });
        Assert.Equal(1, asked);
    });

    [Fact]
    public void AnUnreadableQuestionPointsToClaudeCode() => StaThread.Run(() =>
    {
        var card = new QuestionCard(TwoQuestions() with { Questions = [] }, "Faqra", S, _ => { }, () => { }, () => { });
        Assert.Contains("Answer it in Claude Code", AgentsRenderTests.AllText(card));
        Assert.Null(card.SendButton);
    });

    [Fact]
    public void TheModuleShowsTheWaitingCardAboveTheList() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(WaitingLine, WorkingLine);
        hub.ReplaceRequestsForTests(Approval());
        var module = new AgentsModule(hub, () => true);
        Render(module, "island-agents-approval", 480);
        var text = AgentsRenderTests.AllText(module);
        Assert.Contains("site · Wants to run a command", text);
        Assert.True(text.IndexOf("Wants to run a command", StringComparison.Ordinal) < text.IndexOf("Reads a.cs", StringComparison.Ordinal));
    });

    [Fact]
    public void TheCardKeepsWhatTheOwnerTypedWhileEventsArrive() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(AskingLine, WorkingLine);
        hub.ReplaceRequestsForTests(TwoQuestions());
        var module = new AgentsModule(hub, () => true);
        var card = All<QuestionCard>(module).Single();
        card.ChoicesFor(0)[1].IsChecked = true;
        card.OwnAnswerFor(1).Text = "half typed";

        hub.ReplaceBoardForTests(hub.Board.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"PostToolUse\",\"session_id\":\"s2\",\"tool_name\":\"Read\"}")!, T0));
        hub.ReplaceBoardForTests(hub.Board.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s2\",\"tool_name\":\"Grep\",\"tool_input\":{\"pattern\":\"x\"}}")!, T0));

        var after = All<QuestionCard>(module).Single();
        Assert.Same(card, after);
        Assert.True(after.ChoicesFor(0)[1].IsChecked);
        Assert.Equal("half typed", after.OwnAnswerFor(1).Text);
    });

    [Fact]
    public void AnAnsweredTurnShowsWhatClaudeSaidUntilDismissed() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(
            "{\"hook_event_name\":\"UserPromptSubmit\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\Windows\",\"prompt\":\"Fix the tray crash and run the tests\"}",
            "{\"hook_event_name\":\"Stop\",\"session_id\":\"s1\",\"last_assistant_message\":\"Fixed the tray crash. All 1,022 tests pass.\"}");
        hub.ReplaceBoardForTests(hub.Board.Titled("s1", "Faqra milestones 5 and 6"));
        var module = new AgentsModule(hub, () => true);
        Render(module, "island-agents-answered", 420);

        var text = AgentsRenderTests.AllText(module);
        Assert.Contains("Faqra milestones 5 and 6 · Has answered", text);
        Assert.Contains("Fixed the tray crash. All 1,022 tests pass.", text);
        Assert.Contains("Prompted", text);
        Assert.Contains("Fix the tray crash and run the tests", text);
        Assert.Contains("In Windows", text);

        Click(ButtonNamed(module, "Dismiss"));
        var after = AgentsRenderTests.AllText(module);
        Assert.DoesNotContain("Has answered", after);
        Assert.Contains("Fixed the tray crash. All 1,022 tests pass.", after); // now under "Claude said"
    });

    [Fact]
    public void GoToWindowAppearsOnceTheWindowIsKnown() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(WorkingLine);
        var module = new AgentsModule(hub, () => true);
        Assert.DoesNotContain(All<Button>(module), button => button.Content as string == "Go to window");

        hub.RememberWindowForTests("s2", 4242);
        hub.ReplaceBoardForTests(hub.Board);
        AgentSession? asked = null;
        module.GoToWindowRequested += session => asked = session;
        Click(ButtonNamed(module, "Go to window"));
        Assert.Equal("s2", asked!.Id);
    });

    [Fact]
    public void TheShortcutsStepThroughSessions() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(
            "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"a\"}",
            "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"b\"}",
            "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"c\"}");
        var module = new AgentsModule(hub, () => true);
        var order = hub.Board.Ordered.Select(session => session.Id).ToList();
        Assert.Equal(order[0], module.FocusedSession!.Id);
        module.MoveFocus(1);
        Assert.Equal(order[1], module.FocusedSession!.Id);
        module.MoveFocus(-1);
        module.MoveFocus(-1);
        Assert.Equal(order[2], module.FocusedSession!.Id); // wraps around
    });

    [Fact]
    public void ANewRequestDoesNotPullTheFocusFromACardInUse() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(WaitingLine, "{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s3\",\"tool_name\":\"Bash\"}");
        hub.ReplaceRequestsForTests(Approval(session: "s1"));
        var module = new AgentsModule(hub, () => true);
        module.FocusSession("s1");

        module.OfferFocus("s3");
        Assert.Equal("s1", module.FocusedSession!.Id);

        hub.ReplaceRequestsForTests();
        module.OfferFocus("s3");
        Assert.Equal("s3", module.FocusedSession!.Id);
    });
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.App.Tests --filter "FullyQualifiedName~AgentCardRenderTests"`
Expected: build FAILS with "The type or namespace name 'ApprovalCard' could not be found".

- [ ] **Step 3: Add the card strings**

In `src/Faqra.Core/Localization/AgentsStrings.cs`, add under `// Island` after `public required string LastMessageHeader { get; init; }`:

```csharp
    public required string PromptedHeader { get; init; }
    public required string InFolderFormat { get; init; }
    public required string GoToWindow { get; init; }

    // Cards
    public required string CardTitleFormat { get; init; }
    public required string Allow { get; init; }
    public required string Deny { get; init; }
    public required string AlwaysAllow { get; init; }
    public required string AlwaysSavesFormat { get; init; }
    public required string AlwaysAcceptEdits { get; init; }
    public required string AnswerInClaude { get; init; }
    public required string QuestionOwnAnswer { get; init; }
    public required string Send { get; init; }
    public required string QuestionUnreadable { get; init; }
    public required string AnsweredTitle { get; init; }
    public required string Dismiss { get; init; }
```

In `src/Faqra.Core/Localization/AgentsStrings.EnUS.cs`, add after `LastMessageHeader = "Claude said",`:

```csharp
        PromptedHeader = "Prompted",
        InFolderFormat = "In {0}",
        GoToWindow = "Go to window",
        CardTitleFormat = "{0} · {1}",
        Allow = "Allow",
        Deny = "Deny",
        AlwaysAllow = "Always allow",
        AlwaysSavesFormat = "Always allow also saves: {0}",
        AlwaysAcceptEdits = "accept edits for this session",
        AnswerInClaude = "Answer in Claude Code",
        QuestionOwnAnswer = "Your own answer",
        Send = "Send",
        QuestionUnreadable = "Faqra can't read this question. Answer it in Claude Code.",
        AnsweredTitle = "Has answered",
        Dismiss = "Dismiss",
```

- [ ] **Step 4: Write the cards**

`src/Faqra.App/Island/Modules/AgentCards.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// The cards play the role of Coucou's approval and question cards (windows/src/island/hooks.ts),
// https://github.com/Louis-CFM/coucou (MIT License, Copyright (c) 2026 Louis Raillé).

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Faqra.Core.Agents;
using Faqra.Core.Localization;

namespace Faqra.App.Island.Modules;

/// <summary>The cards' shared look: a filled rounded panel, a title, pill buttons with the primary one in the accent.</summary>
internal static class AgentCardParts
{
    internal static readonly FontFamily TextFont = new("Segoe UI Variable Text, Segoe UI");
    internal static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas");

    internal static string Format(string format, params object[] values) => string.Format(CultureInfo.CurrentCulture, format, values);

    internal static Border Panel(UIElement content) => new()
    {
        Background = IslandPalette.Fill,
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(12),
        Margin = new Thickness(0, 0, 0, 10),
        Child = content,
    };

    internal static TextBlock Title(string text) => new()
    {
        Text = text,
        FontFamily = TextFont,
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Foreground = IslandPalette.Primary,
        TextWrapping = TextWrapping.Wrap,
    };

    internal static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontFamily = TextFont,
        FontSize = 12,
        Foreground = IslandPalette.Secondary,
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>A pill button. The primary one is filled with the accent and carries dark text, so it reads first.</summary>
    internal static Button Action(string text, bool primary, RoutedEventHandler click)
    {
        var button = new Button
        {
            Content = text,
            FontFamily = TextFont,
            FontSize = 12,
            FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
            MinWidth = 72,
            Height = 30,
            Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(8, 0, 0, 0),
            Foreground = primary ? IslandPalette.Surface : IslandPalette.Primary,
            Background = primary ? IslandPalette.Accent : IslandPalette.FillStrong,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Template = ActionTemplate,
        };
        button.Click += click;
        return button;
    }

    /// <summary>A low-key text button for the way out ("Answer in Claude Code").</summary>
    internal static Button Quiet(string text, RoutedEventHandler click)
    {
        var button = Action(text, primary: false, click);
        button.Background = Brushes.Transparent;
        button.Foreground = IslandPalette.Secondary;
        button.Padding = new Thickness(0);
        button.Margin = new Thickness(0, 6, 0, 0);
        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.MinWidth = 0;
        return button;
    }

    internal static StackPanel Actions(params UIElement[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var button in buttons)
        {
            row.Children.Add(button);
        }
        return row;
    }

    /// <summary>The actions on the right, the way out under them on the left: both fit the island's width.</summary>
    internal static StackPanel Footer(StackPanel actions, Button? quiet)
    {
        var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        footer.Children.Add(actions);
        if (quiet is not null)
        {
            footer.Children.Add(quiet);
        }
        return footer;
    }

    /// <summary>Rounded chrome that keeps its own colour: lighter on hover, pressed in on click, dimmed when disabled.</summary>
    private static readonly ControlTemplate ActionTemplate = BuildActionTemplate();

    private static ControlTemplate BuildActionTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border), "Chrome");
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(15));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.88, "Chrome"));
        template.Triggers.Add(hover);
        var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(UIElement.RenderTransformProperty, new ScaleTransform(0.97, 0.97), "Chrome"));
        pressed.Setters.Add(new Setter(UIElement.RenderTransformOriginProperty, new Point(0.5, 0.5), "Chrome"));
        template.Triggers.Add(pressed);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4, "Chrome"));
        template.Triggers.Add(disabled);
        template.Seal();
        return template;
    }
}

/// <summary>Claude wants to use a tool: what it would do, then Deny, Always allow and Allow, with Allow the primary.</summary>
internal sealed class ApprovalCard : ContentControl
{
    public ApprovalCard(AgentRequest request, string name, AgentsStrings s, Action<AgentDecision> decide, Action release)
    {
        var stack = new StackPanel();
        stack.Children.Add(AgentCardParts.Title(AgentCardParts.Format(s.CardTitleFormat, name, AgentsText.RequestTitle(request, s))));
        if (request.Detail.Length > 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = request.Detail,
                FontFamily = AgentCardParts.MonoFont,
                FontSize = 12,
                Foreground = IslandPalette.Secondary,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxHeight = 96,
                Margin = new Thickness(0, 6, 0, 0),
            });
        }
        if (request.CanAlways)
        {
            var saves = request.AlwaysRules.ToList();
            if (request.AlwaysAcceptsEdits)
            {
                saves.Add(s.AlwaysAcceptEdits);
            }
            var note = AgentCardParts.Caption(AgentCardParts.Format(s.AlwaysSavesFormat, string.Join(", ", saves)));
            note.Foreground = IslandPalette.Tertiary;
            note.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(note);
        }
        var actions = AgentCardParts.Actions(AgentCardParts.Action(s.Deny, primary: false, (_, _) => decide(AgentDecision.Deny())));
        if (request.CanAlways)
        {
            actions.Children.Add(AgentCardParts.Action(s.AlwaysAllow, primary: false, (_, _) => decide(AgentDecision.Always)));
        }
        // Allow is never the default button: Enter must not run a command the owner has not read.
        actions.Children.Add(AgentCardParts.Action(s.Allow, primary: true, (_, _) => decide(AgentDecision.Allow)));
        stack.Children.Add(AgentCardParts.Footer(actions, AgentCardParts.Quiet(s.AnswerInClaude, (_, _) => release())));
        Content = AgentCardParts.Panel(stack);
    }
}

/// <summary>
/// Claude asks one or more questions: every option shown (radio buttons, or check boxes when several may be picked),
/// a box for the owner's own words, then Send once every question has an answer.
/// </summary>
internal sealed class QuestionCard : ContentControl
{
    private readonly AgentRequest _request;
    private readonly Action<AgentDecision> _decide;
    private readonly List<Part> _parts = [];
    private readonly Button? _send;

    /// <summary>One question's controls.</summary>
    private sealed record Part(AgentQuestion Question, List<ToggleButton> Choices, TextBox Own);

    public QuestionCard(AgentRequest request, string name, AgentsStrings s, Action<AgentDecision> decide, Action release, Action wantKeyboard)
    {
        _request = request;
        _decide = decide;
        var stack = new StackPanel();
        stack.Children.Add(AgentCardParts.Title(AgentCardParts.Format(s.CardTitleFormat, name, s.StateQuestion)));
        var elsewhere = AgentCardParts.Quiet(s.AnswerInClaude, (_, _) => release());
        if (request.Questions.Count == 0)
        {
            var unreadable = AgentCardParts.Caption(s.QuestionUnreadable);
            unreadable.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(unreadable);
            stack.Children.Add(AgentCardParts.Footer(AgentCardParts.Actions(), elsewhere));
            Content = AgentCardParts.Panel(stack);
            return;
        }
        for (var i = 0; i < request.Questions.Count; i++)
        {
            _parts.Add(AddQuestion(stack, request.Questions[i], $"{request.Id}-{i}", s, wantKeyboard));
        }
        _send = AgentCardParts.Action(s.Send, primary: true, (_, _) => Send());
        stack.Children.Add(AgentCardParts.Footer(AgentCardParts.Actions(_send), elsewhere));
        Content = AgentCardParts.Panel(stack);
        UpdateSend();
    }

    internal IReadOnlyList<ToggleButton> ChoicesFor(int question) => _parts[question].Choices;

    internal TextBox OwnAnswerFor(int question) => _parts[question].Own;

    internal Button? SendButton => _send;

    /// <summary>Every question's answer keyed by its text, or null until each has one.</summary>
    internal IReadOnlyDictionary<string, string>? Answers() => AgentQuestions.Answers(
        _request.Questions,
        _parts.Select(part => AgentQuestions.Compose(part.Question, Picked(part), part.Own.Text)).ToList());

    private Part AddQuestion(StackPanel stack, AgentQuestion question, string group, AgentsStrings s, Action wantKeyboard)
    {
        if (question.Header.Length > 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = question.Header,
                FontFamily = AgentCardParts.TextFont,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = IslandPalette.Tertiary,
                Margin = new Thickness(0, 12, 0, 0),
            });
        }
        var text = AgentCardParts.Caption(question.Text);
        text.Foreground = IslandPalette.Primary;
        text.FontSize = 13;
        text.Margin = new Thickness(0, question.Header.Length > 0 ? 2 : 12, 0, 0);
        stack.Children.Add(text);

        var own = new Wpf.Ui.Controls.TextBox { PlaceholderText = s.QuestionOwnAnswer, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };
        System.Windows.Automation.AutomationProperties.SetName(own, s.QuestionOwnAnswer);
        var choices = new List<ToggleButton>();
        foreach (var option in question.Options)
        {
            ToggleButton choice = question.MultiSelect ? new CheckBox() : new RadioButton { GroupName = group };
            choice.Content = OptionContent(option);
            choice.Tag = option.Label;
            choice.Margin = new Thickness(0, 6, 0, 0);
            System.Windows.Automation.AutomationProperties.SetName(choice, option.Label);
            choice.Checked += (_, _) =>
            {
                // One answer per single-choice question: a pick clears the owner's own words.
                if (!question.MultiSelect)
                {
                    own.Text = string.Empty;
                }
                UpdateSend();
            };
            choice.Unchecked += (_, _) => UpdateSend();
            choices.Add(choice);
            stack.Children.Add(choice);
        }
        // The island never takes the keyboard by itself; clicking into the box is the owner asking for it. Registered
        // with handledEventsToo so the text box's own mouse handling cannot swallow it.
        own.AddHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler((_, _) => wantKeyboard()), handledEventsToo: true);
        own.TextChanged += (_, _) =>
        {
            if (!question.MultiSelect && own.Text.Trim().Length > 0)
            {
                foreach (var choice in choices)
                {
                    choice.IsChecked = false;
                }
            }
            UpdateSend();
        };
        own.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _send?.IsEnabled == true)
            {
                e.Handled = true;
                Send();
            }
        };
        stack.Children.Add(own);
        return new Part(question, choices, own);
    }

    private static List<string> Picked(Part part) =>
        part.Choices.Where(choice => choice.IsChecked == true).Select(choice => choice.Tag as string).OfType<string>().ToList();

    private void UpdateSend()
    {
        if (_send is not null)
        {
            _send.IsEnabled = Answers() is not null;
        }
    }

    private void Send()
    {
        if (Answers() is not { } answers)
        {
            return;
        }
        var ownWords = _parts.Any(part => part.Own.Text.Trim().Length > 0);
        _decide(ownWords && !AgentQuestions.OwnTextInAnswers
            ? AgentDecision.Deny(AgentQuestions.AsDenialMessage(answers))
            : AgentDecision.Answer(answers));
    }

    private static UIElement OptionContent(AgentOption option)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = option.Label,
            FontFamily = AgentCardParts.TextFont,
            FontSize = 12,
            Foreground = IslandPalette.Primary,
            TextWrapping = TextWrapping.Wrap,
        });
        if (option.Description.Length > 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = option.Description,
                FontFamily = AgentCardParts.TextFont,
                FontSize = 11,
                Foreground = IslandPalette.Secondary,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        return stack;
    }
}

/// <summary>Claude finished a turn: what it said, and Dismiss. A3 adds the reply box here.</summary>
internal sealed class AnsweredCard : ContentControl
{
    public AnsweredCard(AgentSession session, AgentsStrings s, Action dismiss)
    {
        var stack = new StackPanel();
        stack.Children.Add(AgentCardParts.Title(AgentCardParts.Format(s.CardTitleFormat, session.Name, s.AnsweredTitle)));
        stack.Children.Add(new TextBlock
        {
            Text = session.LastMessage ?? string.Empty,
            FontFamily = AgentCardParts.TextFont,
            FontSize = 12,
            Foreground = IslandPalette.Primary,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.WordEllipsis,
            MaxHeight = 128,
            Margin = new Thickness(0, 6, 0, 0),
        });
        stack.Children.Add(AgentCardParts.Footer(AgentCardParts.Actions(AgentCardParts.Action(s.Dismiss, primary: false, (_, _) => dismiss())), quiet: null));
        Content = AgentCardParts.Panel(stack);
    }
}
```

- [ ] **Step 5: Replace the module**

`src/Faqra.App/Island/Modules/AgentsModule.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// The session list plays the role of Coucou's session ticker (windows/src/views), https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé), with a thinking orb in place of its mascot.

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Faqra.App.Agents;
using Faqra.Core.Agents;
using Faqra.Core.Localization;
using Faqra.Services.Agents;

namespace Faqra.App.Island.Modules;

/// <summary>
/// Every agent session, most urgent first. Above the list sits the focused session's card (an approval, a question, or
/// its latest answer); below it, the session's folder, the owner's last prompt, Claude's last words and its activity.
/// Rows and cards are kept and updated in place, so orbs keep moving and the owner's picks and typing survive events.
/// </summary>
public sealed class AgentsModule : UserControl
{
    private const int ActivityLines = 6;
    private const string AnsweredKey = "answered:";
    private static readonly FontFamily TextFont = new("Segoe UI Variable Text, Segoe UI");
    private static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas");

    private readonly AgentHub _hub;
    private readonly Func<bool> _hooksInstalled;
    private readonly AgentsStrings _s = AgentsStrings.For(L10n.Shared.Language);
    private readonly Dictionary<string, SessionRow> _rows = new(StringComparer.Ordinal);
    private readonly ContentControl _card = new();
    private readonly StackPanel _list = new();
    private readonly StackPanel _detail = new() { Margin = new Thickness(0, 12, 0, 0) };
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private string? _focused;
    private string? _cardKey;

    public AgentsModule(AgentHub hub, Func<bool> hooksInstalled)
    {
        _hub = hub;
        _hooksInstalled = hooksInstalled;
        var body = new StackPanel();
        body.Children.Add(_card);
        body.Children.Add(_list);
        body.Children.Add(_detail);
        Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _clock.Tick += (_, _) => RefreshTimes();
        Render();
        _hub.Changed += Render;
        Loaded += (_, _) => _clock.Start();
        Unloaded += (_, _) =>
        {
            _clock.Stop();
            _hub.Changed -= Render;
        };
    }

    /// <summary>The owner clicked into a box that needs typing: the island should take the keyboard.</summary>
    public event Action? KeyboardWanted;

    /// <summary>The owner asked to see a session's window.</summary>
    public event Action<AgentSession>? GoToWindowRequested;

    /// <summary>The session the card and the details are about.</summary>
    public AgentSession? FocusedSession =>
        _focused is not null && _hub.Board.Sessions.TryGetValue(_focused, out var session) ? session : null;

    /// <summary>Shows this session's card and details.</summary>
    public void FocusSession(string sessionId)
    {
        if (_hub.Board.Sessions.ContainsKey(sessionId))
        {
            _focused = sessionId;
            Render();
        }
    }

    /// <summary>Moves to this session, unless the owner is in the middle of another session's request.</summary>
    public void OfferFocus(string sessionId)
    {
        if (_focused is null || _focused == sessionId || _hub.Requests.All(request => request.SessionId != _focused))
        {
            FocusSession(sessionId);
        }
    }

    /// <summary>Moves the focus to the next (+1) or previous (-1) session in the list, wrapping around.</summary>
    public void MoveFocus(int delta)
    {
        var sessions = _hub.Board.Ordered;
        if (sessions.Count == 0)
        {
            return;
        }
        var at = sessions.Select(session => session.Id).ToList().IndexOf(_focused ?? string.Empty);
        var next = at < 0 ? 0 : ((at + delta) % sessions.Count + sessions.Count) % sessions.Count;
        _focused = sessions[next].Id;
        Render();
    }

    private void Render()
    {
        var sessions = _hub.Board.Ordered;
        if (sessions.Count == 0)
        {
            _rows.Clear();
            _list.Children.Clear();
            _detail.Children.Clear();
            ShowCard(null, null);
            _list.Children.Add(Empty());
            return;
        }
        if (_list.Children.Count > 0 && _list.Children[0] is not SessionRow)
        {
            _list.Children.Clear();
        }
        foreach (var gone in _rows.Keys.Except(sessions.Select(s => s.Id)).ToList())
        {
            _list.Children.Remove(_rows[gone]);
            _rows.Remove(gone);
        }
        if (_focused is null || sessions.All(s => s.Id != _focused))
        {
            // The board orders sessions waiting on the owner first.
            _focused = sessions[0].Id;
        }
        for (var i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            if (!_rows.TryGetValue(session.Id, out var row))
            {
                row = new SessionRow(id => FocusSession(id));
                _rows[session.Id] = row;
            }
            row.Update(session, _s, DateTimeOffset.Now, session.Id == _focused);
            var at = _list.Children.IndexOf(row);
            if (at != i)
            {
                if (at >= 0)
                {
                    _list.Children.RemoveAt(at);
                }
                _list.Children.Insert(i, row);
            }
        }
        var focused = sessions.First(s => s.Id == _focused);
        RenderCard(focused);
        RenderDetail(focused);
    }

    /// <summary>The focused session's card, rebuilt only when what it shows changes, so picks and typing survive.</summary>
    private void RenderCard(AgentSession session)
    {
        if (_hub.Requests.FirstOrDefault(r => r.SessionId == session.Id) is { } request)
        {
            ShowCard(request.Id, () => request.Kind == AgentRequestKind.Question
                ? new QuestionCard(request, session.Name, _s, decision => _hub.Answer(request.Id, decision), () => _hub.Release(request.Id), () => KeyboardWanted?.Invoke())
                : new ApprovalCard(request, session.Name, _s, decision => _hub.Answer(request.Id, decision), () => _hub.Release(request.Id)));
            return;
        }
        if (session.Unread && session.LastMessage is { Length: > 0 })
        {
            ShowCard($"{AnsweredKey}{session.Id}:{session.FinishedAt:O}", () => new AnsweredCard(session, _s, () => _hub.MarkRead(session.Id)));
            return;
        }
        ShowCard(null, null);
    }

    private void ShowCard(string? key, Func<UIElement>? build)
    {
        if (key == _cardKey)
        {
            return;
        }
        _cardKey = key;
        _card.Content = build?.Invoke();
    }

    private void RefreshTimes()
    {
        foreach (var session in _hub.Board.Ordered)
        {
            if (_rows.TryGetValue(session.Id, out var row))
            {
                row.Update(session, _s, DateTimeOffset.Now, session.Id == _focused);
            }
        }
    }

    private void RenderDetail(AgentSession session)
    {
        _detail.Children.Clear();
        var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 10) };
        if (_hub.WindowOwner(session.Id) is not null)
        {
            var go = AgentCardParts.Action(_s.GoToWindow, primary: false, (_, _) => GoToWindowRequested?.Invoke(session));
            DockPanel.SetDock(go, Dock.Right);
            header.Children.Add(go);
        }
        header.Children.Add(new TextBlock
        {
            Text = string.Format(CultureInfo.CurrentCulture, _s.InFolderFormat, session.Project),
            FontFamily = TextFont,
            FontSize = 12,
            Foreground = IslandPalette.Tertiary,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        _detail.Children.Add(header);
        if (session.LastPrompt is { Length: > 0 } prompt)
        {
            _detail.Children.Add(Label(_s.PromptedHeader));
            _detail.Children.Add(Paragraph(prompt));
        }
        // An answered card already shows Claude's words.
        var answeredOnCard = _cardKey?.StartsWith(AnsweredKey, StringComparison.Ordinal) == true;
        if (!answeredOnCard && session.LastMessage is { Length: > 0 } message)
        {
            _detail.Children.Add(Label(_s.LastMessageHeader));
            _detail.Children.Add(Paragraph(message));
        }
        if (session.Steps.Count == 0)
        {
            return;
        }
        _detail.Children.Add(Label(_s.ActivityHeader));
        foreach (var step in session.Steps.Reverse().Take(ActivityLines))
        {
            _detail.Children.Add(new TextBlock
            {
                Text = AgentsText.Step(step, _s),
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontFamily = TextFont,
                FontSize = 12,
                Foreground = IslandPalette.Secondary,
                Margin = new Thickness(0, 2, 0, 0),
            });
        }
    }

    private static TextBlock Paragraph(string text) => new()
    {
        Text = text,
        MaxHeight = 54,
        TextWrapping = TextWrapping.Wrap,
        TextTrimming = TextTrimming.WordEllipsis,
        FontFamily = TextFont,
        FontSize = 12,
        Foreground = IslandPalette.Primary,
        Margin = new Thickness(0, 2, 0, 10),
    };

    private UIElement Empty()
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0) };
        var orb = new OrbView { Diameter = 48, HorizontalAlignment = HorizontalAlignment.Center };
        orb.Apply(AgentOrbStyles.For(AgentState.Idle), IslandPalette.Secondary);
        stack.Children.Add(orb);
        stack.Children.Add(new TextBlock
        {
            Text = _s.EmptyTitle,
            Margin = new Thickness(0, 12, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Center,
            FontFamily = TextFont,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = IslandPalette.Primary,
        });
        var installed = _hooksInstalled();
        stack.Children.Add(new TextBlock
        {
            Text = installed ? _s.EmptyHintInstalled : _s.EmptyHintNotInstalled,
            MaxWidth = 340,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontFamily = TextFont,
            FontSize = 12,
            Foreground = IslandPalette.Secondary,
        });
        if (!installed)
        {
            var open = new Button
            {
                Content = _s.OpenSettings,
                FontFamily = TextFont,
                FontSize = 12,
                MinWidth = 120,
                Height = 32,
                Margin = new Thickness(0, 14, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = IslandPalette.Primary,
                Background = IslandPalette.Fill,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Template = MusicModule.RoundButtonTemplate(16),
            };
            open.Click += (_, _) => App.ShowSettings(Core.Settings.SettingsPage.Agents);
            stack.Children.Add(open);
        }
        return stack;
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        FontFamily = TextFont,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = IslandPalette.Tertiary,
    };

    /// <summary>One session: orb, conversation name, status, and how long since its last event.</summary>
    private sealed class SessionRow : Button
    {
        private readonly OrbView _orb = new() { Diameter = 20, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _name = new() { FontFamily = TextFont, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = IslandPalette.Primary, TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly TextBlock _status = new() { FontFamily = TextFont, FontSize = 12, Foreground = IslandPalette.Secondary, TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly TextBlock _elapsed = new() { FontFamily = MonoFont, FontSize = 11, Foreground = IslandPalette.Tertiary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        private string _id = string.Empty;

        public SessionRow(Action<string> focus)
        {
            var grid = new Grid { Margin = new Thickness(10, 8, 10, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(_name);
            text.Children.Add(_status);
            Grid.SetColumn(text, 1);
            Grid.SetColumn(_elapsed, 2);
            grid.Children.Add(_orb);
            grid.Children.Add(text);
            grid.Children.Add(_elapsed);
            Content = grid;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            Margin = new Thickness(0, 0, 0, 4);
            BorderThickness = new Thickness(0);
            Cursor = System.Windows.Input.Cursors.Hand;
            Template = MusicModule.RoundButtonTemplate(10);
            Click += (_, _) => focus(_id);
        }

        public void Update(AgentSession session, AgentsStrings s, DateTimeOffset now, bool focused)
        {
            _id = session.Id;
            var style = AgentOrbStyles.For(session.State);
            _orb.Apply(style, AgentInk.For(style.Tone));
            _name.Text = session.Name;
            _status.Text = AgentsText.Status(session, s);
            _elapsed.Text = AgentsText.Elapsed(now - session.UpdatedAt, s);
            Background = focused ? IslandPalette.FillStrong : IslandPalette.Fill;
            System.Windows.Automation.AutomationProperties.SetName(this, $"{_name.Text}, {_status.Text}");
        }
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.App.Tests`
Expected: PASS (every App test, including A1's `AgentsRenderTests`). Then open the PNGs `island-card-approval.png`, `island-card-question.png`, `island-agents-approval.png` and `island-agents-answered.png` in `%TEMP%\faqra-ui` and check that nothing overlaps or runs past the 412 px width; describe what you saw in the report.

- [ ] **Step 7: Commit**

```bash
git add src/Faqra.App/Island/Modules src/Faqra.Core/Localization tests/Faqra.App.Tests/AgentCardRenderTests.cs
git commit -m "feat(windows): approval, question and answered cards in the island"
```

---

### Task 8: The island opens for agents, chimes, and answers the shortcuts

**Files:**
- Create: `src/Faqra.App/Agents/ClaudeHooks.cs`
- Create: `src/Faqra.Win32/Agents/AlertSound.cs`
- Modify: `src/Faqra.App/Island/IslandController.cs`
- Modify: `src/Faqra.App/AppServices.cs`
- Test: `tests/Faqra.App.Tests/ClaudeHooksTests.cs`

**Interfaces:**
- Consumes: everything above: `AgentHub.CanAsk`, `RequestArrived`, `TurnFinished`, `Requests`, `Release`, `MarkRead`, `WindowOwner` (Tasks 4 and 5), `SessionWindows.Locate`/`BringForward`, `SessionTitles.DefaultProjectsRoot` (Task 5), the Task 6 keys and roles, the Task 7 module API.
- Produces: `internal static class ClaudeHooks` with `HookStatus Read(string path)` and `HookStatus Read()`; `enum AlertKind { NeedsYou, Answered }` and `AlertSound.Play(AlertKind)`; on `IslandController`: `void JumpToWaitingAgent()`, `void GoToAgentWindow()`, `void CycleAgentSession(int delta)`, `internal void TakeKeyboard()`.

- [ ] **Step 1: Write the failing test**

`tests/Faqra.App.Tests/ClaudeHooksTests.cs`:

```csharp
using System.IO;
using Faqra.App.Agents;
using Faqra.Core.Agents.Install;

namespace Faqra.App.Tests;

public class ClaudeHooksTests
{
    [Fact]
    public void ReadsWhatIsInstalledAndShrugsAtWhatCannotBeRead()
    {
        var dir = Directory.CreateTempSubdirectory("faqra-hooks-").FullName;
        var settings = Path.Combine(dir, "settings.json");

        Assert.Equal(new HookStatus(0, 0), ClaudeHooks.Read(settings));

        File.WriteAllText(settings, ClaudeHookConfig.Install(null, @"C:\x\faqra-hook.exe", removeCoucou: false));
        Assert.True(ClaudeHooks.Read(settings).Installed);

        File.WriteAllText(settings, "{\"hooks\":{\"Stop\":[{\"hooks\":[{\"type\":\"command\",\"command\":\"C:/x/coucou-hook.exe Stop\"}]}]}}");
        Assert.Equal(1, ClaudeHooks.Read(settings).CoucouEvents);

        File.WriteAllBytes(settings, [0x7B, 0xFF, 0xFE, 0x7D]);
        Assert.Equal(new HookStatus(0, 0), ClaudeHooks.Read(settings));
        Directory.Delete(dir, recursive: true);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Faqra.App.Tests --filter "FullyQualifiedName~ClaudeHooksTests"`
Expected: build FAILS with "The name 'ClaudeHooks' does not exist in the current context".

- [ ] **Step 3: Write `ClaudeHooks` and `AlertSound`**

`src/Faqra.App/Agents/ClaudeHooks.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.IO;
using Faqra.Core;
using Faqra.Core.Agents.Install;

namespace Faqra.App.Agents;

/// <summary>Which hooks Claude Code's settings hold, for the island: Faqra's (installed?) and Coucou's (watch only?).</summary>
internal static class ClaudeHooks
{
    public static HookStatus Read() => Read(AppPaths.ClaudeSettingsFile);

    /// <summary>The hooks in <paramref name="path"/>; none when the file is missing or not plain UTF-8 JSON.</summary>
    public static HookStatus Read(string path)
    {
        try
        {
            return ClaudeHookConfig.Inspect(File.Exists(path) ? ConfigEdit.Decode(File.ReadAllBytes(path)) : null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ConfigFormatException)
        {
            return new HookStatus(0, 0);
        }
    }
}
```

`src/Faqra.Win32/Agents/AlertSound.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Runtime.InteropServices;

namespace Faqra.Win32.Agents;

public enum AlertKind { NeedsYou, Answered }

/// <summary>
/// Windows' own sounds, as the owner set them in the Sound control panel (Coucou's sounds are not licensed for reuse):
/// the notification sound when an agent needs the owner, the message sound when it answers. Silent when none is set.
/// </summary>
public static class AlertSound
{
    private const uint SND_ASYNC = 0x0001;
    private const uint SND_NODEFAULT = 0x0002;
    private const uint SND_ALIAS = 0x00010000;

    public static void Play(AlertKind kind) =>
        PlaySound(kind == AlertKind.NeedsYou ? "Notification.Default" : "Notification.IM", IntPtr.Zero, SND_ALIAS | SND_ASYNC | SND_NODEFAULT);

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(string sound, IntPtr module, uint flags);
}
```

- [ ] **Step 4: Teach the island about agents**

In `src/Faqra.App/Island/IslandController.cs`:

1. Add `using Faqra.App.Agents;` and `using Faqra.Win32.Agents;` to the usings.
2. Add these fields after `private (string? Id, AgentState? State) _agentsShown;`:

```csharp
    private AgentsModule? _agentsModule;
    private string? _agentsFocus;
```

3. In the constructor, after `_agents.Changed += OnAgentsChanged;`, add:

```csharp
        _agents.RequestArrived += OnAgentRequest;
        _agents.TurnFinished += OnAgentFinished;
        _agents.CanAsk = CanShowAgentCards;
```

4. In `Stop()`, add as its first lines (a closed island can show no card, so Claude Code should ask at once):

```csharp
        foreach (var request in _agents.Requests.ToList())
        {
            _agents.Release(request.Id);
        }
```

5. Replace `BuildModule` with:

```csharp
    private UIElement? BuildModule(IslandModule module)
    {
        _agentsModule = null;
        return module switch
        {
            IslandModule.Music => new MusicModule(_nowPlaying),
            IslandModule.Timer => new TimerModule(),
            IslandModule.System => new SystemModule(_systemMonitor, SystemCards(), _geometry.SystemColumns),
            IslandModule.Mixer => new MixerModule(_store, _runtime.IsAvailable, _mixer),
            IslandModule.FaqraAgents => BuildAgentsModule(),
            // Modules whose feature is not ported yet keep their place in the section list; their
            // content arrives with the feature.
            _ => new ModulePlaceholder(ModuleTitle(module)),
        };
    }

    private AgentsModule BuildAgentsModule()
    {
        var module = new AgentsModule(_agents, () => ClaudeHooks.Read().Installed);
        module.KeyboardWanted += TakeKeyboard;
        module.GoToWindowRequested += GoToWindow;
        if (_agentsFocus is { } sessionId)
        {
            module.FocusSession(sessionId);
            _agentsFocus = null;
        }
        _agentsModule = module;
        return module;
    }
```

6. Delete the `AgentHooksInstalled` method (its one caller is now `BuildAgentsModule`).

7. Replace `ShapeClicked` with:

```csharp
    internal void ShapeClicked()
    {
        if (_presentation == IslandPresentation.Expanded)
        {
            return;
        }
        // A waiting card is what the owner came for.
        if (_agents.Requests.FirstOrDefault() is { } waiting)
        {
            _agentsFocus = waiting.SessionId;
            Expand(takeFocus: true, IslandModule.FaqraAgents);
            return;
        }
        Expand(takeFocus: true);
    }
```

8. Add a new region before `// MARK: environment`:

```csharp
    // MARK: agents

    /// <summary>The island can show the Agents section right now: running, not stepped aside for a fullscreen app, and the section on.</summary>
    private bool CanShowAgents() =>
        _window is not null
        && !_suspendedForFullscreen
        && _runtime.IsAvailable(AppFeature.FaqraAgents)
        && VisibleModules().Contains(IslandModule.FaqraAgents);

    /// <summary>
    /// Whether a card may take a request (the hub asks for each one). Never while Coucou's hooks are installed: two apps
    /// answering one request would race, so Faqra only watches then.
    /// </summary>
    private bool CanShowAgentCards() => CanShowAgents() && ClaudeHooks.Read().CoucouEvents == 0;

    private bool IsShowingAgents() =>
        _presentation == IslandPresentation.Expanded && _module == IslandModule.FaqraAgents && !_sectionsOpen && _agentsModule is not null;

    /// <summary>An agent waits on the owner: chime if asked to, then open on its card without taking the keyboard.</summary>
    private void OnAgentRequest(AgentRequest request)
    {
        if (_store.Bool(DefaultsKey.FaqraAgentsNeedsYouSound))
        {
            AlertSound.Play(AlertKind.NeedsYou);
        }
        OpenOnAgent(request.SessionId);
    }

    /// <summary>An agent finished a turn: chime and, if the owner wants it, open on its answer without taking the keyboard.</summary>
    private void OnAgentFinished(AgentSession session)
    {
        if (!CanShowAgents())
        {
            return;
        }
        if (_store.Bool(DefaultsKey.FaqraAgentsAnsweredSound))
        {
            AlertSound.Play(AlertKind.Answered);
        }
        if (_store.Bool(DefaultsKey.FaqraAgentsOpenOnAnswer))
        {
            OpenOnAgent(session.Id);
        }
    }

    /// <summary>Opens on a session without activating; an Agents section already open only offers it the focus.</summary>
    private void OpenOnAgent(string sessionId)
    {
        if (_window is null)
        {
            return;
        }
        if (IsShowingAgents())
        {
            _agentsModule!.OfferFocus(sessionId);
            return;
        }
        _agentsFocus = sessionId;
        _sectionsOpen = false;
        Expand(takeFocus: false, IslandModule.FaqraAgents);
    }

    /// <summary>The owner is about to type in the island (a question's own-answer box): take the keyboard now.</summary>
    internal void TakeKeyboard()
    {
        if (_window is null || _presentation == IslandPresentation.Collapsed || WindowStyles.ForegroundWindow() == _window.Handle)
        {
            return;
        }
        _previousForeground = WindowStyles.ForegroundWindow();
        WindowStyles.SetNonActivating(_window.Handle, nonActivating: false);
        WindowStyles.Focus(_window.Handle);
        _window.Activate();
    }

    private void GoToWindow(AgentSession session)
    {
        if (_agents.WindowOwner(session.Id) is not { } owner)
        {
            return;
        }
        _agents.MarkRead(session.Id);
        // The keyboard goes to the window being brought forward, not back to whatever had it before.
        _previousForeground = IntPtr.Zero;
        Collapse();
        SessionWindows.BringForward(owner, session.Project);
    }

    /// <summary>Shortcut: open on the oldest waiting card (else the most urgent session) and take the keyboard.</summary>
    public void JumpToWaitingAgent()
    {
        if (_window is null || _suspendedForFullscreen)
        {
            return;
        }
        _agentsFocus = _agents.Requests.FirstOrDefault()?.SessionId ?? _agents.Board.MostUrgent?.Id;
        _sectionsOpen = false;
        Expand(takeFocus: true, IslandModule.FaqraAgents);
    }

    /// <summary>Shortcut: bring the focused (else the most urgent) session's window forward.</summary>
    public void GoToAgentWindow()
    {
        if ((_agentsModule?.FocusedSession ?? _agents.Board.MostUrgent) is { } session)
        {
            GoToWindow(session);
        }
    }

    /// <summary>Shortcut: the next (+1) or previous (-1) session, opening the island on Agents first.</summary>
    public void CycleAgentSession(int delta)
    {
        if (_window is null || _suspendedForFullscreen)
        {
            return;
        }
        if (IsShowingAgents())
        {
            _agentsModule!.MoveFocus(delta);
            return;
        }
        _agentsFocus = _agents.Board.MostUrgent?.Id;
        _sectionsOpen = false;
        Expand(takeFocus: false, IslandModule.FaqraAgents);
    }
```

9. In `Dispose`, after `_agents.Changed -= OnAgentsChanged;`, add:

```csharp
        _agents.RequestArrived -= OnAgentRequest;
        _agents.TurnFinished -= OnAgentFinished;
        _agents.CanAsk = () => false;
```

- [ ] **Step 5: Wire the hub and the shortcuts**

In `src/Faqra.App/AppServices.cs`:

1. Replace the `Agents = new AgentHub(...)` statement with:

```csharp
        Agents = new AgentHub(
            AgentPipe.Name(WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName, Environment.GetEnvironmentVariable),
            new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher),
            () => DateTimeOffset.Now,
            AppPaths.AgentsLogFile,
            SessionWindows.Locate,
            SessionTitles.DefaultProjectsRoot);
```

2. In `StartFeatures`, after `HotKeys.Bind(Core.Shortcuts.GlobalShortcutRole.CommandBar, CommandBar.Toggle);`, add:

```csharp
        HotKeys.Bind(Core.Shortcuts.GlobalShortcutRole.AgentsJumpToWaiting, Island.JumpToWaitingAgent);
        HotKeys.Bind(Core.Shortcuts.GlobalShortcutRole.AgentsGoToWindow, Island.GoToAgentWindow);
        HotKeys.Bind(Core.Shortcuts.GlobalShortcutRole.AgentsNextSession, () => Island.CycleAgentSession(1));
        HotKeys.Bind(Core.Shortcuts.GlobalShortcutRole.AgentsPreviousSession, () => Island.CycleAgentSession(-1));
```

3. In the `[AppFeature.FaqraAgents]` binding, add `HotKeys?.Sync();` right after `Agents.SetRunning(on);`, so the four shortcuts follow the feature.

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test Faqra.sln`
Expected: PASS (every test in the three projects).

- [ ] **Step 7: Commit**

```bash
git add src/Faqra.App src/Faqra.Win32/Agents/AlertSound.cs tests/Faqra.App.Tests/ClaudeHooksTests.cs
git commit -m "feat(windows): island opens for agent requests and answers, with sounds and shortcuts"
```

---

### Task 9: Live check with Claude Code, then docs

This task is the controller's, not a subagent's: it needs the owner's OK to stop their running Faqra-test, to run Claude Code sessions on their account, and it reads the desktop. Ask before each of the three numbered OKs below.

**Files:**
- Modify: `Windows/KNOWN-ISSUES.md`, `Windows/design-system.md`, `Windows/THIRD-PARTY-NOTICES.md`
- Possibly modify: `src/Faqra.Core/Agents/AgentQuestions.cs` (Step 5's outcome)

- [ ] **Step 1: Full suite and build**

Run: `dotnet test Faqra.sln` from `Windows/`. Expected: PASS.

- [ ] **Step 2 (OK 1): Publish to Faqra-test**

Check whether the owner is using Faqra-test (it runs from `C:\Users\Tigre\Faqra-test\Faqra.exe`); ask before stopping it. Then publish both executables there (A1's dual publish):

```powershell
dotnet publish src/Faqra.App/Faqra.App.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o C:\Users\Tigre\Faqra-test
dotnet publish src/Faqra.Hook/Faqra.Hook.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:PublishReadyToRun=true -o C:\Users\Tigre\Faqra-test
```

Start Faqra-test again and check `%LOCALAPPDATA%\Faqra\bin\faqra-hook.exe` has the new build's timestamp.

- [ ] **Step 3: Decide where the hooks come from**

Read `ClaudeHookConfig.Inspect` of `~/.claude/settings.json`. If Faqra's hooks are installed globally (the owner clicked Install after A1), use them: a scratch folder with no project hooks. If they are not, use a scratch folder with project-local hooks in `.claude/settings.local.json` (A1's `live-agents-test.ps1` writes them; keep `PermissionRequest`'s timeout at 120). Never both: two sets of hooks would put two cards up for one request.

- [ ] **Step 4 (OK 2): Approvals**

In a scratch folder, run headless sessions; Claude Code runs PermissionRequest hooks in `-p` mode too, and denies when no hook decides:

1. Record the foreground window (`GetForegroundWindow`), run `claude -p "Run exactly this command and nothing else: echo faqra-a2-allow"` in the background, wait for Faqra's island to show the approval card, and check the foreground window did not change (the island must not take focus). Click Allow through UI Automation (`System.Windows.Automation`, the island window of the Faqra-test process, the button named "Allow"). Expected: the output contains `faqra-a2-allow`; `agents.log` shows `Decision … Bash allow`.
2. Same with `echo faqra-a2-deny` and the "Deny" button. Expected: Claude reports the command was denied; nothing ran.
3. Same with `echo faqra-a2-always` and "Always allow". Expected: the scratch folder's `.claude/settings.local.json` gains an allow rule for that command, and a second identical run asks nothing.
4. Same, but click nothing for a while and use "Answer in Claude Code". Expected: the relay prints nothing and `claude -p` denies at once (no 108 s wait).

- [ ] **Step 5: Questions, and settling free text**

Run `claude -p "Use the AskUserQuestion tool to ask me two questions at once: 'Which color?' with options Red and Blue, and 'Which extras?' allowing several of Tests, Docs and Lint. Then repeat my answers back verbatim."`. In the question card pick Blue, check Tests and Lint, type `and a changelog` in the second own-answer box, and press Send (UI Automation: radio "Blue", check boxes "Tests" and "Lint", the text box "Your own answer", button "Send").

- If AskUserQuestion is not offered in `-p` mode (the hooks reference says `-p` offers it only with a permission host), ask the owner (OK 3) to run the same prompt in an interactive Claude Code session in Windows Terminal (`Start-Process wt -ArgumentList '-d', '<scratch>', 'claude'`), then answer from the island the same way.
- Expected: Claude repeats `Blue` and `Tests, Lint, and a changelog`. That settles free text: keep `AgentQuestions.OwnTextInAnswers` true.
- If Claude Code rejects the answer or reads it wrongly, change `OwnTextInAnswers` to `false` (one line in `src/Faqra.Core/Agents/AgentQuestions.cs`), rebuild, publish, and repeat: Claude should now read the owner's words from the denial message. Commit that as `fix(windows): own answers reach Claude as a message it reads`.
- In the interactive run, also note whether Claude Code shows its own question or permission prompt while Faqra's card is up, or only after Faqra lets go. Record it for KNOWN-ISSUES.

- [ ] **Step 6: Names, Prompted, answered alert, go to window, shortcuts**

With an interactive session in Windows Terminal (OK 3 covers it):

1. Send a prompt; when the turn ends, the island chimes, opens without taking focus, and shows "<conversation name> · Has answered" with Claude's words. The row shows the conversation's name, not the folder. "Prompted" shows the prompt.
2. "Go to window" brings the Windows Terminal window forward.
3. Post `WM_HOTKEY` for each agent role to Faqra's `SystemEventsWindow` (IDs: jump 4, window 5, next 6, previous 7) and check: jump opens on Agents with the keyboard; window brings the terminal forward; next and previous move the focused row.
4. With the island's Agents section hidden (Settings > Island > sections), a permission request gets no card and Claude Code asks at once.

- [ ] **Step 7: Docs, memory, push**

- `Windows/KNOWN-ISSUES.md`: replace A1's "Agents only watches for now" bullet with what A2 leaves: replies come in A3 (the answered card has no reply box yet), what Step 5 found about Claude Code's own prompt while a card is up, `-p` behaviour for questions if it mattered, Markdown in Claude's words shows as plain text until A3, and the session name appearing only once Claude has titled the conversation.
- `Windows/design-system.md`: add the agent cards to Components (approval, question, answered; accent primary pill with dark text, `FillStrong` secondary pills, a quiet text button for the way out) and the four shortcut defaults.
- `Windows/THIRD-PARTY-NOTICES.md`: extend the Coucou paragraph's list with "the permission replies, question answers and session window lookup".
- Commit (`docs(windows): …`), push `windows-stage-1`, and update the project memory with where A2 left off.
