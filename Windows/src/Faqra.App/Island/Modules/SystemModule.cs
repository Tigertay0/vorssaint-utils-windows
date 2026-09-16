// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/UI/Notch/NotchSystemView.swift and NotchMeter (NotchComponents.swift 86-104).
// Upstream's cards open a metric detail view on tap; that view is not ported yet, so the cards only show.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Faqra.Core.Island;
using Faqra.Core.Localization;
using Faqra.Core.Metrics;
using Faqra.Services.Monitor;

namespace Faqra.App.Island.Modules;

/// <summary>A grid of metric cards: value, then a meter or a detail line. Live while the module is on screen.</summary>
public sealed class SystemModule : UserControl
{
    /// <summary>Upstream turns a card orange above 85% load, at 20% battery, or under 10% free disk.</summary>
    private const double AttentionLoad = 0.85;
    private const int AttentionBattery = 20;
    private const double AttentionFreeDisk = 0.10;

    private readonly SystemMonitor _monitor;
    private readonly MonitorStrings _s = MonitorStrings.For(L10n.Shared.Language);
    private readonly Dictionary<IslandSystemCard, Card> _cards = [];

    public SystemModule(SystemMonitor monitor, IReadOnlyList<IslandSystemCard> cards, int columns)
    {
        _monitor = monitor;
        if (cards.Count == 0)
        {
            Content = new TextBlock
            {
                Text = _s.SensorsUnavailable,
                FontSize = 12,
                Foreground = IslandPalette.Secondary,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            return;
        }

        var grid = new StackPanel();
        foreach (var row in cards.Chunk(Math.Max(1, columns)))
        {
            // A short last row stretches its cards across the width, as upstream's tile grid does.
            var line = new UniformGrid { Rows = 1, Columns = row.Length, Margin = new Thickness(-5, grid.Children.Count == 0 ? 0 : 10, -5, 0) };
            foreach (var kind in row)
            {
                var card = new Card(Glyph(kind), Title(kind), hasDetail: kind == IslandSystemCard.Network);
                _cards[kind] = card;
                line.Children.Add(card.Root);
            }
            grid.Children.Add(line);
        }
        Content = grid;
        Update(monitor.Snapshot);
        Loaded += (_, _) => Subscribe(true);
        Unloaded += (_, _) => Subscribe(false);
    }

    private bool _subscribed;

    /// <summary>WPF can raise Loaded again without an Unloaded in between; subscribe once regardless.</summary>
    private void Subscribe(bool subscribe)
    {
        if (subscribe == _subscribed)
        {
            return;
        }
        _subscribed = subscribe;
        if (subscribe)
        {
            _monitor.SnapshotChanged += OnSnapshot;
        }
        else
        {
            _monitor.SnapshotChanged -= OnSnapshot;
        }
    }

    private void OnSnapshot(SystemSnapshot snapshot) => Dispatcher.BeginInvoke(() => Update(snapshot));

    internal void Update(SystemSnapshot snapshot)
    {
        foreach (var (kind, card) in _cards)
        {
            switch (kind)
            {
                case IslandSystemCard.Cpu:
                    card.SetUsage(snapshot.CpuUsage, snapshot.CpuUsage >= AttentionLoad);
                    break;
                case IslandSystemCard.Gpu:
                    card.SetUsage(snapshot.GpuUsage, snapshot.GpuUsage >= AttentionLoad);
                    break;
                case IslandSystemCard.Memory:
                    // Upstream's card always shows used memory, whichever measure the panel uses.
                    double? memory = snapshot is { MemoryUsed: { } used, MemoryTotal: > 0 and var total } ? (double)used / total : null;
                    card.SetUsage(memory, memory >= AttentionLoad);
                    break;
                case IslandSystemCard.Battery:
                    var charge = snapshot.Power?.ChargePercent;
                    card.Set(charge is { } c ? $"{c}%" : "…", charge / 100d,
                        charge is { } level && snapshot.Power is { ExternalConnected: false } && level <= AttentionBattery);
                    break;
                case IslandSystemCard.Network:
                    card.Set(
                        snapshot.NetDownBytesPerSec is { } down ? $"↓ {MetricFormat.BytesPerSec(down)}" : "…",
                        detail: snapshot.NetUpBytesPerSec is { } up ? $"↑ {MetricFormat.BytesPerSec(up)}" : string.Empty);
                    break;
                case IslandSystemCard.Disk:
                    var disk = snapshot.PrimaryDisk;
                    var free = disk is null ? (double?)null : 1 - disk.UsedFraction;
                    card.Set(disk is null ? "…" : MetricFormat.DiskBytes(disk.FreeBytes), free, free < AttentionFreeDisk);
                    break;
            }
        }
    }

    private string Title(IslandSystemCard kind) => kind switch
    {
        IslandSystemCard.Cpu => _s.Cpu,
        IslandSystemCard.Gpu => _s.Gpu,
        IslandSystemCard.Memory => _s.Memory,
        IslandSystemCard.Battery => _s.Battery,
        IslandSystemCard.Network => _s.NetworkSection,
        _ => _s.Available,
    };

    private static string Glyph(IslandSystemCard kind) => kind switch
    {
        IslandSystemCard.Cpu => "",
        IslandSystemCard.Gpu => "",
        IslandSystemCard.Memory => "",
        IslandSystemCard.Battery => "",
        IslandSystemCard.Network => "",
        _ => "",
    };

    /// <summary>One tile: title, large value, and either a meter or a detail line.</summary>
    private sealed class Card
    {
        private static readonly Brush TileFill = Frozen(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
        private static readonly Brush MeterTrack = Frozen(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
        private static readonly Brush MeterFill = Frozen(Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF));
        private static readonly Brush Attention = Frozen(Color.FromRgb(0xFF, 0x9F, 0x0A));

        private readonly TextBlock _value = new()
        {
            FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
            FontWeight = FontWeights.SemiBold,
            Foreground = IslandPalette.Primary,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 8, 0, 0),
        };
        private readonly TextBlock _detail = new()
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 11,
            Foreground = IslandPalette.Secondary,
            Margin = new Thickness(0, 6, 0, 0),
        };
        private readonly Border _meter = new() { Height = 5, CornerRadius = new CornerRadius(2.5), Background = MeterTrack, Margin = new Thickness(0, 10, 0, 0) };
        private readonly Border _meterFill = new() { Height = 5, CornerRadius = new CornerRadius(2.5), HorizontalAlignment = HorizontalAlignment.Left };

        public Card(string glyph, string title, bool hasDetail)
        {
            _value.FontSize = hasDetail ? 17 : 25;
            _meter.Child = _meterFill;
            _meter.Visibility = hasDetail ? Visibility.Collapsed : Visibility.Visible;
            _detail.Visibility = hasDetail ? Visibility.Visible : Visibility.Collapsed;
            _meter.SizeChanged += (_, _) => ResizeFill();

            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 11,
                Foreground = IslandPalette.Secondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
            });
            header.Children.Add(new TextBlock { Text = title, FontSize = 11, Foreground = IslandPalette.Secondary, VerticalAlignment = VerticalAlignment.Center });

            var stack = new StackPanel();
            stack.Children.Add(header);
            stack.Children.Add(_value);
            stack.Children.Add(_detail);
            stack.Children.Add(_meter);
            Root = new Border
            {
                Background = TileFill,
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(12),
                MinHeight = 72,
                Margin = new Thickness(5, 0, 5, 0),
                Child = stack,
            };
        }

        public UIElement Root { get; }

        private double _level;

        public void SetUsage(double? fraction, bool attention) =>
            Set(fraction is { } f && double.IsFinite(f) ? MetricFormat.Percent(f) : "…", fraction, attention);

        public void Set(string value, double? level = null, bool attention = false, string? detail = null)
        {
            _value.Text = value;
            _detail.Text = detail ?? string.Empty;
            _level = level is { } l && double.IsFinite(l) ? Math.Clamp(l, 0, 1) : 0;
            _meterFill.Background = attention ? Attention : MeterFill;
            ResizeFill();
        }

        private void ResizeFill() =>
            _meterFill.Width = _level > 0 ? Math.Max(_meter.Height, _meter.ActualWidth * _level) : 0;

        private static Brush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
