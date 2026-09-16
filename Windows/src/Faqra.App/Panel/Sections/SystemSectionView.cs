// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/UI/MenuPanel/SystemSection.swift. Temperatures have no Windows source and
// the per-app breakdowns are not ported yet, so the temps block and the row chevrons are left out.

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Localization;
using Faqra.Core.Metrics;

namespace Faqra.App.Panel.Sections;

internal sealed class SystemSectionView : IPanelSectionView
{
    private static readonly IReadOnlyList<string> UpstreamOrder = ["temps", "usage", "memory", "alerts", "uptime"];
    private static readonly IReadOnlySet<string> Built = new HashSet<string> { "usage", "memory", "uptime" };

    private readonly MonitorStrings _s;
    private readonly UsageRow? _cpu;
    private readonly UsageRow? _gpu;
    private readonly PressureIndicator _pressure = new();
    private readonly TextBlock _memoryTotals = PanelText.Value(string.Empty, 12, PanelBrushes.Secondary, FontWeights.Normal);
    private readonly (TextBlock Value, Grid Row) _compressed;
    private readonly (TextBlock Value, Grid Row) _cached;
    private readonly (TextBlock Value, Grid Row) _swap;
    private readonly Sparkline? _memoryGraph;
    private readonly TextBlock _uptime = PanelText.Label(string.Empty, 12, PanelBrushes.Secondary, FontWeights.SemiBold);

    public SystemSectionView(SectionContext context, MonitorStrings strings)
    {
        _s = strings;
        var store = context.Store;
        var blocks = new List<UIElement>();

        var showCpu = context.IsAvailable(AppFeature.MonitorCPU) && store.Bool(DefaultsKey.MonitorSysCPU);
        var showGpu = context.IsAvailable(AppFeature.MonitorGPU) && store.Bool(DefaultsKey.MonitorSysGPU);
        var showMemory = context.IsAvailable(AppFeature.MonitorMemory) && store.Bool(DefaultsKey.MonitorSysMemory);
        _compressed = DetailRow(strings.MemoryCompressed);
        _cached = DetailRow(strings.MemoryCachedFiles);
        _swap = DetailRow(strings.MemorySwapUsed);

        foreach (var block in SectionKit.Order(store, DefaultsKey.PanelSystemOrder, UpstreamOrder, Built))
        {
            switch (block)
            {
                case "usage" when showCpu || showGpu:
                    var usage = new StackPanel();
                    usage.Children.Add(SectionKit.Row(PanelText.Subsection(strings.HardwareUsage), TaskManagerButton()));
                    if (showCpu)
                    {
                        _cpu = new UsageRow(strings.Cpu, store.Bool(DefaultsKey.MonitorGraphCPU) ? new Sparkline(22, PanelBrushes.Accent) : null);
                        usage.Children.Add(_cpu.Root);
                    }
                    if (showGpu)
                    {
                        _gpu = new UsageRow(strings.Gpu, store.Bool(DefaultsKey.MonitorGraphGPU) ? new Sparkline(22, PanelBrushes.Accent) : null);
                        usage.Children.Add(_gpu.Root);
                    }
                    blocks.Add(usage);
                    break;
                case "memory" when showMemory:
                    _memoryGraph = store.Bool(DefaultsKey.MonitorGraphMemory) ? new Sparkline(22, PanelBrushes.Success) : null;
                    blocks.Add(MemoryBlock());
                    break;
                case "uptime" when store.Bool(DefaultsKey.MonitorSysUptime):
                    var uptime = new StackPanel { Orientation = Orientation.Horizontal };
                    var clock = PanelText.Glyph("", 12);
                    clock.Margin = new Thickness(0, 0, 6, 0);
                    uptime.Children.Add(clock);
                    uptime.Children.Add(_uptime);
                    blocks.Add(uptime);
                    break;
            }
        }
        Root = SectionKit.Card(blocks);
    }

    public UIElement Root { get; }

    public void Update(SystemSnapshot snapshot)
    {
        _cpu?.Set(snapshot.CpuUsage, snapshot.CpuHistory, _s.Measuring);
        _gpu?.Set(snapshot.GpuUsage, snapshot.GpuHistory, _s.Measuring);

        _pressure.Set(snapshot.MemoryPressure, _s);
        _memoryTotals.Text = snapshot is { MemoryUsed: { } used, MemoryTotal: { } total }
            ? $"{MetricFormat.Bytes(used)} / {MetricFormat.Bytes(total)}"
            : string.Empty;
        SetDetail(_compressed, snapshot.MemoryCompressed);
        SetDetail(_cached, snapshot.MemoryCached);
        SetDetail(_swap, snapshot.MemorySwapUsed);
        _memoryGraph?.Set(snapshot.MemoryHistory, maxValue: 1);

        _uptime.Text = string.Format(_s.UptimeFormat, MetricFormat.Uptime(snapshot.UptimeSeconds));
    }

    private UIElement MemoryBlock()
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        var pressureLabel = PanelText.Label(_s.Pressure, 12);
        pressureLabel.Margin = new Thickness(0, 0, 8, 0);
        header.Children.Add(pressureLabel);
        header.Children.Add(_pressure);
        _memoryTotals.HorizontalAlignment = HorizontalAlignment.Right;

        var stack = new StackPanel();
        stack.Children.Add(PanelText.Subsection(_s.Memory));
        var main = SectionKit.Row(header, _memoryTotals);
        main.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(main);
        stack.Children.Add(_compressed.Row);
        stack.Children.Add(_cached.Row);
        stack.Children.Add(_swap.Row);
        if (_memoryGraph is not null)
        {
            _memoryGraph.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(_memoryGraph);
        }
        return stack;
    }

    private static (TextBlock Value, Grid Row) DetailRow(string title)
    {
        var value = PanelText.Value(string.Empty, 12, PanelBrushes.Secondary, FontWeights.Normal);
        value.HorizontalAlignment = HorizontalAlignment.Right;
        var row = SectionKit.Row(PanelText.Label(title, 12), value);
        row.Margin = new Thickness(16, 4, 0, 0);
        return (value, row);
    }

    private static void SetDetail((TextBlock Value, Grid Row) detail, ulong? bytes)
    {
        detail.Row.Visibility = bytes is null ? Visibility.Collapsed : Visibility.Visible;
        detail.Value.Text = bytes is { } b ? MetricFormat.Bytes(b) : string.Empty;
    }

    /// <summary>Upstream opens Activity Monitor; the Windows equivalent is Task Manager.</summary>
    private UIElement TaskManagerButton()
    {
        var button = new Wpf.Ui.Controls.Button
        {
            Content = PanelText.Glyph("", 12),
            Appearance = Wpf.Ui.Controls.ControlAppearance.Transparent,
            Padding = new Thickness(6),
            MinWidth = 0,
            MinHeight = 0,
            HorizontalAlignment = HorizontalAlignment.Right,
            ToolTip = _s.OpenTaskManager,
        };
        System.Windows.Automation.AutomationProperties.SetName(button, _s.OpenTaskManager);
        button.Click += (_, _) => OpenTaskManager();
        return button;
    }

    private static void OpenTaskManager()
    {
        try
        {
            Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // Task Manager can be disabled by policy; the button then does nothing rather than crash.
            Trace.TraceWarning($"Could not open Task Manager: {ex.Message}");
        }
    }

    /// <summary>A usage row: fixed-width label, meter, percentage, and an optional graph below.</summary>
    private sealed class UsageRow
    {
        private readonly UsageBar _bar = new();
        private readonly TextBlock _value = PanelText.Value(string.Empty, 12, PanelBrushes.Primary, FontWeights.Normal);
        private readonly Sparkline? _graph;

        public UsageRow(string label, Sparkline? graph)
        {
            _graph = graph;
            var row = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            row.Children.Add(PanelText.Label(label, 12));
            Grid.SetColumn(_bar, 1);
            row.Children.Add(_bar);
            _value.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(_value, 2);
            row.Children.Add(_value);

            var stack = new StackPanel();
            stack.Children.Add(row);
            if (graph is not null)
            {
                graph.Margin = new Thickness(0, 6, 0, 0);
                stack.Children.Add(graph);
            }
            Root = stack;
        }

        public UIElement Root { get; }

        public void Set(double? fraction, IReadOnlyList<double> history, string measuring)
        {
            _bar.Set(fraction ?? 0);
            _value.Text = fraction is { } f ? MetricFormat.Percent(f) : "-";
            _value.ToolTip = fraction is null ? measuring : null;
            _graph?.Set(history, maxValue: 1);
        }
    }
}
