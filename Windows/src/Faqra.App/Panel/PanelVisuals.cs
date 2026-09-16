// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the shared panel components of Sources/Vorssaint/UI/MenuPanel: panelCard (UI/Theme.swift
// 133-153), UsageBar and PressureIndicator (SystemSection.swift 548-614), the rate column
// (NetworkSection.swift 162-226) and Sparkline.swift. Colors come from WPF UI theme brushes.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Faqra.Core.Metrics;

namespace Faqra.App.Panel;

/// <summary>Theme brush keys the panel draws with, so light and dark both follow Windows.</summary>
internal static class PanelBrushes
{
    public const string Primary = "TextFillColorPrimaryBrush";
    public const string Secondary = "TextFillColorSecondaryBrush";
    public const string Tertiary = "TextFillColorTertiaryBrush";
    public const string Accent = "AccentFillColorDefaultBrush";
    public const string Success = "SystemFillColorSuccessBrush";
    public const string Caution = "SystemFillColorCautionBrush";
    public const string Critical = "SystemFillColorCriticalBrush";
    public const string Card = "CardBackgroundFillColorDefaultBrush";
    public const string CardStroke = "CardStrokeColorDefaultBrush";
    public const string Track = "ControlStrongStrokeColorDisabledBrush";
    public const string Subtle = "SubtleFillColorSecondaryBrush";
}

/// <summary>Text and glyph factories on the Faqra type scale.</summary>
internal static class PanelText
{
    public static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static TextBlock Label(string text, double size = 12, string brush = PanelBrushes.Secondary, FontWeight? weight = null)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight ?? FontWeights.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        block.SetResourceReference(TextBlock.FontFamilyProperty, "FaqraFont");
        block.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return block;
    }

    /// <summary>A measured value: the design system's mono face, so digits never jitter as they change.</summary>
    public static TextBlock Value(string text, double size = 12, string brush = PanelBrushes.Primary, FontWeight? weight = null)
    {
        var block = Label(text, size, brush, weight ?? FontWeights.SemiBold);
        block.SetResourceReference(TextBlock.FontFamilyProperty, "FaqraMonoFont");
        return block;
    }

    public static TextBlock Glyph(string glyph, double size = 12, string brush = PanelBrushes.Secondary)
    {
        var block = new TextBlock
        {
            Text = glyph,
            FontFamily = IconFont,
            FontSize = size,
            VerticalAlignment = VerticalAlignment.Center,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return block;
    }

    public static TextBlock Subsection(string text) => Label(text, 12, PanelBrushes.Secondary, FontWeights.SemiBold);
}

/// <summary>A section card: Fluent's card fill and 1px stroke on the 8px radius.</summary>
internal sealed class PanelCard : Border
{
    public PanelCard(UIElement content)
    {
        CornerRadius = new CornerRadius(8);
        BorderThickness = new Thickness(1);
        Padding = new Thickness(12);
        Child = content;
        SetResourceReference(BackgroundProperty, PanelBrushes.Card);
        SetResourceReference(BorderBrushProperty, PanelBrushes.CardStroke);
    }
}

/// <summary>A thin horizontal meter. Without an explicit tint it turns caution at 60% and critical at 85%, like upstream.</summary>
internal sealed class UsageBar : FrameworkElement
{
    public static readonly DependencyProperty TrackBrushProperty = Brush(nameof(TrackBrush));
    public static readonly DependencyProperty NormalBrushProperty = Brush(nameof(NormalBrush));
    public static readonly DependencyProperty CautionBrushProperty = Brush(nameof(CautionBrush));
    public static readonly DependencyProperty CriticalBrushProperty = Brush(nameof(CriticalBrush));

    private double _fraction;
    private string? _tint;

    public UsageBar(double height = 4)
    {
        Height = height;
        VerticalAlignment = VerticalAlignment.Center;
        SetResourceReference(TrackBrushProperty, PanelBrushes.Track);
        SetResourceReference(NormalBrushProperty, PanelBrushes.Accent);
        SetResourceReference(CautionBrushProperty, PanelBrushes.Caution);
        SetResourceReference(CriticalBrushProperty, PanelBrushes.Critical);
    }

    public Brush? TrackBrush { get => (Brush?)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public Brush? NormalBrush { get => (Brush?)GetValue(NormalBrushProperty); set => SetValue(NormalBrushProperty, value); }
    public Brush? CautionBrush { get => (Brush?)GetValue(CautionBrushProperty); set => SetValue(CautionBrushProperty, value); }
    public Brush? CriticalBrush { get => (Brush?)GetValue(CriticalBrushProperty); set => SetValue(CriticalBrushProperty, value); }

    /// <summary>Sets the fill; <paramref name="tint"/> pins it to one of the <see cref="PanelBrushes"/> keys.</summary>
    public void Set(double fraction, string? tint = null)
    {
        var clamped = double.IsFinite(fraction) ? Math.Clamp(fraction, 0, 1) : 0;
        if (clamped == _fraction && tint == _tint)
        {
            return;
        }
        _fraction = clamped;
        if (tint != _tint)
        {
            _tint = tint;
            if (tint is not null)
            {
                SetResourceReference(NormalBrushProperty, tint);
            }
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }
        var radius = height / 2;
        dc.DrawRoundedRectangle(TrackBrush, null, new Rect(0, 0, width, height), radius, radius);
        var fill = _tint is not null ? NormalBrush : _fraction < 0.6 ? NormalBrush : _fraction < 0.85 ? CautionBrush : CriticalBrush;
        dc.DrawRoundedRectangle(fill, null, new Rect(0, 0, Math.Max(height, width * _fraction), height), radius, radius);
    }

    private static DependencyProperty Brush(string name) =>
        DependencyProperty.Register(name, typeof(Brush), typeof(UsageBar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
}

/// <summary>
/// A history graph: straight segments over a fill that fades to nothing, on a zero baseline.
/// A second series draws over the first with a lighter fill, as network and disk do.
/// </summary>
internal sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush), typeof(Brush), typeof(Sparkline), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SecondBrushProperty = DependencyProperty.Register(
        nameof(SecondBrush), typeof(Brush), typeof(Sparkline), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BaselineBrushProperty = DependencyProperty.Register(
        nameof(BaselineBrush), typeof(Brush), typeof(Sparkline), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double LineWidth = 1.5;
    private const double FillOpacity = 0.16;
    private const double SecondFillOpacity = 0.08;

    private IReadOnlyList<double> _values = [];
    private IReadOnlyList<double> _second = [];
    private double? _maxValue;

    public Sparkline(double height, string brush, string? secondBrush = null)
    {
        Height = height;
        SetResourceReference(LineBrushProperty, brush);
        if (secondBrush is not null)
        {
            SetResourceReference(SecondBrushProperty, secondBrush);
        }
        SetResourceReference(BaselineBrushProperty, PanelBrushes.Track);
    }

    public Brush? LineBrush { get => (Brush?)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public Brush? SecondBrush { get => (Brush?)GetValue(SecondBrushProperty); set => SetValue(SecondBrushProperty, value); }
    public Brush? BaselineBrush { get => (Brush?)GetValue(BaselineBrushProperty); set => SetValue(BaselineBrushProperty, value); }

    /// <summary>
    /// <paramref name="maxValue"/> fixes the scale (1 for fractions); null scales to the shared peak
    /// of both series, never below 1, so a quiet line does not fill the graph.
    /// </summary>
    public void Set(IReadOnlyList<double> values, IReadOnlyList<double>? second = null, double? maxValue = null)
    {
        _values = values;
        _second = second ?? [];
        _maxValue = maxValue;
        Visibility = values.Count >= 2 ? Visibility.Visible : Visibility.Collapsed;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0 || _values.Count < 2)
        {
            return;
        }
        var peak = Math.Max(_maxValue ?? Math.Max(1, Math.Max(Peak(_values), Peak(_second))), 0.0001);
        var baseline = height - 0.5;
        dc.DrawLine(new Pen(BaselineBrush, 1), new Point(0, baseline), new Point(width, baseline));
        Draw(dc, _values, LineBrush, FillOpacity, width, baseline, peak);
        if (_second.Count >= 2)
        {
            Draw(dc, _second, SecondBrush ?? LineBrush, SecondFillOpacity, width, baseline, peak);
        }
    }

    private static void Draw(DrawingContext dc, IReadOnlyList<double> values, Brush? brush, double fillOpacity, double width, double baseline, double peak)
    {
        if (brush is not SolidColorBrush solid)
        {
            return;
        }
        var plotHeight = Math.Max(1, baseline - 0.5);
        var points = new Point[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var value = double.IsFinite(values[i]) ? values[i] : 0;
            points[i] = new Point(width * i / (values.Count - 1), baseline - plotHeight * Math.Clamp(value / peak, 0, 1));
        }

        var area = new StreamGeometry();
        using (var ctx = area.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, baseline), isFilled: true, isClosed: true);
            ctx.PolyLineTo(points, isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new Point(points[^1].X, baseline), isStroked: false, isSmoothJoin: false);
        }
        area.Freeze();
        var top = solid.Color;
        var fill = new LinearGradientBrush(
            Color.FromArgb((byte)(255 * fillOpacity), top.R, top.G, top.B),
            Color.FromArgb(0, top.R, top.G, top.B),
            90);
        fill.Freeze();
        dc.DrawGeometry(fill, null, area);

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(points[0], isFilled: false, isClosed: false);
            ctx.PolyLineTo(points[1..], isStroked: true, isSmoothJoin: true);
        }
        line.Freeze();
        var pen = new Pen(solid, LineWidth) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        pen.Freeze();
        dc.DrawGeometry(null, pen, line);
    }

    private static double Peak(IReadOnlyList<double> values) => values.Count == 0 ? 0 : values.Where(double.IsFinite).DefaultIfEmpty(0).Max();
}

/// <summary>An arrow, a live rate and its caption, e.g. Download above 1.2 MB/s.</summary>
internal sealed class RateColumn : Grid
{
    private readonly TextBlock _value;

    public RateColumn(string glyph, string label, string brush)
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = PanelText.Glyph(glyph, 14, brush);
        icon.Margin = new Thickness(0, 0, 8, 0);
        Children.Add(icon);

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _value = PanelText.Value(string.Empty, 14);
        stack.Children.Add(_value);
        stack.Children.Add(PanelText.Label(label, 12));
        SetColumn(stack, 1);
        Children.Add(stack);
    }

    public void Set(string value) => _value.Text = value;
}

/// <summary>A colored dot and word in a pill. The word carries the meaning, so color is never the only signal.</summary>
internal sealed class PressureIndicator : Border
{
    private readonly System.Windows.Shapes.Ellipse _dot = new() { Width = 7, Height = 7, Margin = new Thickness(0, 0, 5, 0) };
    private readonly TextBlock _label = PanelText.Label(string.Empty, 12, PanelBrushes.Primary, FontWeights.SemiBold);

    public PressureIndicator()
    {
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(8, 2, 8, 2);
        VerticalAlignment = VerticalAlignment.Center;
        SetResourceReference(BackgroundProperty, PanelBrushes.Subtle);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        _dot.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(_dot);
        row.Children.Add(_label);
        Child = row;
    }

    public void Set(MemoryPressure pressure, Core.Localization.MonitorStrings s)
    {
        var (brush, text) = pressure switch
        {
            MemoryPressure.Normal => (PanelBrushes.Success, s.PressureNormal),
            MemoryPressure.Warning => (PanelBrushes.Caution, s.PressureWarning),
            MemoryPressure.Critical => (PanelBrushes.Critical, s.PressureCritical),
            _ => (PanelBrushes.Tertiary, "-"),
        };
        _dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, brush);
        _label.Text = text;
    }
}
