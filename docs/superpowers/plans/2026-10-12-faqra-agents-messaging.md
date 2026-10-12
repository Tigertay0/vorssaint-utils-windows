# Faqra Agents A3.1: Message Any Session Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the owner prompt any Claude Code session from the island's Agents section, waiting or not, and stop holding turns of sessions nobody is watching.

**Architecture:** Claude Code (2.1.234 and later on Windows) gives every session an inbox: a named pipe for cross-session messages, exported to hooks with a per-session token. Faqra's relay already forwards chosen environment variables with each event, so it now forwards the inbox address, the token and Claude Code's "attended" flag. The hub keeps each session's inbox in memory and writes the owner's words to it on request; Claude Code starts a new turn when the session is idle, or reads the message between steps when it is busy, and the normal hook events update the island. The Agents section gains a message box under the focused session's details.

**Tech Stack:** C# / .NET 8, WPF + WPF UI 4.3, System.Text.Json (Nodes), System.IO.Pipes, xunit. No new NuGet packages.

**Spec:** `docs/superpowers/specs/2026-10-09-faqra-agents-design.md` (the agents design; this plan adds the owner's 2026-10-12 request, recorded there in Task 4) and the official cross-session messaging docs, https://code.claude.com/docs/en/cross-session-messaging.

**What the spike proved (2026-10-12, Claude Code 2.1.296, on this PC):**
- Every session, interactive or `claude -p`, exports `CLAUDE_CODE_MESSAGING_SOCKET` (for example `\\.\pipe\LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c`) and `CLAUDE_CODE_MESSAGING_TOKEN` (32 lowercase hex) to its hooks.
- Writing `{"type":"auth","token":"<token>"}` then `{"type":"user","message":{"role":"user","content":"<text>"}}`, each followed by `\n`, then closing, delivers the text. This is the recipe Claude Code's own debug output prints.
- A session that had finished its turn started a new turn from it within about 4 s and answered as asked. A message to a busy session arrives between tool calls.
- The new turn fires `UserPromptSubmit` and `Stop` as usual. Its prompt reads `Another Claude session sent a message:\n<text>\n\nThis came from another Claude session — not typed by your user, but very likely working on their behalf. …`, and Claude acts on it.
- No settings change is needed: on Windows the token is how Claude Code recognises its own child (the hook), so the message is delivered whatever the permission mode.
- `CLAUDE_CODE_SESSION_ATTENDED` is `1` for the owner's interactive Antigravity session and `0` for a `claude -p` run.

## Global Constraints

- Solution lives in `Windows/`; run every `dotnet` command from `C:\Users\Tigre\vorssaint-utils-windows\Windows`.
- Every new source file under `src/` starts with `// SPDX-License-Identifier: GPL-3.0-or-later` and `// Copyright (C) 2026 Faqra contributors`. Test files have no header.
- Nullable warnings are errors. No new NuGet packages. The build has no warnings.
- The inbox token lets anyone holding it post into a session: it is kept in memory only, and never written to the agents log, a file, `Trace`, an exception message or any `ToString()`.
- Faqra opens a pipe only when its full path matches `\\.\pipe\cc-msg-<32 hex>` or `\\.\pipe\LOCAL\cc-msg-<32 hex>` (case-insensitive) and the token is exactly 32 lowercase hex characters; anything else is ignored.
- Wire format, verbatim: line 1 `{"type":"auth","token":"<token>"}`, line 2 `{"type":"user","message":{"role":"user","content":"<preamble>\n\n<text>"}}`, each ending with `\n`, UTF-8 without a byte order mark, then close. Preamble: `The user typed this in Faqra, their desktop island:`.
- A message is 1 to 9,000 characters after trimming and is not blank; the box stops at 9,000.
- Connecting and writing give up after 3 s; a failed send says so on the island and never blocks it.
- A session whose hooks report `CLAUDE_CODE_SESSION_ATTENDED=0` is never held for a reply.
- The agents log records `Message <session> sent` or `Message <session> unreachable`, never the words.
- User-visible copy: sentence case, no em or en dashes, no exclamation marks.
- Commits: conventional prefixes, and no `Co-Authored-By` or any Claude attribution line.
- WPF tests run inside `StaThread.Run(...)`. Subagents dispatched for this plan run on Sonnet (`model: "sonnet"`).

## Review Focus

1. A message goes only to the session the box was showing: switching sessions drops the draft, and a send that finishes after a switch changes nothing for the new session. Pinned in Task 3 (`ADraftNeverFollowsTheFocusToAnotherSession`, `ASendThatFinishesAfterASwitchLeavesTheNewSessionAlone`).
2. A session that has closed (its pipe is gone) answers "Couldn't reach" within about 3 s and the island stays usable. Pinned in Task 2 (`AClosedInboxIsUnreachableQuickly`).
3. The token never leaks into the log or a `ToString()`. Pinned in Task 1 (`NeverShowsTheToken`, `AnEventNeverPrintsItsToken`) and Task 2 (`TheLogNeverHoldsTheWordsOrTheToken`).
4. Words with quotes, new lines, accents and emoji arrive intact. Pinned in Task 1 (`TheWordsSurviveTheWire`) and Task 2 (`SendsTheTwoLinesToTheInbox`).
5. A crafted event naming some other pipe (Faqra's own, a remote one, a path with `..`) never makes Faqra open it. Pinned in Task 1 (`RefusesAnythingButClaudeCodesInbox`).

---

## File structure

| File | Responsibility |
|---|---|
| `src/Faqra.Core/Agents/AgentInbox.cs` (new) | A validated inbox address and token, never printed |
| `src/Faqra.Core/Agents/AgentMessage.cs` (new) | The wire lines, the length rule, and reading an island message back out of a prompt |
| `src/Faqra.Core/Agents/HookPayload.cs` (modify) | The relay forwards the inbox and the attended flag |
| `src/Faqra.Core/Agents/AgentEvent.cs`, `AgentBoard.cs`, `ReplyRules.cs` (modify) | Events carry them; the board shows the owner's words and knows who is attended; unattended turns are never held |
| `src/Faqra.Services/Agents/SessionInboxes.cs`, `PeerInbox.cs` (new) | Inboxes per session in memory; writing to one |
| `src/Faqra.Services/Agents/AgentHub.cs` (modify) | `CanMessage`, `MessageAsync`, forgetting inboxes |
| `src/Faqra.App/Island/Modules/MessageComposer.cs` (new), `AgentsModule.cs` (modify) | The message box under the session's details |
| `src/Faqra.Core/Localization/AgentsStrings*.cs` (modify) | Its words |

---

### Task 1: Inboxes, messages and attended sessions in Core

**Files:**
- Create: `src/Faqra.Core/Agents/AgentInbox.cs`, `src/Faqra.Core/Agents/AgentMessage.cs`
- Modify: `src/Faqra.Core/Agents/HookPayload.cs`, `AgentEvent.cs`, `AgentBoard.cs`, `ReplyRules.cs`
- Test: `tests/Faqra.Core.Tests/Agents/AgentInboxTests.cs`, `AgentMessageTests.cs` (new); `HookPayloadTests.cs`, `AgentEventTests.cs`, `AgentBoardTests.cs`, `ReplyRulesTests.cs` (add)

**Interfaces:**
- Consumes: `AgentJson.Compact`, `AgentEvent.TryParse`, `AgentBoard.Apply`, `ReplyRules.HoldFor` (A1 to A3).
- Produces: `sealed partial class AgentInbox` with `string PipeName`, `string Token`, `static AgentInbox? TryCreate(string? socket, string? token)`; `static class AgentMessage` with `const string Preamble`, `const int MaxLength = 9_000`, `static string Lines(AgentInbox inbox, string text)`, `static bool Fits(string? text)`, `static string? Unwrap(string? prompt)`, `static bool IsFromIsland(string? prompt)`; `AgentEvent.Inbox` (`AgentInbox?`, init) and `AgentEvent.Attended` (`bool?`, init); `AgentSession.Attended` (`bool?`, init); a `UserPromptSubmit` from the island sets `LastPrompt` to the owner's words and `TurnFromIsland = true`; `ReplyRules.HoldFor` returns null when `session.Attended == false`.

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.Core.Tests/Agents/AgentInboxTests.cs`:

```csharp
using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class AgentInboxTests
{
    private const string Token = "0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData(@"\\.\pipe\LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c", @"LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c")]
    [InlineData(@"\\.\pipe\cc-msg-F482AC3A68B5E314E2A7BCADB825F61C", "cc-msg-F482AC3A68B5E314E2A7BCADB825F61C")]
    public void AcceptsClaudeCodesOwnInbox(string socket, string pipeName)
    {
        var inbox = AgentInbox.TryCreate(socket, Token)!;
        Assert.Equal((pipeName, Token), (inbox.PipeName, inbox.Token));
    }

    [Theory]
    [InlineData(@"\\.\pipe\faqra-agents-S-1-5-21")]
    [InlineData(@"\\server\pipe\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c")]
    [InlineData(@"\\.\pipe\LOCAL\..\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c")]
    [InlineData(@"\\.\pipe\LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61")]
    [InlineData(@"\\.\pipe\LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c\x")]
    [InlineData("/tmp/cc-socks-1000/cc-msg-f482ac3a68b5e314e2a7bcadb825f61c.sock")]
    [InlineData("")]
    [InlineData(null)]
    public void RefusesAnythingButClaudeCodesInbox(string? socket) => Assert.Null(AgentInbox.TryCreate(socket, Token));

    [Theory]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("0123456789abcdef0123456789abcde")]
    [InlineData("0123456789abcdef0123456789abcdefa")]
    [InlineData("0123456789abcdef0123456789abcdeg")]
    [InlineData(null)]
    public void RefusesATokenThatIsNotClaudeCodes(string? token) =>
        Assert.Null(AgentInbox.TryCreate(@"\\.\pipe\LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c", token));

    [Fact]
    public void NeverShowsTheToken()
    {
        var inbox = AgentInbox.TryCreate(@"\\.\pipe\LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c", Token)!;
        Assert.DoesNotContain(Token, inbox.ToString());
    }
}
```

`tests/Faqra.Core.Tests/Agents/AgentMessageTests.cs`:

```csharp
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
```

Add to `tests/Faqra.Core.Tests/Agents/HookPayloadTests.cs` (inside the class, next to `TagsTheAgentTheTerminalAndAMissingFolder`):

```csharp
    [Fact]
    public void ForwardsTheSessionsInboxAndWhetherItIsAttended()
    {
        var env = new Dictionary<string, string?>
        {
            ["CLAUDE_CODE_MESSAGING_SOCKET"] = @"\\.\pipe\LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c",
            ["CLAUDE_CODE_MESSAGING_TOKEN"] = "0123456789abcdef0123456789abcdef",
            ["CLAUDE_CODE_SESSION_ATTENDED"] = "1",
        };
        var line = Line("{\"session_id\":\"s1\"}", env: name => env.GetValueOrDefault(name));
        Assert.Equal(@"\\.\pipe\LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c", line["messaging_socket"]!.GetValue<string>());
        Assert.Equal("0123456789abcdef0123456789abcdef", line["messaging_token"]!.GetValue<string>());
        Assert.Equal("1", line["session_attended"]!.GetValue<string>());
    }
```

Add to `tests/Faqra.Core.Tests/Agents/AgentEventTests.cs` (inside the class):

```csharp
    [Fact]
    public void ReadsTheInboxAndTheAttendedFlag()
    {
        var e = AgentEvent.TryParse("{\"hook_event_name\":\"SessionStart\",\"session_id\":\"s1\",\"messaging_socket\":\"\\\\\\\\.\\\\pipe\\\\LOCAL\\\\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c\",\"messaging_token\":\"0123456789abcdef0123456789abcdef\",\"session_attended\":\"0\"}")!;
        Assert.Equal(@"LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c", e.Inbox!.PipeName);
        Assert.False(e.Attended);
        Assert.True(AgentEvent.TryParse("{\"hook_event_name\":\"Stop\",\"session_id\":\"s1\",\"session_attended\":\"1\"}")!.Attended);
        Assert.Null(AgentEvent.TryParse("{\"hook_event_name\":\"Stop\",\"session_id\":\"s1\"}")!.Attended);
        Assert.Null(AgentEvent.TryParse("{\"hook_event_name\":\"Stop\",\"session_id\":\"s1\",\"messaging_socket\":\"\\\\\\\\.\\\\pipe\\\\faqra-agents\",\"messaging_token\":\"0123456789abcdef0123456789abcdef\"}")!.Inbox);
    }

    [Fact]
    public void AnEventNeverPrintsItsToken()
    {
        var e = AgentEvent.TryParse("{\"hook_event_name\":\"SessionStart\",\"session_id\":\"s1\",\"messaging_socket\":\"\\\\\\\\.\\\\pipe\\\\LOCAL\\\\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c\",\"messaging_token\":\"0123456789abcdef0123456789abcdef\"}")!;
        Assert.DoesNotContain("0123456789abcdef0123456789abcdef", e.ToString());
    }
```

(In those JSON strings `\\\\\\\\.\\\\pipe\\\\…` is the C# escaping of the JSON text `"\\\\.\\pipe\\…"`, which decodes to `\\.\pipe\…`. Keep them byte for byte.)

Add to `tests/Faqra.Core.Tests/Agents/AgentBoardTests.cs` (inside the class):

```csharp
    [Fact]
    public void AMessageFromTheIslandShowsTheOwnersWords()
    {
        var now = DateTimeOffset.Now;
        var prompt = "Another Claude session sent a message:\n" + AgentMessage.Preamble + "\n\nRun the tests again\n\nThis came from another Claude session — not typed by your user.";
        var board = With("{\"hook_event_name\":\"Stop\",\"session_id\":\"s\",\"last_assistant_message\":\"Done.\"}")
            .Apply(AgentEvent.TryParse(new System.Text.Json.Nodes.JsonObject
            {
                ["hook_event_name"] = "UserPromptSubmit",
                ["session_id"] = "s",
                ["prompt"] = prompt,
            }.ToJsonString())!, now);
        var s = board.Sessions["s"];
        Assert.Equal((AgentState.Thinking, "Run the tests again", true), (s.State, s.LastPrompt, s.TurnFromIsland));
        Assert.Equal("Run the tests again", s.Steps[^1].Detail);

        var typed = board.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"UserPromptSubmit\",\"session_id\":\"s\",\"prompt\":\"typed\"}")!, now);
        Assert.False(typed.Sessions["s"].TurnFromIsland);
    }

    [Fact]
    public void TheBoardRemembersWhetherASessionIsAttended()
    {
        var now = DateTimeOffset.Now;
        var board = With("{\"hook_event_name\":\"SessionStart\",\"session_id\":\"s\",\"session_attended\":\"0\"}");
        Assert.False(board.Sessions["s"].Attended);
        // Events that do not say keep what the session last said.
        var later = board.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s\",\"tool_name\":\"Read\"}")!, now);
        Assert.False(later.Sessions["s"].Attended);
        Assert.Null(With("{\"hook_event_name\":\"SessionStart\",\"session_id\":\"t\"}").Sessions["t"].Attended);
    }
```

(If `AgentStep`'s text property is not named `Detail`, use its real name; A3's `AHeldTurnWaitsThenCarriesOnWithTheReply` compares a whole `AgentStep`, which shows the shape.)

Add to `tests/Faqra.Core.Tests/Agents/ReplyRulesTests.cs` (inside the class):

```csharp
    [Fact]
    public void ARunNobodyIsWatchingIsNeverHeld()
    {
        var asked = Finished("Should I push the branch?") with { Attended = false, RepliesOn = true };
        Assert.Null(ReplyRules.HoldFor(asked, true, watching: true, opensOnAnswer: true, 300));
        Assert.NotNull(ReplyRules.HoldFor(asked with { Attended = null }, true, true, true, 300));
        Assert.NotNull(ReplyRules.HoldFor(asked with { Attended = true }, true, true, true, 300));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~AgentInboxTests|FullyQualifiedName~AgentMessageTests|FullyQualifiedName~HookPayloadTests|FullyQualifiedName~AgentEventTests|FullyQualifiedName~AgentBoardTests|FullyQualifiedName~ReplyRulesTests"`
Expected: build FAILS with "The name 'AgentInbox' does not exist in the current context".

- [ ] **Step 3: Write `AgentInbox`**

`src/Faqra.Core/Agents/AgentInbox.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text.RegularExpressions;

namespace Faqra.Core.Agents;

/// <summary>
/// A Claude Code session's inbox: the named pipe Claude Code binds for cross-session messages, and the token a
/// connection must send first (https://code.claude.com/docs/en/cross-session-messaging). The token lets its holder post
/// into the session, so it lives in memory only and never appears in a log, a file or <see cref="ToString"/>.
/// </summary>
public sealed partial class AgentInbox
{
    private const string PipePrefix = @"\\.\pipe\";

    private AgentInbox(string pipeName, string token)
    {
        PipeName = pipeName;
        Token = token;
    }

    /// <summary>The pipe's name without the <c>\\.\pipe\</c> prefix, as <c>NamedPipeClientStream</c> takes it.</summary>
    public string PipeName { get; }

    public string Token { get; }

    /// <summary>The inbox a hook was given, or null unless the address and the token look exactly like Claude Code's own.</summary>
    public static AgentInbox? TryCreate(string? socket, string? token)
    {
        if (socket is null || token is null || !SocketPattern().IsMatch(socket) || !TokenPattern().IsMatch(token))
        {
            return null;
        }
        return new AgentInbox(socket[PipePrefix.Length..], token);
    }

    public override string ToString() => $"AgentInbox({PipeName}, token hidden)";

    [GeneratedRegex(@"^\\\\\.\\pipe\\(LOCAL\\)?cc-msg-[0-9a-f]{32}$", RegexOptions.IgnoreCase)]
    private static partial Regex SocketPattern();

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex TokenPattern();
}
```

- [ ] **Step 4: Write `AgentMessage`**

`src/Faqra.Core/Agents/AgentMessage.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

/// <summary>
/// Words the owner sends to a Claude Code session from the island, through the session's inbox. Claude Code wraps them
/// as a message from another session, tells Claude they were not typed at its prompt, and starts a new turn when the
/// session is idle. The format is the one Claude Code 2.1.296 prints in its own debug output for posting to an inbox.
/// </summary>
public static class AgentMessage
{
    /// <summary>Tells Claude whose words these are; Claude Code's wrapper only says another session sent them.</summary>
    public const string Preamble = "The user typed this in Faqra, their desktop island:";

    public const int MaxLength = 9_000;

    /// <summary>How Claude Code 2.1.296 opens a message from another session in the prompt it hands to hooks.</summary>
    private const string Opening = "Another Claude session sent a message:\n";

    /// <summary>How it closes one.</summary>
    private const string Closing = "\n\nThis came from another Claude session";

    /// <summary>The two lines an inbox takes: the token first, then the words as a user turn. Each ends with a new line.</summary>
    public static string Lines(AgentInbox inbox, string text)
    {
        var auth = new JsonObject { ["type"] = "auth", ["token"] = inbox.Token };
        var message = new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject { ["role"] = "user", ["content"] = $"{Preamble}\n\n{text}" },
        };
        return auth.ToJsonString(AgentJson.Compact) + "\n" + message.ToJsonString(AgentJson.Compact) + "\n";
    }

    /// <summary>True for words worth sending: not blank, and no longer than <see cref="MaxLength"/>.</summary>
    public static bool Fits(string? text) => !string.IsNullOrWhiteSpace(text) && text.Length <= MaxLength;

    /// <summary>
    /// The words a prompt carries once Claude Code's wrapper and Faqra's preamble are taken off. A prompt typed in Claude
    /// Code comes back unchanged.
    /// </summary>
    public static string? Unwrap(string? prompt)
    {
        if (prompt is null)
        {
            return null;
        }
        var text = prompt.Replace("\r\n", "\n");
        if (!text.StartsWith(Opening, StringComparison.Ordinal))
        {
            return prompt;
        }
        var body = text[Opening.Length..];
        var end = body.IndexOf(Closing, StringComparison.Ordinal);
        if (end >= 0)
        {
            body = body[..end];
        }
        if (body.StartsWith(Preamble, StringComparison.Ordinal))
        {
            body = body[Preamble.Length..];
        }
        return body.Trim();
    }

    /// <summary>True when the prompt is one the owner sent from the island.</summary>
    public static bool IsFromIsland(string? prompt) =>
        prompt is not null && prompt.Replace("\r\n", "\n").StartsWith(Opening + Preamble, StringComparison.Ordinal);
}
```

- [ ] **Step 5: Forward and read the inbox and the attended flag**

In `src/Faqra.Core/Agents/HookPayload.cs`, extend `EnvironmentFields`:

```csharp
    private static readonly (string Field, string Variable)[] EnvironmentFields =
    [
        ("term_program", "TERM_PROGRAM"),
        ("wt_session", "WT_SESSION"),
        ("claude_entrypoint", "CLAUDE_CODE_ENTRYPOINT"),
        ("messaging_socket", "CLAUDE_CODE_MESSAGING_SOCKET"),
        ("messaging_token", "CLAUDE_CODE_MESSAGING_TOKEN"),
        ("session_attended", "CLAUDE_CODE_SESSION_ATTENDED"),
    ];
```

In `src/Faqra.Core/Agents/AgentEvent.cs`, add after `InputTruncated`:

```csharp
    /// <summary>The session's inbox, when Claude Code gave the hook one. Never logged.</summary>
    public AgentInbox? Inbox { get; init; }

    /// <summary>False for a session nobody is watching (a scripted <c>claude -p</c> run), true for an interactive one, null when Claude Code did not say.</summary>
    public bool? Attended { get; init; }
```

and in `TryParse`'s object initializer, after `InputTruncated = …,`:

```csharp
                Inbox = AgentInbox.TryCreate(Text(obj, "messaging_socket"), Text(obj, "messaging_token")),
                Attended = Text(obj, "session_attended") switch { "1" => true, "0" => false, _ => null },
```

- [ ] **Step 6: The board and the hold rule**

In `src/Faqra.Core/Agents/AgentBoard.cs`:

1. Add to `AgentSession`'s body, after `TurnFromIsland`:

```csharp
    /// <summary>Whether someone is watching the session, as Claude Code last said; null until it says.</summary>
    public bool? Attended { get; init; }
```

2. In `Apply`, replace `next with { UpdatedAt = now, Cwd = e.Cwd.Length > 0 ? e.Cwd : next.Cwd }` with:

```csharp
next with { UpdatedAt = now, Cwd = e.Cwd.Length > 0 ? e.Cwd : next.Cwd, Attended = e.Attended ?? next.Attended }
```

3. Replace the `"UserPromptSubmit" => …` arm of `Next` with `"UserPromptSubmit" => Prompted(s, e.Prompt, now),` and add this method next to `Finished`:

```csharp
    /// <summary>A new turn from a prompt typed in Claude Code, or from words the owner sent from the island.</summary>
    private static AgentSession Prompted(AgentSession s, string? prompt, DateTimeOffset now)
    {
        var words = AgentMessage.Unwrap(prompt);
        var started = s with
        {
            State = AgentState.Thinking,
            FinishedAt = null,
            LastPrompt = words ?? s.LastPrompt,
            Unread = false,
            ConsecutiveReplies = 0,
            TurnFromIsland = AgentMessage.IsFromIsland(prompt),
        };
        return Step(started, now, AgentStepKind.Prompt, OneLine(words ?? string.Empty, PromptChars));
    }
```

In `src/Faqra.Core/Agents/ReplyRules.cs`, replace the first guard with:

```csharp
        if (!cardsCanShow || session.Attended == false || session.ConsecutiveReplies >= AgentBoard.MaxConsecutiveReplies)
```

and add to the class summary: `A session Claude Code reports as unattended (a scripted run) is never held: nobody would see the box.`

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.Core.Tests`
Expected: PASS (every Core test).

- [ ] **Step 8: Commit**

```bash
git add src/Faqra.Core/Agents tests/Faqra.Core.Tests/Agents
git commit -m "feat(windows): read each session's inbox and attended flag, and message words from the island"
```

---

### Task 2: The hub messages a session

**Files:**
- Create: `src/Faqra.Services/Agents/SessionInboxes.cs`, `src/Faqra.Services/Agents/PeerInbox.cs`
- Modify: `src/Faqra.Services/Agents/AgentHub.cs`
- Test: `tests/Faqra.Services.Tests/Agents/AgentHubMessageTests.cs` (new)

**Interfaces:**
- Consumes: `AgentInbox`, `AgentMessage.Lines`, `AgentMessage.Fits`, `AgentEvent.Inbox` (Task 1); the hub's `Serve`, `NoteWindow`, `Stop`, `Post`, `WriteLog`, `Short` (A1 to A3).
- Produces: `public enum AgentMessageOutcome { Sent, Unavailable, Unreachable, Invalid }` (in `AgentHub.cs`'s namespace, file `PeerInbox.cs`); on `AgentHub`: `bool CanMessage(string sessionId)`, `Task<AgentMessageOutcome> MessageAsync(string sessionId, string text)`, `void RememberInboxForTests(string sessionId, AgentInbox inbox)`. Inboxes are remembered from any event that carries one, forgotten on `SessionEnd` and when the hub stops.

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.Services.Tests/Agents/AgentHubMessageTests.cs`:

```csharp
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using Faqra.Core.Agents;
using Faqra.Services.Agents;
using static Faqra.Services.Tests.Agents.HubTestKit;

namespace Faqra.Services.Tests.Agents;

public class AgentHubMessageTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(5);
    private const string Token = "0123456789abcdef0123456789abcdef";

    private static string NewInboxName() => @"LOCAL\cc-msg-" + Guid.NewGuid().ToString("N");

    private static AgentInbox InboxFor(string pipeName) => AgentInbox.TryCreate(@"\\.\pipe\" + pipeName, Token)!;

    private static AgentHub Hub(SerialContext context, string pipe, string? log = null)
    {
        var hub = new AgentHub(pipe, context, () => T0, log);
        hub.SetRunning(true);
        return hub;
    }

    /// <summary>Plays a session's inbox: accepts one connection and returns everything written to it.</summary>
    private static Task<string> InboxReceives(string pipeName) => Task.Run(async () =>
    {
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await server.WaitForConnectionAsync();
        using var reader = new StreamReader(server, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    });

    [Fact]
    public async Task RemembersTheInboxAnEventCarries()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        var start = new JsonObject
        {
            ["hook_event_name"] = "SessionStart",
            ["session_id"] = "s1",
            ["messaging_socket"] = @"\\.\pipe\" + NewInboxName(),
            ["messaging_token"] = Token,
        }.ToJsonString();
        using (await FakeRelay.SendAsync(pipe, start))
        {
        }
        Assert.True(await Eventually(() => hub.CanMessage("s1")));
        Assert.False(hub.CanMessage("s2"));

        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"SessionEnd\",\"session_id\":\"s1\"}"))
        {
        }
        Assert.True(await Eventually(() => !hub.CanMessage("s1")));
    }

    [Fact]
    public async Task SendsTheTwoLinesToTheInbox()
    {
        const string words = "Oui, \"bleu\" 🙂\nRun the tests again";
        var name = NewInboxName();
        using var context = new SerialContext();
        using var hub = Hub(context, NewPipe());
        hub.RememberInboxForTests("s1", InboxFor(name));
        var received = InboxReceives(name);

        Assert.Equal(AgentMessageOutcome.Sent, await hub.MessageAsync("s1", "  " + words + "  ").WaitAsync(Within));

        var lines = (await received.WaitAsync(Within)).TrimEnd('\n').Split('\n');
        Assert.Equal("{\"type\":\"auth\",\"token\":\"" + Token + "\"}", lines[0]);
        var content = JsonNode.Parse(lines[1])!["message"]!["content"]!.GetValue<string>();
        Assert.Equal(AgentMessage.Preamble + "\n\n" + words, content);
    }

    [Fact]
    public async Task OnlyAKnownSessionAndRealWordsAreSent()
    {
        using var context = new SerialContext();
        using var hub = Hub(context, NewPipe());
        Assert.Equal(AgentMessageOutcome.Unavailable, await hub.MessageAsync("nobody", "hello"));
        hub.RememberInboxForTests("s1", InboxFor(NewInboxName()));
        Assert.Equal(AgentMessageOutcome.Invalid, await hub.MessageAsync("s1", "   "));
        Assert.Equal(AgentMessageOutcome.Invalid, await hub.MessageAsync("s1", new string('x', AgentMessage.MaxLength + 1)));
    }

    [Fact]
    public async Task AClosedInboxIsUnreachableQuickly()
    {
        using var context = new SerialContext();
        using var hub = Hub(context, NewPipe());
        hub.RememberInboxForTests("s1", InboxFor(NewInboxName()));
        var clock = Stopwatch.StartNew();
        Assert.Equal(AgentMessageOutcome.Unreachable, await hub.MessageAsync("s1", "hello").WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.InRange(clock.ElapsedMilliseconds, 0, 4500);
    }

    [Fact]
    public async Task TheLogNeverHoldsTheWordsOrTheToken()
    {
        var log = Path.Combine(Path.GetTempPath(), $"faqra-agents-{Guid.NewGuid():N}.log");
        var name = NewInboxName();
        using var context = new SerialContext();
        try
        {
            using (var hub = Hub(context, NewPipe(), log))
            {
                hub.RememberInboxForTests("s1", InboxFor(name));
                var received = InboxReceives(name);
                await hub.MessageAsync("s1", "secret plans");
                await received.WaitAsync(Within);
                Assert.True(await Eventually(() => File.Exists(log) && File.ReadAllText(log).Contains("Message s1 sent")));
            }
            var text = File.ReadAllText(log);
            Assert.DoesNotContain("secret plans", text);
            Assert.DoesNotContain(Token, text);
        }
        finally
        {
            File.Delete(log);
        }
    }

    [Fact]
    public async Task StoppingForgetsEveryInbox()
    {
        using var context = new SerialContext();
        using var hub = Hub(context, NewPipe());
        hub.RememberInboxForTests("s1", InboxFor(NewInboxName()));
        await OnContext(context, () => { hub.SetRunning(false); return 0; });
        Assert.False(hub.CanMessage("s1"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Services.Tests --filter "FullyQualifiedName~AgentHubMessageTests"`
Expected: build FAILS with "'AgentHub' does not contain a definition for 'RememberInboxForTests'".

- [ ] **Step 3: Keep inboxes and write to one**

`src/Faqra.Services/Agents/SessionInboxes.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Faqra.Core.Agents;

namespace Faqra.Services.Agents;

/// <summary>
/// Each session's inbox, from its latest event, in memory only: the token in it lets its holder post into the session.
/// The oldest go once there are more than 64. Thread-safe: the hub writes from pipe threads and the island reads on the
/// UI thread.
/// </summary>
internal sealed class SessionInboxes
{
    public const int Capacity = 64;
    private readonly List<(string Session, AgentInbox Inbox)> _sessions = [];
    private readonly object _gate = new();

    public AgentInbox? Find(string session)
    {
        lock (_gate)
        {
            return _sessions.Find(entry => entry.Session == session).Inbox;
        }
    }

    public void Remember(string session, AgentInbox inbox)
    {
        if (!SessionWindowCache.IsPlausible(session))
        {
            return;
        }
        lock (_gate)
        {
            _sessions.RemoveAll(entry => entry.Session == session);
            _sessions.Add((session, inbox));
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

    public void Clear()
    {
        lock (_gate)
        {
            _sessions.Clear();
        }
    }
}
```

`src/Faqra.Services/Agents/PeerInbox.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using Faqra.Core.Agents;

namespace Faqra.Services.Agents;

/// <summary>What became of words the owner sent to a session from the island.</summary>
public enum AgentMessageOutcome
{
    /// <summary>The session's inbox took them; Claude Code starts a turn or reads them between steps.</summary>
    Sent,

    /// <summary>Faqra has no inbox for the session (an older Claude Code, a session not seen since Faqra started).</summary>
    Unavailable,

    /// <summary>The inbox did not answer in time: the session has most likely closed.</summary>
    Unreachable,

    /// <summary>Blank, or longer than <see cref="AgentMessage.MaxLength"/>; nothing was sent.</summary>
    Invalid,
}

/// <summary>Writes to a session's inbox: connect, write the lines, close. Never throws, never says what it sent.</summary>
internal static class PeerInbox
{
    public static async Task<bool> SendAsync(AgentInbox inbox, string lines, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        try
        {
            using var pipe = new NamedPipeClientStream(".", inbox.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(deadline.Token).ConfigureAwait(false);
            await pipe.WriteAsync(new UTF8Encoding(false).GetBytes(lines), deadline.Token).ConfigureAwait(false);
            await pipe.FlushAsync(deadline.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException)
        {
            // The type only: the message text and the token never reach a log.
            Trace.TraceWarning($"Faqra agents message: {ex.GetType().Name}");
            return false;
        }
    }
}
```

- [ ] **Step 4: Teach the hub**

In `src/Faqra.Services/Agents/AgentHub.cs`:

1. Add after `private readonly SessionWindowCache _windows = new();`:

```csharp
    private readonly SessionInboxes _inboxes = new();

    /// <summary>How long a message may take to reach a session's inbox before Faqra says it could not.</summary>
    private static readonly TimeSpan MessageTimeout = TimeSpan.FromSeconds(3);
```

2. Add after `RememberWindowForTests`:

```csharp
    /// <summary>Whether Faqra can send words to this session: it has seen the session's inbox. Any thread.</summary>
    public bool CanMessage(string sessionId) => _inboxes.Find(sessionId) is not null;

    /// <summary>For render tests: an inbox without a relay. Never called by the app.</summary>
    public void RememberInboxForTests(string sessionId, AgentInbox inbox) => _inboxes.Remember(sessionId, inbox);

    /// <summary>
    /// Sends the owner's words to a session through its inbox. Claude Code starts a new turn when the session is idle or
    /// reads them between steps; the hook events that follow update the board as for a typed prompt. Any thread.
    /// </summary>
    public async Task<AgentMessageOutcome> MessageAsync(string sessionId, string text)
    {
        var words = text.Trim();
        if (!AgentMessage.Fits(words))
        {
            return AgentMessageOutcome.Invalid;
        }
        if (_inboxes.Find(sessionId) is not { } inbox)
        {
            return AgentMessageOutcome.Unavailable;
        }
        var sent = await PeerInbox.SendAsync(inbox, AgentMessage.Lines(inbox, words), MessageTimeout).ConfigureAwait(false);
        Post(() => WriteLog($"{_now():O} Message {Short(sessionId)} {(sent ? "sent" : "unreachable")}"));
        return sent ? AgentMessageOutcome.Sent : AgentMessageOutcome.Unreachable;
    }
```

3. In `Serve`, right after the `NoteWindow(connection, e);` call, add `NoteInbox(e);`, and add after `NoteWindow`:

```csharp
    /// <summary>Keeps the session's inbox from its latest event, so the island can message it; forgets it when the session ends.</summary>
    private void NoteInbox(AgentEvent e)
    {
        if (e.Event == "SessionEnd")
        {
            _inboxes.Forget(e.SessionId);
            return;
        }
        if (e.Inbox is { } inbox)
        {
            _inboxes.Remember(e.SessionId, inbox);
        }
    }
```

4. In `Stop()`, add `_inboxes.Clear();` right after `_tick = null;`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.Services.Tests`
Expected: PASS. Then five two-processor runs in Git Bash: `for i in 1 2 3 4 5; do DOTNET_PROCESSOR_COUNT=2 dotnet test tests/Faqra.Services.Tests --nologo -v q 2>&1 | grep -E "Passed!|Failed!|\[FAIL\]"; done`; all five must pass.

If creating a test server named `LOCAL\cc-msg-…` fails on this machine, make `NewInboxName()` return a name without the `LOCAL\` part (`AgentInbox` accepts both) and say so in the report.

- [ ] **Step 6: Commit**

```bash
git add src/Faqra.Services/Agents tests/Faqra.Services.Tests/Agents/AgentHubMessageTests.cs
git commit -m "feat(windows): agent hub sends the owner's words to a session's inbox"
```

---

### Task 3: A message box for the focused session

**Files:**
- Create: `src/Faqra.App/Island/Modules/MessageComposer.cs`
- Modify: `src/Faqra.App/Island/Modules/AgentsModule.cs`
- Modify: `src/Faqra.Core/Localization/AgentsStrings.cs`, `AgentsStrings.EnUS.cs`
- Test: `tests/Faqra.App.Tests/MessageComposerTests.cs` (new)

**Interfaces:**
- Consumes: `AgentHub.CanMessage`, `AgentHub.MessageAsync`, `AgentHub.RememberInboxForTests`, `AgentMessageOutcome` (Task 2); `AgentMessage.Fits`, `AgentMessage.MaxLength`, `AgentInbox` (Task 1); `AnsweredCard.SendsOnEnter`, `AgentCardParts` (A3).
- Produces: `internal sealed class MessageComposer : ContentControl` with `MessageComposer(AgentsStrings s, Func<string, string, Task<AgentMessageOutcome>> send, Action wantKeyboard)`, `void ShowFor(string sessionId)`, `string? SessionId`, `Wpf.Ui.Controls.TextBox Box`, `Button SendButton`, `TextBlock Status`, `Task SendAsync()`; strings `MessagePlaceholder`, `MessageSent`, `MessageFailed`. One composer per module, always in the window between the session list and the details, visible only when Faqra can message the focused session and no card waits for it.

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.App.Tests/MessageComposerTests.cs`:

```csharp
using System.Windows;
using Faqra.App.Island.Modules;
using Faqra.Core.Agents;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;
using Faqra.Services.Agents;

namespace Faqra.App.Tests;

public class MessageComposerTests
{
    private static readonly AgentsStrings S = AgentsStrings.EnUS;
    private const string WorkingLine = "{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s2\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Read\",\"tool_input\":{\"file_path\":\"C:\\\\code\\\\faqra\\\\a.cs\"}}";

    private static AgentInbox Inbox() =>
        AgentInbox.TryCreate(@"\\.\pipe\LOCAL\cc-msg-" + Guid.NewGuid().ToString("N"), "0123456789abcdef0123456789abcdef")!;

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

    private static MessageComposer Composer(Func<string, string, Task<AgentMessageOutcome>> send, Action? keyboard = null) =>
        new(S, send, keyboard ?? (() => { }));

    /// <summary>Waits for a send while letting this thread's dispatcher run, as the island's would: the composer resumes on it.</summary>
    private static void Drain(Task task)
    {
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        var frame = new System.Windows.Threading.DispatcherFrame();
        task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
        if (!task.IsCompleted)
        {
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }

    [Fact]
    public void TheBoxShowsForASessionFaqraCanReach() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(WorkingLine);
        var module = new AgentsModule(hub, () => true);
        Assert.All(All<MessageComposer>(module), c => Assert.Equal(Visibility.Collapsed, c.Visibility));

        hub.RememberInboxForTests("s2", Inbox());
        hub.ReplaceBoardForTests(hub.Board);
        var composer = Assert.Single(All<MessageComposer>(module));
        Assert.Equal(Visibility.Visible, composer.Visibility);
        Assert.Equal("s2", composer.SessionId);
        AgentsRenderTests.RenderInk(new System.Windows.Controls.Border { Background = IslandPalette.Surface, Child = module, Width = 412, Height = 520 }, "island-agents-message", 412, 520);
    });

    [Fact]
    public void TheBoxKeepsItsWordsWhileEventsArrive() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(WorkingLine);
        hub.RememberInboxForTests("s2", Inbox());
        var module = new AgentsModule(hub, () => true);
        var composer = All<MessageComposer>(module).Single(c => c.Visibility == Visibility.Visible);
        composer.Box.Text = "half typed";

        // A busy session sends many events; the box is never taken out of the window, so the caret stays in it.
        hub.ReplaceBoardForTests(hub.Board.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"PostToolUse\",\"session_id\":\"s2\",\"tool_name\":\"Read\"}")!, DateTimeOffset.Now));
        hub.ReplaceBoardForTests(hub.Board.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s2\",\"tool_name\":\"Grep\",\"tool_input\":{\"pattern\":\"x\"}}")!, DateTimeOffset.Now));

        var after = Assert.Single(All<MessageComposer>(module));
        Assert.Same(composer, after);
        Assert.Equal("half typed", after.Box.Text);
    });

    [Fact]
    public void TheBoxStepsAsideWhileACardWaits() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(WorkingLine);
        hub.RememberInboxForTests("s2", Inbox());
        hub.ReplaceRequestsForTests(AgentRequest.ForReply("r1", "s2", DateTimeOffset.Now, DateTimeOffset.Now.AddMinutes(5)));
        var module = new AgentsModule(hub, () => true);
        Assert.All(All<MessageComposer>(module), c => Assert.Equal(Visibility.Collapsed, c.Visibility));
    });

    [Fact]
    public void SendingGoesToTheSessionShownAndClearsTheBox() => StaThread.Run(() =>
    {
        (string Session, string Text)? sent = null;
        var composer = Composer((session, text) =>
        {
            sent = (session, text);
            return Task.FromResult(AgentMessageOutcome.Sent);
        });
        composer.ShowFor("s1");
        Assert.False(composer.SendButton.IsEnabled);
        composer.Box.Text = "  Run the tests again  ";
        Assert.True(composer.SendButton.IsEnabled);

        Drain(composer.SendAsync());

        Assert.Equal(("s1", "Run the tests again"), sent);
        Assert.Equal(string.Empty, composer.Box.Text);
        Assert.Equal(S.MessageSent, composer.Status.Text);
    });

    [Fact]
    public void AFailedSendKeepsTheWords() => StaThread.Run(() =>
    {
        var composer = Composer((_, _) => Task.FromResult(AgentMessageOutcome.Unreachable));
        composer.ShowFor("s1");
        composer.Box.Text = "Run the tests again";
        Drain(composer.SendAsync());
        Assert.Equal("Run the tests again", composer.Box.Text);
        Assert.Equal(S.MessageFailed, composer.Status.Text);
    });

    [Fact]
    public void ADraftNeverFollowsTheFocusToAnotherSession() => StaThread.Run(() =>
    {
        var composer = Composer((_, _) => Task.FromResult(AgentMessageOutcome.Sent));
        composer.ShowFor("s1");
        composer.Box.Text = "for s1 only";
        composer.ShowFor("s1");
        Assert.Equal("for s1 only", composer.Box.Text);
        composer.ShowFor("s2");
        Assert.Equal(string.Empty, composer.Box.Text);
        Assert.Equal("s2", composer.SessionId);
    });

    [Fact]
    public void ASendThatFinishesAfterASwitchLeavesTheNewSessionAlone() => StaThread.Run(() =>
    {
        var pending = new TaskCompletionSource<AgentMessageOutcome>();
        var composer = Composer((_, _) => pending.Task);
        composer.ShowFor("s1");
        composer.Box.Text = "for s1";
        var sending = composer.SendAsync();
        composer.ShowFor("s2");
        composer.Box.Text = "draft for s2";

        pending.SetResult(AgentMessageOutcome.Sent);
        Drain(sending);

        Assert.Equal("draft for s2", composer.Box.Text);
        Assert.Equal(string.Empty, composer.Status.Text);
    });

    [Fact]
    public void TheBoxStopsAtTheLimitAndAsksForTheKeyboard() => StaThread.Run(() =>
    {
        var asked = 0;
        var composer = Composer((_, _) => Task.FromResult(AgentMessageOutcome.Sent), () => asked++);
        Assert.Equal(AgentMessage.MaxLength, composer.Box.MaxLength);
        Assert.Equal(S.MessagePlaceholder, System.Windows.Automation.AutomationProperties.GetName(composer.Box));
        composer.Box.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
        {
            RoutedEvent = UIElement.PreviewMouseDownEvent,
        });
        Assert.Equal(1, asked);
    });
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.App.Tests --filter "FullyQualifiedName~MessageComposerTests"`
Expected: build FAILS with "The type or namespace name 'MessageComposer' could not be found".

- [ ] **Step 3: Add the strings**

In `src/Faqra.Core/Localization/AgentsStrings.cs`, add after `RepliesOnToggle`:

```csharp
    public required string MessagePlaceholder { get; init; }
    public required string MessageSent { get; init; }
    public required string MessageFailed { get; init; }
```

In `AgentsStrings.EnUS.cs`, add after `RepliesOnToggle = …,`:

```csharp
        MessagePlaceholder = "Message Claude",
        MessageSent = "Sent. Claude's answer shows here.",
        MessageFailed = "Couldn't reach this session. It may have closed.",
```

- [ ] **Step 4: Write `MessageComposer`**

`src/Faqra.App/Island/Modules/MessageComposer.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Faqra.Core.Agents;
using Faqra.Core.Localization;
using Faqra.Services.Agents;

namespace Faqra.App.Island.Modules;

/// <summary>
/// A box under the focused session's details for prompting it from the island, whether or not Claude is waiting. The
/// words go to the session the box shows; switching sessions drops the draft, so they can never reach another one.
/// </summary>
internal sealed class MessageComposer : ContentControl
{
    private readonly AgentsStrings _s;
    private readonly Func<string, string, Task<AgentMessageOutcome>> _send;
    private bool _sending;

    public MessageComposer(AgentsStrings s, Func<string, string, Task<AgentMessageOutcome>> send, Action wantKeyboard)
    {
        _s = s;
        _send = send;
        Box = new Wpf.Ui.Controls.TextBox
        {
            PlaceholderText = s.MessagePlaceholder,
            FontSize = 12,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxLength = AgentMessage.MaxLength,
            MaxHeight = AgentCardParts.DetailMaxHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        AutomationProperties.SetName(Box, s.MessagePlaceholder);
        // The island never takes the keyboard by itself; clicking into the box is the owner asking for it.
        Box.AddHandler(PreviewMouseDownEvent, new MouseButtonEventHandler((_, _) => wantKeyboard()), handledEventsToo: true);
        Box.TextChanged += (_, _) => UpdateSend();
        Box.PreviewKeyDown += (_, e) =>
        {
            if (AnsweredCard.SendsOnEnter(e.Key, Keyboard.Modifiers))
            {
                e.Handled = true;
                _ = SendAsync();
            }
        };
        SendButton = AgentCardParts.Action(s.Send, primary: true, (_, _) => _ = SendAsync());
        Status = AgentCardParts.Caption(string.Empty);
        Status.Foreground = IslandPalette.Tertiary;
        Status.Margin = new Thickness(0, 6, 0, 0);

        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        stack.Children.Add(Box);
        stack.Children.Add(Status);
        stack.Children.Add(AgentCardParts.Footer(AgentCardParts.Actions(SendButton), quiet: null));
        Content = stack;
        UpdateSend();
    }

    internal Wpf.Ui.Controls.TextBox Box { get; }

    internal Button SendButton { get; }

    internal TextBlock Status { get; }

    /// <summary>The session the words go to.</summary>
    internal string? SessionId { get; private set; }

    /// <summary>Points the box at a session; a draft written for another session is dropped.</summary>
    public void ShowFor(string sessionId)
    {
        if (SessionId == sessionId)
        {
            return;
        }
        SessionId = sessionId;
        Box.Text = string.Empty;
        Status.Text = string.Empty;
    }

    /// <summary>Sends the box's words to its session. Never throws; the caption says how it went.</summary>
    internal async Task SendAsync()
    {
        if (_sending || SessionId is not { } session || !AgentMessage.Fits(Box.Text))
        {
            return;
        }
        var words = Box.Text.Trim();
        _sending = true;
        UpdateSend();
        AgentMessageOutcome outcome;
        try
        {
            outcome = await _send(session, words);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Trace.TraceWarning($"Faqra agents message: {ex.GetType().Name}");
            outcome = AgentMessageOutcome.Unreachable;
        }
        finally
        {
            _sending = false;
        }
        // The owner may have moved to another session meanwhile: that session's box is not touched.
        if (SessionId == session)
        {
            if (outcome == AgentMessageOutcome.Sent)
            {
                Box.Text = string.Empty;
            }
            Status.Text = outcome == AgentMessageOutcome.Sent ? _s.MessageSent : _s.MessageFailed;
        }
        UpdateSend();
    }

    private void UpdateSend() => SendButton.IsEnabled = !_sending && AgentMessage.Fits(Box.Text);
}
```

- [ ] **Step 5: Show it in the Agents section**

In `src/Faqra.App/Island/Modules/AgentsModule.cs`:

1. Add after the `_repliesOn` field:

```csharp
    private readonly MessageComposer _composer;
```

2. In the constructor, replace

```csharp
        body.Children.Add(_list);
        body.Children.Add(_detail);
```

with

```csharp
        body.Children.Add(_list);
        // The box lives outside the details, which are rebuilt on every event: taking it out of the window would drop the
        // caret while the owner types. It is only shown or hidden.
        _composer = new MessageComposer(_s, (session, words) => _hub.MessageAsync(session, words), () => KeyboardWanted?.Invoke())
        {
            Margin = new Thickness(12, 12, 12, 0),
            Visibility = Visibility.Collapsed,
        };
        body.Children.Add(_composer);
        body.Children.Add(_detail);
```

(The field is assigned before `Render()` runs, so `Render` can use it.)

3. In `Render`, in the branch for no sessions (right after `_detail.Children.Clear();`), add `_composer.Visibility = Visibility.Collapsed;`.

4. At the start of `RenderDetail(AgentSession session)`, before `_detail.Children.Clear();`, add:

```csharp
        // A waiting card has its own buttons and reply box; otherwise any session Faqra can reach takes a message.
        var canMessage = _hub.CanMessage(session.Id) && _hub.Requests.All(request => request.SessionId != session.Id);
        if (canMessage)
        {
            _composer.ShowFor(session.Id);
        }
        _composer.Visibility = canMessage ? Visibility.Visible : Visibility.Collapsed;
```

5. Update the class summary's second sentence to: `Above the list sits the focused session's card (an approval, a question, or its latest answer with a reply box while Claude waits); under the list, a box to message the focused session; below it, the session's folder, the replies switch, the owner's last prompt, Claude's last words and its activity.`

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.App.Tests`
Expected: PASS (every App test). Open `island-agents-message.png` in `%TEMP%\faqra-ui` and check: the box sits between the session list and the focused session's details at full width, Send on the right (dimmed until there is text), and nothing is cut at 412 px.

- [ ] **Step 7: Commit**

```bash
git add src/Faqra.App/Island/Modules src/Faqra.Core/Localization tests/Faqra.App.Tests/MessageComposerTests.cs
git commit -m "feat(windows): message any agent session from the island"
```

---

### Task 4: Live check and docs (controller, with the owner)

Run by the controller, not a subagent: it touches the owner's running Faqra and their Claude Code sessions. Ask before each live step in the conversation at that moment.

**Files:**
- Modify: `KNOWN-ISSUES.md`, `design-system.md`, `docs/superpowers/specs/2026-10-09-faqra-agents-design.md`

- [ ] **Step 1: Full suite**

Run: `dotnet test` (whole solution): PASS, no build warnings. Then the five two-processor Services runs.

- [ ] **Step 2: Publish to Faqra-test (ask first)**

Same commands as A3 (`dotnet publish src/Faqra.App/Faqra.App.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o C:/Users/Tigre/Faqra-test`, and `src/Faqra.Hook` with `-p:PublishReadyToRun=true`), after checking no card is waiting in `%LOCALAPPDATA%\Faqra\agents.log`. Start it; confirm `%LOCALAPPDATA%\Faqra\bin\faqra-hook.exe` matches the published relay and `~/.claude/settings.json` is unchanged.

- [ ] **Step 3: Message an idle session (ask first)**

Start a scratch headless session that stays open (the spike's `spike-idle.js` approach: `claude -p --input-format stream-json --output-format stream-json --verbose` in an empty folder, no inherited `CLAUDE_CODE_*` variables), let it answer "READY", then with UI Automation type `Reply with the single word BLUE in capitals.` into the element named `Message Claude` for that session (check the island's details show `In <scratch folder>`) and invoke `Send`. Expected:
- The island says "Sent".
- The scratch session answers BLUE within about 10 s.
- The island's Prompted line shows the words without Claude Code's wrapper.
- `agents.log` has `Message <id> sent` and none of the words.

Stop the scratch process afterwards by its exact command line.

- [ ] **Step 4: An unattended run is not held (ask first)**

Run `claude -p "Ask me whether I prefer red or blue, then end your turn."` from an empty folder with the island open on Agents. Expected: it exits at once (no hold), because its hooks report `CLAUDE_CODE_SESSION_ATTENDED=0`.

- [ ] **Step 5: The owner tries it**

Ask the owner to message one of their own sessions from the island while it is idle, and while it is busy, and to say how Claude's answer reads (Claude is told the words came through another session).

- [ ] **Step 6: Docs**

`KNOWN-ISSUES.md`:
- Replace the bullet starting `- **Unattended \`claude -p\` runs can be held.**` with: `- **Messages from the island arrive as relayed words.** Claude Code tells Claude that words sent from the island came through another session and were not typed at its prompt, so Claude treats them as the owner's request relayed by Faqra. They cannot approve a permission prompt or run a slash command. Sessions in WSL, sessions started with \`--bare\`, and Claude Code older than 2.1.234 have no inbox, so the box does not show for them.`
- Add whatever Step 5 found.

`design-system.md`: add one row to the agents table: `| Message box (\`MessageComposer\`) | Faqra-only | Between the session list and the focused session's details: a full-width Fluent text box ("Message Claude", at most 120px tall, scrolls; Enter sends, Shift+Enter starts a line), a tertiary caption for "Sent" or "Couldn't reach", and Send (primary) on the right. Hidden while a card waits for that session, and for sessions Faqra cannot reach. |`

Agents spec, Owner's additions table, add: `| Message any session (asked 2026-10-12) | From the Agents section the owner can send words to any session, waiting or not, through Claude Code's cross-session inbox (2.1.234+). The relay forwards the session's inbox and token from its environment; Faqra keeps them in memory only. Sessions Claude Code reports as unattended are never held. |`

- [ ] **Step 7: Commit and push**

```bash
git add KNOWN-ISSUES.md design-system.md docs
git commit -m "docs(windows): messaging sessions from the island checked live"
git push
```

Update the Faqra memory file: A3.1 done, what Step 5 showed, next H1a.
