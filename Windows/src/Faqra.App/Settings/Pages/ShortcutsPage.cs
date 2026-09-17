// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/UI/Settings/ShortcutsSettings.swift: a caption, then one section per feature
// group holding a row for each installed feature's global shortcut, with its Active or Inactive state.
// Only roles whose features are built on Windows appear; upstream's capture-tool and window-layout
// disclosures arrive with those features.

using System.Windows;
using System.Windows.Controls;
using Faqra.Core.Features;
using Faqra.Core.Localization;
using Faqra.Core.Settings;
using Faqra.Core.Shortcuts;
using Wpf.Ui.Controls;

namespace Faqra.App.Settings.Pages;

public sealed class ShortcutsPage : UserControl
{
    private readonly FeatureHubStrings _hub = FeatureHubStrings.For(L10n.Shared.Language);
    private readonly ShortcutStrings _s = ShortcutStrings.For(L10n.Shared.Language);

    public ShortcutsPage()
    {
        var services = AppServices.Current;
        var page = new StackPanel();
        page.Children.Add(Text(L10n.Shared.S.SettingsPageTitles[SettingsPage.Shortcuts], "PageTitle"));
        var caption = Text(_s.PageCaption, "Caption");
        caption.TextWrapping = TextWrapping.Wrap;
        page.Children.Add(caption);

        var roles = Enum.GetValues<GlobalShortcutRole>()
            .Where(r => FeatureWindowsSupport.IsBuilt(r.Feature()) && services.FeatureRuntime.IsAvailable(r.Feature()))
            .GroupBy(r => r.Feature().Group());
        foreach (var group in roles)
        {
            page.Children.Add(Text(_hub.GroupTitles[group.Key], "SectionHeader"));
            var top = 0.0;
            foreach (var role in group)
            {
                page.Children.Add(Row(role, top));
                top = 6;
            }
        }
        Content = page;
    }

    private string RoleTitle(GlobalShortcutRole role) => _hub.FeatureTitles[role.Feature()];

    private CardControl Row(GlobalShortcutRole role, double top)
    {
        var services = AppServices.Current;
        var recorder = new ShortcutRecorder(role, services.Store, services.HotKeys, services.FeatureRuntime.IsAvailable, RoleTitle);
        var status = Text(string.Empty, "Caption");
        void SyncStatus() => status.Text = role.IsActive(services.Store) ? _s.Active : _s.Inactive;
        SyncStatus();
        services.Store.Changed += OnChanged;
        recorder.Unloaded += (_, _) => services.Store.Changed -= OnChanged;
        void OnChanged(object? sender, Core.Defaults.SettingsChangedEventArgs e)
        {
            if (role.RequiredEnableKeys().Contains(e.Key))
            {
                Dispatcher.BeginInvoke(SyncStatus);
            }
        }

        var header = new StackPanel();
        header.Children.Add(Text(RoleTitle(role), "Body"));
        header.Children.Add(status);
        return new CardControl
        {
            Icon = new SymbolIcon(Icon(role)),
            Header = header,
            Content = recorder,
            Margin = new Thickness(0, top, 0, 0),
        };
    }

    private static SymbolRegular Icon(GlobalShortcutRole role) => role switch
    {
        GlobalShortcutRole.KeepAwake => SymbolRegular.WeatherMoon24,
        GlobalShortcutRole.SoundOutputSwitcher => SymbolRegular.Speaker224,
        _ => SymbolRegular.Search24,
    };

    private static System.Windows.Controls.TextBlock Text(string text, string style) =>
        new() { Text = text, Style = (Style)Application.Current.Resources[style] };
}
