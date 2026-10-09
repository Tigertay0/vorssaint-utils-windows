// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// The review-before-write flow follows Coucou's settings window (windows/src-tauri/src/config_file.rs),
// https://github.com/Louis-CFM/coucou (MIT License, Copyright (c) 2026 Louis Raillé).

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Faqra.Core;
using Faqra.Core.Agents.Install;
using Faqra.Core.Localization;
using Faqra.Services.Agents;
using Wpf.Ui.Controls;

namespace Faqra.App.Settings;

/// <summary>
/// Shows the exact edit to Claude's settings and writes it only on confirm, after checking the file did
/// not change in the meantime. The old file is kept byte for byte as the backup.
/// </summary>
public sealed class ConfigReviewWindow : FluentWindow
{
    public enum Mode { Install, Remove }

    private static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas");

    private readonly Mode _mode;
    private readonly string _settingsPath;
    private readonly string _relayPath;
    private readonly AgentsStrings _s = AgentsStrings.For(L10n.Shared.Language);
    private readonly System.Windows.Controls.CheckBox _removeCoucou = new();
    private readonly System.Windows.Controls.TextBlock _backup = new() { TextWrapping = TextWrapping.Wrap };
    private readonly System.Windows.Controls.TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly StackPanel _diff = new();
    private readonly Wpf.Ui.Controls.Button _confirm = new() { Appearance = ControlAppearance.Primary, Margin = new Thickness(8, 0, 0, 0) };
    private ConfigPreview? _preview;

    internal ConfigReviewWindow(Mode mode, string settingsPath, string relayPath, int coucouEvents)
    {
        _mode = mode;
        _settingsPath = settingsPath;
        _relayPath = relayPath;
        Title = _s.ReviewTitle;
        Width = 760;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ExtendsContentIntoTitleBar = false;

        var page = new DockPanel { Margin = new Thickness(24) };
        var top = new StackPanel();
        top.Children.Add(Text(_s.ReviewTitle, "PageTitle"));
        _backup.Style = (Style)Application.Current.Resources["Caption"];
        top.Children.Add(_backup);
        if (coucouEvents > 0)
        {
            _removeCoucou.Content = _s.ReviewRemoveCoucou;
            _removeCoucou.IsChecked = true;
            _removeCoucou.Margin = new Thickness(0, 8, 0, 0);
            _removeCoucou.Click += (_, _) => Refresh();
            top.Children.Add(_removeCoucou);
        }
        DockPanel.SetDock(top, Dock.Top);
        page.Children.Add(top);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Wpf.Ui.Controls.Button { Content = _s.ReviewCancel, Appearance = ControlAppearance.Secondary };
        cancel.Click += (_, _) => { DialogResult = false; };
        _confirm.Content = mode == Mode.Install ? _s.ReviewConfirmInstall : _s.ReviewConfirmRemove;
        _confirm.Click += (_, _) => Confirm();
        buttons.Children.Add(cancel);
        buttons.Children.Add(_confirm);
        var bottom = new StackPanel();
        bottom.Children.Add(_message);
        bottom.Children.Add(buttons);
        DockPanel.SetDock(bottom, Dock.Bottom);
        page.Children.Add(bottom);

        page.Children.Add(new Border
        {
            Margin = new Thickness(0, 12, 0, 0),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            Child = new ScrollViewer
            {
                Content = _diff,
                Padding = new Thickness(12),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
        });
        Content = page;
        Refresh();
    }

    /// <summary>Opens the review; true when the edit was written.</summary>
    public static bool Review(Window? owner, Mode mode, string settingsPath, string relayPath, int coucouEvents)
    {
        var window = new ConfigReviewWindow(mode, settingsPath, relayPath, coucouEvents) { Owner = owner };
        return window.ShowDialog() == true;
    }

    private string Transform(string? json) => _mode == Mode.Install
        ? ClaudeHookConfig.Install(json, _relayPath, _removeCoucou.IsChecked == true)
        : ClaudeHookConfig.Uninstall(json, _removeCoucou.IsChecked == true);

    /// <summary>True when the confirm button can be clicked.</summary>
    internal bool CanConfirm => _confirm.IsEnabled;

    /// <summary>Ticks or unticks the Coucou cleanup box and recomputes the preview.</summary>
    internal void SetRemoveCoucou(bool value)
    {
        _removeCoucou.IsChecked = value;
        Refresh();
    }

    private bool Refresh()
    {
        try
        {
            _preview = ConfigFile.Preview(_settingsPath, Transform, DateTime.Now);
        }
        catch (Exception ex) when (ex is ConfigFormatException or IOException or UnauthorizedAccessException)
        {
            _preview = null;
            _diff.Children.Clear();
            _backup.Text = string.Empty;
            var format = ex is ConfigFormatException ? _s.FileUnreadableFormat : _s.WriteFailedFormat;
            Show(string.Format(System.Globalization.CultureInfo.CurrentCulture, format, ex.Message), isError: true);
            _confirm.IsEnabled = false;
            return false;
        }
        Show(string.Empty, isError: false);
        _backup.Text = _preview.BackupPath is { } backup
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, _s.ReviewBackupFormat, backup)
            : _s.ReviewNewFile;
        _diff.Children.Clear();
        if (!_preview.Changes)
        {
            _diff.Children.Add(Line(_s.ReviewNoChange, UnifiedDiff.LineKind.Context));
        }
        foreach (var line in _preview.Diff)
        {
            var prefix = line.Kind switch { UnifiedDiff.LineKind.Removed => "-", UnifiedDiff.LineKind.Added => "+", UnifiedDiff.LineKind.Context => " ", _ => string.Empty };
            _diff.Children.Add(Line(prefix + line.Text, line.Kind));
        }
        _confirm.IsEnabled = _preview.Changes;
        return true;
    }

    private void Confirm()
    {
        if (TryApply())
        {
            DialogResult = true;
        }
    }

    /// <summary>Writes the previewed edit; false when nothing was written (file changed, or a write error).</summary>
    internal bool TryApply()
    {
        if (_preview is null)
        {
            return false;
        }
        try
        {
            ConfigFile.Apply(_preview);
            return true;
        }
        catch (ConfigChangedException)
        {
            if (Refresh())
            {
                Show(_s.FileChanged, isError: false);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Show(string.Format(System.Globalization.CultureInfo.CurrentCulture, _s.WriteFailedFormat, ex.Message), isError: true);
        }
        return false;
    }

    private void Show(string text, bool isError)
    {
        _message.Text = text;
        _message.Foreground = (Brush)Application.Current.Resources[isError ? "SystemFillColorCriticalBrush" : "TextFillColorSecondaryBrush"];
    }

    private static System.Windows.Controls.TextBlock Line(string text, UnifiedDiff.LineKind kind) => new()
    {
        Text = text,
        FontFamily = MonoFont,
        FontSize = 12,
        Foreground = (Brush)Application.Current.Resources[kind == UnifiedDiff.LineKind.Hunk ? "TextFillColorTertiaryBrush" : "TextFillColorPrimaryBrush"],
        Background = kind switch
        {
            UnifiedDiff.LineKind.Added => (Brush)Application.Current.Resources["SystemFillColorSuccessBackgroundBrush"],
            UnifiedDiff.LineKind.Removed => (Brush)Application.Current.Resources["SystemFillColorCriticalBackgroundBrush"],
            _ => Brushes.Transparent,
        },
    };

    private static System.Windows.Controls.TextBlock Text(string text, string style) =>
        new() { Text = text, Style = (Style)Application.Current.Resources[style] };
}
