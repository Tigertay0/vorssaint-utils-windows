# Faqra Agents: design

Date: 2026-10-09. Status: approved by the owner 2026-10-09; M7 (installer) runs after A3.

Faqra Agents watches the AI coding agents running on this PC (Claude Code first) from the island,
lets the owner approve, answer and reply to them without switching to the terminal or the
Antigravity panel, and shows their state with a thinking orb. It ports the agent-watching side of
Coucou to Faqra's own stack and replaces Coucou's mascot with Thinking Orbs.

## Sources and licences

| Source | Licence | What Faqra takes |
|---|---|---|
| [Coucou](https://github.com/Louis-CFM/coucou) (Louis Raillé) | Code MIT. The names Coucou and Mochi, the Mochi character, icons and sounds are all rights reserved (`LICENSE-ASSETS.md`) | The hook contract, relay protocol, config install with diff and backup, session model, event-to-state rules, plan usage, recap and integration logic, ported to C#. No names, character, icons or sounds. |
| [Thinking Orbs](https://github.com/yogesharc/thinking-orbs) (Yogesh) | MIT | `orb-core.ts`'s drawing math, ported to C# and drawn natively in WPF. |

Both MIT notices ship in Faqra's About and licence files. Faqra stays GPL-3.0; MIT code may be
included in a GPL work with its notice kept.

## Decisions confirmed with the owner

| Topic | Decision |
|---|---|
| Scope | Everything from Coucou that fits Windows, in milestones (below). |
| Mascot | The yogesharc dotted-sphere orb, ported to WPF. No character play (poke, dizzy, wardrobe). |
| When an agent needs the owner | The island opens on its own, without taking keyboard focus. |
| Reading Claude | The island shows Claude's last message for each turn. |
| Replying to Claude | End-of-turn reply window: when Claude finishes a turn, its Stop hook stays open while the owner can type a reply in the island; the reply makes Claude carry on. Works in the Antigravity panel and terminals with no launch flag. Not mid-turn. |
| Multiple-choice questions | Every option is shown, multi-select works, and there is a box for the owner's own answer. |
| Config files | Nothing under `~/.claude` or any other agent's folder is written until the owner has seen the exact diff and clicked. A dated backup is taken first. The owner's own hooks are never touched. |

Not chosen: Claude Code channels (research preview; needs a launch flag the Antigravity extension
cannot pass), `claude -p --resume` into a live session (the docs warn it interleaves two writers
into one transcript), and typing into the terminal window (steals focus, fragile).

## Coucou's leftover hooks

Coucou has been removed from this PC (no app folder, no `coucou-hook.exe`, no uninstall entry), but
its 12 hooks are still registered in `~/.claude/settings.json`, so every Claude Code event runs a
command that no longer exists. Installing Faqra's hooks offers, in the same reviewed diff, to remove
every entry whose command contains `coucou-hook`. If Coucou is ever reinstalled, the same rule
applies: two apps answering one permission request would race, so Faqra answers only when
Coucou's hooks are gone and otherwise watches only.

## How it works

```
Claude Code ──hook stdin──▶ faqra-hook.exe ──named pipe──▶ Faqra (AgentHub) ──▶ island, tray, recap
             ◀─decision JSON─               ◀─one line────
```

**Relay, `faqra-hook.exe`.** A small .NET 8 console app (framework-dependent, ReadyToRun) copied
to `%LOCALAPPDATA%\Faqra\bin\` at launch. Claude Code runs it per event with the event name as its
argument. It reads the payload, trims it (strings capped at 2,000 characters, edit bodies at
256 KB), connects to `\\.\pipe\faqra-agents-<user SID>` within 300 ms after checking with
`GetNamedPipeServerProcessId` that the server runs as the same user, writes one JSON line and,
for waiting events only, reads one line back. If Faqra is closed, slow or crashed it exits 0 with
no output, so **a session is never blocked or changed by Faqra being absent**. Startup is measured
in A1; if the median passes 80 ms, the fallback is Claude Code's `http` hook type pointed at a
local endpoint with a per-install token header, which needs no process at all.

Waiting events and their budgets:

| Event | Waits for | Budget | Reply printed |
|---|---|---|---|
| PermissionRequest (a tool) | Allow, Deny, Always | 110 s | `{"hookSpecificOutput":{"hookEventName":"PermissionRequest","decision":{"behavior":"allow"}}}`, deny with a message, Always adds `updatedPermissions` `addRules` to `localSettings` |
| PermissionRequest (AskUserQuestion) | The answers | 110 s | allow with `updatedInput` = the original input plus `answers` (`{question text: label}` or a list for multi-select) |
| Stop | A reply, Done, or nothing | the reply window, 5 min default | `{"decision":"block","reason":"<reply>"}`, or nothing |

Everything else is fire-and-forget. With no answer the relay prints nothing and Claude Code asks
in its own UI as usual. The Stop hook's `timeout` is set above the reply window (default 600 s).

**Reply window rules.** The relay holds Stop only when the owner has asked for replies on that
session, or Claude's last message reads as a question to the owner, or the island is open on that
session; otherwise Stop returns at once. Done, Esc, the window running out, or a new prompt from
the terminal releases it. Claude Code caps consecutive Stop continuations at 8 (reset whenever
Claude uses a tool); at the cap the reply box explains it and points to the Antigravity panel.

**Free-text answers.** Claude Code documents only option labels for AskUserQuestion answers. A1's
live check tests a free-text answer; if Claude Code rejects it, the island's "Other" box sends the
text as a reply instead (deny the question with the text as its message, which Claude reads).

**Install.** Settings → Agents → Claude Code → Install. Shows the unified diff of
`settings.json`, the backup name (`settings.json.bak-YYYYMMDD-HHMMSS`), and writes only on click,
after re-checking that the file did not change since the preview (fingerprint). Faqra's entries
are found by the substring `faqra-hook` in their command; uninstall removes only those. Plan usage
adds a `statusLine` that forwards `rate_limits` to Faqra and runs any previous status line command
unchanged (none is set today).

## What the owner sees

**The orb.** A native WPF port of Thinking Orbs, 20 px in the collapsed island and 64 px in the
open one. Its motion says what the agent is doing; a Fluent semantic colour (never colour alone)
says how urgent it is.

| Agent state (Coucou's) | Orb | Colour | Status line |
|---|---|---|---|
| Needs the owner's OK | waiting | caution | Needs your OK |
| Asked a question | waiting | accent | Has a question |
| Error | retrying, surge | critical | Error |
| Rate limited | retrying | caution | Rate limited |
| Working (tool running) | working | text | Working, step 3 of 7 |
| Thinking (prompt sent) | reasoning | text | Thinking |
| Searching (Grep, Glob, WebSearch, WebFetch) | searching | text | Searching |
| Compacting (PreCompact) | compacting | text | Compacting |
| Subagents running in the background | background, spiral | text secondary | 2 subagents |
| Finished (5 s, then idle) | base | success | Done |
| Idle | base, half speed | text secondary | Idle |

The most urgent session drives the collapsed orb (Coucou's order: OK, question, error, working,
done, idle). With reduced motion on, the orb holds its first frame and the status line carries the
change.

**The island.** A new Faqra-only module, Agents (persisted as `faqraAgents`, so it can never
collide with an upstream name). Collapsed, the pill shows the orb and the status line whenever a
session is active. It opens by itself for an approval, a question, or a reply window, without
activating. Open, it shows one row per session (agent, project folder, orb, status, time), and the
focused session's card:

- Approval: the tool and its command or file, Deny, Allow and Always, Allow the primary.
- Question: each question with all its options (radio or checkboxes), the "Other" box, Send.
- Reply: Claude's last message (Markdown rendered as plain styled text), a reply box, Send and
  Done.
- Activity: the live ticker (reads, edits with +N -M, commands, subagents); an edit opens its diff.
- Go to window: brings the session's terminal or Antigravity window forward (process-tree walk
  from the relay, as Coucou does).

**Shortcuts.** Faqra's shortcut registry gains: jump to the waiting card, go to the session's
window, next and previous session. Defaults chosen in A2 against the existing Ctrl+Alt+Win set.

**Sounds.** None by default: Coucou's sounds are not licensed for reuse. An optional Windows
notification sound for "needs you" is a setting.

## Milestones

Each ends with a published build in `Faqra-test`, a desktop check, and a commit.

| | Milestone | Done when |
|---|---|---|
| A1 | Orb, relay, install, watching | The orb renders all states (PNG tests); `faqra-hook.exe` passes recorded-payload contract tests and starts under the 80 ms budget or the http fallback is chosen; install shows the diff and writes only on click, with the Coucou removal offered; a real Claude Code session's activity and states appear in the island. |
| A2 | Approvals, questions, alerts, go to window, shortcuts | Allow, Deny and Always work on a real permission prompt; a real AskUserQuestion (single, multi, two questions) is answered from the island; the free-text test is settled; the island opens without stealing focus; go to window brings the right window forward. |
| A3 | Reading and replying | Claude's last message shows after each turn; a reply sent from the island makes Claude carry on, in the Antigravity panel and in a terminal; Done and timeout release the turn. |
| A4 | Edits and plan usage | +N -M per edit with a readable diff; the 5-hour and weekly Claude limits show in the island header. |
| A5 | Other agents | Install and watch for Codex, Cursor, Gemini CLI, Antigravity's agent, Copilot CLI, OpenCode and Amp, with Coucou's per-agent formats; approvals where the agent supports them. |
| A6 | Chat | Ask Claude, OpenAI, Gemini, OpenRouter or a local model (Ollama, LM Studio) from the island, keys in Windows Credential Manager; drop a file on the island to ask about it. Uses the owner's own API keys, which cost per use. |
| A7 | Weekly recap | Counts only (time, sessions, files, lines, commands, permissions, top agent and project, busiest day), 12 weeks kept locally, a Monday summary, a shareable image. |
| A8 | Service pills | GitHub, Vercel, Stripe, n8n, Resend, Notion and Cal.com, each polled only once its key exists. |

M7 (installer and release) runs after A3, then A4 to A8.

## Not ported

Mochi's character play (poke, dizzy, hearts, wardrobe, desktop Mochi, drag onto a window), Coucou's
sounds and icons (not licensed), the iPhone app and its CloudKit relay (Apple only), Spotify and
Apple Music pills (Faqra's island already has a music module), dictation, and email from a dropped
file (no safe Windows equivalent, as Coucou's own Windows notes say).

## Code layout

- `Faqra.Core/Agents`: event model, normalisation, session state machine, urgency, ticker, diff
  engine, answer validation, config diff and fingerprint, orb math (`OrbMath`: state and time in,
  dots out). All unit-tested, TDD.
- `Faqra.Win32/Agents`: the named pipe server, process-tree walk, window activation, Credential
  Manager.
- `Faqra.Services/Agents`: `AgentHub` (pipe server, sessions, pending decisions), installers per
  agent, plan usage, recap, chat clients, pollers.
- `Faqra.App`: `OrbView` (WPF element drawing `OrbMath`'s dots each frame while visible), the Agents
  island module and cards, the Agents settings pages.
- `Faqra.Hook`: the relay console app.

## Risks

- **Hook contract drift.** Claude Code's hook payloads and replies can change between versions.
  Contract tests run against recorded payloads from this PC's Claude Code (2.1.296), and the relay
  fails open.
- **Reply window feel.** While a Stop hook is held, the Antigravity panel shows Claude as still
  running. A3's desktop check decides the default window length and whether holding is opt-in per
  session.
- **Free text for questions** is undocumented; the fallback above keeps the box useful either way.
- **Relay start-up cost** on every tool call; measured in A1 with the http-hook fallback ready.
- **Two watchers.** Coucou's hooks alongside Faqra's would double every card; install removes them, and Faqra stays watch-only while they exist.

## Testing

TDD for everything in `Faqra.Core/Agents`. Relay contract tests feed recorded payloads through
`faqra-hook.exe` against a test pipe server. Orb and cards render to PNG in `UiRenderTests`. Live
checks use a scratch project with a project-level `.claude/settings.local.json` until the owner
approves the global install.
