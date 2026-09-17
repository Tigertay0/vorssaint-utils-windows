// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/HotkeyManager.swift: one global shortcut that toggles keep awake,
// registered while the feature is installed and the shortcut is enabled.

using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Shortcuts;
using Faqra.Win32.Windows;

namespace Faqra.Services.KeepAwake;

public sealed class KeepAwakeHotkey : IDisposable
{
    private const int HotKeyId = 1;

    private readonly ISettingsStore _store;
    private readonly Func<bool> _featureAvailable;
    private readonly SystemEventsWindow _events;
    private readonly Action _toggle;
    private bool _registered;

    public KeepAwakeHotkey(ISettingsStore store, Func<bool> featureAvailable, SystemEventsWindow events, Action toggle)
    {
        _store = store;
        _featureAvailable = featureAvailable;
        _events = events;
        _toggle = toggle;
        _events.HotKeyPressed += OnHotKey;
        _store.Changed += OnSettingChanged;
    }

    /// <summary>True when the shortcut should be active but Windows refused it (another app holds it).</summary>
    public bool RegistrationFailed { get; private set; }

    /// <summary>The shortcut in effect: the stored one, or the default when the stored text is not a shortcut.</summary>
    public GlobalShortcut Shortcut => GlobalShortcut.Parse(_store.String(DefaultsKey.KeepAwakeShortcut)) ?? GlobalShortcut.KeepAwakeDefault;

    public event Action? RegistrationChanged;

    public void SyncWithPreferences()
    {
        Unregister();
        var wanted = _featureAvailable() && _store.Bool(DefaultsKey.HotkeyEnabled);
        if (wanted)
        {
            var shortcut = Shortcut;
            _registered = _events.RegisterHotKey(HotKeyId, (uint)shortcut.Modifiers, (uint)shortcut.VirtualKey);
        }
        var failed = wanted && !_registered;
        if (failed != RegistrationFailed)
        {
            RegistrationFailed = failed;
            RegistrationChanged?.Invoke();
        }
    }

    private void Unregister()
    {
        if (_registered)
        {
            _events.UnregisterHotKey(HotKeyId);
            _registered = false;
        }
    }

    private void OnHotKey(int id)
    {
        if (id == HotKeyId && _registered)
        {
            _toggle();
        }
    }

    private void OnSettingChanged(object? sender, Core.Defaults.SettingsChangedEventArgs e)
    {
        if (e.Key is DefaultsKey.HotkeyEnabled or DefaultsKey.KeepAwakeShortcut)
        {
            SyncWithPreferences();
        }
    }

    public void Dispose()
    {
        _store.Changed -= OnSettingChanged;
        _events.HotKeyPressed -= OnHotKey;
        Unregister();
    }
}
