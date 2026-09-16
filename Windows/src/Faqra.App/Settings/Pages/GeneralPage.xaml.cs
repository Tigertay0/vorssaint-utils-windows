// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors GeneralSettings in Sources/Vorssaint/UI/Settings/SettingsView.swift (lines 386-533)

using System.Windows;
using System.Windows.Controls;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;
using Faqra.Services.Startup;

namespace Faqra.App.Settings.Pages;

public partial class GeneralPage : UserControl
{
    private bool _loading;

    public GeneralPage()
    {
        InitializeComponent();
        Populate();
    }

    private void Populate()
    {
        _loading = true;
        var s = L10n.Shared.S;
        var store = AppServices.Current.Store;

        TitleText.Text = s.SettingsPageTitles[Core.Settings.SettingsPage.General];
        LaunchAtLoginLabel.Text = s.LaunchAtLogin;
        LanguageLabel.Text = s.LanguageLabel;
        AppearanceLabel.Text = s.AppearanceLabel;
        TrayHeader.Text = s.TraySection;
        ShowTrayIconLabel.Text = s.ShowTrayIcon;
        ShowTrayIconHint.Text = s.ShowTrayIconHint;
        ShowTrayIconButton.Content = s.ShowTrayIconAction;

        LaunchAtLoginToggle.IsChecked = AppServices.Current.LaunchAtLogin.IsEnabled;

        LanguageBox.ItemsSource = AppLanguages.All.Select(language => language.DisplayName()).ToList();
        LanguageBox.SelectedIndex = AppLanguages.All.ToList().IndexOf(L10n.Shared.Language);

        AppearanceBox.ItemsSource = new[] { s.AppearanceSystem, s.AppearanceLight, s.AppearanceDark };
        AppearanceBox.SelectedIndex = (int)DefaultsSanitizers.Appearance(store.String(DefaultsKey.Appearance));

        _loading = false;
    }

    private void OnLaunchAtLoginToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }
        var wanted = LaunchAtLoginToggle.IsChecked == true;
        try
        {
            AppServices.Current.LaunchAtLogin.SetEnabled(wanted, L10n.Shared.S);
            LaunchAtLoginError.Visibility = Visibility.Collapsed;
        }
        catch (LaunchAtLoginException ex)
        {
            LaunchAtLoginError.Text = ex.Message;
            LaunchAtLoginError.Visibility = Visibility.Visible;
        }
        // Re-read the real state, so a refused change snaps the switch back.
        LaunchAtLoginToggle.IsChecked = AppServices.Current.LaunchAtLogin.IsEnabled;
    }

    private void OnLanguageChosen(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageBox.SelectedIndex < 0)
        {
            return;
        }
        L10n.Shared.Language = AppLanguages.All[LanguageBox.SelectedIndex];
    }

    private void OnAppearanceChosen(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || AppearanceBox.SelectedIndex < 0)
        {
            return;
        }
        var appearance = (AppAppearance)AppearanceBox.SelectedIndex;
        AppServices.Current.Store.Set(DefaultsKey.Appearance, appearance.RawValue());
    }

    /// <summary>Re-adds the tray icon after Windows or Explorer dropped it.</summary>
    private void OnShowTrayIcon(object sender, RoutedEventArgs e) =>
        (Application.Current as App)?.ApplyAppearance();
}
