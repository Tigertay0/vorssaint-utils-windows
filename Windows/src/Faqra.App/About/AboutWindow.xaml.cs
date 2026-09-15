// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors AboutSettings in Sources/Vorssaint/UI/Settings/SettingsView.swift (lines 1577-1640)

using System.Diagnostics;
using System.Windows;
using Faqra.App.Tray;
using Faqra.Core;
using Faqra.Core.Localization;

namespace Faqra.App.About;

public partial class AboutWindow : Window
{
    private static AboutWindow? s_instance;

    public AboutWindow()
    {
        InitializeComponent();
        Populate();
        L10n.Shared.Changed += OnLanguageChanged;
        Closed += (_, _) =>
        {
            L10n.Shared.Changed -= OnLanguageChanged;
            s_instance = null;
        };
    }

    /// <summary>Opens the About window or brings the existing one to the front.</summary>
    public static void ShowSingleton()
    {
        if (s_instance is null)
        {
            s_instance = new AboutWindow();
            s_instance.Show();
        }
        else
        {
            s_instance.Activate();
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Populate();

    private void Populate()
    {
        var s = L10n.Shared.S;
        Title = s.MenuAbout;
        GlyphImage.Source = TrayIconBitmap.Render(152, active: false, lightTaskbar: false);
        NameText.Text = AppInfo.Name;
        VersionText.Text = $"{s.VersionPrefix} {AppInfo.Version}";
        BetaText.Text = s.BetaBadgeLabel;
        BetaBadge.Visibility = AppInfo.IsBeta ? Visibility.Visible : Visibility.Collapsed;
        DescriptionText.Text = s.AboutDescription;
        GitHubButton.Content = s.ViewOnGitHub;
        CopyrightText.Text = AppInfo.Copyright;
        CreditText.Text = AppInfo.UpstreamCredit;
    }

    private void OnViewOnGitHub(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(AppInfo.RepositoryUrl.ToString()) { UseShellExecute = true });
    }
}
