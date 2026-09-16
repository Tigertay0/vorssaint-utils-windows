// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/UI/MenuPanel/DiskSection.swift. SMART, eject and the tools block are not
// ported yet, so the selector, usage and live activity blocks are built.

using System.Windows;
using System.Windows.Controls;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;
using Faqra.Core.Metrics;

namespace Faqra.App.Panel.Sections;

internal sealed class DiskSectionView : IPanelSectionView
{
    private static readonly IReadOnlyList<string> UpstreamOrder = ["usage", "activity", "smart", "protection", "tools"];
    private static readonly IReadOnlySet<string> Built = new HashSet<string> { "usage", "activity" };

    private readonly MonitorStrings _s;
    private readonly WrapPanel _chips = new();
    private readonly TextBlock _empty;
    private readonly StackPanel _content = new();
    private readonly TextBlock _name = PanelText.Label(string.Empty, 13, PanelBrushes.Primary, FontWeights.SemiBold);
    private readonly TextBlock _tags = PanelText.Label(string.Empty, 12, PanelBrushes.Secondary);
    private readonly UsageBar _bar = new(6);
    private readonly TextBlock _usedPercent = PanelText.Value(string.Empty, 12, PanelBrushes.Secondary, FontWeights.Normal);
    private readonly TextBlock _available = PanelText.Value(string.Empty, 12, PanelBrushes.Secondary, FontWeights.Normal);
    private readonly TextBlock _usedOfTotal = PanelText.Value(string.Empty, 12, PanelBrushes.Secondary, FontWeights.Normal);
    private readonly RateColumn _read;
    private readonly RateColumn _write;
    private readonly Sparkline? _graph;
    private readonly TextBlock _totals = PanelText.Value(string.Empty, 12, PanelBrushes.Secondary, FontWeights.Normal);
    private string? _selectedId;
    private string _chipKey = string.Empty;

    public DiskSectionView(SectionContext context, MonitorStrings strings)
    {
        _s = strings;
        var store = context.Store;
        _read = new RateColumn("", strings.Read, PanelBrushes.Accent);
        _write = new RateColumn("", strings.Write, PanelBrushes.Success);
        _empty = PanelText.Label(strings.NoDisks, 12, PanelBrushes.Secondary);

        var selector = new StackPanel();
        selector.Children.Add(PanelText.Label(strings.SelectDisk, 12, PanelBrushes.Secondary));
        _chips.Margin = new Thickness(0, 6, 0, 0);
        selector.Children.Add(_chips);
        var blocks = new List<UIElement> { selector };

        foreach (var block in SectionKit.Order(store, DefaultsKey.PanelDiskOrder, UpstreamOrder, Built))
        {
            if (block == "usage" && store.Bool(DefaultsKey.MonitorDiskUsage))
            {
                blocks.Add(UsageBlock());
            }
            else if (block == "activity" && store.Bool(DefaultsKey.MonitorDiskActivity))
            {
                _graph = store.Bool(DefaultsKey.MonitorGraphDisk) ? new Sparkline(30, PanelBrushes.Accent, PanelBrushes.Success) : null;
                var activity = new StackPanel();
                activity.Children.Add(PanelText.Label(strings.LiveActivity, 12, PanelBrushes.Secondary));
                var speed = NetworkSectionView.SpeedBlock(_read, _write, _graph);
                ((FrameworkElement)speed).Margin = new Thickness(0, 8, 0, 0);
                activity.Children.Add(speed);
                _totals.HorizontalAlignment = HorizontalAlignment.Right;
                var totals = SectionKit.Row(PanelText.Label(strings.ThisSession, 12, PanelBrushes.Secondary), _totals);
                totals.Margin = new Thickness(0, 8, 0, 0);
                activity.Children.Add(totals);
                blocks.Add(activity);
            }
        }
        _content.Children.Add(SectionKit.Card(blocks));
        _content.Children.Add(_empty);
        Root = _content;
    }

    public UIElement Root { get; }

    public void Update(SystemSnapshot snapshot)
    {
        var disks = snapshot.Disks;
        _empty.Visibility = disks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _content.Children[0].Visibility = disks.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (disks.Count == 0)
        {
            return;
        }
        var selected = disks.FirstOrDefault(d => d.Id == _selectedId) ?? disks[0];
        _selectedId = selected.Id;
        RebuildChips(disks);

        _name.Text = selected.Name;
        _tags.Text = string.Join("  ", new[] { selected.FileSystem, selected.IsInternal ? _s.Internal : _s.External }.Where(t => !string.IsNullOrEmpty(t)));
        _bar.Set(selected.UsedFraction);
        _usedPercent.Text = string.Format(_s.UsedFormat, MetricFormat.Percent(selected.UsedFraction));
        _available.Text = string.Format(_s.AvailableFormat, MetricFormat.DiskBytes(selected.FreeBytes));
        _usedOfTotal.Text = $"{MetricFormat.DiskBytes(selected.UsedBytes)} / {MetricFormat.DiskBytes(selected.TotalBytes)}";

        _read.Set(SectionKit.Or(selected.ReadBytesPerSec, v => MetricFormat.BytesPerSec(v), _s.Measuring));
        _write.Set(SectionKit.Or(selected.WriteBytesPerSec, v => MetricFormat.BytesPerSec(v), _s.Measuring));
        _graph?.Set(snapshot.DiskReadHistory, snapshot.DiskWriteHistory);
        _totals.Text = $"↓ {MetricFormat.DiskBytes(selected.TotalReadBytes ?? 0)}  ↑ {MetricFormat.DiskBytes(selected.TotalWrittenBytes ?? 0)}";
    }

    private UIElement UsageBlock()
    {
        var stack = new StackPanel();
        stack.Children.Add(PanelText.Label(_s.DiskUsage, 12, PanelBrushes.Secondary));
        _tags.HorizontalAlignment = HorizontalAlignment.Right;
        var title = SectionKit.Row(_name, _tags);
        title.Margin = new Thickness(0, 8, 0, 0);
        stack.Children.Add(title);
        _bar.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(_bar);
        _available.HorizontalAlignment = HorizontalAlignment.Right;
        var numbers = SectionKit.Row(_usedPercent, _available);
        numbers.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(numbers);
        _usedOfTotal.Margin = new Thickness(0, 2, 0, 0);
        stack.Children.Add(_usedOfTotal);
        return stack;
    }

    /// <summary>Rebuilds the chips only when the set of disks or the selection changed, so a tick never steals a click.</summary>
    private void RebuildChips(IReadOnlyList<DiskDeviceReading> disks)
    {
        var key = _selectedId + "|" + string.Join(",", disks.Select(d => $"{d.Id}:{MetricFormat.Percent(d.UsedFraction)}"));
        if (key == _chipKey)
        {
            return;
        }
        _chipKey = key;
        _chips.Children.Clear();
        foreach (var disk in disks)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = PanelText.Glyph("", 12);
            icon.Margin = new Thickness(0, 0, 6, 0);
            content.Children.Add(icon);
            var text = new StackPanel();
            text.Children.Add(PanelText.Label(disk.Name, 12, PanelBrushes.Primary, FontWeights.SemiBold));
            text.Children.Add(PanelText.Label(string.Format(_s.UsedFormat, MetricFormat.Percent(disk.UsedFraction)), 11));
            content.Children.Add(text);

            var chip = new Wpf.Ui.Controls.Button
            {
                Content = content,
                Appearance = disk.Id == _selectedId ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Secondary,
                Padding = new Thickness(8, 4, 10, 4),
                Margin = new Thickness(0, 0, 6, 6),
                MaxWidth = 150,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            System.Windows.Automation.AutomationProperties.SetName(chip, disk.Name);
            var id = disk.Id;
            chip.Click += (_, _) =>
            {
                _selectedId = id;
                _chipKey = string.Empty;
                SelectionChanged?.Invoke();
            };
            _chips.Children.Add(chip);
        }
    }

    /// <summary>Raised when a chip is clicked, so the panel re-renders from the latest snapshot.</summary>
    public event Action? SelectionChanged;
}
