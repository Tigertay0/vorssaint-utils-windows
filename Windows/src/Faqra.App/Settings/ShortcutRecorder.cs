// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors ShortcutPreferenceRow and RecorderButton in Sources/Vorssaint/UI/ShortcutRecorderButton.swift:
// a key-cap button that records a role's global shortcut, a Reset button, and one message line for the
// recording hint or the reason a combination was refused. Recording silences every global shortcut
// (ShortcutCapture.swift) so the keys land here instead of running their feature.

using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;
using Faqra.Core.Shortcuts;
using Faqra.Services.Shortcuts;
using Wpf.Ui.Controls;

namespace Faqra.App.Settings;

public sealed class ShortcutRecorder : StackPanel
{
    private const double KeyCapWidth = 150;
    private const double MessageMaxWidth = 280;

    private readonly GlobalShortcutRole _role;
    private readonly ISettingsStore _store;
    private readonly HotKeyRegistry? _hotKeys;
    private readonly Func<Core.Features.AppFeature, bool> _isInstalled;
    private readonly Func<GlobalShortcutRole, string> _roleTitle;
    private readonly ShortcutStrings _s = ShortcutStrings.For(L10n.Shared.Language);
    private readonly Wpf.Ui.Controls.Button _keyCap;
    private readonly Wpf.Ui.Controls.Button _reset;
    private readonly System.Windows.Controls.TextBlock _message;
    private bool _recording;
    private bool _showingHint;

    public ShortcutRecorder(GlobalShortcutRole role, ISettingsStore store, HotKeyRegistry? hotKeys,
        Func<Core.Features.AppFeature, bool> isInstalled, Func<GlobalShortcutRole, string> roleTitle)
    {
        _role = role;
        _store = store;
        _hotKeys = hotKeys;
        _isInstalled = isInstalled;
        _roleTitle = roleTitle;
        HorizontalAlignment = HorizontalAlignment.Right;

        _keyCap = new Wpf.Ui.Controls.Button { Width = KeyCapWidth, Appearance = ControlAppearance.Secondary };
        _keyCap.SetResourceReference(Control.FontFamilyProperty, "FaqraMonoFont");
        _keyCap.Click += (_, _) => BeginRecording();
        _keyCap.PreviewKeyDown += OnPreviewKeyDown;
        _keyCap.LostKeyboardFocus += (_, _) => StopRecording();
        AutomationPropertiesName(_keyCap, roleTitle(role));

        _reset = new Wpf.Ui.Controls.Button { Content = _s.Reset, Appearance = ControlAppearance.Transparent, Margin = new Thickness(6, 0, 0, 0) };
        _reset.Click += (_, _) =>
        {
            StopRecording();
            Save(role.DefaultShortcut());
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        row.Children.Add(_keyCap);
        row.Children.Add(_reset);
        Children.Add(row);

        _message = new System.Windows.Controls.TextBlock
        {
            Style = (Style)Application.Current.Resources["Caption"],
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Right,
            MaxWidth = MessageMaxWidth,
            Margin = new Thickness(0, 4, 0, 0),
            Visibility = Visibility.Collapsed,
        };
        Children.Add(_message);

        Unloaded += (_, _) => StopRecording();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
            {
                StopRecording();
            }
        };
        if (_hotKeys is not null)
        {
            _hotKeys.Changed += Refresh;
            Unloaded += (_, _) => _hotKeys.Changed -= Refresh;
        }
        Refresh();
    }

    public bool IsRecording => _recording;

    private GlobalShortcut Current => _role.Saved(_store);

    private void BeginRecording()
    {
        if (_recording)
        {
            return;
        }
        // Take the keyboard first: a recorder that thinks it listens while focus is elsewhere would
        // silence every shortcut and never hear the keys that end it (ShortcutRecorderButton.swift:128-133).
        if (!_keyCap.Focus())
        {
            return;
        }
        _recording = true;
        _hotKeys?.Suspend();
        ShowMessage($"{_s.Recording} {_s.EscapeHint}", error: false);
        Refresh();
    }

    private void StopRecording()
    {
        if (!_recording)
        {
            return;
        }
        _recording = false;
        if (_showingHint)
        {
            ClearMessage();
        }
        _hotKeys?.Resume();
        Refresh();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recording)
        {
            return;
        }
        e.Handled = true;
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };
        var result = ShortcutRecording.Handle(KeyInterop.VirtualKeyFromKey(key), HeldModifiers());
        switch (result.Outcome)
        {
            case RecordingOutcome.Waiting:
                return;
            case RecordingOutcome.Cancel:
                ClearMessage();
                StopRecording();
                return;
            case RecordingOutcome.Invalid:
                SystemSounds.Beep.Play();
                ShowMessage(_s.Invalid, error: true);
                return;
            case RecordingOutcome.Reserved:
                SystemSounds.Beep.Play();
                ShowMessage(_s.Reserved, error: true);
                return;
        }

        var shortcut = result.Shortcut!.Value;
        StopRecording();
        var conflict = _role.Conflict(shortcut, _store, _isInstalled, includeInactive: true);
        if (conflict is { } other)
        {
            SystemSounds.Beep.Play();
            ShowMessage(string.Format(_s.ConflictFormat, _roleTitle(other)), error: true);
            return;
        }
        Save(shortcut);
    }

    private void Save(GlobalShortcut shortcut)
    {
        ClearMessage();
        _store.Set(_role.StorageKey(), shortcut.StorageValue);
        Refresh();
    }

    /// <summary>Re-reads the stored shortcut and whether Windows accepted it.</summary>
    private void Refresh()
    {
        _keyCap.Content = _recording ? _s.PressKeys : Current.DisplayText;
        _keyCap.Appearance = _recording ? ControlAppearance.Primary : ControlAppearance.Secondary;
        _reset.IsEnabled = Current != _role.DefaultShortcut();
        if (_recording)
        {
            return;
        }
        if (_hotKeys?.State(_role).Failed == true)
        {
            ShowMessage(_s.Unavailable, error: true);
        }
        else if (_message.Text == _s.Unavailable)
        {
            ClearMessage();
        }
    }

    private void ShowMessage(string text, bool error)
    {
        _message.Text = text;
        _showingHint = !error;
        _message.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty,
            error ? "SystemFillColorCautionBrush" : "TextFillColorSecondaryBrush");
        _message.Visibility = Visibility.Visible;
    }

    private void ClearMessage()
    {
        _message.Text = string.Empty;
        _showingHint = false;
        _message.Visibility = Visibility.Collapsed;
    }

    private static ShortcutModifiers HeldModifiers()
    {
        var held = ShortcutModifiers.None;
        var wpf = Keyboard.Modifiers;
        if (wpf.HasFlag(ModifierKeys.Control)) held |= ShortcutModifiers.Control;
        if (wpf.HasFlag(ModifierKeys.Alt)) held |= ShortcutModifiers.Alt;
        if (wpf.HasFlag(ModifierKeys.Shift)) held |= ShortcutModifiers.Shift;
        if (wpf.HasFlag(ModifierKeys.Windows) || Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) held |= ShortcutModifiers.Win;
        return held;
    }

    private static void AutomationPropertiesName(UIElement element, string name) =>
        System.Windows.Automation.AutomationProperties.SetName(element, name);
}
