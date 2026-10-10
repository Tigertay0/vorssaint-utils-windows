// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of Coucou's Settings → Claude Code → Install hooks (windows/README.md, "Claude Code"),
// https://github.com/Louis-CFM/coucou (MIT License, Copyright (c) 2026 Louis Raillé).

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Faqra.Core;
using Faqra.Core.Agents.Install;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;
using Wpf.Ui.Controls;

namespace Faqra.App.Settings.Pages;

/// <summary>Whether Claude Code's hooks are installed, and the reviewed install and removal.</summary>
public sealed class AgentsPage : UserControl
{
    private readonly string _settingsPath;
    private readonly string _relayPath;
    private readonly AgentsStrings _s = AgentsStrings.For(L10n.Shared.Language);
    private readonly StackPanel _page = new();

    public AgentsPage()
        : this(AppPaths.ClaudeSettingsFile, AppPaths.AgentsRelayFile)
    {
    }

    internal AgentsPage(string settingsPath, string relayPath)
    {
        _settingsPath = settingsPath;
        _relayPath = relayPath;
        Content = _page;
        Build(note: null);
    }

    private void Build(string? note)
    {
        _page.Children.Clear();
        _page.Children.Add(Text(L10n.Shared.S.SettingsPageTitles[Core.Settings.SettingsPage.Agents], "PageTitle"));
        var caption = Text(_s.PageCaption, "Caption");
        caption.TextWrapping = TextWrapping.Wrap;
        caption.MaxWidth = 560;
        caption.HorizontalAlignment = HorizontalAlignment.Left;
        _page.Children.Add(caption);
        _page.Children.Add(Text(_s.HooksSection, "SectionHeader"));

        HookStatus status;
        string? problem = null;
        try
        {
            status = ClaudeHookConfig.Inspect(File.Exists(_settingsPath) ? ConfigEdit.Decode(File.ReadAllBytes(_settingsPath)) : null);
        }
        catch (ConfigFormatException ex)
        {
            status = new HookStatus(0, 0);
            problem = Format(_s.FileUnreadableFormat, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            status = new HookStatus(0, 0);
            problem = ex.Message;
        }

        var title = status.Installed ? _s.HooksInstalled : status.FaqraEvents > 0 ? _s.HooksOutdated : _s.HooksNotInstalled;
        var header = new StackPanel();
        header.Children.Add(Text(title, "Body"));
        header.Children.Add(Text(Format(_s.HooksCaptionFormat, _settingsPath), "Caption"));

        var relayReady = File.Exists(_relayPath);
        var action = new Wpf.Ui.Controls.Button
        {
            Content = status.Installed ? _s.ReviewRemove : _s.ReviewInstall,
            Appearance = status.Installed ? ControlAppearance.Secondary : ControlAppearance.Primary,
            IsEnabled = problem is null && (status.Installed || relayReady),
        };
        action.Click += (_, _) =>
        {
            var mode = status.Installed ? ConfigReviewWindow.Mode.Remove : ConfigReviewWindow.Mode.Install;
            if (ConfigReviewWindow.Review(Window.GetWindow(this), mode, _settingsPath, _relayPath, status.CoucouEvents))
            {
                Build(mode == ConfigReviewWindow.Mode.Install ? _s.Installed : _s.Removed);
            }
        };
        _page.Children.Add(new CardControl
        {
            Icon = new SymbolIcon(SymbolRegular.Bot24),
            Header = header,
            Content = action,
            Margin = new Thickness(0, 0, 0, 0),
        });

        foreach (var line in new[]
        {
            status.CoucouEvents > 0 ? Format(_s.CoucouLeftoversFormat, status.CoucouEvents) : null,
            status.CoucouEvents > 0 ? _s.WatchOnly : null,
            !relayReady && !status.Installed ? _s.RelayMissing : null,
            problem,
            note,
        })
        {
            if (line is null)
            {
                continue;
            }
            var text = Text(line, "Caption");
            text.TextWrapping = TextWrapping.Wrap;
            text.Margin = new Thickness(0, 8, 0, 0);
            text.MaxWidth = 560;
            text.HorizontalAlignment = HorizontalAlignment.Left;
            _page.Children.Add(text);
        }

        _page.Children.Add(Text(_s.AlertsSection, "SectionHeader"));
        _page.Children.Add(Toggle(SymbolRegular.Alert24, _s.NeedsYouSound, _s.NeedsYouSoundCaption, DefaultsKey.FaqraAgentsNeedsYouSound, top: 0));
        _page.Children.Add(Toggle(SymbolRegular.CheckmarkCircle24, _s.AnsweredSound, _s.AnsweredSoundCaption, DefaultsKey.FaqraAgentsAnsweredSound, top: 6));
        _page.Children.Add(Toggle(SymbolRegular.Chat24, _s.OpenOnAnswer, _s.OpenOnAnswerCaption, DefaultsKey.FaqraAgentsOpenOnAnswer, top: 6));
        _page.Children.Add(Toggle(SymbolRegular.Keyboard24, _s.ShortcutsToggle, _s.ShortcutsCaption, DefaultsKey.FaqraAgentsShortcutsEnabled, top: 6));
    }

    /// <summary>A switch bound to one setting, written the moment it flips.</summary>
    private static CardControl Toggle(SymbolRegular icon, string title, string caption, string key, double top)
    {
        var store = AppServices.Current.Store;
        var toggle = new ToggleSwitch { IsChecked = store.Bool(key) };
        System.Windows.Automation.AutomationProperties.SetName(toggle, title);
        toggle.Click += (_, _) => store.Set(key, toggle.IsChecked == true);
        var header = new StackPanel();
        header.Children.Add(Text(title, "Body"));
        var note = Text(caption, "Caption");
        note.TextWrapping = TextWrapping.Wrap;
        header.Children.Add(note);
        return new CardControl { Icon = new SymbolIcon(icon), Header = header, Content = toggle, Margin = new Thickness(0, top, 0, 0) };
    }

    private static string Format(string format, object value) => string.Format(CultureInfo.CurrentCulture, format, value);

    private static System.Windows.Controls.TextBlock Text(string text, string style) =>
        new() { Text = text, Style = (Style)Application.Current.Resources[style] };
}
