// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

namespace Faqra.Core.Agents;

/// <summary>The pipe between the relay and Faqra: one per Windows user, so two users never share one.</summary>
public static class AgentPipe
{
    public const string Prefix = "faqra-agents-";

    /// <summary>Tests point both ends at a private pipe with this environment variable.</summary>
    public const string OverrideVariable = "FAQRA_AGENTS_PIPE";

    public static string Name(string userSid, Func<string, string?> env) =>
        env(OverrideVariable) is { Length: > 0 } name ? name : Prefix + userSid;
}
