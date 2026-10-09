// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Event rules ported from Coucou's island (windows/src/island/hooks.ts), https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé), plus Searching, Compacting and Background.

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

public enum AgentState { Idle, Thinking, Working, Searching, Compacting, Background, Approval, Question, Error, RateLimited, Finished }

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
    /// <summary>The folder the session runs in, as the owner calls the project.</summary>
    public string Project => Cwd.Length == 0 ? Id : Path.GetFileName(Cwd.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : Cwd;
}

/// <summary>Every agent session Faqra knows about. Immutable: Apply and Tick return a new board.</summary>
public sealed record AgentBoard(ImmutableDictionary<string, AgentSession> Sessions)
{
    public const int MaxSteps = 20;
    private const int CommandChars = 40;
    private const int PromptChars = 60;

    public static readonly AgentBoard Empty = new(ImmutableDictionary<string, AgentSession>.Empty.WithComparers(StringComparer.Ordinal));

    /// <summary>How long Done shows before the session rests, Coucou's 5.2 s.</summary>
    public static readonly TimeSpan FinishedHold = TimeSpan.FromMilliseconds(5200);

    /// <summary>A session with no event for this long is gone (its terminal was closed without SessionEnd).</summary>
    public static readonly TimeSpan IdleExpiry = TimeSpan.FromHours(12);

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
        return this with { Sessions = Sessions.SetItem(e.SessionId, next with { UpdatedAt = now, Cwd = e.Cwd.Length > 0 ? e.Cwd : next.Cwd }) };
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
        }
        return ReferenceEquals(sessions, Sessions) ? this : this with { Sessions = sessions };
    }

    private static AgentState Resting(AgentSession session) => session.Subagents > 0 ? AgentState.Background : AgentState.Idle;

    private static AgentSession? Next(AgentSession s, AgentEvent e, DateTimeOffset now) => e.Event switch
    {
        "SessionStart" => s,
        "UserPromptSubmit" => Step(s with { State = AgentState.Thinking, FinishedAt = null }, now, AgentStepKind.Prompt, OneLine(e.Prompt ?? string.Empty, PromptChars)),
        "PreToolUse" => ToolStep(s with { State = e.ToolName is { } t && SearchTools.Contains(t) ? AgentState.Searching : AgentState.Working }, e, now),
        "PostToolUse" => s with { State = AgentState.Working },
        "PostToolUseFailure" => Step(s with { State = AgentState.Working }, now, AgentStepKind.Failed, e.ToolName ?? string.Empty),
        "PermissionRequest" => s with { State = e.ToolName == "AskUserQuestion" ? AgentState.Question : AgentState.Approval },
        "Notification" => Notified(s, e.Message ?? string.Empty),
        "Stop" => s with { State = AgentState.Finished, FinishedAt = now, LastMessage = e.LastAssistantMessage ?? e.Message ?? s.LastMessage },
        "StopFailure" => s with { State = AgentState.Error, LastMessage = e.Message ?? s.LastMessage },
        "SubagentStart" => Step(s with { Subagents = s.Subagents + 1 }, now, AgentStepKind.Subagent, string.Empty),
        "SubagentStop" => SubagentDone(s, now),
        "PreCompact" => s with { State = AgentState.Compacting },
        "PostCompact" => s with { State = AgentState.Idle },
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
