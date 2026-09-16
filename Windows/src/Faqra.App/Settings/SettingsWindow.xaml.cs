// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/UI/Settings/SettingsView.swift (split view, sidebar, search) and the
// window lifecycle in App/AppDelegate.swift (openSettingsWindow, lines 1359-1475).

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Faqra.App.Settings.Pages;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;
using Faqra.Core.Settings;

namespace Faqra.App.Settings;

public partial class SettingsWindow
{
    /// <summary>Pages with content in this milestone. The rest arrive with their features.</summary>
    private static readonly IReadOnlySet<SettingsPage> ImplementedPages = new HashSet<SettingsPage>
    {
        SettingsPage.General, SettingsPage.Features, SettingsPage.Notch, SettingsPage.Monitor, SettingsPage.Advanced, SettingsPage.About,
    };

    private static SettingsWindow? s_instance;

    private readonly List<SidebarRow> _rows = [];

    /// <summary>
    /// Pages are kept once built. Rebuilding the Feature Hub's sixty-six rows on every click was
    /// the slowest thing in the window.
    /// </summary>
    private readonly Dictionary<SettingsPage, UIElement> _pages = [];

    private bool _loading;

    public SettingsWindow()
    {
        InitializeComponent();
        RestoreSize();
        Populate();
        L10n.Shared.Changed += OnLanguageChanged;
        AppServices.Current.FeatureRuntime.RevisionChanged += OnFeaturesChanged;
        Closed += (_, _) =>
        {
            L10n.Shared.Changed -= OnLanguageChanged;
            AppServices.Current.FeatureRuntime.RevisionChanged -= OnFeaturesChanged;
            SaveSize();
            s_instance = null;
        };
    }

    /// <summary>Opens Settings on a page, or brings the open window forward and navigates it.</summary>
    public static void ShowSingleton(SettingsPage page = SettingsPage.General)
    {
        if (s_instance is null)
        {
            s_instance = new SettingsWindow();
            s_instance.Show();
        }
        else
        {
            if (s_instance.WindowState == WindowState.Minimized)
            {
                s_instance.WindowState = WindowState.Normal;
            }
            s_instance.Activate();
        }
        s_instance.Navigate(page);
    }

    private void RestoreSize()
    {
        var store = AppServices.Current.Store;
        var width = store.Double(DefaultsKey.SettingsWindowWidth);
        var height = store.Double(DefaultsKey.SettingsWindowHeight);
        if (width >= MinWidth && height >= MinHeight)
        {
            Width = width;
            Height = height;
        }
        else
        {
            Height = Math.Min(Height, Math.Max(SystemParameters.WorkArea.Height - 40, MinHeight));
        }
    }

    private void SaveSize()
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }
        var store = AppServices.Current.Store;
        store.Set(DefaultsKey.SettingsWindowWidth, Width);
        store.Set(DefaultsKey.SettingsWindowHeight, Height);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _pages.Clear();
        Populate();
    }

    private void OnFeaturesChanged(object? sender, EventArgs e)
    {
        // A feature flipping changes which pages exist and what the hub shows, so the cache goes.
        _pages.Clear();
        Populate();
    }

    /// <summary>Rebuilds the sidebar from the pages this build has and the features installed now.</summary>
    private void Populate(string? query = null)
    {
        var s = L10n.Shared.S;
        Title = s.SettingsTitle;
        WindowTitleBar.Title = s.SettingsTitle;
        SearchBox.PlaceholderText = s.SettingsSearchPlaceholder;

        var runtime = AppServices.Current.FeatureRuntime;
        var selected = SelectedPage();
        _rows.Clear();

        var folded = Fold(query);
        foreach (var (section, pages) in SettingsDirectory.VisibleSections(runtime.IsAvailable))
        {
            var matching = pages
                .Where(info => ImplementedPages.Contains(info.Page))
                .Where(info => folded.Length == 0 || Fold(s.SettingsPageTitles[info.Page]).Contains(folded))
                .ToList();
            if (matching.Count == 0)
            {
                continue;
            }
            _rows.Add(SidebarRow.Header(s.SettingsSectionTitles[section]));
            _rows.AddRange(matching.Select(info => SidebarRow.ForPage(info, s.SettingsPageTitles[info.Page])));
        }

        _loading = true;
        PageList.ItemsSource = null;
        PageList.ItemsSource = _rows;
        _loading = false;

        // Headers carry no page, so a null "selected" must not match one of them.
        var target = _rows.FirstOrDefault(row => selected is not null && row.Page == selected)
            ?? _rows.FirstOrDefault(row => !row.IsHeader);
        if (target is not null)
        {
            PageList.SelectedItem = target;
        }
        else
        {
            DetailHost.Content = null;
        }
    }

    private SettingsPage? SelectedPage() => (PageList.SelectedItem as SidebarRow)?.Page;

    private void Navigate(SettingsPage page)
    {
        var row = _rows.FirstOrDefault(r => r.Page == page);
        if (row is not null)
        {
            PageList.SelectedItem = row;
        }
    }

    private void OnPageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || PageList.SelectedItem is not SidebarRow { Page: { } page })
        {
            return;
        }
        DetailScroll.ScrollToTop();
        if (!_pages.TryGetValue(page, out var view))
        {
            view = CreatePage(page);
            _pages[page] = view;
        }
        DetailHost.Content = view;
    }

    private static UIElement CreatePage(SettingsPage page) => page switch
    {
        SettingsPage.General => new GeneralPage(),
        SettingsPage.Features => new FeatureHubPage(),
        SettingsPage.Notch => new IslandPage(),
        SettingsPage.Monitor => new MonitorPage(),
        SettingsPage.Advanced => new AdvancedPage(),
        SettingsPage.About => new AboutPage(),
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, "no view for this page yet"),
    };

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => Populate(SearchBox.Text);

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape when SearchBox.Text.Length > 0:
                SearchBox.Text = string.Empty;
                e.Handled = true;
                break;
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        var pages = _rows.Where(row => !row.IsHeader).ToList();
        if (pages.Count == 0)
        {
            return;
        }
        var current = pages.IndexOf((PageList.SelectedItem as SidebarRow)!);
        var next = current < 0 ? 0 : (current + delta + pages.Count) % pages.Count;
        PageList.SelectedItem = pages[next];
        PageList.ScrollIntoView(pages[next]);
    }

    /// <summary>Case- and diacritic-insensitive folding, matching upstream's search behavior.</summary>
    private static string Fold(string? text) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim().ToLowerInvariant();
}

/// <summary>A sidebar row: either a group header or a page.</summary>
public sealed class SidebarRow
{
    public required string Title { get; init; }
    public string Glyph { get; init; } = string.Empty;
    public SettingsPage? Page { get; init; }

    public bool IsHeader => Page is null;
    public Visibility HeaderVisibility => IsHeader ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PageVisibility => IsHeader ? Visibility.Collapsed : Visibility.Visible;

    public static SidebarRow Header(string title) => new() { Title = title };

    public static SidebarRow ForPage(SettingsPageInfo info, string title) =>
        new() { Title = title, Glyph = info.Glyph, Page = info.Page };
}
