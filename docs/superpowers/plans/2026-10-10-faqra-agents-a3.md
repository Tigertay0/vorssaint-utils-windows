# Faqra Agents A3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the owner read Claude's last message as styled text and reply to it from the island: when a turn finishes and the owner is likely to answer, Faqra holds Claude Code's Stop hook open, shows a reply box with a countdown, and a reply makes Claude carry on.

**Architecture:** The relay now waits on `Stop` the way it waits on `PermissionRequest`, in two phases: Faqra answers at once with a hold line or by hanging up (so a closed, slow or uninterested Faqra costs at most the 2 s run budget), and a held turn then waits for the owner's reply, which the relay prints as Claude Code's documented Stop output (`hookSpecificOutput.additionalContext`). `AgentHub` decides each hold through a `HoldFor` callback the island sets, using a tested Core rule (hold when Claude's last words ask the owner something, the island is showing that session, or the owner switched replies on for it; never past Claude Code's 8-replies-in-a-row cap). The held turn is an `AgentRequest` of a new `Reply` kind, so the A2 machinery (cards, release on Esc, fullscreen and settings, hang-up, timeout) covers it. The answered card renders Markdown through a small Core parser and, while a turn is held, gains a countdown, a reply box, Send and Done; the session's details gain a "Wait for my reply after each turn" switch, so the owner can turn it on while Claude is still working.

**Tech Stack:** C# / .NET 8, WPF + WPF UI 4.3, System.Text.Json (Nodes), System.IO.Pipes, `System.Text.RegularExpressions` source generator, xunit. No new NuGet packages.

**Spec:** `docs/superpowers/specs/2026-10-09-faqra-agents-design.md`: the A3 row of the milestone table, "How it works" (the Stop row of the waiting-events table, "Reply window rules"), "What the owner sees" (the Reply card), "Risks" (reply window feel) and "Owner's additions (2026-10-10)" (the A3 reply-window row: the length is the owner's setting, default 5 minutes; hold when any of the three conditions holds).

## Global Constraints

- Solution lives in `Windows/`; run every `dotnet` command from `C:\Users\Tigre\vorssaint-utils-windows\Windows`.
- Every new source file under `src/` starts with `// SPDX-License-Identifier: GPL-3.0-or-later` and `// Copyright (C) 2026 Faqra contributors`. Test files follow the existing convention (no header).
- Nullable warnings are errors (`Directory.Build.props`). No new NuGet packages.
- Pipe name, options, and A1/A2 timings are unchanged: connect budget 300 ms, fire-and-forget whole-run budget 2 s, PermissionRequest 110 s (relay) / 108 s (hub) / 120 s (hook). Claude Code's Stop hook timeout stays 600 s (A1's `ClaudeHookConfig`), so nothing in `~/.claude/settings.json` changes.
- Stop protocol: after writing its line the relay waits for Faqra's first line within what is left of the 2 s run budget. Faqra either hangs up (not held: the relay prints nothing and exits) or writes exactly `{"hold":true}`; then the relay waits up to `Relay.ReplyBudgetMs = 590_000` for one decision line. Only `{"decision":"reply","message":"<text>"}` with non-blank text of at most 20,000 characters prints anything.
- Stop output: `{"hookSpecificOutput":{"hookEventName":"Stop","additionalContext":"<preamble>\n\n<reply>"}}`, preamble `The user replied from Faqra, their desktop island, instead of the Claude Code prompt:`. The block form (`{"decision":"block","reason":…}`) is kept behind `StopReply.UseAdditionalContext` for the live check to settle.
- Hold rule (Core `ReplyRules.HoldFor`): never when cards cannot show (island off, Agents section hidden, a fullscreen app, Coucou's hooks present) or when the session already took 8 replies in a row; otherwise hold when the owner switched replies on for that session, or the island is showing that session's details when the turn finishes, or Claude's new last message asks the owner something and "Open the island when an agent answers" is on.
- Reply window: Faqra-only key `faqraAgentsReplyWindowSeconds`, default 300; the settings page offers 60, 120, 300 and 480 seconds; the rule clamps any stored value to 30..480 so the hold always ends inside the 590 s relay budget.
- A held turn ends when the owner replies, presses Done, presses Esc or the collapse button (A2's dismissal), the window runs out, a new prompt or the session's end arrives, the relay hangs up, or Faqra stops.
- The agents log never records a reply, a prompt, a command, an answer or Claude's words.
- User-visible copy: sentence case, no em or en dashes, no exclamation marks.
- Commits: conventional prefixes, and no `Co-Authored-By` or any Claude attribution line.
- WPF tests run inside `StaThread.Run(...)`.
- Subagents dispatched for this plan run on Sonnet (`model: "sonnet"`).

## Review Focus

1. A turn nobody is waiting on (no reply switch, the island not showing it, Claude asking nothing) is never held: Claude Code stops at once instead of looking busy for minutes. Pinned in Task 3 (`NothingHoldsATurnNobodyAwaits`) and Task 5 (`ATurnNobodyHoldsClosesAtOnce`).
2. A closed, hung or slow Faqra never holds a turn: the relay gives up on the hold answer within the run budget. Pinned in Task 4 (`AFaqraThatNeverAnswersAStopCostsAtMostTheRunBudget`, `AFinishedTurnNeverWaitsWhenFaqraIsClosed`).
3. A reply with newlines, quotes, accents and emoji reaches Claude whole and as UTF-8; a reply longer than 20,000 characters is never sent. Pinned in Task 2 (`StopReplyTests`), Task 4 (`AReplyComesBackAsUtf8`) and Task 7 (`TheReplyBoxStopsAtTheLimit`).
4. Typing a reply while the countdown ticks and other sessions send events keeps the text. Pinned in Task 7 (`TheReplyBoxKeepsItsTextWhileEventsArrive`).
5. After 8 replies in a row Claude Code stops on its own: Faqra stops holding that session and the card says why. Pinned in Task 3 (`TheCapStopsTheHold`) and Task 7 (`AtTheCapTheCardExplains`).

---

## File structure

| File | Responsibility |
|---|---|
| `src/Faqra.Core/Agents/MarkdownLite.cs` (new) | Claude's Markdown read into styled blocks and runs; a plain-text form for previews |
| `src/Faqra.Core/Agents/StopReply.cs` (new) | The hold line and Claude Code's Stop output |
| `src/Faqra.Core/Agents/ReplyRules.cs` (new) | When a finished turn is held, and for how long |
| `src/Faqra.Core/Agents/AgentDecision.cs` (modify) | `Reply` decision |
| `src/Faqra.Core/Agents/AgentRequest.cs` (modify) | `Reply` kind, `ClosesAt`, `ForReply` |
| `src/Faqra.Core/Agents/AgentBoard.cs` (modify) | `AwaitingReply` state, reply counters, `RepliesOn` |
| `src/Faqra.Core/Agents/AgentOrbStyles.cs`, `AgentsText.cs` (modify) | The new state's orb and words; `AsksOwner` |
| `src/Faqra.Core/Localization/AgentsStrings*.cs`, `src/Faqra.Core/Defaults/*` (modify) | Strings, the reply window key |
| `src/Faqra.Hook/Relay.cs` (modify) | Two-phase Stop |
| `src/Faqra.Services/Agents/AgentHub.cs` (modify) | Held turns, `HoldFor`, `SetRepliesOn` |
| `src/Faqra.App/Island/Modules/MarkdownView.cs` (new) | Markdown blocks as WPF text |
| `src/Faqra.App/Island/Modules/AgentCards.cs`, `AgentsModule.cs` (modify) | Reply box, countdown, switch, styled message |
| `src/Faqra.App/Island/IslandController.Agents.cs` (new, split out) | The island's agents logic, now with `HoldFor` |
| `src/Faqra.App/Settings/Pages/AgentsPage.cs` (modify) | The reply window setting |

---

### Task 1: Markdown for Claude's words (`MarkdownLite`)

**Files:**
- Create: `src/Faqra.Core/Agents/MarkdownLite.cs`
- Test: `tests/Faqra.Core.Tests/Agents/MarkdownLiteTests.cs`

**Interfaces:**
- Produces: `enum MdBlockKind { Paragraph, Heading, Bullet, Numbered, Quote, Code, Rule }`; `enum MdStyle { Plain, Bold, Italic, Code, Link }`; `sealed record MdInline(string Text, MdStyle Style, string? Url = null)`; `sealed record MdBlock(MdBlockKind Kind, IReadOnlyList<MdInline> Inlines, int Level = 0, string Marker = "", string Code = "")`; `static partial class MarkdownLite` with `IReadOnlyList<MdBlock> Parse(string? text)`, `IReadOnlyList<MdInline> Inlines(string text)`, `string PlainText(string? text)`.

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.Core.Tests/Agents/MarkdownLiteTests.cs`:

```csharp
using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class MarkdownLiteTests
{
    private static string Joined(IReadOnlyList<MdInline> inlines) => string.Concat(inlines.Select(i => i.Text));

    [Fact]
    public void ReadsTheBlocksClaudeWrites()
    {
        var blocks = MarkdownLite.Parse(
            "# Fixed the tray\n" +
            "The crash came from a display wake\nwhile the icon was redrawn.\n" +
            "\n" +
            "## Changes\n" +
            "- retry the icon\n" +
            "  - after 2 s\n" +
            "3. run the tests\n" +
            "> nothing else changed\n" +
            "---\n" +
            "```csharp\n" +
            "    var x = 1;\n" +
            "```");

        Assert.Equal(
            [MdBlockKind.Heading, MdBlockKind.Paragraph, MdBlockKind.Heading, MdBlockKind.Bullet, MdBlockKind.Bullet,
             MdBlockKind.Numbered, MdBlockKind.Quote, MdBlockKind.Rule, MdBlockKind.Code],
            blocks.Select(b => b.Kind));
        Assert.Equal(1, blocks[0].Level);
        Assert.Equal("Fixed the tray", Joined(blocks[0].Inlines));
        Assert.Equal("The crash came from a display wake while the icon was redrawn.", Joined(blocks[1].Inlines));
        Assert.Equal(2, blocks[2].Level);
        Assert.Equal((0, "retry the icon"), (blocks[3].Level, Joined(blocks[3].Inlines)));
        Assert.Equal((1, "after 2 s"), (blocks[4].Level, Joined(blocks[4].Inlines)));
        Assert.Equal(("3.", "run the tests"), (blocks[5].Marker, Joined(blocks[5].Inlines)));
        Assert.Equal("nothing else changed", Joined(blocks[6].Inlines));
        Assert.Equal("    var x = 1;", blocks[8].Code);
    }

    [Fact]
    public void AnUnclosedCodeBlockRunsToTheEnd()
    {
        var blocks = MarkdownLite.Parse("Look:\n```\nline one\nline two");
        Assert.Equal(MdBlockKind.Code, blocks[^1].Kind);
        Assert.Equal("line one\nline two", blocks[^1].Code);
    }

    [Fact]
    public void BlankLinesSeparateParagraphs()
    {
        var blocks = MarkdownLite.Parse("One.\r\n\r\nTwo.");
        Assert.Equal(["One.", "Two."], blocks.Select(b => Joined(b.Inlines)));
    }

    [Fact]
    public void StylesTheRunsInsideALine()
    {
        var runs = MarkdownLite.Inlines("Run **all** the *unit* tests with `dotnet test`, see [the docs](https://example.com/a).");
        Assert.Equal(
            [("Run ", MdStyle.Plain), ("all", MdStyle.Bold), (" the ", MdStyle.Plain), ("unit", MdStyle.Italic), (" tests with ", MdStyle.Plain),
             ("dotnet test", MdStyle.Code), (", see ", MdStyle.Plain), ("the docs", MdStyle.Link), (".", MdStyle.Plain)],
            runs.Select(r => (r.Text, r.Style)));
        Assert.Equal("https://example.com/a", runs.Single(r => r.Style == MdStyle.Link).Url);
    }

    [Theory]
    [InlineData("2 * 3 = 6")]
    [InlineData("snake_case_name stays")]
    [InlineData("an unclosed **bold")]
    [InlineData("a lone ` tick")]
    [InlineData("[not a link]")]
    public void MarksWithoutAPartnerStayAsText(string text)
    {
        var runs = MarkdownLite.Inlines(text);
        Assert.Equal(text, Joined(runs));
        Assert.All(runs, r => Assert.Equal(MdStyle.Plain, r.Style));
    }

    [Fact]
    public void UnderscoresAndTripleStarsWork()
    {
        Assert.Equal([("both", MdStyle.Bold)], MarkdownLite.Inlines("***both***").Select(r => (r.Text, r.Style)));
        Assert.Equal([("strong", MdStyle.Bold)], MarkdownLite.Inlines("__strong__").Select(r => (r.Text, r.Style)));
        Assert.Equal([("soft", MdStyle.Italic)], MarkdownLite.Inlines("_soft_").Select(r => (r.Text, r.Style)));
    }

    [Fact]
    public void PlainTextDropsTheMarks()
    {
        Assert.Equal("Done\nThe tests pass.\n• one\n2. two\nx = 1",
            MarkdownLite.PlainText("# Done\n\nThe **tests** pass.\n\n- one\n2. two\n\n```\nx = 1\n```"));
        Assert.Equal(string.Empty, MarkdownLite.PlainText(null));
    }

    [Fact]
    public void NothingIsNoBlocks()
    {
        Assert.Empty(MarkdownLite.Parse(null));
        Assert.Empty(MarkdownLite.Parse(""));
        Assert.Empty(MarkdownLite.Parse("\n\n  \n"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~MarkdownLiteTests"`
Expected: build FAILS with "The name 'MarkdownLite' does not exist in the current context".

- [ ] **Step 3: Write `MarkdownLite`**

`src/Faqra.Core/Agents/MarkdownLite.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text;
using System.Text.RegularExpressions;

namespace Faqra.Core.Agents;

public enum MdBlockKind { Paragraph, Heading, Bullet, Numbered, Quote, Code, Rule }

public enum MdStyle { Plain, Bold, Italic, Code, Link }

/// <summary>A run of text in one style. For a link, Text is what shows and Url is where it points.</summary>
public sealed record MdInline(string Text, MdStyle Style, string? Url = null);

/// <summary>
/// One block of Claude's message. Inlines hold its styled text (empty for Code and Rule). Level is a heading's level
/// (1 to 6) or a list item's nesting (0 at the margin); Marker is a numbered item's number ("3."); Code is a code block's
/// text, indentation kept.
/// </summary>
public sealed record MdBlock(MdBlockKind Kind, IReadOnlyList<MdInline> Inlines, int Level = 0, string Marker = "", string Code = "");

/// <summary>
/// The Markdown Claude writes, read into blocks a card can style: headings, paragraphs, lists, quotes, code blocks and
/// rules, with bold, italic, inline code and links inside them. Anything else (tables, HTML) stays as its plain text.
/// </summary>
public static partial class MarkdownLite
{
    public static IReadOnlyList<MdBlock> Parse(string? text)
    {
        var blocks = new List<MdBlock>();
        if (string.IsNullOrEmpty(text))
        {
            return blocks;
        }
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var paragraph = new List<string>();
        void Flush()
        {
            if (paragraph.Count > 0)
            {
                blocks.Add(new MdBlock(MdBlockKind.Paragraph, Inlines(string.Join(" ", paragraph))));
                paragraph.Clear();
            }
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                Flush();
                var fence = trimmed[..3];
                var code = new List<string>();
                for (i++; i < lines.Length && !lines[i].Trim().StartsWith(fence, StringComparison.Ordinal); i++)
                {
                    code.Add(lines[i]);
                }
                blocks.Add(new MdBlock(MdBlockKind.Code, [], Code: string.Join("\n", code)));
                continue;
            }
            if (trimmed.Length == 0)
            {
                Flush();
                continue;
            }
            if (HeadingPattern().Match(trimmed) is { Success: true } heading)
            {
                Flush();
                blocks.Add(new MdBlock(MdBlockKind.Heading, Inlines(heading.Groups[2].Value.Trim()), Level: heading.Groups[1].Value.Length));
                continue;
            }
            if (RulePattern().IsMatch(trimmed))
            {
                Flush();
                blocks.Add(new MdBlock(MdBlockKind.Rule, []));
                continue;
            }
            var nesting = (line.Length - line.TrimStart().Length) / 2;
            if (BulletPattern().Match(trimmed) is { Success: true } bullet)
            {
                Flush();
                blocks.Add(new MdBlock(MdBlockKind.Bullet, Inlines(bullet.Groups[1].Value), Level: nesting));
                continue;
            }
            if (NumberedPattern().Match(trimmed) is { Success: true } numbered)
            {
                Flush();
                blocks.Add(new MdBlock(MdBlockKind.Numbered, Inlines(numbered.Groups[2].Value), Level: nesting, Marker: numbered.Groups[1].Value + "."));
                continue;
            }
            if (trimmed.StartsWith('>'))
            {
                Flush();
                blocks.Add(new MdBlock(MdBlockKind.Quote, Inlines(trimmed.TrimStart('>').Trim())));
                continue;
            }
            paragraph.Add(trimmed);
        }
        Flush();
        return blocks;
    }

    /// <summary>Styled runs from one line of Markdown. A mark without a closing partner stays as text.</summary>
    public static IReadOnlyList<MdInline> Inlines(string text)
    {
        var runs = new List<MdInline>();
        var plain = new StringBuilder();
        void Emit(string value, MdStyle style, string? url = null)
        {
            if (plain.Length > 0)
            {
                runs.Add(new MdInline(plain.ToString(), MdStyle.Plain));
                plain.Clear();
            }
            if (value.Length > 0)
            {
                runs.Add(new MdInline(value, style, url));
            }
        }

        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            var rest = text.AsSpan(i);
            if (c == '`' && Close(text, i + 1, "`") is { } codeEnd)
            {
                Emit(text[(i + 1)..codeEnd], MdStyle.Code);
                i = codeEnd + 1;
                continue;
            }
            if (rest.StartsWith("***") && Close(text, i + 3, "***") is { } strongEnd)
            {
                Emit(text[(i + 3)..strongEnd], MdStyle.Bold);
                i = strongEnd + 3;
                continue;
            }
            if ((rest.StartsWith("**") || rest.StartsWith("__")) && Close(text, i + 2, text.Substring(i, 2)) is { } boldEnd)
            {
                Emit(text[(i + 2)..boldEnd], MdStyle.Bold);
                i = boldEnd + 2;
                continue;
            }
            // An underscore inside a word (snake_case) is not emphasis; a star may be (a*b*c).
            if ((c == '*' || (c == '_' && (i == 0 || !char.IsLetterOrDigit(text[i - 1]))))
                && i + 1 < text.Length && !char.IsWhiteSpace(text[i + 1])
                && Close(text, i + 1, c.ToString()) is { } italicEnd)
            {
                Emit(text[(i + 1)..italicEnd], MdStyle.Italic);
                i = italicEnd + 1;
                continue;
            }
            if (c == '[' && LinkPattern().Match(text, i) is { Success: true } link && link.Index == i)
            {
                Emit(link.Groups[1].Value, MdStyle.Link, link.Groups[2].Value);
                i += link.Length;
                continue;
            }
            plain.Append(c);
            i++;
        }
        Emit(string.Empty, MdStyle.Plain);
        return runs;
    }

    /// <summary>The message without its Markdown marks, one block per line: for short previews.</summary>
    public static string PlainText(string? text) => string.Join("\n", Parse(text)
        .Select(block => block.Kind switch
        {
            MdBlockKind.Code => block.Code,
            MdBlockKind.Rule => string.Empty,
            MdBlockKind.Bullet => "• " + Joined(block.Inlines),
            MdBlockKind.Numbered => block.Marker + " " + Joined(block.Inlines),
            _ => Joined(block.Inlines),
        })
        .Where(line => line.Length > 0));

    private static string Joined(IReadOnlyList<MdInline> inlines) => string.Concat(inlines.Select(inline => inline.Text));

    /// <summary>Where <paramref name="marker"/> closes a span that starts at <paramref name="from"/>; null when it does not, or the span is empty.</summary>
    private static int? Close(string text, int from, string marker)
    {
        if (from >= text.Length)
        {
            return null;
        }
        var at = text.IndexOf(marker, from, StringComparison.Ordinal);
        return at > from ? at : null;
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^(-{3,}|\*{3,}|_{3,})$")]
    private static partial Regex RulePattern();

    [GeneratedRegex(@"^[-*+]\s+(.*)$")]
    private static partial Regex BulletPattern();

    [GeneratedRegex(@"^(\d{1,3})[.)]\s+(.*)$")]
    private static partial Regex NumberedPattern();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)\)")]
    private static partial Regex LinkPattern();
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~MarkdownLiteTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Faqra.Core/Agents/MarkdownLite.cs tests/Faqra.Core.Tests/Agents/MarkdownLiteTests.cs
git commit -m "feat(windows): read Claude's Markdown into styled blocks"
```

---

### Task 2: The reply on the wire (`AgentDecision.Reply`, `StopReply`, reply requests)

**Files:**
- Create: `src/Faqra.Core/Agents/StopReply.cs`
- Modify: `src/Faqra.Core/Agents/AgentDecision.cs`
- Modify: `src/Faqra.Core/Agents/AgentRequest.cs`
- Test: `tests/Faqra.Core.Tests/Agents/StopReplyTests.cs` (new), `AgentDecisionTests.cs`, `PermissionReplyTests.cs`, `AgentRequestTests.cs` (add)

**Interfaces:**
- Consumes: `AgentDecision`, `PermissionReply`, `AgentRequest` (A2), `AgentJson.Compact`.
- Produces: `AgentDecisionKind.Reply`; `AgentDecision.Reply(string text)` (text in `Message`; wire `{"decision":"reply","message":"…"}`); `static class StopReply` with `const string HoldLine = "{\"hold\":true}"`, `const int MaxReplyLength = 20_000`, `const string Preamble`, `bool UseAdditionalContext { get; }` (true), `string? Stdout(AgentDecision? decision)`; `AgentRequestKind.Reply`; `AgentRequest.ClosesAt` (`DateTimeOffset?`, init); `static AgentRequest ForReply(string id, string sessionId, DateTimeOffset now, DateTimeOffset closesAt)`.

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.Core.Tests/Agents/StopReplyTests.cs`:

```csharp
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
```

Add to `tests/Faqra.Core.Tests/Agents/AgentDecisionTests.cs` (inside the class):

```csharp
    [Fact]
    public void AReplySurvivesTheWire()
    {
        Assert.Equal("{\"decision\":\"reply\",\"message\":\"Go on\"}", AgentDecision.Reply("Go on").ToLine());
        var reply = AgentDecision.TryParse(AgentDecision.Reply("Ligne 1\nLigne 2 é").ToLine())!;
        Assert.Equal(AgentDecisionKind.Reply, reply.Kind);
        Assert.Equal("Ligne 1\nLigne 2 é", reply.Message);
        Assert.Null(AgentDecision.TryParse("{\"decision\":\"reply\"}"));
        Assert.Null(AgentDecision.TryParse("{\"decision\":\"reply\",\"message\":\"\"}"));
    }
```

Add to `tests/Faqra.Core.Tests/Agents/PermissionReplyTests.cs` (inside the class):

```csharp
    [Fact]
    public void AReplyIsNoAnswerToAPermissionRequest()
    {
        Assert.Null(PermissionReply.Stdout(Bash(), AgentDecision.Reply("go")));
        Assert.Null(PermissionReply.Stdout(Question(), AgentDecision.Reply("go")));
    }
```

Add to `tests/Faqra.Core.Tests/Agents/AgentRequestTests.cs` (inside the class):

```csharp
    [Fact]
    public void AHeldTurnIsAReplyRequestWithItsDeadline()
    {
        var closes = T0.AddMinutes(5);
        var request = AgentRequest.ForReply("r9", "s1", T0, closes);
        Assert.Equal(("r9", "s1", AgentRequestKind.Reply, T0, closes), (request.Id, request.SessionId, request.Kind, request.ReceivedAt, request.ClosesAt));
        Assert.False(request.CanAlways);
        Assert.Empty(request.Questions);
        Assert.Null(AgentRequest.From("r1", AgentEvent.TryParse("{\"hook_event_name\":\"PermissionRequest\",\"tool_name\":\"Bash\"}")!, T0).ClosesAt);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~StopReplyTests|FullyQualifiedName~AgentDecisionTests|FullyQualifiedName~PermissionReplyTests|FullyQualifiedName~AgentRequestTests"`
Expected: build FAILS with "'AgentDecision' does not contain a definition for 'Reply'".

- [ ] **Step 3: Add the reply decision**

In `src/Faqra.Core/Agents/AgentDecision.cs`:

1. Replace the enum with `public enum AgentDecisionKind { Allow, Always, Deny, Answer, Reply }`.
2. Replace the `Message` property's summary with `/// <summary>For Deny: what Claude is told (null means <see cref="PermissionReply.DefaultDenyMessage"/>). For Reply: the owner's words.</summary>`.
3. Add after `Answer(...)`:

```csharp
    public static AgentDecision Reply(string text) => new(AgentDecisionKind.Reply, text, null);
```

4. In `TryParse`, add this arm before `"answer" when …`:

```csharp
                "reply" when Text(obj["message"]) is { Length: > 0 } text => Reply(text),
```

5. Replace `Word` with:

```csharp
    private static string Word(AgentDecisionKind kind) => kind switch
    {
        AgentDecisionKind.Allow => "allow",
        AgentDecisionKind.Always => "always",
        AgentDecisionKind.Deny => "deny",
        AgentDecisionKind.Reply => "reply",
        _ => "answer",
    };
```

- [ ] **Step 4: Write `StopReply`**

`src/Faqra.Core/Agents/StopReply.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

/// <summary>
/// The Stop hook's side of a reply from the island: the line Faqra sends at once when it holds a finished turn open, and
/// what the relay prints when the owner replies, from Claude Code's documented Stop output
/// (https://code.claude.com/docs/en/hooks). With no reply the relay prints nothing and Claude stops as it would have.
/// </summary>
public static class StopReply
{
    /// <summary>Faqra's first line on a held turn: the owner may reply, so the relay keeps waiting.</summary>
    public const string HoldLine = "{\"hold\":true}";

    /// <summary>Longest reply the island sends; the reply box stops there.</summary>
    public const int MaxReplyLength = 20_000;

    /// <summary>How Claude is told the words that follow are the owner's.</summary>
    public const string Preamble = "The user replied from Faqra, their desktop island, instead of the Claude Code prompt:";

    /// <summary>
    /// True: the reply goes back as Stop hook context, which Claude reads as feedback with no hook error notice. False: as
    /// a block decision with the reply as its reason, the documented older form. Settled by A3's live check.
    /// </summary>
    public static bool UseAdditionalContext { get; } = true;

    /// <summary>The line the relay prints for a held turn, or null to print nothing and let Claude stop.</summary>
    public static string? Stdout(AgentDecision? decision)
    {
        if (decision is not { Kind: AgentDecisionKind.Reply, Message: { } text } || string.IsNullOrWhiteSpace(text) || text.Length > MaxReplyLength)
        {
            return null;
        }
        var said = $"{Preamble}\n\n{text}";
        var reply = UseAdditionalContext
            ? new JsonObject { ["hookSpecificOutput"] = new JsonObject { ["hookEventName"] = "Stop", ["additionalContext"] = said } }
            : new JsonObject { ["decision"] = "block", ["reason"] = said };
        return reply.ToJsonString(AgentJson.Compact);
    }
}
```

- [ ] **Step 5: Add the reply request**

In `src/Faqra.Core/Agents/AgentRequest.cs`:

1. Replace the enum with `public enum AgentRequestKind { Approval, Question, Reply }`.
2. Replace the record's summary with `/// <summary>What a card is waiting on: a permission request (what Claude wants, what Always would save) or a finished turn held open for the owner's reply.</summary>`.
3. Add inside the record body, before `CanAlways`:

```csharp
    /// <summary>For a held turn: when Faqra lets it go if the owner has not replied.</summary>
    public DateTimeOffset? ClosesAt { get; init; }

    /// <summary>A finished turn held open for the owner's reply until <paramref name="closesAt"/>.</summary>
    public static AgentRequest ForReply(string id, string sessionId, DateTimeOffset now, DateTimeOffset closesAt) =>
        new(id, sessionId, AgentRequestKind.Reply, string.Empty, string.Empty, [], [], false, now) { ClosesAt = closesAt };
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.Core.Tests`
Expected: PASS (every Core test).

- [ ] **Step 7: Commit**

```bash
git add src/Faqra.Core/Agents tests/Faqra.Core.Tests/Agents
git commit -m "feat(windows): reply decisions and Claude Code's Stop output"
```

---
### Task 3: Held turns on the board, and when to hold (`ReplyRules`)

**Files:**
- Create: `src/Faqra.Core/Agents/ReplyRules.cs`
- Modify: `src/Faqra.Core/Agents/AgentBoard.cs`
- Modify: `src/Faqra.Core/Agents/AgentOrbStyles.cs`
- Modify: `src/Faqra.Core/Agents/AgentsText.cs`
- Modify: `src/Faqra.Core/Localization/AgentsStrings.cs`, `AgentsStrings.EnUS.cs`
- Test: `tests/Faqra.Core.Tests/Agents/ReplyRulesTests.cs` (new), `AgentBoardTests.cs`, `AgentsTextTests.cs` (add)

**Interfaces:**
- Consumes: `AgentBoard`, `AgentSession`, `AgentsText` (A1/A2).
- Produces: `AgentState.AwaitingReply` (appended to the enum); orb `Waiting`/`Accent`/0.75 and urgency 1 for it; `AgentsStrings.StateAwaitingReply` ("Waiting for your reply"); `AgentSession.ConsecutiveReplies` (int, init) and `RepliesOn` (bool, init); `AgentBoard.MaxConsecutiveReplies = 8`; `AgentBoard AwaitingReply(string sessionId, DateTimeOffset now)`, `AgentBoard Replied(string sessionId, string text, DateTimeOffset now)`, `AgentBoard ReplyClosed(string sessionId)`, `AgentBoard SetRepliesOn(string sessionId, bool on)`; `UserPromptSubmit` and `PreToolUse` reset `ConsecutiveReplies` to 0; `AgentsText.AsksOwner(string? message)`; `static class ReplyRules` with `const int MinWindowSeconds = 30`, `const int MaxWindowSeconds = 480`, `TimeSpan? HoldFor(AgentSession session, bool cardsCanShow, bool watching, bool opensOnAnswer, int windowSeconds)`.

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.Core.Tests/Agents/ReplyRulesTests.cs`:

```csharp
using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class ReplyRulesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private static AgentSession Finished(string said, bool unread = true, int replies = 0, bool repliesOn = false) =>
        new AgentSession("s1", "claude", @"C:\code\faqra", AgentState.Finished, [], said, 0, T0, T0, T0)
        {
            Unread = unread,
            ConsecutiveReplies = replies,
            RepliesOn = repliesOn,
        };

    [Fact]
    public void NothingHoldsATurnNobodyAwaits() =>
        Assert.Null(ReplyRules.HoldFor(Finished("All 1,022 tests pass."), cardsCanShow: true, watching: false, opensOnAnswer: true, windowSeconds: 300));

    [Fact]
    public void AQuestionHoldsWhenTheIslandWillShowIt()
    {
        var asked = Finished("Done. Should I push the branch?");
        Assert.Equal(TimeSpan.FromSeconds(300), ReplyRules.HoldFor(asked, true, false, true, 300));
        // With "open on answer" off nobody would see the reply box, so Claude stops as usual.
        Assert.Null(ReplyRules.HoldFor(asked, true, false, false, 300));
        // A question from an earlier turn does not count when this turn said nothing new.
        Assert.Null(ReplyRules.HoldFor(asked with { Unread = false }, true, false, true, 300));
    }

    [Fact]
    public void WatchingOrTheSwitchHoldsAnyTurn()
    {
        var plain = Finished("Done.");
        Assert.NotNull(ReplyRules.HoldFor(plain, true, watching: true, opensOnAnswer: false, 300));
        Assert.NotNull(ReplyRules.HoldFor(plain with { RepliesOn = true }, true, false, false, 300));
    }

    [Fact]
    public void NoCardMeansNoHold() =>
        Assert.Null(ReplyRules.HoldFor(Finished("Which one?", repliesOn: true), cardsCanShow: false, watching: true, opensOnAnswer: true, 300));

    [Fact]
    public void TheCapStopsTheHold()
    {
        Assert.NotNull(ReplyRules.HoldFor(Finished("Again?", replies: AgentBoard.MaxConsecutiveReplies - 1), true, true, true, 300));
        Assert.Null(ReplyRules.HoldFor(Finished("Again?", replies: AgentBoard.MaxConsecutiveReplies), true, true, true, 300));
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(45, 45)]
    [InlineData(600, 480)]
    public void TheWindowStaysInsideTheRelaysBudget(int stored, int expected) =>
        Assert.Equal(TimeSpan.FromSeconds(expected), ReplyRules.HoldFor(Finished("Done."), true, true, true, stored));
}
```

Add to `tests/Faqra.Core.Tests/Agents/AgentBoardTests.cs` (inside the class; the A2 `With(...)` helper is already there):

```csharp
    [Fact]
    public void AHeldTurnWaitsThenCarriesOnWithTheReply()
    {
        var now = new DateTimeOffset(2026, 10, 10, 9, 5, 0, TimeSpan.Zero);
        var finished = With(
            "{\"hook_event_name\":\"UserPromptSubmit\",\"session_id\":\"s\",\"prompt\":\"go\"}",
            "{\"hook_event_name\":\"Stop\",\"session_id\":\"s\",\"last_assistant_message\":\"Red or blue?\"}");
        var waiting = finished.AwaitingReply("s", now);
        Assert.Equal(AgentState.AwaitingReply, waiting.Sessions["s"].State);
        Assert.Same(waiting, waiting.AwaitingReply("other", now));

        var replied = waiting.Replied("s", "Blue, please", now);
        var s = replied.Sessions["s"];
        Assert.Equal((AgentState.Thinking, "Blue, please", 1, false), (s.State, s.LastPrompt, s.ConsecutiveReplies, s.Unread));
        Assert.Equal(new AgentStep(now, AgentStepKind.Prompt, "Blue, please"), s.Steps[^1]);
        Assert.Null(s.FinishedAt);
    }

    [Fact]
    public void ALetGoTurnRests()
    {
        var waiting = With("{\"hook_event_name\":\"Stop\",\"session_id\":\"s\",\"last_assistant_message\":\"Red or blue?\"}")
            .AwaitingReply("s", DateTimeOffset.Now);
        Assert.Equal(AgentState.Idle, waiting.ReplyClosed("s").Sessions["s"].State);
        var busy = With("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s\",\"tool_name\":\"Read\"}");
        Assert.Same(busy, busy.ReplyClosed("s"));
    }

    [Fact]
    public void AHeldTurnNeverGoesStale()
    {
        var waiting = With("{\"hook_event_name\":\"Stop\",\"session_id\":\"s\",\"last_assistant_message\":\"Red or blue?\"}")
            .AwaitingReply("s", new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero));
        var later = new DateTimeOffset(2026, 10, 10, 9, 45, 0, TimeSpan.Zero);
        Assert.Equal(AgentState.AwaitingReply, waiting.Tick(later).Sessions["s"].State);
    }

    [Fact]
    public void ToolUseOrANewPromptResetsTheReplyCount()
    {
        var now = DateTimeOffset.Now;
        var twice = With("{\"hook_event_name\":\"Stop\",\"session_id\":\"s\",\"last_assistant_message\":\"?\"}")
            .Replied("s", "a", now).Replied("s", "b", now);
        Assert.Equal(2, twice.Sessions["s"].ConsecutiveReplies);
        var tool = twice.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s\",\"tool_name\":\"Read\"}")!, now);
        Assert.Equal(0, tool.Sessions["s"].ConsecutiveReplies);
        var prompt = twice.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"UserPromptSubmit\",\"session_id\":\"s\",\"prompt\":\"x\"}")!, now);
        Assert.Equal(0, prompt.Sessions["s"].ConsecutiveReplies);
    }

    [Fact]
    public void TheReplySwitchIsPerSession()
    {
        var board = With("{\"hook_event_name\":\"SessionStart\",\"session_id\":\"s\"}");
        var on = board.SetRepliesOn("s", true);
        Assert.True(on.Sessions["s"].RepliesOn);
        Assert.Same(on, on.SetRepliesOn("s", true));
        Assert.False(on.SetRepliesOn("s", false).Sessions["s"].RepliesOn);
        Assert.Same(board, board.SetRepliesOn("other", true));
    }

    [Fact]
    public void AWaitingTurnLooksLikeAQuestion()
    {
        Assert.Equal(new AgentOrbStyle(OrbLook.Waiting, AgentTone.Accent, 0.75), AgentOrbStyles.For(AgentState.AwaitingReply));
        Assert.Equal(AgentOrbStyles.Urgency(AgentState.Question), AgentOrbStyles.Urgency(AgentState.AwaitingReply));
    }
```

Add to `tests/Faqra.Core.Tests/Agents/AgentsTextTests.cs` (inside the class):

```csharp
    [Theory]
    [InlineData("Which do you prefer?", true)]
    [InlineData("Done. Should I push the branch?", true)]
    [InlineData("All tests pass.\n\nWant me to open a pull request?", true)]
    [InlineData("**Want me to continue?**", true)]
    [InlineData("Which approach?\n\n- Keep it\n- Rewrite it", true)]
    [InlineData("Pick one:\n\n1. Red\n2. Blue", true)]
    [InlineData("Fixed it. All 1,022 tests pass.", false)]
    [InlineData("Why did it fail? A race on the pipe. Fixed now.", false)]
    [InlineData("Here is the check:\n\n```\nif (x?)\n```\nDone.", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void KnowsWhenClaudeAsksTheOwner(string? message, bool asks) => Assert.Equal(asks, AgentsText.AsksOwner(message));

    [Fact]
    public void AWaitingTurnSaysSo() =>
        Assert.Equal("Waiting for your reply", AgentsText.State(Session(AgentState.AwaitingReply), S));
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~ReplyRulesTests|FullyQualifiedName~AgentBoardTests|FullyQualifiedName~AgentsTextTests"`
Expected: build FAILS with "'AgentState' does not contain a definition for 'AwaitingReply'".

- [ ] **Step 3: Extend the board**

In `src/Faqra.Core/Agents/AgentBoard.cs`:

1. Replace the `AgentState` enum with (the new member goes last so nothing else renumbers):

```csharp
public enum AgentState { Idle, Thinking, Working, Searching, Compacting, Background, Approval, Question, Error, RateLimited, Finished, AwaitingReply }
```

2. Add to `AgentSession`'s body, after `Unread`:

```csharp
    /// <summary>Replies from the island in a row with no tool use between them; Claude Code stops taking them at 8.</summary>
    public int ConsecutiveReplies { get; init; }

    /// <summary>The owner wants every finished turn of this session held for a reply.</summary>
    public bool RepliesOn { get; init; }
```

3. Add after `public const int MaxSteps = 20;`:

```csharp
    /// <summary>How many Stop-hook replies in a row Claude Code takes before it stops anyway.</summary>
    public const int MaxConsecutiveReplies = 8;
```

4. Replace the `"UserPromptSubmit"` and `"PreToolUse"` arms of `Next` with:

```csharp
        "UserPromptSubmit" => Step(s with { State = AgentState.Thinking, FinishedAt = null, LastPrompt = e.Prompt ?? s.LastPrompt, Unread = false, ConsecutiveReplies = 0 }, now, AgentStepKind.Prompt, OneLine(e.Prompt ?? string.Empty, PromptChars)),
        "PreToolUse" => ToolStep(s with { State = e.ToolName is { } t && SearchTools.Contains(t) ? AgentState.Searching : AgentState.Working, ConsecutiveReplies = 0 }, e, now),
```

5. Add these members after `MarkRead`:

```csharp
    /// <summary>A finished turn is held open for the owner's reply.</summary>
    public AgentBoard AwaitingReply(string sessionId, DateTimeOffset now) =>
        Sessions.TryGetValue(sessionId, out var s)
            ? this with { Sessions = Sessions.SetItem(sessionId, s with { State = AgentState.AwaitingReply, UpdatedAt = now }) }
            : this;

    /// <summary>The owner replied from the island: Claude carries on with the reply as its next prompt.</summary>
    public AgentBoard Replied(string sessionId, string text, DateTimeOffset now)
    {
        if (!Sessions.TryGetValue(sessionId, out var s))
        {
            return this;
        }
        var carrying = s with
        {
            State = AgentState.Thinking,
            FinishedAt = null,
            Unread = false,
            LastPrompt = text,
            ConsecutiveReplies = s.ConsecutiveReplies + 1,
            UpdatedAt = now,
        };
        return this with { Sessions = Sessions.SetItem(sessionId, Step(carrying, now, AgentStepKind.Prompt, OneLine(text, PromptChars))) };
    }

    /// <summary>A held turn was let go without a reply: the session rests as it would have.</summary>
    public AgentBoard ReplyClosed(string sessionId) =>
        Sessions.TryGetValue(sessionId, out var s) && s.State == AgentState.AwaitingReply
            ? this with { Sessions = Sessions.SetItem(sessionId, s with { State = Resting(s) }) }
            : this;

    /// <summary>The owner wants (or no longer wants) every finished turn of this session held for a reply.</summary>
    public AgentBoard SetRepliesOn(string sessionId, bool on) =>
        Sessions.TryGetValue(sessionId, out var s) && s.RepliesOn != on
            ? this with { Sessions = Sessions.SetItem(sessionId, s with { RepliesOn = on }) }
            : this;
```

(`IsStale` lists only busy states and Error, so a waiting turn never goes stale; the hub's window ends it.)

- [ ] **Step 4: Give the new state its orb, urgency and words**

In `src/Faqra.Core/Agents/AgentOrbStyles.cs`, add to `For` after the `Question` arm:

```csharp
        AgentState.AwaitingReply => new(OrbLook.Waiting, AgentTone.Accent, 0.75),
```

and replace `AgentState.Question => 1,` in `Urgency` with:

```csharp
        AgentState.Question or AgentState.AwaitingReply => 1,
```

In `src/Faqra.Core/Localization/AgentsStrings.cs`, add `public required string StateAwaitingReply { get; init; }` after `StateFinished`; in `AgentsStrings.EnUS.cs` add `StateAwaitingReply = "Waiting for your reply",` after `StateFinished = "Done",`.

In `src/Faqra.Core/Agents/AgentsText.cs`, add to `State` after the `Finished` arm:

```csharp
        AgentState.AwaitingReply => s.StateAwaitingReply,
```

and add after `RequestTitle`:

```csharp
    /// <summary>
    /// True when Claude's last words ask the owner something: the last paragraph of prose (code blocks skipped) has a line
    /// ending in a question mark, or the last paragraph is a list of options and the one before it asks or ends in a colon.
    /// </summary>
    public static bool AsksOwner(string? message)
    {
        var paragraphs = ProseParagraphs(message);
        if (paragraphs.Count == 0)
        {
            return false;
        }
        if (paragraphs[^1].Any(EndsAsking))
        {
            return true;
        }
        return paragraphs.Count > 1
            && paragraphs[^1].All(IsListLine)
            && paragraphs[^2].Any(line => EndsAsking(line) || Unwrapped(line).EndsWith(':'));
    }

    private static List<List<string>> ProseParagraphs(string? message)
    {
        var paragraphs = new List<List<string>>();
        var current = new List<string>();
        var inFence = false;
        foreach (var raw in (message ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("```", StringComparison.Ordinal) || line.StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }
            if (inFence)
            {
                continue;
            }
            if (line.Length == 0)
            {
                if (current.Count > 0)
                {
                    paragraphs.Add(current);
                    current = [];
                }
                continue;
            }
            current.Add(line);
        }
        if (current.Count > 0)
        {
            paragraphs.Add(current);
        }
        return paragraphs;
    }

    private static bool EndsAsking(string line) => Unwrapped(line) is var bare && (bare.EndsWith('?') || bare.EndsWith('？'));

    /// <summary>A line without the emphasis, quotes and brackets that may close it.</summary>
    private static string Unwrapped(string line) => line.TrimEnd('*', '_', '`', '"', '\'', ')', ']', '»', '”', '’', ' ');

    private static bool IsListLine(string line) =>
        line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal) || line.StartsWith("+ ", StringComparison.Ordinal)
        || (line.Length > 2 && char.IsDigit(line[0]) && (line.Contains(". ", StringComparison.Ordinal) || line.Contains(") ", StringComparison.Ordinal)));
```

- [ ] **Step 5: Write `ReplyRules`**

`src/Faqra.Core/Agents/ReplyRules.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

namespace Faqra.Core.Agents;

/// <summary>
/// When a finished turn waits for the owner's reply, as the owner chose it (2026-10-10): when they switched replies on
/// for the session, when they are looking at that session as it finishes, or when Claude's new words ask them something
/// and the island will open on them. Never when no card can show, and never past Claude Code's replies-in-a-row cap.
/// </summary>
public static class ReplyRules
{
    public const int MinWindowSeconds = 30;

    /// <summary>The longest hold; the relay waits 590 s and Claude Code's Stop hook timeout is 600 s.</summary>
    public const int MaxWindowSeconds = 480;

    /// <summary>How long to hold <paramref name="session"/>'s finished turn, or null to let Claude stop at once.</summary>
    public static TimeSpan? HoldFor(AgentSession session, bool cardsCanShow, bool watching, bool opensOnAnswer, int windowSeconds)
    {
        if (!cardsCanShow || session.ConsecutiveReplies >= AgentBoard.MaxConsecutiveReplies)
        {
            return null;
        }
        var asks = opensOnAnswer && session.Unread && AgentsText.AsksOwner(session.LastMessage);
        if (!session.RepliesOn && !watching && !asks)
        {
            return null;
        }
        return TimeSpan.FromSeconds(Math.Clamp(windowSeconds, MinWindowSeconds, MaxWindowSeconds));
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.Core.Tests`
Expected: PASS (every Core test, including A1's orb tests, which iterate every state).

- [ ] **Step 7: Commit**

```bash
git add src/Faqra.Core tests/Faqra.Core.Tests/Agents
git commit -m "feat(windows): held turns on the agents board and the rule for holding them"
```

---

### Task 4: The relay holds a finished turn when Faqra asks

**Files:**
- Modify: `src/Faqra.Hook/Relay.cs`
- Test: `tests/Faqra.Services.Tests/Agents/RelayTests.cs` (add)

**Interfaces:**
- Consumes: `StopReply.HoldLine`, `StopReply.Stdout`, `AgentDecision.TryParse` (Task 2).
- Produces: `Relay.ReplyBudgetMs = 590_000`. Protocol (Global Constraints): for `Stop` only, after its line the relay reads Faqra's first line within what is left of `RunBudgetMs`; anything but `StopReply.HoldLine` (including a hang-up) ends the run with nothing printed; after the hold line it reads one more line within `ReplyBudgetMs` and prints `StopReply.Stdout(...)` + `\n` as UTF-8. `StopFailure` and every other event behave as before.

- [ ] **Step 1: Write the failing tests**

Add to `tests/Faqra.Services.Tests/Agents/RelayTests.cs`, inside the class, after `FaqraAnswers`:

```csharp
    private const string StopLine = "{\"session_id\":\"s1\",\"hook_event_name\":\"Stop\",\"last_assistant_message\":\"Red or blue?\"}";

    /// <summary>Plays Faqra holding a finished turn: reads the relay's line, writes the hold line (or not), waits, then the reply (or nothing), and hangs up.</summary>
    private static Task<string?> FaqraHolds(NamedPipeServerStream server, bool hold, string? reply, int delayMs = 0) => Task.Run(async () =>
    {
        string? line = null;
        try
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
            line = await reader.ReadLineAsync();
            if (hold)
            {
                await server.WriteAsync(Encoding.UTF8.GetBytes(Faqra.Core.Agents.StopReply.HoldLine + "\n"));
                await server.FlushAsync();
            }
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

    [Fact]
    public async Task AFinishedTurnNobodyHoldsEndsAtOnce()
    {
        var name = PipeName();
        var faqra = FaqraHolds(Server(name), hold: false, reply: null);
        using var stdout = new MemoryStream();
        var clock = Stopwatch.StartNew();
        Assert.Equal(0, Relay.Run(["Stop"], Stdin(StopLine), stdout, NoEnv, @"C:\work", name));
        Assert.InRange(clock.ElapsedMilliseconds, 0, 1000);
        Assert.Empty(stdout.ToArray());
        Assert.Contains("\"hook_event_name\":\"Stop\"", await faqra);
    }

    [Fact]
    public async Task AHeldTurnPrintsTheOwnersReply()
    {
        var name = PipeName();
        var faqra = FaqraHolds(Server(name), hold: true, reply: "{\"decision\":\"reply\",\"message\":\"Blue, please\"}", delayMs: 300);
        using var stdout = new MemoryStream();
        Relay.Run(["Stop"], Stdin(StopLine), stdout, NoEnv, @"C:\work", name);
        var printed = JsonNode.Parse(Printed(stdout))!["hookSpecificOutput"]!;
        Assert.Equal("Stop", printed["hookEventName"]!.GetValue<string>());
        Assert.EndsWith("Blue, please", printed["additionalContext"]!.GetValue<string>());
        await faqra;
    }

    [Fact]
    public async Task AHeldTurnWaitsBeyondTheRunBudget()
    {
        var name = PipeName();
        var faqra = FaqraHolds(Server(name), hold: true, reply: "{\"decision\":\"reply\",\"message\":\"later\"}", delayMs: Relay.RunBudgetMs + 600);
        using var stdout = new MemoryStream();
        Relay.Run(["Stop"], Stdin(StopLine), stdout, NoEnv, @"C:\work", name);
        Assert.Contains("later", Printed(stdout));
        await faqra;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{\"decision\":\"allow\"}")]
    [InlineData("not json")]
    public async Task AHeldTurnLetGoPrintsNothing(string? reply)
    {
        var name = PipeName();
        var faqra = FaqraHolds(Server(name), hold: true, reply: reply);
        using var stdout = new MemoryStream();
        Relay.Run(["Stop"], Stdin(StopLine), stdout, NoEnv, @"C:\work", name);
        Assert.Empty(stdout.ToArray());
        await faqra;
    }

    [Fact]
    public async Task AFaqraThatNeverAnswersAStopCostsAtMostTheRunBudget()
    {
        var name = PipeName();
        using var server = Server(name);
        var reading = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
            await reader.ReadLineAsync();
            await Task.Delay(Relay.RunBudgetMs + 3000); // never answers, never hangs up
        });
        using var stdout = new MemoryStream();
        var clock = Stopwatch.StartNew();
        Relay.Run(["Stop"], Stdin(StopLine), stdout, NoEnv, @"C:\work", name);
        Assert.InRange(clock.ElapsedMilliseconds, 0, Relay.RunBudgetMs + 1500);
        Assert.Empty(stdout.ToArray());
    }

    [Fact]
    public void AFinishedTurnNeverWaitsWhenFaqraIsClosed()
    {
        Relay.Run(["Stop"], Stdin(StopLine), Stream.Null, NoEnv, @"C:\work", PipeName()); // warm up the JIT
        var clock = Stopwatch.StartNew();
        Relay.Run(["Stop"], Stdin(StopLine), Stream.Null, NoEnv, @"C:\work", PipeName());
        Assert.InRange(clock.ElapsedMilliseconds, 0, 250);
    }

    [Fact]
    public async Task AFailedTurnIsNeverHeld()
    {
        var name = PipeName();
        var faqra = FaqraHolds(Server(name), hold: true, reply: "{\"decision\":\"reply\",\"message\":\"x\"}", delayMs: 200);
        using var stdout = new MemoryStream();
        var clock = Stopwatch.StartNew();
        Relay.Run(["StopFailure"], Stdin("{\"session_id\":\"s1\",\"hook_event_name\":\"StopFailure\"}"), stdout, NoEnv, @"C:\work", name);
        Assert.InRange(clock.ElapsedMilliseconds, 0, 1000);
        Assert.Empty(stdout.ToArray());
        await faqra;
    }

    [Fact]
    public async Task AReplyComesBackAsUtf8()
    {
        const string words = "Oui, en bleu 🙂\nEt « merci ».";
        var reply = new JsonObject { ["decision"] = "reply", ["message"] = words }.ToJsonString();
        var name = PipeName();
        var faqra = FaqraHolds(Server(name), hold: true, reply: reply);
        using var stdout = new MemoryStream();
        Relay.Run(["Stop"], Stdin(StopLine), stdout, NoEnv, @"C:\work", name);
        var bytes = stdout.ToArray();
        Assert.Equal((byte)'{', bytes[0]);
        var said = JsonNode.Parse(Encoding.UTF8.GetString(bytes))!["hookSpecificOutput"]!["additionalContext"]!.GetValue<string>();
        Assert.EndsWith(words, said);
        await faqra;
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Services.Tests --filter "FullyQualifiedName~RelayTests"`
Expected: FAIL in `AHeldTurnPrintsTheOwnersReply`, `AHeldTurnWaitsBeyondTheRunBudget` and `AReplyComesBackAsUtf8` (nothing printed: the relay does not wait on Stop yet).

- [ ] **Step 3: Teach the relay to hold a turn**

In `src/Faqra.Hook/Relay.cs`:

1. Add after `DecisionBudgetMs`:

```csharp
    /// <summary>How long a held turn waits for the owner's reply; Claude Code's Stop hook timeout is 600 s.</summary>
    public const int ReplyBudgetMs = 590_000;
```

2. Replace `private sealed record Outgoing(string Line, JsonObject Request, bool WaitsForAnswer);` with:

```csharp
    private enum Waits { Nothing, Decision, Reply }

    /// <summary>The line for Faqra; for a permission request also the untrimmed payload its reply is built from.</summary>
    private sealed record Outgoing(string Line, JsonObject Request, Waits Wait);
```

3. In `Prepare`, replace the body after the `ToLine` check with:

```csharp
            // A finished turn may be held for the owner's reply. StopFailure's name continues past "Stop", so the
            // closing quote keeps it out.
            if (line.Contains("\"hook_event_name\":\"Stop\"", StringComparison.Ordinal))
            {
                return new Outgoing(line, new JsonObject(), Waits.Reply);
            }
            // Only a permission request needs the payload again, whole: Claude reads its own question back.
            var maybe = eventArg == "PermissionRequest" || line.Contains("\"hook_event_name\":\"PermissionRequest\"", StringComparison.Ordinal);
            if (!maybe || JsonNode.Parse(text.TrimStart('\uFEFF')) is not JsonObject request)
            {
                return new Outgoing(line, new JsonObject(), Waits.Nothing);
            }
            var name = request["hook_event_name"] is JsonValue value && value.GetValueKind() == JsonValueKind.String
                ? value.GetValue<string>()
                : eventArg;
            return new Outgoing(line, request, name == "PermissionRequest" ? Waits.Decision : Waits.Nothing);
```

4. Replace `Run` with:

```csharp
    public static int Run(string[] args, Stream stdin, Stream stdout, Func<string, string?> env, string cwd, string pipeName)
    {
        var clock = Stopwatch.StartNew();
        // The work runs on pool (background) threads, so a blocked read cannot keep the process alive once Main returns.
        var prepared = Task.Run(() => Prepare(args.Length > 0 ? args[0] : string.Empty, stdin, env, cwd));
        if (!prepared.Wait(RunBudgetMs) || prepared.Result is not { } outgoing)
        {
            return 0;
        }
        if (outgoing.Wait == Waits.Reply)
        {
            return HoldTurn(pipeName, outgoing.Line, stdout, Math.Max(0, RunBudgetMs - (int)clock.ElapsedMilliseconds));
        }
        var waits = outgoing.Wait == Waits.Decision;
        var talk = Task.Run(() => Talk(pipeName, outgoing.Line, waits));
        var budget = waits ? DecisionBudgetMs : Math.Max(0, RunBudgetMs - (int)clock.ElapsedMilliseconds);
        if (!talk.Wait(budget) || !waits)
        {
            return 0;
        }
        Print(stdout, PermissionReply.Stdout(outgoing.Request, AgentDecision.TryParse(talk.Result)));
        return 0;
    }

    /// <summary>
    /// A finished turn: Faqra says at once whether it holds it for the owner's reply. Not held, or no answer inside the run
    /// budget, and the relay ends with nothing printed. Held, and it waits up to <see cref="ReplyBudgetMs"/> for the reply,
    /// which it prints for Claude to carry on with.
    /// </summary>
    private static int HoldTurn(string pipeName, string line, Stream stdout, int holdBudgetMs)
    {
        var held = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var talk = Task.Run(() => TalkHeld(pipeName, line, held));
        if (!held.Task.Wait(holdBudgetMs) || !held.Task.Result || !talk.Wait(ReplyBudgetMs))
        {
            return 0;
        }
        Print(stdout, StopReply.Stdout(AgentDecision.TryParse(talk.Result)));
        return 0;
    }

    private static void Print(Stream stdout, string? reply)
    {
        if (reply is not null)
        {
            stdout.Write(Encoding.UTF8.GetBytes(reply + "\n"));
            stdout.Flush();
        }
    }
```

5. In `Talk`, replace `return waitsForAnswer ? ReadReply(pipe) : null;` with `return waitsForAnswer ? new LineReader(pipe).Next() : null;`, delete `ReadReply`, and add after `Talk`:

```csharp
    /// <summary>Sends the line, then reads Faqra's hold line and, once held, the reply line. Always completes <paramref name="held"/>.</summary>
    private static string? TalkHeld(string pipeName, string line, TaskCompletionSource<bool> held)
    {
        try
        {
            if (!WaitNamedPipe(@"\\.\pipe\" + pipeName, ConnectBudgetMs))
            {
                return null;
            }
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
            pipe.Connect(ConnectBudgetMs);
            pipe.Write(Encoding.UTF8.GetBytes(line + "\n"));
            pipe.Flush();
            var lines = new LineReader(pipe);
            if (lines.Next() != StopReply.HoldLine)
            {
                return null;
            }
            held.TrySetResult(true);
            return lines.Next();
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            held.TrySetResult(false);
        }
    }

    /// <summary>Faqra's answer one line at a time: null once it hangs up with nothing more, or a line passes 1 MB.</summary>
    private sealed class LineReader(Stream pipe)
    {
        private readonly MemoryStream _pending = new();
        private readonly byte[] _chunk = new byte[16 * 1024];

        public string? Next()
        {
            while (true)
            {
                var buffered = _pending.GetBuffer();
                var newline = Array.IndexOf(buffered, (byte)'\n', 0, (int)_pending.Length);
                if (newline >= 0)
                {
                    var text = Encoding.UTF8.GetString(buffered, 0, newline);
                    var rest = buffered.AsSpan(newline + 1, (int)_pending.Length - newline - 1).ToArray();
                    _pending.SetLength(0);
                    _pending.Write(rest);
                    return text;
                }
                if (_pending.Length > MaxReplyBytes)
                {
                    return null;
                }
                var read = pipe.Read(_chunk, 0, _chunk.Length);
                if (read == 0)
                {
                    if (_pending.Length == 0)
                    {
                        return null;
                    }
                    var last = Encoding.UTF8.GetString(_pending.GetBuffer(), 0, (int)_pending.Length);
                    _pending.SetLength(0);
                    return last;
                }
                _pending.Write(_chunk, 0, read);
            }
        }
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.Services.Tests --filter "FullyQualifiedName~RelayTests"`
Expected: PASS (every relay test, A2's included). Then run them five times with two processors, as CI has: in Git Bash `for i in 1 2 3 4 5; do DOTNET_PROCESSOR_COUNT=2 dotnet test tests/Faqra.Services.Tests --filter "FullyQualifiedName~RelayTests" --nologo -v q 2>&1 | grep -E "Passed!|Failed!|\[FAIL\]"; done`. All five must pass.

- [ ] **Step 5: Commit**

```bash
git add src/Faqra.Hook/Relay.cs tests/Faqra.Services.Tests/Agents/RelayTests.cs
git commit -m "feat(windows): relay holds a finished turn when Faqra asks and prints the owner's reply"
```

---
### Task 5: The hub holds a finished turn for the owner's reply

**Files:**
- Modify: `src/Faqra.Services/Agents/AgentHub.cs`
- Modify: `tests/Faqra.Services.Tests/Agents/HubTestKit.cs` (one reader per fake relay)
- Test: `tests/Faqra.Services.Tests/Agents/AgentHubReplyTests.cs` (new)

**Interfaces:**
- Consumes: `StopReply.HoldLine`, `AgentDecision.Reply`, `AgentRequest.ForReply`, `AgentRequestKind.Reply` (Task 2); `AgentBoard.AwaitingReply`, `Replied`, `ReplyClosed`, `SetRepliesOn` (Task 3); the A2 hub (`Requests`, `Answer`, `Release`, `Settle`, `ReleaseSession`, `HangUp`, `Fold`, `Show`, `Open`).
- Produces on `AgentHub`: `Func<AgentSession, TimeSpan?> HoldFor { get; set; }` (default `_ => null`; asked on the context's thread for every finished turn, with the board already showing the turn's words, before `TurnFinished` is raised); `TurnFinished` is now raised for a held turn even when it has no words (Unread false); `void SetRepliesOn(string sessionId, bool on)`. A held turn is a `Reply` request in `Requests` (never raised through `RequestArrived`), answered with `Answer(id, AgentDecision.Reply(text))` and let go with `Release(id)`; the session shows `AwaitingReply` while held, `Thinking` after a reply, and rests when let go. The agents log writes `Decision <session> - replied` (or `released`), never the words.

- [ ] **Step 1: Give each fake relay one reader**

In `tests/Faqra.Services.Tests/Agents/HubTestKit.cs`, replace the body of `FakeRelay` after its constructor with:

```csharp
    private StreamReader? _reader;

    public static async Task<FakeRelay> SendAsync(string pipeName, string line)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
        await pipe.ConnectAsync(2000);
        await pipe.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"));
        await pipe.FlushAsync();
        return new FakeRelay(pipe);
    }

    /// <summary>The next line Faqra writes, or null once it hangs up. One reader per relay, so a buffered second line is not lost.</summary>
    public async Task<string?> ReplyAsync(TimeSpan within)
    {
        _reader ??= new StreamReader(_pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
        return await _reader.ReadLineAsync().WaitAsync(within);
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _pipe.Dispose();
    }
```

- [ ] **Step 2: Write the failing tests**

`tests/Faqra.Services.Tests/Agents/AgentHubReplyTests.cs`:

```csharp
using System.Diagnostics;
using Faqra.Core.Agents;
using Faqra.Services.Agents;
using static Faqra.Services.Tests.Agents.HubTestKit;

namespace Faqra.Services.Tests.Agents;

public class AgentHubReplyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(5);

    private const string Asked = "{\"hook_event_name\":\"Stop\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\faqra\",\"last_assistant_message\":\"Red or blue?\"}";

    private static AgentHub Hub(SerialContext context, string pipe, Func<AgentSession, TimeSpan?>? holdFor = null, string? log = null)
    {
        var hub = new AgentHub(pipe, context, () => T0, log) { CanAsk = () => true, HoldFor = holdFor ?? (_ => null) };
        hub.SetRunning(true);
        return hub;
    }

    [Fact]
    public async Task ATurnNobodyHoldsClosesAtOnce()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        using var relay = await FakeRelay.SendAsync(pipe, Asked);
        var clock = Stopwatch.StartNew();

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.InRange(clock.ElapsedMilliseconds, 0, 1000);
        Assert.Empty(hub.Requests);
        Assert.Equal(AgentState.Finished, (await OnContext(context, () => hub.Board)).Sessions["s1"].State);
    }

    [Fact]
    public async Task AHeldTurnSendsTheHoldLineThenTheReply()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe, _ => TimeSpan.FromMinutes(5));
        using var relay = await FakeRelay.SendAsync(pipe, Asked);

        Assert.Equal(StopReply.HoldLine, await relay.ReplyAsync(Within));
        var request = Assert.Single(hub.Requests);
        Assert.Equal((AgentRequestKind.Reply, "s1", (DateTimeOffset?)T0.AddMinutes(5)), (request.Kind, request.SessionId, request.ClosesAt));
        Assert.Equal(AgentState.AwaitingReply, (await OnContext(context, () => hub.Board)).Sessions["s1"].State);

        Assert.True(await OnContext(context, () => hub.Answer(request.Id, AgentDecision.Reply("Blue, please"))));
        var reply = AgentDecision.TryParse(await relay.ReplyAsync(Within))!;
        Assert.Equal((AgentDecisionKind.Reply, "Blue, please"), (reply.Kind, reply.Message));
        var session = (await OnContext(context, () => hub.Board)).Sessions["s1"];
        Assert.Equal((AgentState.Thinking, "Blue, please", 1), (session.State, session.LastPrompt, session.ConsecutiveReplies));
        Assert.Empty(hub.Requests);
    }

    [Fact]
    public async Task DoneLetsTheTurnEnd()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe, _ => TimeSpan.FromMinutes(5));
        using var relay = await FakeRelay.SendAsync(pipe, Asked);
        Assert.Equal(StopReply.HoldLine, await relay.ReplyAsync(Within));

        await OnContext(context, () => { hub.Release(hub.Requests[0].Id); return 0; });

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.Empty(hub.Requests);
        Assert.Equal(AgentState.Idle, (await OnContext(context, () => hub.Board)).Sessions["s1"].State);
    }

    [Fact]
    public async Task TheReplyWindowRunsOut()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe, _ => TimeSpan.FromMilliseconds(300));
        using var relay = await FakeRelay.SendAsync(pipe, Asked);
        Assert.Equal(StopReply.HoldLine, await relay.ReplyAsync(Within));

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.True(await Eventually(() => hub.Requests.Count == 0));
        Assert.Equal(AgentState.Idle, (await OnContext(context, () => hub.Board)).Sessions["s1"].State);
    }

    [Fact]
    public async Task ANewPromptLetsAHeldTurnGo()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe, _ => TimeSpan.FromMinutes(5));
        using var relay = await FakeRelay.SendAsync(pipe, Asked);
        Assert.Equal(StopReply.HoldLine, await relay.ReplyAsync(Within));

        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"UserPromptSubmit\",\"session_id\":\"s1\",\"prompt\":\"never mind\"}"))
        {
        }

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.True(await Eventually(() => hub.Requests.Count == 0));
    }

    [Fact]
    public async Task TheIslandDecidesBeforeItHearsOfTheAnswer()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        var order = new List<string>();
        using var hub = Hub(context, pipe, session =>
        {
            order.Add($"hold:{session.LastMessage}:{session.State}");
            return TimeSpan.FromMinutes(1);
        });
        hub.TurnFinished += session => order.Add($"finished:{session.State}");
        using var relay = await FakeRelay.SendAsync(pipe, Asked);
        Assert.Equal(StopReply.HoldLine, await relay.ReplyAsync(Within));

        Assert.Equal(["hold:Red or blue?:Finished", "finished:AwaitingReply"], await OnContext(context, () => order.ToList()));
    }

    [Fact]
    public async Task AThrowingHoldForLetsTheTurnEnd()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe, _ => throw new InvalidOperationException("island gone"));
        using var relay = await FakeRelay.SendAsync(pipe, Asked);

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.Empty(hub.Requests);
        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"s2\"}"))
        {
        }
        Assert.True(await Eventually(() => hub.Board.Sessions.ContainsKey("s2")));
    }

    [Fact]
    public async Task RepliesCanBeSwitchedOnPerSession()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"s1\"}"))
        {
        }
        Assert.True(await Eventually(() => hub.Board.Sessions.ContainsKey("s1")));

        var on = await OnContext(context, () =>
        {
            hub.SetRepliesOn("s1", true);
            return hub.Board.Sessions["s1"].RepliesOn;
        });
        Assert.True(on);
    }

    [Fact]
    public async Task TheLogNamesTheReplyButNeverItsWords()
    {
        var pipe = NewPipe();
        var log = Path.Combine(Path.GetTempPath(), $"faqra-agents-{Guid.NewGuid():N}.log");
        using var context = new SerialContext();
        try
        {
            using (var hub = Hub(context, pipe, _ => TimeSpan.FromMinutes(5), log))
            using (var relay = await FakeRelay.SendAsync(pipe, Asked))
            {
                Assert.Equal(StopReply.HoldLine, await relay.ReplyAsync(Within));
                await OnContext(context, () => hub.Answer(hub.Requests[0].Id, AgentDecision.Reply("secret plans")));
                await relay.ReplyAsync(Within);
                Assert.True(await Eventually(() => File.Exists(log) && File.ReadAllText(log).Contains("replied")));
            }
            var text = File.ReadAllText(log);
            Assert.Contains("Decision s1 - replied", text);
            Assert.DoesNotContain("secret plans", text);
            Assert.DoesNotContain("Red or blue", text);
        }
        finally
        {
            File.Delete(log);
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Services.Tests --filter "FullyQualifiedName~AgentHubReplyTests"`
Expected: build FAILS with "'AgentHub' does not contain a definition for 'HoldFor'".

- [ ] **Step 4: Teach the hub to hold a finished turn**

In `src/Faqra.Services/Agents/AgentHub.cs`:

1. Add after the `CanAsk` property:

```csharp
    /// <summary>
    /// How long to hold a finished turn open for the owner's reply, or null to let Claude stop at once. Asked on the
    /// context's thread for each Stop, with the turn's words already on the board and before <see cref="TurnFinished"/>,
    /// so "the island is open on it" means the owner was already looking. Nothing is held until the island sets it.
    /// </summary>
    public Func<AgentSession, TimeSpan?> HoldFor { get; set; } = _ => null;
```

2. Add after `MarkRead`:

```csharp
    /// <summary>Holds (or stops holding) every finished turn of a session for the owner's reply. Call on the context's thread.</summary>
    public void SetRepliesOn(string sessionId, bool on)
    {
        var next = Board.SetRepliesOn(sessionId, on);
        if (!ReferenceEquals(next, Board))
        {
            Board = next;
            Changed?.Invoke();
        }
    }
```

3. In `Serve`, add after the `PermissionRequest` branch:

```csharp
            if (e.Event == "Stop")
            {
                await ServeStop(connection, e, title, token).ConfigureAwait(false);
                return;
            }
```

and in the `Post` lambda that follows, replace

```csharp
                Changed?.Invoke();
                if (e.Event == "Stop" && Board.Sessions.TryGetValue(e.SessionId, out var session) && session.Unread)
                {
                    TurnFinished?.Invoke(session);
                }
```

with `Changed?.Invoke();` (a Stop now goes through `ServeStop`).

4. In `ServeRequest`, replace everything from `AgentDecision? decision;` to the end of the method with:

```csharp
        await WaitAndAnswer(connection, id, decided, DecisionWait, token).ConfigureAwait(false);
```

and add these methods after `ServeRequest`:

```csharp
    /// <summary>
    /// Waits for the owner's choice on a held connection, the relay hanging up (Claude Code ended the hook), the wait
    /// running out, or a stop. Writes only a choice; anything else lets the request go and closes without a word.
    /// </summary>
    private async Task WaitAndAnswer(NamedPipeServerStream connection, string id, TaskCompletionSource<AgentDecision?> decided, TimeSpan wait, CancellationToken token)
    {
        AgentDecision? decision;
        using (var waiting = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            var hangUp = HangUp(connection, waiting.Token);
            var timeout = Task.Delay(wait, waiting.Token);
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
            // The relay left between the click and the write: Claude Code carries on as if nobody had answered.
        }
    }

    /// <summary>
    /// A finished turn. Faqra answers at once: it hangs up (Claude stops as usual) or sends the hold line and keeps the
    /// turn open until the owner replies, presses Done, the window runs out, the turn moves on, the relay hangs up, or
    /// Faqra stops.
    /// </summary>
    private async Task ServeStop(NamedPipeServerStream connection, AgentEvent e, string? title, CancellationToken token)
    {
        var id = Guid.NewGuid().ToString("N");
        var decided = new TaskCompletionSource<AgentDecision?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = new TaskCompletionSource<TimeSpan?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() => Finish(id, e, title, decided, held, token));
        TimeSpan? window;
        try
        {
            window = await held.Task.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (window is not { } length)
        {
            return;
        }
        try
        {
            await connection.WriteAsync(Encoding.UTF8.GetBytes(StopReply.HoldLine + "\n")).ConfigureAwait(false);
            await connection.FlushAsync().ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The relay already gave up waiting for the hold: the turn ends as it would have.
            Post(() => Settle(id, null));
            return;
        }
        await WaitAndAnswer(connection, id, decided, length, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Folds a finished turn into the board and, when <see cref="HoldFor"/> says so, opens a reply window on it. Always
    /// completes <paramref name="held"/> with the window's length, or null to let Claude stop at once; if anything throws
    /// (the island's delegate or a handler) the turn is let go. Runs on the context's thread.
    /// </summary>
    private void Finish(string id, AgentEvent e, string? title, TaskCompletionSource<AgentDecision?> decided, TaskCompletionSource<TimeSpan?> held, CancellationToken token)
    {
        TimeSpan? window = null;
        try
        {
            if (token.IsCancellationRequested)
            {
                return;
            }
            var now = _now();
            Board = Fold(e, title, now);
            Log(e);
            ReleaseSession(e.SessionId);
            if (Board.Sessions.TryGetValue(e.SessionId, out var finished))
            {
                window = HoldFor(finished);
                if (window is { } length)
                {
                    Requests = Requests.Add(AgentRequest.ForReply(id, finished.Id, now, now + length));
                    _waiting[id] = decided;
                    Board = Board.AwaitingReply(finished.Id, now);
                }
            }
            Changed?.Invoke();
            // A held turn always tells the island, even with no words to read: Claude is waiting on the owner.
            if (Board.Sessions.TryGetValue(e.SessionId, out var session) && (session.Unread || window is not null))
            {
                TurnFinished?.Invoke(session);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Faqra agents stop: {ex.GetType().Name}: {ex.Message}");
            window = null;
            try
            {
                Settle(id, null);
            }
            catch (Exception inner)
            {
                Trace.TraceWarning($"Faqra agents stop: {inner.GetType().Name}: {inner.Message}");
            }
        }
        finally
        {
            held.TrySetResult(window);
        }
    }
```

5. In `Settle`, replace

```csharp
        if (decision is not null && request is not null)
        {
            Board = Board.Answered(request.SessionId, allowed: decision.Kind != AgentDecisionKind.Deny, _now());
        }
```

with

```csharp
        if (request is not null)
        {
            Board = (request.Kind, decision) switch
            {
                (AgentRequestKind.Reply, { Kind: AgentDecisionKind.Reply, Message: { } words }) => Board.Replied(request.SessionId, words, _now()),
                (AgentRequestKind.Reply, _) => Board.ReplyClosed(request.SessionId),
                (_, { } chosen) => Board.Answered(request.SessionId, allowed: chosen.Kind != AgentDecisionKind.Deny, _now()),
                _ => Board,
            };
        }
```

6. In `LogDecision`, add `AgentDecisionKind.Reply => "replied",` before the `_ => "released"` arm, and replace the `WriteLog(...)` line with:

```csharp
        var tool = request.ToolName.Length > 0 ? request.ToolName : "-";
        WriteLog($"{_now():O} Decision {Short(request.SessionId)} {tool} {outcome}");
```

7. Update the class summary's first sentence to: `Listens on the agents pipe and folds every event into <see cref="Board"/>. A permission request, or a finished turn held for the owner's reply, keeps its connection open while a card shows it, and the owner's choice goes back on that same connection.`

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.Services.Tests`
Expected: PASS (every Services test, A2's hub tests included). Then run the Services tests five times with two processors (`for i in 1 2 3 4 5; do DOTNET_PROCESSOR_COUNT=2 dotnet test tests/Faqra.Services.Tests --nologo -v q 2>&1 | grep -E "Passed!|Failed!|\[FAIL\]"; done` in Git Bash); all five must pass.

- [ ] **Step 6: Commit**

```bash
git add src/Faqra.Services/Agents/AgentHub.cs tests/Faqra.Services.Tests/Agents
git commit -m "feat(windows): agent hub holds a finished turn for the owner's reply"
```

---

### Task 6: The reply window setting

**Files:**
- Modify: `src/Faqra.Core/Defaults/DefaultsKey.Stage1.cs`, `src/Faqra.Core/Defaults/RegisteredDefaults.Stage1.cs`
- Modify: `src/Faqra.Core/Localization/AgentsStrings.cs`, `AgentsStrings.EnUS.cs`
- Modify: `src/Faqra.App/Settings/Pages/AgentsPage.cs`
- Test: `tests/Faqra.Core.Tests/ShortcutRoleTests.cs` (add, next to A2's `TheAgentSwitchesStartOn`), `tests/Faqra.App.Tests/AgentSettingsRenderTests.cs` (add)

**Interfaces:**
- Produces: `DefaultsKey.FaqraAgentsReplyWindowSeconds = "faqraAgentsReplyWindowSeconds"` (registered default 300); `AgentsPage.ReplyWindowChoices = [60, 120, 300, 480]` (internal static); strings `RepliesSection`, `ReplyWindow`, `ReplyWindowCaption`, `OneMinute`, `MinutesFormat`.

- [ ] **Step 1: Write the failing tests**

Add to `tests/Faqra.Core.Tests/ShortcutRoleTests.cs` (inside the class):

```csharp
    [Fact]
    public void TheReplyWindowStartsAtFiveMinutes() =>
        Assert.Equal(300, DefaultsStore.InMemory().Int(DefaultsKey.FaqraAgentsReplyWindowSeconds));
```

Add to `tests/Faqra.App.Tests/AgentSettingsRenderTests.cs` (inside the class):

```csharp
    [Fact]
    public void TheReplyWindowIsTheOwnersChoice() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        var dir = Directory.CreateTempSubdirectory("faqra-page-").FullName;
        try
        {
            var page = new AgentsPage(Path.Combine(dir, "settings.json"), Path.Combine(dir, "faqra-hook.exe"));
            Assert.Contains(S.RepliesSection, AgentsRenderTests.AllText(page));
            var box = FindAll<System.Windows.Controls.ComboBox>(page).Single(b => AutomationProperties.GetName(b) == S.ReplyWindow);
            Assert.Equal(["1 minute", "2 minutes", "5 minutes", "8 minutes"], box.Items.Cast<string>());
            Assert.Equal("5 minutes", box.SelectedItem);

            box.SelectedIndex = 0;
            Assert.Equal(60, services.Store.Int(Faqra.Core.Defaults.DefaultsKey.FaqraAgentsReplyWindowSeconds));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    });

    private static List<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        var found = new List<T>();
        void Walk(DependencyObject node)
        {
            if (node is T match)
            {
                found.Add(match);
            }
            // A CardControl's Content is a logical child; its Header is not until the template applies.
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            {
                Walk(child);
            }
        }
        Walk(root);
        return found;
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~ShortcutRoleTests"`
Expected: build FAILS with "'DefaultsKey' does not contain a definition for 'FaqraAgentsReplyWindowSeconds'".

- [ ] **Step 3: Add the key, its default and the strings**

In `src/Faqra.Core/Defaults/DefaultsKey.Stage1.cs`, add after `FaqraAgentsPreviousShortcut`:

```csharp
    public const string FaqraAgentsReplyWindowSeconds = "faqraAgentsReplyWindowSeconds";
```

In `src/Faqra.Core/Defaults/RegisteredDefaults.Stage1.cs`, add after the `FaqraAgentsPreviousShortcut` entry:

```csharp
        [DefaultsKey.FaqraAgentsReplyWindowSeconds] = 300L,  // 5 minutes; the island offers 1, 2, 5 and 8
```

In `src/Faqra.Core/Localization/AgentsStrings.cs`, add after `WatchOnly`:

```csharp
    public required string RepliesSection { get; init; }
    public required string ReplyWindow { get; init; }
    public required string ReplyWindowCaption { get; init; }
    public required string OneMinute { get; init; }
    public required string MinutesFormat { get; init; }
```

In `src/Faqra.Core/Localization/AgentsStrings.EnUS.cs`, add after the `WatchOnly = ...` line:

```csharp
        RepliesSection = "Replies",
        ReplyWindow = "How long Claude waits for your reply",
        ReplyWindowCaption = "When Claude asks you something, when you're looking at its session as it finishes, or when you switched replies on for it, Claude waits this long for an answer from the island.",
        OneMinute = "1 minute",
        MinutesFormat = "{0} minutes",
```

- [ ] **Step 4: Add the setting to the Agents page**

In `src/Faqra.App/Settings/Pages/AgentsPage.cs`, add this field after `_page`:

```csharp
    /// <summary>The reply window's choices, in seconds; each ends well inside Claude Code's 600 s Stop hook timeout.</summary>
    internal static readonly int[] ReplyWindowChoices = [60, 120, 300, 480];
```

add at the end of `Build` (after the Alerts toggles):

```csharp
        _page.Children.Add(Text(_s.RepliesSection, "SectionHeader"));
        _page.Children.Add(ReplyWindowCard());
```

and add this method before `Toggle`:

```csharp
    private CardControl ReplyWindowCard()
    {
        var store = AppServices.Current.Store;
        var box = new ComboBox { MinWidth = 160 };
        foreach (var seconds in ReplyWindowChoices)
        {
            box.Items.Add(seconds == 60 ? _s.OneMinute : Format(_s.MinutesFormat, seconds / 60));
        }
        box.SelectedIndex = Array.IndexOf(ReplyWindowChoices, store.Int(DefaultsKey.FaqraAgentsReplyWindowSeconds));
        System.Windows.Automation.AutomationProperties.SetName(box, _s.ReplyWindow);
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex >= 0)
            {
                store.Set(DefaultsKey.FaqraAgentsReplyWindowSeconds, ReplyWindowChoices[box.SelectedIndex]);
            }
        };
        var header = new StackPanel();
        header.Children.Add(Text(_s.ReplyWindow, "Body"));
        var note = Text(_s.ReplyWindowCaption, "Caption");
        note.TextWrapping = TextWrapping.Wrap;
        header.Children.Add(note);
        return new CardControl { Icon = new SymbolIcon(SymbolRegular.Clock24), Header = header, Content = box, Margin = new Thickness(0) };
    }
```

(If `ComboBox` is ambiguous between `System.Windows.Controls` and `Wpf.Ui.Controls` in this file, qualify it as `System.Windows.Controls.ComboBox`, as EnergyPage's usage resolves.)

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~ShortcutRoleTests"` then `dotnet test tests/Faqra.App.Tests --filter "FullyQualifiedName~AgentSettingsRenderTests|FullyQualifiedName~AgentsRenderTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Faqra.Core src/Faqra.App/Settings/Pages/AgentsPage.cs tests
git commit -m "feat(windows): owner's reply window length for held turns"
```

---
### Task 7: The answered card shows styled words and takes a reply

**Files:**
- Create: `src/Faqra.App/Island/Modules/MarkdownView.cs`
- Modify: `src/Faqra.App/Island/Modules/AgentCards.cs` (`Scrolling` takes any element; `AnsweredCardActions`; `AnsweredCard` rewritten)
- Modify: `src/Faqra.App/Island/Modules/AgentsModule.cs` (reply cards, the replies switch, the countdown tick, plain-text previews)
- Modify: `src/Faqra.Core/Localization/AgentsStrings.cs`, `AgentsStrings.EnUS.cs`
- Test: `tests/Faqra.App.Tests/MarkdownViewTests.cs` (new), `tests/Faqra.App.Tests/AgentCardRenderTests.cs` (add), `tests/Faqra.App.Tests/AgentsRenderTests.cs` (`AllText` reads styled text)

**Interfaces:**
- Consumes: `MarkdownLite`, `MdBlock`, `MdInline` (Task 1); `AgentDecision.Reply`, `StopReply.MaxReplyLength`, `AgentRequestKind.Reply`, `AgentRequest.ClosesAt`, `AgentRequest.ForReply` (Task 2); `AgentBoard.MaxConsecutiveReplies`, `AgentBoard.AwaitingReply`, `AgentSession.ConsecutiveReplies`, `AgentSession.RepliesOn` (Task 3); `AgentHub.SetRepliesOn`, `AgentHub.Answer`, `AgentHub.Release`, `AgentHub.MarkRead` (Task 5 and A2).
- Produces: `internal static class MarkdownView` with `StackPanel Build(string? markdown)` and `Uri? WebLink(string? url)`; `internal sealed record AnsweredCardActions(Action Close, Action<string> Reply, Action WantKeyboard)`; `AnsweredCard(AgentSession session, AgentRequest? window, AgentsStrings s, AnsweredCardActions actions, DateTimeOffset now)` with `ReplyBox` (`Wpf.Ui.Controls.TextBox?`), `SendButton` (`Button?`), `Countdown` (string), `void Tick(DateTimeOffset now)`, `static bool SendsOnEnter(Key key, ModifierKeys modifiers)`; strings `ReplyPlaceholder`, `ReplyDone`, `ReplyWaitsFormat`, `ReplyWindowOver`, `ReplyCapReachedFormat`, `RepliesOnToggle`. `AgentsRenderTests.Words(TextBlock)` for tests.

A `TextBlock` built from runs reports an empty `Text` (checked in WPF on 2026-10-10), so tests read styled text through `AgentsRenderTests.Words`, which walks the runs.

- [ ] **Step 1: Teach the test helper to read styled text**

In `tests/Faqra.App.Tests/AgentsRenderTests.cs`, in `AllText`, replace `builder.Append(block.Text).Append('\n');` with `builder.Append(Words(block)).Append('\n');`, and add after `AllText`:

```csharp
    /// <summary>A text block's words, whether set as Text or built from runs (whose TextBlock.Text stays empty).</summary>
    internal static string Words(System.Windows.Controls.TextBlock block) =>
        string.Concat(block.Inlines.Select(InlineWords));

    private static string InlineWords(System.Windows.Documents.Inline inline) => inline switch
    {
        System.Windows.Documents.Run run => run.Text,
        System.Windows.Documents.LineBreak => "\n",
        System.Windows.Documents.Span span => string.Concat(span.Inlines.Select(InlineWords)),
        _ => string.Empty,
    };
```

Run: `dotnet test tests/Faqra.App.Tests --filter "FullyQualifiedName~AgentsRenderTests|FullyQualifiedName~AgentCardRenderTests"`
Expected: PASS (a text block given `Text` holds it as one run, so nothing changes yet).

- [ ] **Step 2: Write the failing tests**

`tests/Faqra.App.Tests/MarkdownViewTests.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Faqra.App.Island.Modules;

namespace Faqra.App.Tests;

public class MarkdownViewTests
{
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

    [Fact]
    public void ClaudesMarkdownBecomesStyledText() => StaThread.Run(() =>
    {
        var view = MarkdownView.Build("# Fixed\n\nRun **all** tests with `dotnet test`.\n\n- one\n  - two\n\n```\nx = 1\n```");
        AgentsRenderTests.RenderInk(new Border { Background = IslandPalette.Surface, Child = view, Width = 380 }, "island-markdown", 380, 220);

        var blocks = All<TextBlock>(view).ToList();
        Assert.Equal(["Fixed", "Run all tests with dotnet test.", "•", "one", "•", "two", "x = 1"], blocks.Select(AgentsRenderTests.Words));
        Assert.Equal(FontWeights.SemiBold, blocks[0].FontWeight);
        var runs = All<Run>(view).ToList();
        Assert.Equal(FontWeights.SemiBold, runs.Single(run => run.Text == "all").FontWeight);
        Assert.Equal(AgentCardParts.MonoFont, runs.Single(run => run.Text == "dotnet test").FontFamily);
        Assert.Equal(AgentCardParts.MonoFont, blocks[^1].FontFamily);
        var items = All<Grid>(view).ToList();
        Assert.True(items[1].Margin.Left > items[0].Margin.Left, "the nested item is indented");
    });

    [Fact]
    public void OnlyWebLinksCanBeClicked() => StaThread.Run(() =>
    {
        var view = MarkdownView.Build("See [the docs](https://example.com/a) or [this](file:///C:/Windows/notepad.exe).");
        var link = Assert.Single(All<Hyperlink>(view));
        Assert.Equal(new Uri("https://example.com/a"), link.NavigateUri);
        Assert.Equal("See the docs or this.", AgentsRenderTests.Words(All<TextBlock>(view).Single()));
        Assert.Null(MarkdownView.WebLink("javascript:alert(1)"));
        Assert.Null(MarkdownView.WebLink("C:\\Windows\\notepad.exe"));
        Assert.NotNull(MarkdownView.WebLink("http://localhost:3000/"));
    });

    [Fact]
    public void NothingIsAnEmptyView() => StaThread.Run(() =>
    {
        Assert.Empty(MarkdownView.Build(null).Children);
        Assert.Empty(MarkdownView.Build("  \n").Children);
    });
}
```

Add to `tests/Faqra.App.Tests/AgentCardRenderTests.cs`, inside the class:

```csharp
    private const string AskedLine = "{\"hook_event_name\":\"Stop\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\site\",\"last_assistant_message\":\"Red or **blue**?\"}";

    private static AgentSession Answered(int replies = 0) =>
        new AgentSession("s1", "claude", @"C:\code\site", AgentState.AwaitingReply, [], "Red or **blue**?", 0, T0, T0, T0)
        {
            Unread = true,
            ConsecutiveReplies = replies,
        };

    private static AnsweredCard HeldCard(Action<string>? reply = null, Action? close = null, Action? keyboard = null) =>
        new(Answered(), AgentRequest.ForReply("r3", "s1", T0, T0.AddMinutes(5)), S,
            new AnsweredCardActions(close ?? (() => { }), reply ?? (_ => { }), keyboard ?? (() => { })), T0);

    [Fact]
    public void AHeldTurnShowsTheReplyBoxAndCountsDown() => StaThread.Run(() =>
    {
        var card = HeldCard();
        Render(card, "island-card-reply", 330);
        Assert.Contains("Red or blue?", AgentsRenderTests.AllText(card));
        Assert.Equal("Claude waits 5:00 for your reply", card.Countdown);
        Assert.False(card.SendButton!.IsEnabled);

        card.ReplyBox!.Text = "Blue";
        Assert.True(card.SendButton.IsEnabled);
        card.Tick(T0.AddSeconds(61));
        Assert.Equal("Claude waits 3:59 for your reply", card.Countdown);
        card.Tick(T0.AddMinutes(5));
        Assert.Equal(S.ReplyWindowOver, card.Countdown);
        Assert.False(card.SendButton.IsEnabled);
    });

    [Fact]
    public void SendGivesTheReplyAndDoneClosesTheCard() => StaThread.Run(() =>
    {
        string? sent = null;
        var closed = 0;
        var card = HeldCard(reply: text => sent = text, close: () => closed++);
        card.ReplyBox!.Text = "  Blue, please\nand a test  ";
        Click(card.SendButton!);
        Assert.Equal("Blue, please\nand a test", sent);

        Click(ButtonNamed(card, S.ReplyDone));
        Assert.Equal(1, closed);
    });

    [Fact]
    public void EnterSendsAndShiftEnterStartsALine()
    {
        Assert.True(AnsweredCard.SendsOnEnter(Key.Enter, ModifierKeys.None));
        Assert.True(AnsweredCard.SendsOnEnter(Key.Enter, ModifierKeys.Control));
        Assert.False(AnsweredCard.SendsOnEnter(Key.Enter, ModifierKeys.Shift));
        Assert.False(AnsweredCard.SendsOnEnter(Key.A, ModifierKeys.None));
    }

    [Fact]
    public void TheReplyBoxStopsAtTheLimit() => StaThread.Run(() =>
    {
        var box = HeldCard().ReplyBox!;
        Assert.Equal(StopReply.MaxReplyLength, box.MaxLength);
        Assert.True(box.AcceptsReturn);
        Assert.Equal(S.ReplyPlaceholder, System.Windows.Automation.AutomationProperties.GetName(box));
    });

    [Fact]
    public void ClickingIntoTheReplyBoxAsksForTheKeyboard() => StaThread.Run(() =>
    {
        var asked = 0;
        var card = HeldCard(keyboard: () => asked++);
        card.ReplyBox!.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent });
        Assert.Equal(1, asked);
    });

    [Fact]
    public void ATurnNotHeldHasNoReplyBox() => StaThread.Run(() =>
    {
        var closed = 0;
        var card = new AnsweredCard(Answered(), null, S, new AnsweredCardActions(() => closed++, _ => { }, () => { }), T0);
        Assert.Null(card.ReplyBox);
        Assert.Equal(string.Empty, card.Countdown);
        Click(ButtonNamed(card, S.Dismiss));
        Assert.Equal(1, closed);
    });

    [Fact]
    public void AtTheCapTheCardExplains() => StaThread.Run(() =>
    {
        var card = new AnsweredCard(Answered(replies: AgentBoard.MaxConsecutiveReplies), null, S, new AnsweredCardActions(() => { }, _ => { }, () => { }), T0);
        Assert.Contains("Claude Code stops after 8 replies in a row from the island", AgentsRenderTests.AllText(card));
        Assert.DoesNotContain("8 replies", AgentsRenderTests.AllText(new AnsweredCard(Answered(), null, S, new AnsweredCardActions(() => { }, _ => { }, () => { }), T0)));
    });

    [Fact]
    public void TheReplyBoxKeepsItsTextWhileEventsArrive() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(AskedLine, WorkingLine);
        var now = DateTimeOffset.Now;
        hub.ReplaceBoardForTests(hub.Board.AwaitingReply("s1", now));
        hub.ReplaceRequestsForTests(AgentRequest.ForReply("r3", "s1", now, now.AddMinutes(5)));
        var module = new AgentsModule(hub, () => true);
        module.FocusSession("s1");
        Render(module, "island-agents-reply", 520);
        var card = All<AnsweredCard>(module).Single();
        card.ReplyBox!.Text = "Blue, with a darker";

        hub.ReplaceBoardForTests(hub.Board.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"PostToolUse\",\"session_id\":\"s2\",\"tool_name\":\"Read\"}")!, T0));
        hub.ReplaceBoardForTests(hub.Board.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s2\",\"tool_name\":\"Grep\",\"tool_input\":{\"pattern\":\"x\"}}")!, T0));
        card.Tick(now.AddSeconds(30));

        var after = All<AnsweredCard>(module).Single();
        Assert.Same(card, after);
        Assert.Equal("Blue, with a darker", after.ReplyBox!.Text);
        // Claude's words show once, styled on the card, not again under "Claude said".
        Assert.DoesNotContain(S.LastMessageHeader, AgentsRenderTests.AllText(module));
    });

    [Fact]
    public void TheRepliesSwitchIsPerSession() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(WorkingLine);
        var module = new AgentsModule(hub, () => true);
        var toggle = All<CheckBox>(module).Single(box => System.Windows.Automation.AutomationProperties.GetName(box) == S.RepliesOnToggle);
        Assert.False(toggle.IsChecked);

        toggle.IsChecked = true;
        toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.True(hub.Board.Sessions["s2"].RepliesOn);
        Assert.True(All<CheckBox>(module).Single(box => System.Windows.Automation.AutomationProperties.GetName(box) == S.RepliesOnToggle).IsChecked);
    });

    [Fact]
    public void ClaudeSaidShowsTheWordsWithoutTheirMarks() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith("{\"hook_event_name\":\"Stop\",\"session_id\":\"s1\",\"last_assistant_message\":\"## Done\\n\\nThe **tests** pass.\"}");
        hub.MarkRead("s1");
        var module = new AgentsModule(hub, () => true);
        var text = AgentsRenderTests.AllText(module);
        Assert.Contains(S.LastMessageHeader, text);
        Assert.Contains("Done\nThe tests pass.", text);
        Assert.DoesNotContain("**", text);
    });
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.App.Tests --filter "FullyQualifiedName~MarkdownViewTests|FullyQualifiedName~AgentCardRenderTests"`
Expected: build FAILS with "The name 'MarkdownView' does not exist in the current context".

- [ ] **Step 4: Add the strings**

In `src/Faqra.Core/Localization/AgentsStrings.cs`, add after `public required string Dismiss { get; init; }`:

```csharp
    public required string ReplyPlaceholder { get; init; }
    public required string ReplyDone { get; init; }
    public required string ReplyWaitsFormat { get; init; }
    public required string ReplyWindowOver { get; init; }
    public required string ReplyCapReachedFormat { get; init; }
    public required string RepliesOnToggle { get; init; }
```

In `src/Faqra.Core/Localization/AgentsStrings.EnUS.cs`, add after `Dismiss = "Dismiss",`:

```csharp
        ReplyPlaceholder = "Reply to Claude",
        ReplyDone = "Done",
        ReplyWaitsFormat = "Claude waits {0} for your reply",
        ReplyWindowOver = "Claude stopped waiting for a reply",
        ReplyCapReachedFormat = "Claude Code stops after {0} replies in a row from the island. Reply in Claude Code to carry on.",
        RepliesOnToggle = "Wait for my reply after each turn",
```

- [ ] **Step 5: Write `MarkdownView`**

`src/Faqra.App/Island/Modules/MarkdownView.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Faqra.Core.Agents;

namespace Faqra.App.Island.Modules;

/// <summary>Claude's Markdown as island text: headings, paragraphs, lists, quotes, code blocks and rules, with styled runs.</summary>
internal static class MarkdownView
{
    private const double ListIndent = 14;
    private const double BlockGap = 8;
    private const double ItemGap = 2;

    public static StackPanel Build(string? markdown)
    {
        var panel = new StackPanel();
        var blocks = MarkdownLite.Parse(markdown);
        for (var i = 0; i < blocks.Count; i++)
        {
            var element = Block(blocks[i]);
            if (i > 0)
            {
                var gap = IsItem(blocks[i - 1]) && IsItem(blocks[i]) ? ItemGap : BlockGap;
                element.Margin = new Thickness(element.Margin.Left, gap, 0, 0);
            }
            panel.Children.Add(element);
        }
        return panel;
    }

    /// <summary>Only web links open: a click never runs a file or a script Claude named.</summary>
    internal static Uri? WebLink(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? uri : null;

    private static bool IsItem(MdBlock block) => block.Kind is MdBlockKind.Bullet or MdBlockKind.Numbered;

    private static FrameworkElement Block(MdBlock block) => block.Kind switch
    {
        MdBlockKind.Heading => Text(block.Inlines, block.Level == 1 ? 14 : 13, FontWeights.SemiBold),
        MdBlockKind.Bullet => Item("•", block),
        MdBlockKind.Numbered => Item(block.Marker, block),
        MdBlockKind.Quote => new Border
        {
            BorderBrush = IslandPalette.FillStrong,
            BorderThickness = new Thickness(2, 0, 0, 0),
            Padding = new Thickness(8, 0, 0, 0),
            Child = Text(block.Inlines, 12, FontWeights.Normal, IslandPalette.Secondary),
        },
        MdBlockKind.Code => new Border
        {
            Background = IslandPalette.FillStrong,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6, 8, 6),
            Child = new TextBlock
            {
                Text = block.Code,
                FontFamily = AgentCardParts.MonoFont,
                FontSize = 11.5,
                Foreground = IslandPalette.Primary,
                TextWrapping = TextWrapping.Wrap,
            },
        },
        MdBlockKind.Rule => new Border { Height = 1, Background = IslandPalette.FillStrong },
        _ => Text(block.Inlines, 12, FontWeights.Normal),
    };

    private static Grid Item(string marker, MdBlock block)
    {
        var grid = new Grid { Margin = new Thickness(block.Level * ListIndent, 0, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var mark = new TextBlock
        {
            Text = marker,
            FontFamily = AgentCardParts.TextFont,
            FontSize = 12,
            Foreground = IslandPalette.Secondary,
            MinWidth = ListIndent,
            Margin = new Thickness(0, 0, 6, 0),
        };
        var text = Text(block.Inlines, 12, FontWeights.Normal);
        Grid.SetColumn(text, 1);
        grid.Children.Add(mark);
        grid.Children.Add(text);
        return grid;
    }

    private static TextBlock Text(IReadOnlyList<MdInline> inlines, double size, FontWeight weight, Brush? ink = null)
    {
        var text = new TextBlock
        {
            FontFamily = AgentCardParts.TextFont,
            FontSize = size,
            FontWeight = weight,
            Foreground = ink ?? IslandPalette.Primary,
            TextWrapping = TextWrapping.Wrap,
        };
        foreach (var inline in inlines)
        {
            text.Inlines.Add(Run(inline));
        }
        return text;
    }

    private static Inline Run(MdInline inline) => inline.Style switch
    {
        MdStyle.Bold => new Run(inline.Text) { FontWeight = FontWeights.SemiBold },
        MdStyle.Italic => new Run(inline.Text) { FontStyle = FontStyles.Italic },
        MdStyle.Code => new Run(inline.Text) { FontFamily = AgentCardParts.MonoFont, Background = IslandPalette.FillStrong },
        MdStyle.Link when WebLink(inline.Url) is { } uri => Link(inline.Text, uri),
        _ => new Run(inline.Text),
    };

    private static Hyperlink Link(string text, Uri uri)
    {
        var link = new Hyperlink(new Run(text)) { NavigateUri = uri, Foreground = IslandPalette.Accent, ToolTip = uri.AbsoluteUri };
        link.RequestNavigate += (_, e) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                // No browser answered; the link stays readable on the card.
                Trace.TraceWarning($"Faqra agents link: {ex.GetType().Name}: {ex.Message}");
            }
            e.Handled = true;
        };
        return link;
    }
}
```

- [ ] **Step 6: Rewrite the answered card**

In `src/Faqra.App/Island/Modules/AgentCards.cs`:

1. Replace `internal static ScrollViewer Scrolling(TextBlock text, double maxHeight) => new()` and its `Content = text,` line with `internal static ScrollViewer Scrolling(UIElement content, double maxHeight) => new()` and `Content = content,`.

2. Replace the whole `AnsweredCard` class (from its `/// <summary>Claude finished a turn` comment to the end of the file) with:

```csharp
/// <summary>What the answered card's buttons do: close it (Dismiss, or Done on a held turn), send a reply, take the keyboard.</summary>
internal sealed record AnsweredCardActions(Action Close, Action<string> Reply, Action WantKeyboard);

/// <summary>
/// Claude finished a turn: what it said, styled from its Markdown. A turn held for the owner's reply adds a countdown, a
/// reply box, Done and Send; a turn not held has Dismiss, and says so when Claude Code's replies-in-a-row cap is reached.
/// </summary>
internal sealed class AnsweredCard : ContentControl
{
    private readonly AgentRequest? _window;
    private readonly AgentsStrings _s;
    private readonly AnsweredCardActions _actions;
    private readonly TextBlock? _countdown;
    private bool _over;

    public AnsweredCard(AgentSession session, AgentRequest? window, AgentsStrings s, AnsweredCardActions actions, DateTimeOffset now)
    {
        _window = window;
        _s = s;
        _actions = actions;
        var stack = new StackPanel();
        stack.Children.Add(AgentCardParts.Title(AgentCardParts.Format(s.CardTitleFormat, session.Name, s.AnsweredTitle)));
        if (session.LastMessage is { Length: > 0 } message)
        {
            stack.Children.Add(AgentCardParts.Scrolling(MarkdownView.Build(message), AgentCardParts.MessageMaxHeight));
        }
        if (window is null)
        {
            if (session.ConsecutiveReplies >= AgentBoard.MaxConsecutiveReplies)
            {
                var cap = AgentCardParts.Caption(AgentCardParts.Format(s.ReplyCapReachedFormat, AgentBoard.MaxConsecutiveReplies));
                cap.Margin = new Thickness(0, 8, 0, 0);
                stack.Children.Add(cap);
            }
            stack.Children.Add(AgentCardParts.Footer(AgentCardParts.Actions(AgentCardParts.Action(s.Dismiss, primary: false, (_, _) => actions.Close())), quiet: null));
            Content = AgentCardParts.Panel(stack);
            return;
        }

        var countdown = AgentCardParts.Caption(string.Empty);
        countdown.Foreground = IslandPalette.Tertiary;
        countdown.Margin = new Thickness(0, 10, 0, 0);
        stack.Children.Add(countdown);
        _countdown = countdown;

        var box = new Wpf.Ui.Controls.TextBox
        {
            PlaceholderText = s.ReplyPlaceholder,
            FontSize = 12,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxLength = StopReply.MaxReplyLength,
            MaxHeight = AgentCardParts.DetailMaxHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 6, 0, 0),
        };
        System.Windows.Automation.AutomationProperties.SetName(box, s.ReplyPlaceholder);
        // The island never takes the keyboard by itself; clicking into the box is the owner asking for it.
        box.AddHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler((_, _) => actions.WantKeyboard()), handledEventsToo: true);
        box.TextChanged += (_, _) => UpdateSend();
        box.PreviewKeyDown += (_, e) =>
        {
            if (SendsOnEnter(e.Key, Keyboard.Modifiers))
            {
                e.Handled = true;
                Send();
            }
        };
        stack.Children.Add(box);
        ReplyBox = box;

        var send = AgentCardParts.Action(s.Send, primary: true, (_, _) => Send());
        SendButton = send;
        var done = AgentCardParts.Action(s.ReplyDone, primary: false, (_, _) => actions.Close());
        stack.Children.Add(AgentCardParts.Footer(AgentCardParts.Actions(done, send), quiet: null));
        Content = AgentCardParts.Panel(stack);
        Tick(now);
    }

    internal Wpf.Ui.Controls.TextBox? ReplyBox { get; }

    internal Button? SendButton { get; }

    internal string Countdown => _countdown?.Text ?? string.Empty;

    /// <summary>Enter sends; Shift+Enter starts a new line.</summary>
    internal static bool SendsOnEnter(Key key, ModifierKeys modifiers) => key == Key.Enter && (modifiers & ModifierKeys.Shift) == 0;

    /// <summary>Updates the countdown. Once the window has passed, Send is off and the card says Claude stopped waiting.</summary>
    internal void Tick(DateTimeOffset now)
    {
        if (_countdown is null || _window?.ClosesAt is not { } closes)
        {
            return;
        }
        var left = closes - now;
        _over = left <= TimeSpan.Zero;
        _countdown.Text = _over ? _s.ReplyWindowOver : AgentCardParts.Format(_s.ReplyWaitsFormat, Clock(left));
        UpdateSend();
    }

    private static string Clock(TimeSpan left)
    {
        var seconds = (int)Math.Ceiling(left.TotalSeconds);
        return AgentCardParts.Format("{0}:{1:00}", seconds / 60, seconds % 60);
    }

    private void UpdateSend()
    {
        if (SendButton is not null && ReplyBox is not null)
        {
            SendButton.IsEnabled = !_over && ReplyBox.Text.Trim().Length > 0;
        }
    }

    private void Send()
    {
        if (SendButton?.IsEnabled == true && ReplyBox is not null)
        {
            _actions.Reply(ReplyBox.Text.Trim());
        }
    }
}
```

- [ ] **Step 7: Show held turns, the switch and the countdown in the module**

In `src/Faqra.App/Island/Modules/AgentsModule.cs`:

1. Add this field after `_clock`:

```csharp
    private readonly CheckBox _repliesOn = new() { Margin = new Thickness(0, 0, 0, 10) };
```

2. In the constructor, before `Render();`, add:

```csharp
        _repliesOn.Content = new TextBlock { Text = _s.RepliesOnToggle, FontFamily = TextFont, FontSize = 12, Foreground = IslandPalette.Secondary };
        System.Windows.Automation.AutomationProperties.SetName(_repliesOn, _s.RepliesOnToggle);
        // Click, not Checked: it fires only for the owner, never when a render sets the box from the board.
        _repliesOn.Click += (_, _) =>
        {
            if (FocusedSession is { } session)
            {
                _hub.SetRepliesOn(session.Id, _repliesOn.IsChecked == true);
            }
        };
```

3. Replace `RenderCard` with:

```csharp
    /// <summary>The focused session's card, rebuilt only when what it shows changes, so picks and typing survive.</summary>
    private void RenderCard(AgentSession session)
    {
        if (_hub.Requests.FirstOrDefault(r => r.SessionId == session.Id) is { } request)
        {
            ShowCard(request.Id, () => request.Kind switch
            {
                AgentRequestKind.Question => new QuestionCard(request, session.Name, _s, decision => _hub.Answer(request.Id, decision), () => _hub.Release(request.Id), () => KeyboardWanted?.Invoke()),
                AgentRequestKind.Reply => Answered(session, request),
                _ => new ApprovalCard(request, session.Name, _s, decision => _hub.Answer(request.Id, decision), () => _hub.Release(request.Id)),
            });
            return;
        }
        if (session.Unread && session.LastMessage is { Length: > 0 })
        {
            ShowCard($"{AnsweredKey}{session.Id}:{session.FinishedAt:O}", () => Answered(session, null));
            return;
        }
        ShowCard(null, null);
    }

    /// <summary>A finished turn's card; <paramref name="window"/> is the held turn, or null when Claude already stopped.</summary>
    private AnsweredCard Answered(AgentSession session, AgentRequest? window) => new(session, window, _s, new AnsweredCardActions(
        Close: () =>
        {
            // Read first, then let the turn go, so the card closes without passing through the card of a turn not held.
            _hub.MarkRead(session.Id);
            if (window is not null)
            {
                _hub.Release(window.Id);
            }
        },
        Reply: text =>
        {
            if (window is not null)
            {
                _hub.Answer(window.Id, AgentDecision.Reply(text));
            }
        },
        WantKeyboard: () => KeyboardWanted?.Invoke()),
        DateTimeOffset.Now);
```

4. In `RefreshTimes`, add at the end of the method: `(_card.Content as AnsweredCard)?.Tick(DateTimeOffset.Now);`.

5. In `RenderDetail`, add after `_detail.Children.Add(header);`:

```csharp
        _repliesOn.IsChecked = session.RepliesOn;
        _detail.Children.Add(_repliesOn);
```

and replace

```csharp
        // An answered card already shows Claude's words.
        var answeredOnCard = _cardKey?.StartsWith(AnsweredKey, StringComparison.Ordinal) == true;
        if (!answeredOnCard && session.LastMessage is { Length: > 0 } message)
        {
            _detail.Children.Add(Label(_s.LastMessageHeader));
            _detail.Children.Add(Paragraph(message));
        }
```

with

```csharp
        // An answered card (held or not) already shows Claude's words.
        if (_card.Content is not AnsweredCard && session.LastMessage is { Length: > 0 } message)
        {
            _detail.Children.Add(Label(_s.LastMessageHeader));
            _detail.Children.Add(Paragraph(MarkdownLite.PlainText(message)));
        }
```

6. Update the class summary's second sentence to: `Above the list sits the focused session's card (an approval, a question, or its latest answer with a reply box while Claude waits); below it, the session's folder, the replies switch, the owner's last prompt, Claude's last words and its activity.`

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.App.Tests`
Expected: PASS (every App test, A2's card tests included). Open `island-card-reply.png`, `island-agents-reply.png` and `island-markdown.png` in `%TEMP%\faqra-ui` (where `RenderInk` writes, unless `FAQRA_UI_SHOTS` names another folder) and check: the message is styled (bold, code in a darker box, the list indented), the countdown sits under it in grey, the reply box spans the card, Done and Send sit right with Send in the accent, and nothing is cut at the card's 412 px width.

- [ ] **Step 9: Commit**

```bash
git add src/Faqra.App/Island/Modules src/Faqra.Core/Localization tests/Faqra.App.Tests
git commit -m "feat(windows): answered card renders Claude's Markdown and takes a reply while Claude waits"
```

---

### Task 8: The island decides which turns wait

**Files:**
- Create: `src/Faqra.App/Island/IslandController.Agents.cs` (the agents part of the controller, moved out, plus `HoldFor`)
- Modify: `src/Faqra.App/Island/IslandController.cs`
- Test: `tests/Faqra.App.Tests/AgentsRenderTests.cs` (add)

**Interfaces:**
- Consumes: `ReplyRules.HoldFor` (Task 3); `AgentHub.HoldFor` (Task 5); `DefaultsKey.FaqraAgentsReplyWindowSeconds` (Task 6); `AgentsModule.FocusedSession` (A2).
- Produces: `IslandController` is `public sealed partial class`; the hub's `HoldFor` is the island's from construction to `Dispose`; a finished turn the hub holds opens the island on it even when "Open the island when an agent answers" is off (the hold only happens then because the owner switched replies on or was already looking).

- [ ] **Step 1: Write the failing test**

Add to `tests/Faqra.App.Tests/AgentsRenderTests.cs`, inside the class:

```csharp
    [Fact]
    public void TheIslandDecidesWhichTurnsWait() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(Core.Defaults.DefaultsStore.InMemory());
        Assert.IsType<Island.IslandController>(services.Agents.HoldFor.Target);

        // The island has not started (no window), so no card could show a reply box: nothing waits, even with the switch on.
        var at = new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);
        var session = new AgentSession("s1", "claude", @"C:\code\faqra", AgentState.Finished, [], "Red or blue?", 0, at, at, at)
        {
            Unread = true,
            RepliesOn = true,
        };
        Assert.Null(services.Agents.HoldFor(session));
    });
```

(`Island.` and `Core.Defaults.` resolve from the test namespace, as the file's other tests already write them.)

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Faqra.App.Tests --filter "FullyQualifiedName~TheIslandDecidesWhichTurnsWait"`
Expected: FAIL: `Assert.IsType() Failure` (the hub still has its default `HoldFor`).

- [ ] **Step 3: Move the agents part of the controller to its own file**

1. In `src/Faqra.App/Island/IslandController.cs`, change `public sealed class IslandController : IDisposable` to `public sealed partial class IslandController : IDisposable`.

2. Cut everything from the line `    // MARK: agents` down to (not including) the line `    // MARK: environment`, and create `src/Faqra.App/Island/IslandController.Agents.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Faqra.App.Agents;
using Faqra.Core.Agents;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Island;
using Faqra.Win32.Agents;
using Faqra.Win32.Windows;

namespace Faqra.App.Island;

/// <summary>The island's side of the agents: when cards may show, opening on a session, the shortcuts, and which turns wait for a reply.</summary>
public sealed partial class IslandController
{
    // (paste the cut members here, unchanged, then make the two edits below)
}
```

Paste the cut members in place of the comment line. Build once (`dotnet build src/Faqra.App`) and fix any missing `using` the compiler names by copying it from the top of `IslandController.cs`; remove any `using` from `IslandController.cs` that the build reports unused only if the project already treats that warning as an error (it does not today, so leave them).

3. In the moved code, replace `OnAgentFinished` with:

```csharp
    /// <summary>
    /// An agent finished a turn: chime and, if the owner wants it, open on its answer without taking the keyboard. A turn
    /// held for a reply always opens: it is held only because the owner switched replies on or was already looking.
    /// </summary>
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
        if (session.State == AgentState.AwaitingReply || _store.Bool(DefaultsKey.FaqraAgentsOpenOnAnswer))
        {
            OpenOnAgent(session.Id);
        }
    }
```

4. Add after `IsShowingAgents`:

```csharp
    /// <summary>
    /// How long the hub holds a finished turn for the owner's reply, or null to let Claude stop. The hub asks on the UI
    /// thread before the island hears of the answer, so "watching" means the owner was already on that session.
    /// </summary>
    private TimeSpan? HoldFor(AgentSession session) => ReplyRules.HoldFor(
        session,
        cardsCanShow: CanShowAgentCards(),
        watching: IsShowingAgents() && _agentsModule!.FocusedSession?.Id == session.Id,
        opensOnAnswer: _store.Bool(DefaultsKey.FaqraAgentsOpenOnAnswer),
        windowSeconds: _store.Int(DefaultsKey.FaqraAgentsReplyWindowSeconds));
```

5. In `IslandController.cs`'s constructor, add `_agents.HoldFor = HoldFor;` after `_agents.CanAsk = CanShowAgentCards;`, and in `Dispose` add `_agents.HoldFor = _ => null;` after `_agents.CanAsk = () => false;`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.App.Tests`
Expected: PASS. Then `wc -l src/Faqra.App/Island/IslandController.cs src/Faqra.App/Island/IslandController.Agents.cs` (Git Bash): both under 800.

- [ ] **Step 5: Commit**

```bash
git add src/Faqra.App/Island tests/Faqra.App.Tests/AgentsRenderTests.cs
git commit -m "feat(windows): island decides which finished turns wait for a reply"
```

---

### Task 9: Live check and docs (controller, with the owner)

This task is run by the controller, not a subagent: it touches the owner's Claude Code account and their running Faqra, and each live step needs the owner's OK in the conversation at that moment (an OK from an earlier context does not carry over).

**Files:**
- Modify: `KNOWN-ISSUES.md`, `design-system.md`
- Modify (only if the live check says so): `src/Faqra.Core/Agents/StopReply.cs` (`UseAdditionalContext`), `tests/Faqra.Core.Tests/Agents/StopReplyTests.cs`

- [ ] **Step 1: Full suite and a two-core run**

Run: `dotnet test` (the whole solution). Expected: PASS. Then in Git Bash, five two-core runs of the Services tests: `for i in 1 2 3 4 5; do DOTNET_PROCESSOR_COUNT=2 dotnet test tests/Faqra.Services.Tests --nologo -v q 2>&1 | grep -E "Passed!|Failed!|\[FAIL\]"; done`. All five must pass.

- [ ] **Step 2: Publish to Faqra-test (ask first)**

Ask the owner whether they are using Faqra-test and whether to replace it now. On a yes: stop the running Faqra-test process, publish the App and the hook relay the way A2 did (`dotnet publish src/Faqra.App -c Release -r win-x64 --self-contained -o <Faqra-test folder>` and the same for `src/Faqra.Hook`, with the relay landing at the path the installed hooks call), start it, and confirm the island and the Agents section show. `~/.claude/settings.json` does not change in A3; check with `git diff --no-index` against the newest `settings.json.bak-*` that nothing touched it.

- [ ] **Step 3: A held reply reaches Claude (headless, ask first)**

Ask the owner before each `claude -p` run on their account. Then, from an empty temp folder:

1. Start the UI Automation helper in the background: a PowerShell script in the scratchpad that waits up to 90 s for an element named `Reply to Claude` in a Faqra window, sets its value to `blue` with `ValuePattern.SetValue`, and invokes the element named `Send` with `InvokePattern`:

```powershell
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$ae = [System.Windows.Automation.AutomationElement]
$scope = [System.Windows.Automation.TreeScope]
$named = { param($n) New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, $n) }
$deadline = (Get-Date).AddSeconds(90)
$box = $null
while (-not $box -and (Get-Date) -lt $deadline) {
    foreach ($p in Get-Process Faqra -ErrorAction SilentlyContinue) {
        $mine = New-Object System.Windows.Automation.PropertyCondition($ae::ProcessIdProperty, $p.Id)
        foreach ($w in $ae::RootElement.FindAll($scope::Children, $mine)) {
            $box = $w.FindFirst($scope::Descendants, (& $named 'Reply to Claude'))
            if ($box) { $island = $w; break }
        }
        if ($box) { break }
    }
    if (-not $box) { Start-Sleep -Milliseconds 400 }
}
if (-not $box) { 'no reply box'; exit 1 }
$box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($args[0])
Start-Sleep -Milliseconds 300
$island.FindFirst($scope::Descendants, (& $named 'Send')).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
'sent'
```

2. Run `claude -p "Ask me whether I prefer red or blue, then end your turn and wait for my answer. When I answer, reply with my answer in capital letters only." --output-format json --debug` (the `--debug` flag after the prompt, or it eats the prompt).

Expected: the island opens on the held card (the question ends in "?" and "Open the island when an agent answers" is on), the helper prints `sent`, and the run's final `result` contains `BLUE`. In `~/.claude/debug/<session>.txt`, the Stop hook shows the relay's output and a second turn follows.

If Claude does not carry on (the result has no `BLUE` and the debug log shows the context was not used), switch to the block form: set `UseAdditionalContext` to `false` in `StopReply.cs`, change `AReplyGoesBackAsStopContextWithThePreamble` to read `JsonNode.Parse(printed)!["reason"]` and check `["decision"]` is `"block"`, run `dotnet test tests/Faqra.Core.Tests`, republish (ask), and rerun this step. Record which form works in the spec's A3 row.

- [ ] **Step 4: Done, timeout, and a turn nobody holds (headless, ask first)**

1. Done: the same prompt, with a helper that invokes the element named `Done` instead of typing. Expected: `claude -p` exits within a few seconds of the click, with only the question as its result.
2. Timeout: in Settings, Agents, set "How long Claude waits for your reply" to 1 minute; run the same prompt and touch nothing. Expected: the run exits about 60 s after the question, the card turns into the plain answered card, and the agents log shows `released`. Set the window back to 5 minutes.
3. Not held: collapse the island, then run `claude -p "Say hello in five words." --output-format json --debug`. Expected: the run ends as fast as before A3 (the Stop hook takes well under the 2 s budget in the debug log) and no card stays.
4. Check `%LOCALAPPDATA%\Faqra\agents.log` (`AppPaths.AgentsLogFile`): the decisions read `replied` and `released`, and neither `blue` nor the question's words appear.

- [ ] **Step 5: An interactive session, with the owner (ask first)**

Ask the owner to try it in a Claude Code session of their choice: switch "Wait for my reply after each turn" on for it in the island, let a turn finish, reply from the island, and then press Done on the next one. Note whether Claude Code's own prompt shows while a turn is held, and how long the hold feels; record the answers in `KNOWN-ISSUES.md`.

- [ ] **Step 6: Docs**

In `KNOWN-ISSUES.md`, replace the bullet that starts `- **Replying to Claude comes in A3.**` with:

```markdown
- **A held turn keeps Claude Code waiting.** When Faqra holds a finished turn for your reply (Claude asked
  you something, you were looking at that session, or you switched replies on for it), Claude Code waits
  on Faqra's Stop hook for up to the reply window (5 minutes by default, at most 8). Done, Esc, the
  collapse button or a new prompt in Claude Code let it go at once. A reply typed when the window runs out
  is not kept.
- **Claude Code takes at most 8 replies in a row from the island.** After that it stops on its own and
  the card says to reply in Claude Code; any tool use or prompt from Claude Code starts the count again.
```

plus what Step 5 found. In `design-system.md`, in the island cards section, add the answered card's reply state: the message styled from Markdown (headings 14/13 semibold, body 12, lists indented 14 px, code in a `FillStrong` box with Cascadia Mono 11.5, links in the accent and only for web addresses), the countdown as a tertiary caption under the message, the reply box full width (at most 120 px tall, scrolls), Done secondary and Send primary on the right, Enter sends and Shift+Enter starts a line, and the "Wait for my reply after each turn" switch in the session's details.

- [ ] **Step 7: Commit and push**

```bash
git add KNOWN-ISSUES.md design-system.md docs src tests
git commit -m "docs(windows): replies from the island checked live"
git push
```

Then update the Faqra memory file: A3 done, which Stop output form works, what the owner said about the hold, and next steps (H1 home page and H2 shelf spec pass, then M7).
