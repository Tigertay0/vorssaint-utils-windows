// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors AboutSettings in Sources/Vorssaint/UI/Settings/SettingsView.swift (lines 1577-1638)

using System.Windows.Controls;
using Faqra.Core.Localization;

namespace Faqra.App.Settings.Pages;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        TitleText.Text = L10n.Shared.S.SettingsPageTitles[Core.Settings.SettingsPage.About];
    }
}
