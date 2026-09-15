// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of NSMenu in Sources/Vorssaint/App/AppDelegate.swift presentContextMenu()

using Faqra.Win32.Native;

namespace Faqra.Win32.Menus;

/// <summary>A row of a native popup menu. Ids must be positive and unique across the whole tree.</summary>
public sealed record PopupMenuEntry(
    string Title,
    int Id = 0,
    bool Enabled = true,
    bool IsSeparator = false,
    IReadOnlyList<PopupMenuEntry>? Children = null);

/// <summary>Shows a Win32 popup menu at a screen point and returns the chosen id (0 when dismissed).</summary>
public static class PopupMenu
{
    public static int Show(IntPtr ownerWindow, int x, int y, IReadOnlyList<PopupMenuEntry> entries)
    {
        var menu = IntPtr.Zero;
        try
        {
            menu = Build(entries);
            // Without a foreground owner the menu never closes when the user clicks elsewhere
            // (Microsoft KB135788); the WM_NULL afterwards lets it retire cleanly.
            User32.SetForegroundWindow(ownerWindow);
            var flags = User32.TPM_RETURNCMD | User32.TPM_RIGHTBUTTON | User32.TPM_NONOTIFY;
            var chosen = User32.TrackPopupMenuEx(menu, flags, x, y, ownerWindow, IntPtr.Zero);
            User32.PostMessageW(ownerWindow, User32.WM_NULL, IntPtr.Zero, IntPtr.Zero);
            return chosen;
        }
        finally
        {
            if (menu != IntPtr.Zero)
            {
                User32.DestroyMenu(menu); // also destroys every attached submenu
            }
        }
    }

    private static IntPtr Build(IReadOnlyList<PopupMenuEntry> entries)
    {
        var menu = User32.CreatePopupMenu();
        try
        {
            foreach (var entry in entries)
            {
                Append(menu, entry);
            }
            return menu;
        }
        catch
        {
            User32.DestroyMenu(menu);
            throw;
        }
    }

    private static void Append(IntPtr menu, PopupMenuEntry entry)
    {
        if (entry.IsSeparator)
        {
            User32.AppendMenuW(menu, User32.MF_SEPARATOR, UIntPtr.Zero, null);
            return;
        }
        var flags = User32.MF_STRING | (entry.Enabled ? 0 : User32.MF_GRAYED);
        if (entry.Children is { Count: > 0 })
        {
            var submenu = Build(entry.Children);
            User32.AppendMenuW(menu, flags | User32.MF_POPUP, (UIntPtr)(ulong)submenu, entry.Title);
            return;
        }
        if (entry.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entry), entry.Id, "menu ids must be positive");
        }
        User32.AppendMenuW(menu, flags, (UIntPtr)(uint)entry.Id, entry.Title);
    }
}
