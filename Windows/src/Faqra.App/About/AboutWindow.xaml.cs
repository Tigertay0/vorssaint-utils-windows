// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors AppDelegate.showAbout() in Sources/Vorssaint/App/AppDelegate.swift (line 1269)

using Faqra.Core.Localization;

namespace Faqra.App.About;

public partial class AboutWindow
{
    private static AboutWindow? s_instance;

    public AboutWindow()
    {
        InitializeComponent();
        Retitle();
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

    private void OnLanguageChanged(object? sender, EventArgs e) => Retitle();

    private void Retitle()
    {
        Title = L10n.Shared.S.MenuAbout;
        WindowTitleBar.Title = Title;
    }
}
