// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Event rules ported from Coucou's island (windows/src/island/hooks.ts), https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé), plus Searching, Compacting and Background.

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

public enum AgentState { Idle, Thinking, Working, Searching, Compacting, Background, Approval, Question, Error, RateLimited, Finished, AwaitingReply }

public enum AgentStepKind { Prompt, Read, Edit, Run, Search, WebSearch, WebFetch, Subagent, SubagentDone, Plan, Tool, Failed }

/// <summary>One ticker line: what happened, and the file, command or text it concerns.</summary>
public sealed record AgentStep(DateTimeOffset At, AgentStepKind Kind, string Detail);

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

    /// <summary>Replies from the island in a row with no tool use between them; Claude Code stops taking them at 8.</summary>
    public int ConsecutiveReplies { get; init; }

    /// <summary>The owner wants every finished turn of this session held for a reply.</summary>
    public bool RepliesOn { get; init; }

    /// <summary>This turn began with the owner's reply from the island; it lasts until a prompt typed in Claude Code.</summary>
    public bool TurnFromIsland { get; init; }

    /// <summary>Whether someone is watching the session, as Claude Code last said; null until it says.</summary>
    public bool? Attended { get; init; }

    /// <summary>The folder the session runs in, as the owner calls the project.</summary>
    public string Project => Cwd.Length == 0 ? Id : Path.GetFileName(Cwd.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : Cwd;

    /// <summary>What the island calls the session: its conversation's name, else its folder.</summary>
    public string Name => Title is { Length: > 0 } title ? title : Project;
}

/// <summary>Every agent session Faqra knows about. Immutable: Apply and Tick return a new board.</summary>
public sealed record AgentBoard(ImmutableDictionary<string, AgentSession> Sessions)
{
    public const int MaxSteps = 20;

    /// <summary>How many Stop-hook replies in a row Claude Code takes before it stops anyway.</summary>
    public const int MaxConsecutiveReplies = 8;
    private const int CommandChars = 40;
    private const int PromptChars = 60;

    public static readonly AgentBoard Empty = new(ImmutableDictionary<string, AgentSession>.Empty.WithComparers(StringComparer.Ordinal));

    /// <summary>How long Done shows before the session rests, Coucou's 5.2 s.</summary>
    public static readonly TimeSpan FinishedHold = TimeSpan.FromMilliseconds(5200);

    /// <summary>A session with no event for this long is gone (its terminal was closed without SessionEnd).</summary>
    public static readonly TimeSpan IdleExpiry = TimeSpan.FromHours(12);

    /// <summary>
    /// A busy or failed session with no event for this long is treated as resting: its terminal was most likely
    /// closed mid-turn. Long enough that a slow build is not misread.
    /// </summary>
    public static readonly TimeSpan BusyTimeout = TimeSpan.FromMinutes(30);

    private static readonly HashSet<string> SearchTools = new(StringComparer.Ordinal) { "Grep", "Glob", "WebSearch", "WebFetch" };

    public bool IsActive => Sessions.Values.Any(session => session.State != AgentState.Idle);

    public IReadOnlyList<AgentSession> Ordered => Sessions.Values
        .OrderBy(session => AgentOrbStyles.Urgency(session.State))
        .ThenByDescending(session => session.UpdatedAt)
        .ThenBy(session => session.Id, StringComparer.Ordinal)
        .ToList();

    public AgentSession? MostUrgent => Ordered.FirstOrDefault();

    public AgentBoard Apply(AgentEvent e, DateTimeOffset now)
    {
        if (e.Event == "SessionEnd")
        {
            return Sessions.ContainsKey(e.SessionId) ? this with { Sessions = Sessions.Remove(e.SessionId) } : this;
        }
        var session = Sessions.TryGetValue(e.SessionId, out var known)
            ? known
            : new AgentSession(e.SessionId, e.Agent, e.Cwd, AgentState.Idle, [], null, 0, now, now, null);
        var next = Next(session, e, now);
        if (next is null)
        {
            return this;
        }
        return this with { Sessions = Sessions.SetItem(e.SessionId, next with { UpdatedAt = now, Cwd = e.Cwd.Length > 0 ? e.Cwd : next.Cwd, Attended = e.Attended ?? next.Attended }) };
    }

    public AgentBoard Tick(DateTimeOffset now)
    {
        var sessions = Sessions;
        foreach (var session in Sessions.Values)
        {
            if (now - session.UpdatedAt >= IdleExpiry)
            {
                sessions = sessions.Remove(session.Id);
            }
            else if (session.State == AgentState.Finished && session.FinishedAt is { } finished && now - finished >= FinishedHold)
            {
                sessions = sessions.SetItem(session.Id, session with { State = Resting(session) });
            }
            else if (IsStale(session, now))
            {
                sessions = sessions.SetItem(session.Id, session with { State = AgentState.Idle });
            }
        }
        return ReferenceEquals(sessions, Sessions) ? this : this with { Sessions = sessions };
    }

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
            TurnFromIsland = true,
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

    private static bool IsStale(AgentSession session, DateTimeOffset now) =>
        session.State is AgentState.Thinking or AgentState.Working or AgentState.Searching or AgentState.Compacting
            or AgentState.Background or AgentState.Error
        && now - session.UpdatedAt >= BusyTimeout;

    private static AgentState Resting(AgentSession session) => session.Subagents > 0 ? AgentState.Background : AgentState.Idle;

    private static AgentSession? Next(AgentSession s, AgentEvent e, DateTimeOffset now) => e.Event switch
    {
        "SessionStart" => s,
        "UserPromptSubmit" => Prompted(s, e.Prompt, now),
        "PreToolUse" => ToolStep(s with { State = e.ToolName is { } t && SearchTools.Contains(t) ? AgentState.Searching : AgentState.Working, ConsecutiveReplies = 0 }, e, now),
        "PostToolUse" => s with { State = AgentState.Working },
        "PostToolUseFailure" => Step(s with { State = AgentState.Working }, now, AgentStepKind.Failed, e.ToolName ?? string.Empty),
        "PermissionRequest" => s with { State = e.ToolName == "AskUserQuestion" ? AgentState.Question : AgentState.Approval },
        "Notification" => Notified(s, e.Message ?? string.Empty),
        "Stop" => Finished(s, e, now),
        "StopFailure" => s with { State = AgentState.Error, LastMessage = e.Message ?? s.LastMessage },
        "SubagentStart" => s with { Subagents = s.Subagents + 1, State = s.State == AgentState.Idle ? AgentState.Background : s.State },
        "SubagentStop" => SubagentDone(s, now),
        "PreCompact" => s with { State = AgentState.Compacting },
        "PostCompact" => s with { State = Resting(s) },
        _ => null,
    };

    private static AgentSession Notified(AgentSession s, string message)
    {
        if (message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            return s with { State = AgentState.RateLimited };
        }
        return message.TrimEnd().EndsWith('?') ? s with { State = AgentState.Question } : s;
    }

    private static AgentSession SubagentDone(AgentSession s, DateTimeOffset now)
    {
        var left = Math.Max(0, s.Subagents - 1);
        var state = s.State == AgentState.Background && left == 0 ? AgentState.Idle : s.State;
        return Step(s with { Subagents = left, State = state }, now, AgentStepKind.SubagentDone, string.Empty);
    }

    private static AgentSession ToolStep(AgentSession s, AgentEvent e, DateTimeOffset now)
    {
        var input = e.ToolInput;
        var (kind, detail) = e.ToolName switch
        {
            "Read" => (AgentStepKind.Read, FileName(Field(input, "file_path"))),
            "Edit" or "MultiEdit" or "Write" => (AgentStepKind.Edit, FileName(Field(input, "file_path"))),
            "NotebookEdit" => (AgentStepKind.Edit, FileName(Field(input, "notebook_path"))),
            "Bash" or "PowerShell" => (AgentStepKind.Run, OneLine(Field(input, "command"), CommandChars)),
            "Grep" or "Glob" => (AgentStepKind.Search, OneLine(Field(input, "pattern"), CommandChars)),
            "WebSearch" => (AgentStepKind.WebSearch, OneLine(Field(input, "query"), CommandChars)),
            "WebFetch" => (AgentStepKind.WebFetch, Host(Field(input, "url"))),
            "Task" or "Agent" => (AgentStepKind.Subagent, OneLine(Field(input, "description") is { Length: > 0 } d ? d : Field(input, "subagent_type"), CommandChars)),
            "TodoWrite" => (AgentStepKind.Plan, string.Empty),
            _ => (AgentStepKind.Tool, e.ToolName ?? string.Empty),
        };
        return Step(s, now, kind, detail);
    }

    private static AgentSession Step(AgentSession s, DateTimeOffset now, AgentStepKind kind, string detail)
    {
        var steps = s.Steps.Add(new AgentStep(now, kind, detail));
        return s with { Steps = steps.Count > MaxSteps ? steps.RemoveRange(0, steps.Count - MaxSteps) : steps };
    }

    private static string Field(JsonObject? input, string key) =>
        input?[key] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : string.Empty;

    private static string FileName(string path) => path.Length == 0 ? path : Path.GetFileName(path.Replace('\\', '/').TrimEnd('/'));

    private static string Host(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : OneLine(url, CommandChars);

    /// <summary>The first line, cut at a word boundary to at most <paramref name="limit"/> characters.</summary>
    private static string OneLine(string text, int limit)
    {
        var line = text.Trim().Split('\n', 2)[0].Trim();
        if (line.Length <= limit)
        {
            return line;
        }
        var cut = line[..limit];
        var space = cut.LastIndexOf(' ');
        return (space > limit / 2 ? cut[..space] : cut).TrimEnd() + "…";
    }
}
