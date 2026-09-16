// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors MenuPanelView in Sources/Vorssaint/UI/MenuPanel/MenuPanelView.swift (lines 196-477): the tab row,
// the one visible section, and the Settings / Quit footer. MenuPanelController decides what it shows.

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Faqra.Core.Localization;
using Faqra.Core.Panel;

namespace Faqra.App.Panel;

public partial class MenuPanelWindow
{
    public MenuPanelWindow()
    {
        InitializeComponent();
        PreviewKeyDown += OnKeyDown;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // A tray flyout, not an app window: kept out of Alt+Tab like Windows' own volume and network flyouts.
        Faqra.Win32.Windows.WindowStyles.MakeToolWindow(Handle);
    }

    /// <summary>Raised when the user picks a tab.</summary>
    internal event Action<PanelSectionId>? SectionSelected;

    internal event Action? SettingsRequested;

    internal event Action? CloseRequested;

    public IntPtr Handle => new WindowInteropHelper(this).EnsureHandle();

    /// <summary>Rebuilds the tab row. Tabs change only when sections are shown, hidden or reordered.</summary>
    internal void SetTabs(IReadOnlyList<PanelSectionId> sections, PanelSectionId active, MonitorStrings s)
    {
        Tabs.Children.Clear();
        Tabs.Columns = Math.Max(1, sections.Count);
        foreach (var id in sections)
        {
            var title = SectionTitle(id, s);
            var tab = new RadioButton
            {
                Content = id.Glyph(),
                GroupName = "PanelSections",
                IsChecked = id == active,
                ToolTip = title,
                Style = (Style)Resources["PanelTab"],
            };
            AutomationProperties.SetName(tab, title);
            tab.Checked += (_, _) => SectionSelected?.Invoke(id);
            Tabs.Children.Add(tab);
        }
    }

    internal void SetSection(UIElement content) => SectionHost.Content = content;

    internal void SetFooter(MonitorStrings s)
    {
        SettingsButton.Content = s.PanelSettings;
        QuitButton.Content = s.PanelQuit;
    }

    /// <summary>Caps the scrolling area so the whole panel fits the work area.</summary>
    internal void SetMaxHeight(double panelMaxHeightDip) =>
        Scroller.MaxHeight = Math.Max(80, panelMaxHeightDip - ChromeHeight());

    /// <summary>Everything but the scroll area: tabs, footer and margins.</summary>
    private double ChromeHeight() => Root.Margin.Top + Root.Margin.Bottom + 44 + 12 + 32 + 12;

    internal static string SectionTitle(PanelSectionId id, MonitorStrings s) => id switch
    {
        PanelSectionId.System => s.SystemSection,
        PanelSectionId.Network => s.NetworkSection,
        PanelSectionId.Disk => s.DiskSection,
        PanelSectionId.Power => s.PowerSection,
        _ => id.RawValue(),
    };

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke();
            e.Handled = true;
        }
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();

    private void OnQuitClicked(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
