// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/App/StatusItemController.swift and the status-click handlers in
// Sources/Vorssaint/App/AppDelegate.swift (lines 83-98, 1155-1233)

using System.Windows;
using Faqra.App.About;
using Faqra.Core.Localization;
using Faqra.Core.Tray;
using Faqra.Win32.Display;
using Faqra.Win32.Icons;
using Faqra.Win32.Menus;
using Faqra.Win32.Shell;
using Faqra.Win32.Tray;

namespace Faqra.App.Tray;

/// <summary>Owns the main tray icon: its glyph, tooltip, clicks and context menu.</summary>
public sealed class StatusItemController : IDisposable
{
    // Stable identity so the shell remembers the icon's pinned slot across launches.
    private static readonly Guid MainIconGuid = new("6f1c3c0e-3b1a-4b62-9a7e-3f2a1f5d9c01");
    private const uint MainIconId = 1;

    // Actions with a working handler in this milestone; the rest render greyed out.
    private static readonly HashSet<TrayMenuAction> ImplementedActions =
    [
        TrayMenuAction.About,
        TrayMenuAction.Quit,
    ];

    private readonly TrayMessageWindow _window;
    private readonly TrayIcon _icon;
    private NativeIcon? _iconHandle;

    /// <summary>Set by the keep-awake service (milestone 5); drives the icon variant and tooltip.</summary>
    public bool KeepAwakeActive { get; private set; }

    public StatusItemController()
    {
        _window = new TrayMessageWindow();
        _window.Callback += OnCallback;
        _window.TaskbarCreated += OnTaskbarCreated;
        _icon = new TrayIcon(_window.Handle, MainIconId, MainIconGuid);
        L10n.Shared.Changed += OnLanguageChanged;
    }

    public void Show() => Refresh();

    /// <summary>Re-renders glyph and tooltip from current state. Cheap; safe to call often.</summary>
    public void Refresh()
    {
        var pixels = SystemMetrics.SmallIconPixels();
        var fresh = TrayIconBitmap.RenderIcon(pixels, KeepAwakeActive, TaskbarTheme.IsLight());
        var tooltip = StatusTooltip.For(KeepAwakeActive, null, L10n.Shared.S);
        _icon.Update(fresh.Handle, tooltip);
        _iconHandle?.Dispose();
        _iconHandle = fresh;
    }

    private void OnTaskbarCreated()
    {
        // Explorer restarted and dropped every icon; add ours back.
        _icon.MarkRemoved();
        Refresh();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Refresh();

    private void OnCallback(TrayCallback callback)
    {
        if (callback.IconId != MainIconId)
        {
            return;
        }
        if (callback.IsContextMenu)
        {
            ShowContextMenu(callback.X, callback.Y);
        }
        else if (callback.IsSelect)
        {
            OnSelect();
        }
    }

    private void OnSelect()
    {
        // Left click opens the popover panel (milestone 4).
    }

    private void ShowContextMenu(int x, int y)
    {
        var state = new TrayMenuState(KeepAwakeAvailable: true, KeepAwakeActive: KeepAwakeActive);
        var items = ContextMenuBuilder.Build(state, L10n.Shared.S);
        var (entries, lookup) = TrayMenuAdapter.ToEntries(items, ImplementedActions);
        var chosen = PopupMenu.Show(_window.Handle, x, y, entries);
        if (chosen != 0 && lookup.TryGetValue(chosen, out var item))
        {
            Dispatch(item);
        }
    }

    private static void Dispatch(TrayMenuItem item)
    {
        switch (item.Action)
        {
            case TrayMenuAction.About:
                AboutWindow.ShowSingleton();
                break;
            case TrayMenuAction.Quit:
                Application.Current.Shutdown();
                break;
            default:
                // Wired by later milestones (settings, keep awake, updates).
                break;
        }
    }

    public void Dispose()
    {
        L10n.Shared.Changed -= OnLanguageChanged;
        _icon.Dispose();
        _iconHandle?.Dispose();
        _iconHandle = null;
        _window.Dispose();
    }
}
