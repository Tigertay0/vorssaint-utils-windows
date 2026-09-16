// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the aboutContent block of AboutSettings in Sources/Vorssaint/UI/Settings/SettingsView.swift

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Faqra.App.Tray;
using Faqra.Core;
using Faqra.Core.Localization;

namespace Faqra.App.About;

/// <summary>Shared by the About settings page and the tray menu's About window.</summary>
public partial class AboutContent : UserControl
{
    public AboutContent()
    {
        InitializeComponent();
        Populate();
        L10n.Shared.Changed += OnLanguageChanged;
        Unloaded += (_, _) => L10n.Shared.Changed -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Populate();

    private void Populate()
    {
        var s = L10n.Shared.S;
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

    private void OnViewOnGitHub(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppInfo.RepositoryUrl.ToString()) { UseShellExecute = true });
}
