// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/UI/MenuPanel/NetworkSection.swift. The apps list and the speed test are not
// ported yet, so only the live speed and session totals blocks are built.

using System.Windows;
using System.Windows.Controls;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;
using Faqra.Core.Metrics;

namespace Faqra.App.Panel.Sections;

internal sealed class NetworkSectionView : IPanelSectionView
{
    private static readonly IReadOnlyList<string> UpstreamOrder = ["speed", "apps", "totals", "test"];
    private static readonly IReadOnlySet<string> Built = new HashSet<string> { "speed", "totals" };

    private readonly MonitorStrings _s;
    private readonly RateColumn _down;
    private readonly RateColumn _up;
    private readonly Sparkline? _graph;
    private readonly TextBlock _totals = PanelText.Value(string.Empty, 12, PanelBrushes.Secondary, FontWeights.Normal);

    public NetworkSectionView(SectionContext context, MonitorStrings strings)
    {
        _s = strings;
        var store = context.Store;
        _down = new RateColumn("", strings.Download, PanelBrushes.Accent);
        _up = new RateColumn("", strings.Upload, PanelBrushes.Success);

        var blocks = new List<UIElement>();
        foreach (var block in SectionKit.Order(store, DefaultsKey.PanelNetworkOrder, UpstreamOrder, Built))
        {
            if (block == "speed" && store.Bool(DefaultsKey.MonitorNetSpeed))
            {
                _graph = store.Bool(DefaultsKey.MonitorGraphNetwork) ? new Sparkline(30, PanelBrushes.Accent, PanelBrushes.Success) : null;
                blocks.Add(SpeedBlock(_down, _up, _graph));
            }
            else if (block == "totals" && store.Bool(DefaultsKey.MonitorNetTotals))
            {
                _totals.HorizontalAlignment = HorizontalAlignment.Right;
                blocks.Add(SectionKit.Row(PanelText.Label(strings.ThisSession, 12, PanelBrushes.Secondary), _totals));
            }
        }
        Root = SectionKit.Card(blocks);
    }

    public UIElement Root { get; }

    public void Update(SystemSnapshot snapshot)
    {
        _down.Set(SectionKit.Or(snapshot.NetDownBytesPerSec, v => MetricFormat.BytesPerSec(v), _s.Measuring));
        _up.Set(SectionKit.Or(snapshot.NetUpBytesPerSec, v => MetricFormat.BytesPerSec(v), _s.Measuring));
        _graph?.Set(snapshot.NetDownHistory, snapshot.NetUpHistory);
        _totals.Text = snapshot is { NetTotalDown: { } down, NetTotalUp: { } up }
            ? $"↓{MetricFormat.Bytes(down)}  ↑{MetricFormat.Bytes(up)}"
            : string.Empty;
    }

    /// <summary>Two rate columns split by a hairline, over a shared graph. Disk activity uses the same shape.</summary>
    internal static UIElement SpeedBlock(RateColumn left, RateColumn right, Sparkline? graph)
    {
        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var divider = new Border { Width = 1, Height = 28, Margin = new Thickness(10, 0, 10, 0) };
        divider.SetResourceReference(Border.BackgroundProperty, "DividerStrokeColorDefaultBrush");
        Grid.SetColumn(divider, 1);
        Grid.SetColumn(right, 2);
        columns.Children.Add(left);
        columns.Children.Add(divider);
        columns.Children.Add(right);

        var stack = new StackPanel();
        stack.Children.Add(columns);
        if (graph is not null)
        {
            graph.Margin = new Thickness(0, 10, 0, 0);
            stack.Children.Add(graph);
        }
        return stack;
    }
}
