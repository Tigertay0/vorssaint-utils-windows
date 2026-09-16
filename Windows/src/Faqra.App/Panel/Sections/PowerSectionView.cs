// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/UI/MenuPanel/PowerSection.swift. Windows exposes no adapter wattage, battery
// temperature or health without vendor drivers, so those rows are left out.

using System.Windows;
using System.Windows.Controls;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;
using Faqra.Core.Metrics;

namespace Faqra.App.Panel.Sections;

internal sealed class PowerSectionView : IPanelSectionView
{
    private readonly MonitorStrings _s;
    private readonly ISettingsStore _store;
    private readonly TextBlock _unavailable;
    private readonly UIElement _card;
    private readonly Grid _chargeRow;
    private readonly TextBlock _chargeIcon = PanelText.Glyph("", 12);
    private readonly UsageBar _chargeBar = new();
    private readonly TextBlock _chargeValue = PanelText.Value(string.Empty, 12, PanelBrushes.Primary, FontWeights.Normal);
    private readonly Sparkline? _batteryGraph;
    private readonly PowerRow _system;
    private readonly PowerRow _battery;
    private readonly PowerRow _remaining;
    private readonly Sparkline? _systemGraph;

    public PowerSectionView(SectionContext context, MonitorStrings strings)
    {
        _s = strings;
        _store = context.Store;
        _unavailable = PanelText.Label(strings.PowerUnavailable, 12, PanelBrushes.Secondary);
        _unavailable.TextWrapping = TextWrapping.Wrap;

        _chargeRow = new Grid();
        _chargeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        _chargeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        _chargeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _chargeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        _chargeRow.Children.Add(_chargeIcon);
        var label = PanelText.Label(strings.Battery, 12);
        Grid.SetColumn(label, 1);
        _chargeRow.Children.Add(label);
        Grid.SetColumn(_chargeBar, 2);
        _chargeRow.Children.Add(_chargeBar);
        _chargeValue.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(_chargeValue, 3);
        _chargeRow.Children.Add(_chargeValue);
        _batteryGraph = _store.Bool(DefaultsKey.MonitorGraphBattery) ? new Sparkline(22, PanelBrushes.Success) : null;
        _systemGraph = _store.Bool(DefaultsKey.MonitorGraphPower) ? new Sparkline(26, PanelBrushes.Caution) : null;

        _system = new PowerRow("", PanelBrushes.Caution, strings.SystemPower);
        _battery = new PowerRow("", PanelBrushes.Success, strings.Battery);
        _remaining = new PowerRow("", PanelBrushes.Success, strings.BatteryTimeRemaining);

        var stack = new StackPanel();
        stack.Children.Add(_chargeRow);
        if (_batteryGraph is not null)
        {
            _batteryGraph.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(_batteryGraph);
        }
        stack.Children.Add(_system.Root);
        if (_systemGraph is not null)
        {
            _systemGraph.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(_systemGraph);
        }
        stack.Children.Add(_battery.Root);
        stack.Children.Add(_remaining.Root);
        _card = new PanelCard(stack);

        var root = new StackPanel();
        root.Children.Add(_card);
        root.Children.Add(_unavailable);
        Root = root;
    }

    public UIElement Root { get; }

    public void Update(SystemSnapshot snapshot)
    {
        var power = snapshot.Power;
        var showCharge = power is { HasBattery: true, ChargePercent: not null } && _store.Bool(DefaultsKey.MonitorSysBattery);
        var showSystem = power?.SystemWatts is not null && _store.Bool(DefaultsKey.MonitorPwrSystem);
        var showBattery = power is { HasBattery: true, BatteryWatts: not null } && _store.Bool(DefaultsKey.MonitorPwrBattery);
        var showRemaining = power is { HasBattery: true, ExternalConnected: false, IsCharging: false } && _store.Bool(DefaultsKey.MonitorPwrTimeRemaining);
        var any = showCharge || showSystem || showBattery || showRemaining;

        _card.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        _unavailable.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        if (!any || power is null)
        {
            return;
        }

        _chargeRow.Visibility = Show(showCharge);
        if (power.ChargePercent is { } charge)
        {
            _chargeIcon.Text = power.IsCharging ? "" : "";
            _chargeBar.Set(charge / 100d, charge < 20 ? PanelBrushes.Critical : charge < 40 ? PanelBrushes.Caution : PanelBrushes.Success);
            _chargeValue.Text = $"{charge}%";
        }
        _batteryGraph?.Set(showCharge ? snapshot.BatteryHistory : [], maxValue: 1);

        _system.Root.Visibility = Show(showSystem);
        _system.Set(power.SystemWatts is { } watts ? MetricFormat.Watts(watts) : "-", null);
        _systemGraph?.Set(showSystem ? snapshot.SystemPowerHistory : []);

        _battery.Root.Visibility = Show(showBattery);
        if (power.BatteryWatts is { } flow)
        {
            _battery.Set(MetricFormat.Watts(Math.Abs(flow)), flow >= 0 ? _s.Charging : _s.OnBattery);
        }

        _remaining.Root.Visibility = Show(showRemaining);
        var formatted = power.TimeRemainingSeconds is { } seconds ? BatteryTime.Formatted(seconds) : null;
        _remaining.Set(formatted ?? "...", formatted is null ? _s.Calculating : _s.SystemEstimate);
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Icon, label with an optional caption, and a large value on the right.</summary>
    private sealed class PowerRow
    {
        private readonly TextBlock _value = PanelText.Value(string.Empty, 14);
        private readonly TextBlock _caption = PanelText.Label(string.Empty, 11);

        public PowerRow(string glyph, string brush, string label)
        {
            var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(PanelText.Glyph(glyph, 12, brush));
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(PanelText.Label(label, 12, PanelBrushes.Primary));
            text.Children.Add(_caption);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            Grid.SetColumn(_value, 2);
            grid.Children.Add(_value);
            Root = grid;
        }

        public FrameworkElement Root { get; }

        public void Set(string value, string? caption)
        {
            _value.Text = value;
            _caption.Text = caption ?? string.Empty;
            _caption.Visibility = string.IsNullOrEmpty(caption) ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
