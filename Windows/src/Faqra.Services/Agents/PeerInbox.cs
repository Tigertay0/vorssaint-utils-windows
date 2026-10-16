// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using Faqra.Core.Agents;

namespace Faqra.Services.Agents;

/// <summary>What became of words the owner sent to a session from the island.</summary>
public enum AgentMessageOutcome
{
    /// <summary>The session's inbox took them; Claude Code starts a turn or reads them between steps.</summary>
    Sent,

    /// <summary>Faqra has no inbox for the session (an older Claude Code, a session not seen since Faqra started).</summary>
    Unavailable,

    /// <summary>The inbox did not answer in time: the session has most likely closed.</summary>
    Unreachable,

    /// <summary>Blank, or longer than <see cref="AgentMessage.MaxLength"/>; nothing was sent.</summary>
    Invalid,
}

/// <summary>Writes to a session's inbox: connect, write the lines, close. Never throws, never says what it sent.</summary>
internal static class PeerInbox
{
    public static async Task<bool> SendAsync(AgentInbox inbox, string lines, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        try
        {
            using var pipe = new NamedPipeClientStream(".", inbox.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(deadline.Token).ConfigureAwait(false);
            await pipe.WriteAsync(new UTF8Encoding(false).GetBytes(lines), deadline.Token).ConfigureAwait(false);
            await pipe.FlushAsync(deadline.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException)
        {
            // The type only: the message text and the token never reach a log.
            Trace.TraceWarning($"Faqra agents message: {ex.GetType().Name}");
            return false;
        }
    }
}
