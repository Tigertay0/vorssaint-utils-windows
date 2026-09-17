// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of Sources/Vorssaint/Services/HotkeyManager.swift, QuickToolHotkey.swift and
// ShortcutCapture.swift together: every global shortcut role registers here, re-registers when its
// preferences change, and goes quiet while a shortcut recorder listens.

using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Shortcuts;
using Faqra.Win32.Windows;

namespace Faqra.Services.Shortcuts;

/// <summary>What a role holds right now.</summary>
/// <param name="Registered">The combination Windows accepted, or null.</param>
/// <param name="Failed">The role should be active but Windows refused its shortcut.</param>
/// <param name="UsingFallback">The command bar holds Ctrl+Alt+Space because Alt+Space was taken.</param>
public readonly record struct HotKeyState(GlobalShortcut? Registered, bool Failed, bool UsingFallback);

public sealed class HotKeyRegistry : IDisposable
{
    private readonly ISettingsStore _store;
    private readonly Func<AppFeature, bool> _isInstalled;
    private readonly IHotKeyHost _host;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly Dictionary<GlobalShortcutRole, Action> _handlers = new();
    private readonly Dictionary<GlobalShortcutRole, HotKeyState> _states = new();
    private readonly HashSet<string> _watchedKeys;
    private int _suspendCount;

    public HotKeyRegistry(ISettingsStore store, Func<AppFeature, bool> isInstalled, IHotKeyHost host)
    {
        _store = store;
        _isInstalled = isInstalled;
        _host = host;
        _watchedKeys = Enum.GetValues<GlobalShortcutRole>()
            .SelectMany(r => r.RequiredEnableKeys().Append(r.StorageKey()))
            .ToHashSet();
        _host.HotKeyPressed += OnHotKey;
        _store.Changed += OnSettingChanged;
    }

    /// <summary>Raised when any role's registration state changed.</summary>
    public event Action? Changed;

    /// <summary>A bound handler threw; the registry kept running.</summary>
    public event Action<GlobalShortcutRole, Exception>? HandlerFailed;

    public bool IsSuspended => _suspendCount > 0;

    /// <summary>Stable per role; keep awake stays 1 so tools that post WM_HOTKEY 1 keep working.</summary>
    public static int HotKeyId(GlobalShortcutRole role) => (int)role + 1;

    public void Bind(GlobalShortcutRole role, Action handler) => _handlers[role] = handler;

    public HotKeyState State(GlobalShortcutRole role) => _states.GetValueOrDefault(role);

    /// <summary>Re-reads every bound role's preferences and registers what should be active.</summary>
    public void Sync()
    {
        foreach (var role in _handlers.Keys)
        {
            SyncRole(role);
        }
    }

    /// <summary>Silences every shortcut while a recorder listens; calls nest.</summary>
    public void Suspend()
    {
        if (_suspendCount++ > 0)
        {
            return;
        }
        foreach (var role in _handlers.Keys)
        {
            Unregister(role);
        }
    }

    public void Resume()
    {
        if (_suspendCount == 0 || --_suspendCount > 0)
        {
            return;
        }
        Sync();
    }

    private void SyncRole(GlobalShortcutRole role)
    {
        Unregister(role);
        if (IsSuspended)
        {
            return;
        }
        var wanted = _isInstalled(role.Feature()) && role.IsActive(_store);
        var next = new HotKeyState(null, false, false);
        if (wanted)
        {
            var shortcut = role.Saved(_store);
            if (TryRegister(role, shortcut))
            {
                next = new HotKeyState(shortcut, false, false);
            }
            else if (role == GlobalShortcutRole.CommandBar && shortcut == GlobalShortcut.CommandBarDefault
                && TryRegister(role, GlobalShortcut.CommandBarFallback))
            {
                next = new HotKeyState(GlobalShortcut.CommandBarFallback, false, true);
            }
            else
            {
                next = new HotKeyState(null, true, false);
            }
        }
        SetState(role, next);
    }

    private bool TryRegister(GlobalShortcutRole role, GlobalShortcut shortcut) =>
        _host.RegisterHotKey(HotKeyId(role), (uint)shortcut.Modifiers, (uint)shortcut.VirtualKey);

    private void Unregister(GlobalShortcutRole role)
    {
        var state = State(role);
        if (state.Registered is not null)
        {
            _host.UnregisterHotKey(HotKeyId(role));
            // Suspension keeps the failure flag: it describes the preferences, not the pause.
            _states[role] = state with { Registered = null, UsingFallback = false };
        }
    }

    private void SetState(GlobalShortcutRole role, HotKeyState next)
    {
        var previous = _states.GetValueOrDefault(role);
        _states[role] = next;
        if (previous != next)
        {
            Changed?.Invoke();
        }
    }

    private void OnHotKey(int id)
    {
        if (IsSuspended)
        {
            return;
        }
        foreach (var (role, handler) in _handlers)
        {
            if (HotKeyId(role) == id && State(role).Registered is not null)
            {
                // WM_HOTKEY arrives in a window procedure called from native code: an exception escaping here
                // ends the whole process, so one feature's failure must not take every other feature down.
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError($"Faqra: the {role} shortcut failed: {ex}");
                    HandlerFailed?.Invoke(role, ex);
                }
                return;
            }
        }
    }

    private void OnSettingChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (!_watchedKeys.Contains(e.Key))
        {
            return;
        }
        // Hot keys belong to the host window's thread; a setting written elsewhere re-registers there.
        if (_context is null || Environment.CurrentManagedThreadId == _ownerThreadId)
        {
            Sync();
        }
        else
        {
            _context.Post(_ => Sync(), null);
        }
    }

    public void Dispose()
    {
        _store.Changed -= OnSettingChanged;
        _host.HotKeyPressed -= OnHotKey;
        foreach (var role in _handlers.Keys)
        {
            Unregister(role);
        }
    }
}
