// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text.RegularExpressions;

namespace Faqra.Core.Agents;

/// <summary>
/// A Claude Code session's inbox: the named pipe Claude Code binds for cross-session messages, and the token a
/// connection must send first (https://code.claude.com/docs/en/cross-session-messaging). The token lets its holder post
/// into the session, so it lives in memory only and never appears in a log, a file or <see cref="ToString"/>.
/// </summary>
public sealed partial class AgentInbox
{
    private const string PipePrefix = @"\\.\pipe\";

    private AgentInbox(string pipeName, string token)
    {
        PipeName = pipeName;
        Token = token;
    }

    /// <summary>The pipe's name without the <c>\\.\pipe\</c> prefix, as <c>NamedPipeClientStream</c> takes it.</summary>
    public string PipeName { get; }

    public string Token { get; }

    /// <summary>The inbox a hook was given, or null unless the address and the token look exactly like Claude Code's own.</summary>
    public static AgentInbox? TryCreate(string? socket, string? token)
    {
        if (socket is null || token is null || !SocketPattern().IsMatch(socket) || !TokenPattern().IsMatch(token))
        {
            return null;
        }
        return new AgentInbox(socket[PipePrefix.Length..], token);
    }

    public override string ToString() => $"AgentInbox({PipeName}, token hidden)";

    [GeneratedRegex(@"^\\\\\.\\pipe\\(LOCAL\\)?cc-msg-[0-9a-f]{32}\z", RegexOptions.IgnoreCase)]
    private static partial Regex SocketPattern();

    [GeneratedRegex(@"^[0-9a-f]{32}\z")]
    private static partial Regex TokenPattern();
}
