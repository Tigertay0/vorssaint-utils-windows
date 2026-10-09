// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Urgency order from Coucou's MochiActivityState.swift (MIT, Copyright (c) 2026 Louis Raillé).

using Faqra.Core.Agents.Orb;

namespace Faqra.Core.Agents;

/// <summary>How urgent a state is, as a semantic colour role. Never the only signal: the orb's motion changes too.</summary>
public enum AgentTone { Neutral, Secondary, Caution, Accent, Critical, Success }

public readonly record struct AgentOrbStyle(OrbLook Look, AgentTone Tone, double Speed);

public static class AgentOrbStyles
{
    public static AgentOrbStyle For(AgentState state) => state switch
    {
        AgentState.Approval => new(OrbLook.Waiting, AgentTone.Caution, 1),
        AgentState.Question => new(OrbLook.Waiting, AgentTone.Accent, 1),
        AgentState.Error => new(OrbLook.RetryingSurge, AgentTone.Critical, 1),
        AgentState.RateLimited => new(OrbLook.Retrying, AgentTone.Caution, 1),
        AgentState.Working => new(OrbLook.Working, AgentTone.Neutral, 1),
        AgentState.Thinking => new(OrbLook.Reasoning, AgentTone.Neutral, 1),
        AgentState.Searching => new(OrbLook.Searching, AgentTone.Neutral, 1),
        AgentState.Compacting => new(OrbLook.Compacting, AgentTone.Neutral, 1),
        AgentState.Background => new(OrbLook.BackgroundSpiral, AgentTone.Secondary, 1),
        AgentState.Finished => new(OrbLook.Base, AgentTone.Success, 1),
        _ => new(OrbLook.Base, AgentTone.Secondary, 0.5),
    };

    /// <summary>Lower is more urgent: the owner's OK, a question, trouble, work, done, rest.</summary>
    public static int Urgency(AgentState state) => state switch
    {
        AgentState.Approval => 0,
        AgentState.Question => 1,
        AgentState.Error or AgentState.RateLimited => 2,
        AgentState.Working or AgentState.Thinking or AgentState.Searching or AgentState.Compacting or AgentState.Background => 3,
        AgentState.Finished => 4,
        _ => 5,
    };
}
