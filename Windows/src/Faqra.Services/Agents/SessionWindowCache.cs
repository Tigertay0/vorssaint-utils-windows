// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

namespace Faqra.Services.Agents;

/// <summary>
/// Which window owner each session was found under, in memory only. A session looked at but without a window is kept
/// too (no owner), so the process table is not walked on every event. The oldest go once there are more than 64.
/// Thread-safe: the hub writes from pipe threads and the island reads on the UI thread.
/// </summary>
internal sealed class SessionWindowCache
{
    public const int Capacity = 64;
    private readonly List<(string Session, int? Owner)> _sessions = [];
    private readonly object _gate = new();

    /// <summary>Only what Claude Code sends as a session ID: letters, digits, '-' and '_', at most 128 characters.</summary>
    public static bool IsPlausible(string id) =>
        id.Length is > 0 and <= 128 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public bool Knows(string session)
    {
        lock (_gate)
        {
            return _sessions.Exists(entry => entry.Session == session);
        }
    }

    public int? Owner(string session)
    {
        lock (_gate)
        {
            return _sessions.Find(entry => entry.Session == session).Owner;
        }
    }

    public void Remember(string session, int? owner)
    {
        if (!IsPlausible(session))
        {
            return;
        }
        lock (_gate)
        {
            _sessions.RemoveAll(entry => entry.Session == session);
            _sessions.Add((session, owner));
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
}
