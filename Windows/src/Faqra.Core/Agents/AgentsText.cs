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

    public static string Elapsed(TimeSpan span, AgentsStrings s) =>
        span < TimeSpan.FromMinutes(1) ? s.ElapsedNow
        : span < TimeSpan.FromHours(1) ? Format(s.ElapsedMinutesFormat, (int)span.TotalMinutes)
        : Format(s.ElapsedHoursFormat, (int)span.TotalHours);

    private static string Format(string format, object value) => string.Format(CultureInfo.CurrentCulture, format, value);
}
