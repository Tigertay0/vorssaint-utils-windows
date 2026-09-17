// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the header section of Sources/Vorssaint/UI/Settings/CommandBarSettings.swift (lines 41-100): open the
// bar, what it does, the global shortcut switch and recorder, and forgetting what was learned. The sources,
// files, links, pins, names and hidden rows sections arrive with those parts of the bar.

using System.Windows;
using System.Windows.Controls;
using Faqra.Core.CommandBar;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;
using Faqra.Core.Shortcuts;
using Wpf.Ui.Controls;

namespace Faqra.App.Settings.Pages;

public sealed class CommandBarPage : UserControl
{
    private readonly CommandBarStrings _bar = CommandBarStrings.For(L10n.Shared.Language);
    private readonly System.Windows.Controls.TextBlock _fallbackNotice;

    public CommandBarPage()
    {
        var services = AppServices.Current;
        var hub = FeatureHubStrings.For(L10n.Shared.Language);
        var page = new StackPanel();
        page.Children.Add(Text(_bar.PageTitle, "PageTitle"));
        var caption = Text(_bar.SettingsCaption, "Caption");
        caption.TextWrapping = TextWrapping.Wrap;
        page.Children.Add(caption);
        var privacy = Text(_bar.PrivacyNote, "Caption");
        privacy.TextWrapping = TextWrapping.Wrap;
        privacy.Margin = new Thickness(0, 4, 0, 0);
        page.Children.Add(privacy);

        var open = new Wpf.Ui.Controls.Button { Content = _bar.OpenButton, Appearance = ControlAppearance.Primary, Margin = new Thickness(0, 12, 0, 0) };
        open.Click += (_, _) => services.CommandBar?.Show();
        page.Children.Add(open);

        page.Children.Add(Text(L10n.Shared.S.SettingsPageTitles[Core.Settings.SettingsPage.Shortcuts], "SectionHeader"));
        var toggle = new ToggleSwitch { IsChecked = services.Store.Bool(DefaultsKey.CommandBarShortcutEnabled) };
        System.Windows.Automation.AutomationProperties.SetName(toggle, _bar.ShortcutToggle);
        toggle.Click += (_, _) => services.Store.Set(DefaultsKey.CommandBarShortcutEnabled, toggle.IsChecked == true);
        page.Children.Add(Card(SymbolRegular.Keyboard24, _bar.ShortcutToggle, toggle, top: 0));
        var recorder = new ShortcutRecorder(GlobalShortcutRole.CommandBar, services.Store, services.HotKeys,
            services.FeatureRuntime.IsAvailable, role => hub.FeatureTitles[role.Feature()]);
        page.Children.Add(Card(SymbolRegular.KeyCommand24, _bar.PageTitle, recorder, top: 6));

        _fallbackNotice = Text(string.Empty, "Caption");
        _fallbackNotice.TextWrapping = TextWrapping.Wrap;
        _fallbackNotice.Margin = new Thickness(4, 6, 0, 0);
        _fallbackNotice.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "SystemFillColorCautionBrush");
        page.Children.Add(_fallbackNotice);
        SyncFallbackNotice();
        if (services.HotKeys is { } hotKeys)
        {
            hotKeys.Changed += SyncFallbackNotice;
            Unloaded += (_, _) => hotKeys.Changed -= SyncFallbackNotice;
        }

        var forget = new Wpf.Ui.Controls.Button { Content = _bar.ForgetAllButton, Appearance = ControlAppearance.Secondary, Margin = new Thickness(0, 20, 0, 0) };
        forget.Click += (_, _) =>
        {
            services.Store.Remove(DefaultsKey.CommandBarUsage);
            forget.IsEnabled = false;
        };
        forget.IsEnabled = services.Store.Contains(DefaultsKey.CommandBarUsage);
        page.Children.Add(forget);

        Content = page;
    }

    /// <summary>Design doc B.8: when Alt+Space belongs to another app, the bar says which shortcut opens it instead.</summary>
    private void SyncFallbackNotice()
    {
        var state = AppServices.Current.HotKeys?.State(GlobalShortcutRole.CommandBar);
        var usingFallback = state?.UsingFallback == true;
        _fallbackNotice.Text = usingFallback ? string.Format(_bar.ShortcutFallbackFormat, GlobalShortcut.CommandBarFallback.DisplayText) : string.Empty;
        _fallbackNotice.Visibility = usingFallback ? Visibility.Visible : Visibility.Collapsed;
    }

    private static CardControl Card(SymbolRegular icon, string title, UIElement control, double top) => new()
    {
        Icon = new SymbolIcon(icon),
        Header = Text(title, "Body"),
        Content = control,
        Margin = new Thickness(0, top, 0, 0),
    };

    private static System.Windows.Controls.TextBlock Text(string text, string style) =>
        new() { Text = text, Style = (Style)Application.Current.Resources[style] };
}
