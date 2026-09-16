// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// The island's own settings. Upstream's notch page has more rows because it has more modules built;
// this page covers what the island can do today, plus the edge choice Windows needs and macOS does not.

using System.Windows;
using System.Windows.Controls;
using Faqra.Core.Defaults;
using Faqra.Core.Island;
using Faqra.Core.Localization;

namespace Faqra.App.Settings.Pages;

public partial class IslandPage : UserControl
{
    private static readonly IslandEdge[] Edges = [IslandEdge.Top, IslandEdge.Left, IslandEdge.Right];
    private static readonly IslandDisplay[] Displays = [IslandDisplay.Automatic, IslandDisplay.Main];
    private static readonly IslandSize[] Sizes = [IslandSize.Compact, IslandSize.Spacious];
    private static readonly IslandIdleContent[] IdleContents =
        [IslandIdleContent.Music, IslandIdleContent.Battery, IslandIdleContent.None];

    private bool _loading;

    public IslandPage()
    {
        InitializeComponent();
        Populate();
    }

    private static ISettingsStore Store => AppServices.Current.Store;

    private void Populate()
    {
        _loading = true;
        var s = L10n.Shared.S;

        TitleText.Text = s.SettingsPageTitles[Core.Settings.SettingsPage.Notch];
        ShowLabel.Text = s.IslandShow;
        ShowHint.Text = s.IslandShowHint;
        PositionLabel.Text = s.IslandPosition;
        DisplayLabel.Text = s.IslandDisplayLabel;
        SizeLabel.Text = s.IslandSizeLabel;
        IdleLabel.Text = s.IslandIdleLabel;
        HoverHeader.Text = s.IslandOpenOnHover;
        OpenOnHoverLabel.Text = s.IslandOpenOnHover;
        HoverExpandsLabel.Text = s.IslandHoverExpands;
        HoverExpandsHint.Text = s.IslandHoverExpandsHint;

        ShowToggle.IsChecked = Store.Bool(DefaultsKey.NotchEnabled);
        OpenOnHoverToggle.IsChecked = Store.Bool(DefaultsKey.NotchOpenOnHover);
        HoverExpandsToggle.IsChecked = Store.Bool(DefaultsKey.NotchHoverExpands);

        PositionBox.ItemsSource = new[] { s.IslandPositionTop, s.IslandPositionLeft, s.IslandPositionRight };
        PositionBox.SelectedIndex = Array.IndexOf(Edges, IslandSizes.EdgeFromRawValue(Store.String(DefaultsKey.IslandEdge)));

        DisplayBox.ItemsSource = new[] { s.IslandDisplayFollow, s.IslandDisplayPrimary };
        DisplayBox.SelectedIndex = Math.Max(0,
            Array.IndexOf(Displays, IslandSizes.DisplayFromRawValue(Store.String(DefaultsKey.NotchDisplay))));

        SizeBox.ItemsSource = new[] { s.IslandSizeCompact, s.IslandSizeSpacious };
        SizeBox.SelectedIndex = Math.Max(0,
            Array.IndexOf(Sizes, IslandSizes.FromRawValue(Store.String(DefaultsKey.NotchSize))));

        IdleBox.ItemsSource = new[] { s.IslandIdleMusic, s.IslandIdleBattery, s.IslandIdleNone };
        IdleBox.SelectedIndex = Math.Max(0,
            Array.IndexOf(IdleContents, IslandSizes.IdleContentFromRawValue(Store.String(DefaultsKey.NotchIdleContent))));

        RefreshEnabled();
        _loading = false;
    }

    /// <summary>Everything below the master switch is meaningless while the island is off.</summary>
    private void RefreshEnabled() => Options.IsEnabled = ShowToggle.IsChecked == true;

    private void OnShowToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }
        Store.Set(DefaultsKey.NotchEnabled, ShowToggle.IsChecked == true);
        RefreshEnabled();
    }

    private void OnPositionChosen(object sender, SelectionChangedEventArgs e) =>
        Apply(PositionBox, index => Store.Set(DefaultsKey.IslandEdge, Edges[index].RawValue()));

    private void OnDisplayChosen(object sender, SelectionChangedEventArgs e) =>
        Apply(DisplayBox, index => Store.Set(DefaultsKey.NotchDisplay, Displays[index].RawValue()));

    private void OnSizeChosen(object sender, SelectionChangedEventArgs e) =>
        Apply(SizeBox, index => Store.Set(DefaultsKey.NotchSize, Sizes[index].RawValue()));

    private void OnIdleChosen(object sender, SelectionChangedEventArgs e) =>
        Apply(IdleBox, index => Store.Set(DefaultsKey.NotchIdleContent, IdleContents[index].RawValue()));

    private void OnOpenOnHoverToggled(object sender, RoutedEventArgs e) =>
        Toggle(DefaultsKey.NotchOpenOnHover, OpenOnHoverToggle.IsChecked == true);

    private void OnHoverExpandsToggled(object sender, RoutedEventArgs e) =>
        Toggle(DefaultsKey.NotchHoverExpands, HoverExpandsToggle.IsChecked == true);

    private void Apply(ComboBox box, Action<int> write)
    {
        if (!_loading && box.SelectedIndex >= 0)
        {
            write(box.SelectedIndex);
        }
    }

    private void Toggle(string key, bool value)
    {
        if (!_loading)
        {
            Store.Set(key, value);
        }
    }
}
