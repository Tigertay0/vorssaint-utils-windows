// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the menu model implied by Sources/Vorssaint/App/AppDelegate.swift (presentContextMenu)

namespace Faqra.Core.Tray;

/// <summary>What a tray menu item does when chosen. Stable identifiers, not UI text.</summary>
public enum TrayMenuAction
{
    ToggleKeepAwake,
    ActivateKeepAwake,
    CleaningMode,
    OpenSettings,
    About,
    Uninstaller,
    Shelf,
    CheckUpdates,
    Quit,
}

/// <summary>One row of the tray context menu. Immutable; the builder returns a fresh list each time.</summary>
public sealed record TrayMenuItem(
    string Title,
    TrayMenuAction? Action = null,
    int Minutes = 0,
    IReadOnlyList<TrayMenuItem>? Children = null,
    bool IsSeparator = false)
{
    public static TrayMenuItem Separator { get; } = new(string.Empty, IsSeparator: true);

    public bool HasChildren => Children is { Count: > 0 };
}

/// <summary>Everything the menu's shape depends on, captured at the moment of the right click.</summary>
public sealed record TrayMenuState(
    bool KeepAwakeAvailable,
    bool KeepAwakeActive,
    bool CleaningModeAvailable = false,
    bool UninstallerAvailable = false,
    bool ShelfAvailable = false,
    bool ShelfEnabled = false);
