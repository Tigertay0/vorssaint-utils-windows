// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Globalization;
using Faqra.Core.Localization;

namespace Faqra.Core.Agents;

/// <summary>Turns sessions and steps into the short lines the island shows.</summary>
public static class AgentsText
{
    public static string State(AgentSession session, AgentsStrings s) => session.State switch
    {
        AgentState.Thinking => s.StateThinking,
        AgentState.Working => s.StateWorking,
        AgentState.Searching => s.StateSearching,
        AgentState.Compacting => s.StateCompacting,
        AgentState.Background => Format(s.StateBackgroundFormat, session.Subagents),
        AgentState.Approval => s.StateApproval,
        AgentState.Question => s.StateQuestion,
        AgentState.Error => s.StateError,
        AgentState.RateLimited => s.StateRateLimited,
        AgentState.Finished => s.StateFinished,
        AgentState.AwaitingReply => s.StateAwaitingReply,
        _ => s.StateIdle,
    };

    public static string Step(AgentStep step, AgentsStrings s) => step.Kind switch
    {
        AgentStepKind.Prompt => Format(s.StepPromptFormat, step.Detail),
        AgentStepKind.Read => Format(s.StepReadFormat, step.Detail),
        AgentStepKind.Edit => Format(s.StepEditFormat, step.Detail),
        AgentStepKind.Run => Format(s.StepRunFormat, step.Detail),
        AgentStepKind.Search => Format(s.StepSearchFormat, step.Detail),
        AgentStepKind.WebSearch => Format(s.StepWebSearchFormat, step.Detail),
        AgentStepKind.WebFetch => Format(s.StepWebFetchFormat, step.Detail),
        AgentStepKind.Subagent => step.Detail.Length > 0 ? Format(s.StepSubagentFormat, step.Detail) : s.StepSubagent,
        AgentStepKind.SubagentDone => s.StepSubagentDone,
        AgentStepKind.Plan => s.StepPlan,
        AgentStepKind.Failed => Format(s.StepFailedFormat, step.Detail),
        _ => Format(s.StepToolFormat, step.Detail),
    };

    /// <summary>The status line: the latest step while working or searching, the state otherwise.</summary>
    public static string Status(AgentSession session, AgentsStrings s) =>
        session.State is AgentState.Working or AgentState.Searching && session.Steps.Count > 0
            ? Step(session.Steps[^1], s)
            : State(session, s);

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

    public static string Elapsed(TimeSpan span, AgentsStrings s) =>
        span < TimeSpan.FromMinutes(1) ? s.ElapsedNow
        : span < TimeSpan.FromHours(1) ? Format(s.ElapsedMinutesFormat, (int)span.TotalMinutes)
        : Format(s.ElapsedHoursFormat, (int)span.TotalHours);

    private static string Format(string format, object value) => string.Format(CultureInfo.CurrentCulture, format, value);
}
