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

    /// <summary>
    /// Longest reply the island sends; the reply box stops there. Claude Code caps a hook's <c>additionalContext</c> at 10,000
    /// characters (longer text is saved to a file and Claude sees a 2,000-character preview), so this leaves room for the preamble.
    /// </summary>
    public const int MaxReplyLength = 9_000;

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
