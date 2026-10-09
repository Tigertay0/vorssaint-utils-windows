// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/App/StatusItemController.swift and the status-click handlers in
// Sources/Vorssaint/App/AppDelegate.swift (lines 83-98, 1155-1233)

using System.Windows;
using System.Windows.Threading;
using Faqra.App.About;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.KeepAwake;
using Faqra.Core.Localization;
using Faqra.Core.Tray;
using Faqra.Services.KeepAwake;
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

    /// <summary>How soon to try again after the shell refused the icon (sign-in, display wake, Explorer restart).</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    // Actions with a working handler in this milestone; the rest render greyed out.
    private static readonly HashSet<TrayMenuAction> ImplementedActions =
    [
        TrayMenuAction.ToggleKeepAwake,
        TrayMenuAction.ActivateKeepAwake,
        TrayMenuAction.OpenSettings,
        TrayMenuAction.About,
        TrayMenuAction.Quit,
    ];

    private readonly TrayMessageWindow _window;
    private readonly TrayIcon _icon;
    private readonly ISettingsStore _store;
    private readonly KeepAwakeManager _keepAwake;
    private readonly Func<bool> _keepAwakeAvailable;
    private readonly DispatcherTimer _retry = new() { Interval = RetryDelay };
    private NativeIcon? _iconHandle;

    public bool KeepAwakeActive => _keepAwake.Session.IsActive;

    /// <summary>Raised on a left click or keyboard selection of the icon; the popover panel opens from it.</summary>
    public event Action? Selected;

    /// <summary>The icon's screen rectangle in physical pixels, or null while it sits in the overflow flyout.</summary>
    public Core.Panel.PixelRect? IconRect() =>
        _icon.TryGetRect(out var rect) ? new Core.Panel.PixelRect(rect.Left, rect.Top, rect.Right, rect.Bottom) : null;

    public StatusItemController(ISettingsStore store, KeepAwakeManager keepAwake, Func<bool> keepAwakeAvailable)
    {
        _store = store;
        _keepAwake = keepAwake;
        _keepAwakeAvailable = keepAwakeAvailable;
        _keepAwake.Changed += Refresh;
        _keepAwake.SessionEnded += OnSessionEnded;
        _store.Changed += OnSettingChanged;
        _window = new TrayMessageWindow();
        _window.Callback += OnCallback;
        _window.TaskbarCreated += OnTaskbarCreated;
        _icon = new TrayIcon(_window.Handle, MainIconId, MainIconGuid);
        _retry.Tick += OnRetry;
        L10n.Shared.Changed += OnLanguageChanged;
    }

    public void Show() => Refresh();

    /// <summary>Re-renders glyph and tooltip from current state. Cheap; safe to call often.</summary>
    public void Refresh()
    {
        var pixels = SystemMetrics.SmallIconPixels();
        var fresh = TrayIconBitmap.RenderIcon(
            pixels, KeepAwakeActive, TaskbarTheme.IsLight(),
            DefaultsSanitizers.ActiveIcon(_store.String(DefaultsKey.KeepAwakeActiveIcon)),
            DefaultsSanitizers.IconTint(_store.String(DefaultsKey.KeepAwakeIconTint)));
        var session = _keepAwake.Session;
        var tooltip = KeepAwakeFormat.Tooltip(
            session.IsActive, session.Trigger, session.ActiveConditions,
            session.EndsAt is { } end ? KeepAwakeFormat.ShortTime(end) : null,
            L10n.Shared.S, KeepAwakeStrings.For(L10n.Shared.Language));
        var shown = _icon.Update(fresh.Handle, tooltip);
        _iconHandle?.Dispose();
        _iconHandle = fresh;
        if (shown)
        {
            _retry.Stop();
        }
        else
        {
            _retry.Start();
        }
    }

    private void OnRetry(object? sender, EventArgs e) => Refresh();

    private void OnTaskbarCreated()
    {
        // Explorer restarted and dropped every icon; add ours back.
        _icon.MarkRemoved();
        Refresh();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Refresh();

    private void OnSettingChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.Key is DefaultsKey.KeepAwakeActiveIcon or DefaultsKey.KeepAwakeIconTint)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(Refresh);
        }
    }

    /// <summary>Upstream posts a notification when a session ends on its own; manual and quit endings are silent.</summary>
    private void OnSessionEnded(KeepAwakeEndReason reason)
    {
        var s = KeepAwakeStrings.For(L10n.Shared.Language);
        var (title, body) = reason == KeepAwakeEndReason.Battery
            ? (s.BatteryTitle, s.BatteryBody)
            : (s.SessionEndedTitle, s.SessionEndedBody);
        _icon.ShowNotification(title, body);
    }

    private void OnCallback(TrayCallback callback)
    {
        if (callback.IconId != MainIconId)
        {
            return;
        }
        if (callback.IsContextMenu)
        {
            if (_keepAwakeAvailable() && _store.Bool(DefaultsKey.KeepAwakeRightClickToggle))
            {
                _keepAwake.Session.Toggle();
            }
            else
            {
                ShowContextMenu(callback.X, callback.Y);
            }
        }
        else if (callback.IsSelect)
        {
            OnSelect();
        }
    }

    private void OnSelect() => Selected?.Invoke();

    private void ShowContextMenu(int x, int y)
    {
        var state = new TrayMenuState(KeepAwakeAvailable: _keepAwakeAvailable(), KeepAwakeActive: KeepAwakeActive);
        var items = ContextMenuBuilder.Build(state, L10n.Shared.S);
        var (entries, lookup) = TrayMenuAdapter.ToEntries(items, ImplementedActions);
        var chosen = PopupMenu.Show(_window.Handle, x, y, entries);
        if (chosen != 0 && lookup.TryGetValue(chosen, out var item))
        {
            Dispatch(item);
        }
    }

    private void Dispatch(TrayMenuItem item)
    {
        switch (item.Action)
        {
            case TrayMenuAction.ToggleKeepAwake:
                _keepAwake.Session.Toggle();
                break;
            case TrayMenuAction.ActivateKeepAwake:
                _keepAwake.Session.Activate(item.Minutes);
                break;
            case TrayMenuAction.OpenSettings:
                App.ShowSettings();
                break;
            case TrayMenuAction.About:
                AboutWindow.ShowSingleton();
                break;
            case TrayMenuAction.Quit:
                Application.Current.Shutdown();
                break;
            default:
                // Wired by later milestones (updates, cleaning mode, uninstaller, shelf).
                break;
        }
    }

    public void Dispose()
    {
        _retry.Stop();
        _retry.Tick -= OnRetry;
        _keepAwake.Changed -= Refresh;
        _keepAwake.SessionEnded -= OnSessionEnded;
        _store.Changed -= OnSettingChanged;
        L10n.Shared.Changed -= OnLanguageChanged;
        _icon.Dispose();
        _iconHandle?.Dispose();
        _iconHandle = null;
        _window.Dispose();
    }
}
