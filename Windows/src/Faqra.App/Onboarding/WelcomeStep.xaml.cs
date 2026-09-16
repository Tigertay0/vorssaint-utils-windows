// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors WelcomeStep in Sources/Vorssaint/UI/Onboarding/OnboardingView.swift (lines 140-206)

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Faqra.App.Tray;
using Faqra.Core;
using Faqra.Core.Localization;

namespace Faqra.App.Onboarding;

public partial class WelcomeStep : UserControl
{
    public WelcomeStep()
    {
        InitializeComponent();
        var s = L10n.Shared.S;
        GlyphImage.Source = TrayIconBitmap.Render(192, active: false, lightTaskbar: false);
        NameText.Text = AppInfo.Name;
        BodyText.Text = s.OnboardingWelcomeBody;

        AddBullet("", s.OnboardingBullet1Title, s.OnboardingBullet1Body);
        AddBullet("", s.OnboardingBullet2Title, s.OnboardingBullet2Body);
        AddBullet("", s.OnboardingBullet3Title, s.OnboardingBullet3Body);
    }

    private void AddBullet(string glyph, string title, string body)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var tile = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(8),
            Background = (Brush)FindResource("AccentFillColorDefaultBrush"),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(tile, 0);
        row.Children.Add(tile);

        var text = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextFillColorPrimaryBrush"),
        });
        text.Children.Add(new TextBlock
        {
            Text = body,
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("TextFillColorSecondaryBrush"),
        });
        Grid.SetColumn(text, 1);
        row.Children.Add(text);

        Bullets.Children.Add(row);
    }
}
