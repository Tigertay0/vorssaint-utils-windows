// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/App/AppDelegate.swift presentContextMenu() (lines 1155-1233)

using Faqra.Core.Localization;

namespace Faqra.Core.Tray;

/// <summary>Builds the tray context menu in upstream's exact order from a snapshot of app state.</summary>
public static class ContextMenuBuilder
{
    /// <summary>Keep-awake presets in menu order; 0 means indefinitely.</summary>
    public static readonly IReadOnlyList<int> DurationMinutes = [15, 30, 60, 120, 240, 480, 0];

    public static IReadOnlyList<TrayMenuItem> Build(TrayMenuState state, Strings s)
    {
        var items = new List<TrayMenuItem>();

        if (state.KeepAwakeAvailable)
        {
            var title = state.KeepAwakeActive ? s.MenuDisableAwake : s.MenuEnableAwake;
            items.Add(new TrayMenuItem(title, TrayMenuAction.ToggleKeepAwake));
        }
        if (state.KeepAwakeAvailable && !state.KeepAwakeActive)
        {
            items.Add(new TrayMenuItem(s.MenuActivateFor, Children: Durations(s)));
        }
        if (state.CleaningModeAvailable)
        {
            items.Add(new TrayMenuItem(s.CleaningMenuItem, TrayMenuAction.CleaningMode));
        }
        if (items.Count > 0)
        {
            items.Add(TrayMenuItem.Separator);
        }

        items.Add(new TrayMenuItem(s.MenuSettings, TrayMenuAction.OpenSettings));
        items.Add(new TrayMenuItem(s.MenuAbout, TrayMenuAction.About));
        if (state.UninstallerAvailable)
        {
            items.Add(new TrayMenuItem(s.UninstallerMenuItem, TrayMenuAction.Uninstaller));
        }
        if (state.ShelfAvailable && state.ShelfEnabled)
        {
            items.Add(new TrayMenuItem(s.ShelfMenuItem, TrayMenuAction.Shelf));
        }
        items.Add(new TrayMenuItem(s.MenuCheckUpdates, TrayMenuAction.CheckUpdates));
        items.Add(TrayMenuItem.Separator);
        items.Add(new TrayMenuItem(s.MenuQuit, TrayMenuAction.Quit));
        return items;
    }

    public static string DurationTitle(int minutes, Strings s) => minutes switch
    {
        15 => s.Minutes15,
        30 => s.Minutes30,
        60 => s.Hour1,
        120 => s.Hours2,
        240 => s.Hours4,
        480 => s.Hours8,
        0 => s.Indefinitely,
        _ => throw new ArgumentOutOfRangeException(nameof(minutes), minutes, "not a menu preset"),
    };

    private static IReadOnlyList<TrayMenuItem> Durations(Strings s) =>
        DurationMinutes
            .Select(minutes => new TrayMenuItem(DurationTitle(minutes, s), TrayMenuAction.ActivateKeepAwake, minutes))
            .ToList();
}
