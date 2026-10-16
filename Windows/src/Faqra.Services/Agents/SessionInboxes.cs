// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Faqra.Core.Agents;

namespace Faqra.Services.Agents;

/// <summary>
/// Each session's inbox, from its latest event, in memory only: the token in it lets its holder post into the session.
/// The oldest go once there are more than 64. Thread-safe: the hub writes from pipe threads and the island reads on the
/// UI thread.
/// </summary>
internal sealed class SessionInboxes
{
    public const int Capacity = 64;
    private readonly List<(string Session, AgentInbox Inbox)> _sessions = [];
    private readonly object _gate = new();

    public AgentInbox? Find(string session)
    {
        lock (_gate)
        {
            return _sessions.Find(entry => entry.Session == session).Inbox;
        }
    }

    public void Remember(string session, AgentInbox inbox)
    {
        if (!SessionWindowCache.IsPlausible(session))
        {
            return;
        }
        lock (_gate)
        {
            _sessions.RemoveAll(entry => entry.Session == session);
            _sessions.Add((session, inbox));
            if (_sessions.Count > Capacity)
            {
                _sessions.RemoveRange(0, _sessions.Count - Capacity);
            }
        }
    }

    public void Forget(string session)
    {
        lock (_gate)
        {
            _sessions.RemoveAll(entry => entry.Session == session);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _sessions.Clear();
        }
    }
}
