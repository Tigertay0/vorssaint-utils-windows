// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Ported from Coucou's session_window.rs (windows/src-tauri/src), https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé).

namespace Faqra.Core.Agents;

/// <summary>One process as a snapshot lists it: its parent and its executable's file name.</summary>
public readonly record struct ProcessEntry(int ParentId, string Executable);

/// <summary>
/// Where a session's window is. The relay is a child of Claude Code, which runs in the shell, terminal or editor whose
/// window the owner wants; walking up from the relay while it still runs finds that window's process.
/// </summary>
public static class ProcessTree
{
    /// <summary>How far up to look. A session sits a handful of levels below its window.</summary>
    public const int MaxDepth = 16;

    /// <summary>
    /// Never a session's window: the desktop shell and the services at the top of every tree. Reaching one means the
    /// terminal window was not an ancestor (a classic console window belongs to conhost).
    /// </summary>
    private static readonly HashSet<string> TreeTops = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "services.exe", "wininit.exe", "winlogon.exe", "svchost.exe",
        "smss.exe", "csrss.exe", "system", "sihost.exe", "userinit.exe",
    };

    /// <summary>
    /// The ancestors of <paramref name="start"/>, nearest first, stopping below the top of the tree. The start itself is
    /// left out. A loop in the parent links (a parent ID reused by a newer process) ends the walk.
    /// </summary>
    public static IReadOnlyList<int> Ancestors(IReadOnlyDictionary<int, ProcessEntry> table, int start)
    {
        var found = new List<int>();
        var seen = new HashSet<int> { start };
        var current = start;
        while (found.Count < MaxDepth && table.TryGetValue(current, out var entry))
        {
            var parent = entry.ParentId;
            if (parent == 0 || !seen.Add(parent) || !table.TryGetValue(parent, out var up) || TreeTops.Contains(up.Executable))
            {
                break;
            }
            found.Add(parent);
            current = parent;
        }
        return found;
    }

    /// <summary>The nearest ancestor that owns a window: the terminal or the editor.</summary>
    public static int? FirstOwner(IReadOnlyList<int> ancestors, Func<int, bool> ownsWindow)
    {
        foreach (var pid in ancestors)
        {
            if (ownsWindow(pid))
            {
                return pid;
            }
        }
        return null;
    }

    /// <summary>Which of a process's windows to bring forward: the one titled after the project, else the first; -1 for none.</summary>
    public static int PickWindow(IReadOnlyList<string> titles, string folder)
    {
        if (titles.Count == 0)
        {
            return -1;
        }
        if (folder.Length > 0)
        {
            for (var i = 0; i < titles.Count; i++)
            {
                if (titles[i].Contains(folder, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        return 0;
    }
}
