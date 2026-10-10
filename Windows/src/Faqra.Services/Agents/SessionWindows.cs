// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the window lookup in Coucou's platform/windows.rs, https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé).

using Faqra.Core.Agents;
using Faqra.Win32.Agents;
using Faqra.Win32.Windows;

namespace Faqra.Services.Agents;

/// <summary>What looking for a session's window found. Not settled when the relay had already exited: look again on its next event.</summary>
public readonly record struct WindowLookup(bool Settled, int? Owner);

/// <summary>"Go to window": finds the process that owns a session's window, and brings that window forward.</summary>
public static class SessionWindows
{
    /// <summary>The nearest ancestor of the relay that owns a window. Settled with no owner for a classic console.</summary>
    public static WindowLookup Locate(int relayPid)
    {
        var table = new Dictionary<int, ProcessEntry>();
        foreach (var process in ProcessSnapshot.Take())
        {
            table[process.Id] = new ProcessEntry(process.ParentId, process.Executable);
        }
        var ancestors = ProcessTree.Ancestors(table, relayPid);
        if (ancestors.Count == 0)
        {
            return new WindowLookup(false, null);
        }
        var owners = OpenWindows.Enumerate().Select(window => window.ProcessId).ToHashSet();
        return new WindowLookup(true, ProcessTree.FirstOwner(ancestors, owners.Contains));
    }

    /// <summary>
    /// Brings forward the owner's window titled after the project, else its first. Windows allows it because the owner's
    /// click on the island, or the shortcut, was the last input.
    /// </summary>
    public static bool BringForward(int ownerPid, string project)
    {
        var windows = OpenWindows.Enumerate().Where(window => window.ProcessId == ownerPid).ToList();
        var index = ProcessTree.PickWindow(windows.Select(window => window.Title).ToList(), project);
        return index >= 0 && OpenWindows.Activate(windows[index].Handle);
    }
}
