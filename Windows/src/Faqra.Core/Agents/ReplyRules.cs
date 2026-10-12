// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

namespace Faqra.Core.Agents;

/// <summary>
/// When a finished turn waits for the owner's reply, as the owner chose it (2026-10-10): when they switched replies on
/// for the session, when they are looking at that session as it finishes, or when Claude's new words ask them something
/// and the island will open on them. Never when no card can show, and never past Claude Code's replies-in-a-row cap.
/// Looking does not count for a turn the owner started from the island (2026-10-12): a reply sent there goes to Claude,
/// and Claude's answer only waits if it asks something or replies are on.
/// </summary>
public static class ReplyRules
{
    public const int MinWindowSeconds = 30;

    /// <summary>The longest hold; the relay waits 590 s and Claude Code's Stop hook timeout is 600 s.</summary>
    public const int MaxWindowSeconds = 480;

    /// <summary>How long to hold <paramref name="session"/>'s finished turn, or null to let Claude stop at once.</summary>
    public static TimeSpan? HoldFor(AgentSession session, bool cardsCanShow, bool watching, bool opensOnAnswer, int windowSeconds)
    {
        if (!cardsCanShow || session.ConsecutiveReplies >= AgentBoard.MaxConsecutiveReplies)
        {
            return null;
        }
        var asks = opensOnAnswer && session.Unread && AgentsText.AsksOwner(session.LastMessage);
        var looking = watching && !session.TurnFromIsland;
        if (!session.RepliesOn && !looking && !asks)
        {
            return null;
        }
        return TimeSpan.FromSeconds(Math.Clamp(windowSeconds, MinWindowSeconds, MaxWindowSeconds));
    }
}
