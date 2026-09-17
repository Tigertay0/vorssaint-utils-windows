// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of Sources/Vorssaint/Core/SymbolicHotKeys.swift and conflictsWithSystemShortcut
// (GlobalShortcut.swift:1032-1055). Windows has no readable table of its own shortcuts, so this lists
// the combinations the recorder refuses up front; everything else is caught by RegisterHotKey failing.

namespace Faqra.Core.Shortcuts;

public static class WindowsReservedShortcuts
{
    private const ShortcutModifiers Ctrl = ShortcutModifiers.Control;
    private const ShortcutModifiers Alt = ShortcutModifiers.Alt;
    private const ShortcutModifiers Shift = ShortcutModifiers.Shift;
    private const ShortcutModifiers Win = ShortcutModifiers.Win;

    // Taken by the system below RegisterHotKey (Win+L, Ctrl+Alt+Del, the task switchers) or claimable
    // but destructive to take (Task Manager, closing a window).
    private static readonly HashSet<GlobalShortcut> Reserved =
    [
        new(0x4C, Win),                 // Win+L lock
        new(0x2E, Ctrl | Alt),          // Ctrl+Alt+Delete
        new(0x09, Alt),                 // Alt+Tab
        new(0x09, Alt | Shift),
        new(0x09, Ctrl | Alt),
        new(0x09, Win),                 // Win+Tab task view
        new(0x1B, Ctrl | Shift),        // Ctrl+Shift+Esc Task Manager
        new(0x1B, Alt),                 // Alt+Esc
        new(0x1B, Ctrl),                // Ctrl+Esc Start
        new(0x73, Alt),                 // Alt+F4
    ];

    public static bool IsReserved(GlobalShortcut shortcut) => Reserved.Contains(shortcut);
}
