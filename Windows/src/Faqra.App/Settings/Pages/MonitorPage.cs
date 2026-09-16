// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/UI/Settings/MonitorSettings.swift and MonitorPanelConfig.swift, plus the panel
// section order editor (MonitorSettings.swift 460-593). Windows shows each tray metric as its own icon,
// so the bar appearance, spacing and separate-items controls have nothing to configure and are left out;
// temperatures and fans have no Windows source. Upstream drags to reorder; here Move up / Move down do it,
// which also works from the keyboard.

using System.Windows;
using System.Windows.Controls;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Localization;
using Faqra.Core.Panel;
using Faqra.Core.Tray;
using Faqra.App.Panel;
using Wpf.Ui.Controls;

namespace Faqra.App.Settings.Pages;

public sealed class MonitorPage : UserControl
{
    private static readonly int[] Intervals = [1, 2, 5];

    private readonly MonitorStrings _s = MonitorStrings.For(L10n.Shared.Language);
    private readonly StackPanel _orderRows = new();

    public MonitorPage()
    {
        var page = new StackPanel();
        page.Children.Add(Text(L10n.Shared.S.SettingsPageTitles[Core.Settings.SettingsPage.Monitor], "PageTitle"));

        Header(page, _s.TraySection, _s.TrayCaption);
        foreach (var metric in Enum.GetValues<TrayMetric>().Where(IsOffered))
        {
            page.Children.Add(Toggle(TrayIcon(metric), metric.Title(_s), metric.DefaultsKey()));
        }

        page.Children.Add(IntervalCard());

        Header(page, _s.PanelOrder, _s.PanelOrderCaption);
        page.Children.Add(_orderRows);
        BuildOrderRows();

        Header(page, _s.InThePanel, _s.InThePanelCaption);
        foreach (var section in EditableSections())
        {
            page.Children.Add(SectionExpander(section));
        }

        Header(page, _s.Graphs, _s.GraphsCaption);
        foreach (var (feature, key, title, icon) in Graphs())
        {
            if (IsAvailable(feature))
            {
                page.Children.Add(Toggle(icon, title, key));
            }
        }
        Content = page;
    }

    private static ISettingsStore Store => AppServices.Current.Store;

    private static bool IsAvailable(AppFeature feature) => AppServices.Current.FeatureRuntime.IsAvailable(feature);

    private static bool HasBattery => AppServices.Current.Monitor.HasBattery;

    private static bool IsOffered(TrayMetric metric) => IsAvailable(metric.Feature()) && (HasBattery || !metric.NeedsBattery());

    private static IEnumerable<PanelSectionId> EditableSections() =>
        PanelLayout.Order(Store.String(DefaultsKey.PanelSectionOrder))
            .Where(id => MenuPanelController.IsBuilt(id) && id.IsAvailable(IsAvailable));

    // MARK: sections

    private UIElement IntervalCard()
    {
        var box = new ComboBox { MinWidth = 190 };
        foreach (var seconds in Intervals)
        {
            box.Items.Add(seconds == 1 ? _s.OneSecond : string.Format(_s.SecondsFormat, seconds));
        }
        box.SelectedIndex = Array.IndexOf(Intervals, DefaultsSanitizers.MonitorInterval(Store.Int(DefaultsKey.MonitorInterval)));
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex >= 0)
            {
                Store.Set(DefaultsKey.MonitorInterval, Intervals[box.SelectedIndex]);
            }
        };
        return Card(SymbolRegular.Timer24, _s.UpdateEvery, box, top: 12);
    }

    /// <summary>One row per section: its visibility switch and its move buttons. Rebuilt after every move.</summary>
    private void BuildOrderRows()
    {
        _orderRows.Children.Clear();
        var rows = EditableSections().ToList();
        var visibleCount = rows.Count(id => Store.Bool(id.VisibilityKey()));
        for (var i = 0; i < rows.Count; i++)
        {
            var id = rows[i];
            var controls = new StackPanel { Orientation = Orientation.Horizontal };
            controls.Children.Add(MoveButton(SymbolRegular.ArrowUp24, _s.MoveUp, i > 0 ? rows[i - 1] : null, id));
            controls.Children.Add(MoveButton(SymbolRegular.ArrowDown24, _s.MoveDown, i < rows.Count - 1 ? rows[i + 1] : null, id));
            var shown = Store.Bool(id.VisibilityKey());
            var toggle = new ToggleSwitch
            {
                IsChecked = shown,
                Margin = new Thickness(12, 0, 0, 0),
                // The panel needs at least one tab, so the last visible section cannot be hidden.
                IsEnabled = !shown || visibleCount > 1,
            };
            System.Windows.Automation.AutomationProperties.SetName(toggle, MenuPanelWindow.SectionTitle(id, _s));
            toggle.Click += (_, _) =>
            {
                Store.Set(id.VisibilityKey(), toggle.IsChecked == true);
                BuildOrderRows();
            };
            controls.Children.Add(toggle);
            _orderRows.Children.Add(Card(SectionIcon(id), MenuPanelWindow.SectionTitle(id, _s), controls, top: _orderRows.Children.Count == 0 ? 0 : 6));
        }
    }

    private UIElement MoveButton(SymbolRegular icon, string label, PanelSectionId? neighbour, PanelSectionId id)
    {
        var button = new Wpf.Ui.Controls.Button
        {
            Icon = new SymbolIcon(icon),
            Appearance = ControlAppearance.Transparent,
            IsEnabled = neighbour is not null,
            ToolTip = label,
            Margin = new Thickness(2, 0, 0, 0),
        };
        System.Windows.Automation.AutomationProperties.SetName(button, $"{label}: {MenuPanelWindow.SectionTitle(id, _s)}");
        button.Click += (_, _) =>
        {
            if (neighbour is not { } other)
            {
                return;
            }
            var order = PanelLayout.Order(Store.String(DefaultsKey.PanelSectionOrder));
            Store.Set(DefaultsKey.PanelSectionOrder, PanelLayout.Serialize(PanelLayout.Swap(order, id, other)));
            BuildOrderRows();
        };
        return button;
    }

    private UIElement SectionExpander(PanelSectionId id)
    {
        var body = new StackPanel();
        foreach (var (key, title, gate) in Items(id))
        {
            if (gate)
            {
                body.Children.Add(Toggle(null, title, key, top: body.Children.Count == 0 ? 0 : 6));
            }
        }
        return new CardExpander
        {
            Icon = new SymbolIcon(SectionIcon(id)),
            Header = Text(MenuPanelWindow.SectionTitle(id, _s), "Body"),
            Content = body,
            Margin = new Thickness(0, 6, 0, 0),
        };
    }

    private IEnumerable<(string Key, string Title, bool Gate)> Items(PanelSectionId id) => id switch
    {
        PanelSectionId.System =>
        [
            (DefaultsKey.MonitorSysCPU, _s.Cpu, IsAvailable(AppFeature.MonitorCPU)),
            (DefaultsKey.MonitorSysGPU, _s.Gpu, IsAvailable(AppFeature.MonitorGPU)),
            (DefaultsKey.MonitorSysMemory, _s.Memory, IsAvailable(AppFeature.MonitorMemory)),
            (DefaultsKey.MonitorSysUptime, _s.Uptime, true),
        ],
        PanelSectionId.Network =>
        [
            (DefaultsKey.MonitorNetSpeed, _s.LiveSpeed, true),
            (DefaultsKey.MonitorNetTotals, _s.SessionTotals, true),
        ],
        PanelSectionId.Disk =>
        [
            (DefaultsKey.MonitorDiskUsage, _s.DiskUsage, true),
            (DefaultsKey.MonitorDiskActivity, _s.LiveActivity, true),
        ],
        PanelSectionId.Power =>
        [
            (DefaultsKey.MonitorPwrSystem, _s.SystemPower, true),
            // Upstream stores the Charge switch under monitorSysBattery; the key is kept for backup compatibility.
            (DefaultsKey.MonitorSysBattery, _s.Charge, HasBattery),
            (DefaultsKey.MonitorPwrBattery, _s.Battery, HasBattery),
            (DefaultsKey.MonitorPwrTimeRemaining, _s.BatteryTimeRemaining, HasBattery),
        ],
        _ => [],
    };

    private IEnumerable<(AppFeature Feature, string Key, string Title, SymbolRegular Icon)> Graphs()
    {
        yield return (AppFeature.MonitorCPU, DefaultsKey.MonitorGraphCPU, _s.Cpu, SymbolRegular.DeveloperBoard24);
        yield return (AppFeature.MonitorGPU, DefaultsKey.MonitorGraphGPU, _s.Gpu, SymbolRegular.Board24);
        yield return (AppFeature.MonitorMemory, DefaultsKey.MonitorGraphMemory, _s.Memory, SymbolRegular.DataUsage24);
        yield return (AppFeature.MonitorNetwork, DefaultsKey.MonitorGraphNetwork, _s.NetworkSection, SymbolRegular.Globe24);
        yield return (AppFeature.MonitorDisk, DefaultsKey.MonitorGraphDisk, _s.DiskSection, SymbolRegular.Storage24);
        yield return (AppFeature.MonitorPower, DefaultsKey.MonitorGraphPower, _s.PowerSection, SymbolRegular.Flash24);
        if (HasBattery)
        {
            yield return (AppFeature.MonitorPower, DefaultsKey.MonitorGraphBattery, _s.Battery, SymbolRegular.Battery1024);
        }
    }

    // MARK: building blocks

    private static SymbolRegular TrayIcon(TrayMetric metric) => metric switch
    {
        TrayMetric.Cpu => SymbolRegular.DeveloperBoard24,
        TrayMetric.Gpu => SymbolRegular.Board24,
        TrayMetric.Memory => SymbolRegular.DataUsage24,
        TrayMetric.Network => SymbolRegular.Globe24,
        TrayMetric.DiskUsage => SymbolRegular.Storage24,
        TrayMetric.DiskActivity => SymbolRegular.ArrowSort24,
        TrayMetric.Battery => SymbolRegular.Battery1024,
        TrayMetric.BatteryTime => SymbolRegular.Clock24,
        _ => SymbolRegular.Flash24,
    };

    private static SymbolRegular SectionIcon(PanelSectionId id) => id switch
    {
        PanelSectionId.Network => SymbolRegular.Globe24,
        PanelSectionId.Disk => SymbolRegular.Storage24,
        PanelSectionId.Power => SymbolRegular.Flash24,
        _ => SymbolRegular.DeveloperBoard24,
    };

    private static void Header(StackPanel page, string title, string caption)
    {
        page.Children.Add(Text(title, "SectionHeader"));
        var hint = Text(caption, "Caption");
        hint.Margin = new Thickness(0, -4, 0, 8);
        page.Children.Add(hint);
    }

    private static UIElement Toggle(SymbolRegular? icon, string title, string key, double top = 6)
    {
        var toggle = new ToggleSwitch { IsChecked = Store.Bool(key) };
        System.Windows.Automation.AutomationProperties.SetName(toggle, title);
        toggle.Click += (_, _) => Store.Set(key, toggle.IsChecked == true);
        return Card(icon, title, toggle, top);
    }

    private static CardControl Card(SymbolRegular? icon, string title, UIElement control, double top)
    {
        var card = new CardControl
        {
            Header = Text(title, "Body"),
            Content = control,
            Margin = new Thickness(0, top, 0, 0),
        };
        if (icon is { } symbol)
        {
            card.Icon = new SymbolIcon(symbol);
        }
        return card;
    }

    private static System.Windows.Controls.TextBlock Text(string text, string style) =>
        new() { Text = text, Style = (Style)Application.Current.Resources[style] };
}
