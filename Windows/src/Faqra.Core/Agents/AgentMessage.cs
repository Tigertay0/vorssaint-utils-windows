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
