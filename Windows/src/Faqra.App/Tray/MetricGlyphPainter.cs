// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the metricBlock and networkBlock images in Sources/Vorssaint/App/MenuBarRenderer.swift
// (lines 973-1029, 1123-1182): a small label over a bold value, or two rate lines, drawn into one tray icon.

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Faqra.Core.Tray;
using Faqra.Win32.Icons;

namespace Faqra.App.Tray;

/// <summary>
/// Rasterizes a metric into a square tray icon. Text shrinks to fit the slot width, because a tray
/// icon is one fixed square and "100%" has to fit where "5%" does; the tooltip carries the full text.
/// </summary>
public static class MetricGlyphPainter
{
    private static readonly Typeface LabelFace = new(new FontFamily("Segoe UI Variable Small, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Typeface ValueFace = new(new FontFamily("Segoe UI Variable Small, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public static NativeIcon RenderIcon(int pixels, TrayMetricText text, bool lightTaskbar) =>
        IconFactory.CreateFromPbgra32(pixels, pixels, RenderPixels(pixels, text, lightTaskbar));

    public static byte[] RenderPixels(int pixels, TrayMetricText text, bool lightTaskbar)
    {
        var visual = new DrawingVisual();
        TextOptions.SetTextFormattingMode(visual, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Grayscale);
        using (var dc = visual.RenderOpen())
        {
            Draw(dc, pixels, text, lightTaskbar);
        }
        var bitmap = new RenderTargetBitmap(pixels, pixels, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var buffer = new byte[pixels * pixels * 4];
        bitmap.CopyPixels(buffer, pixels * 4, 0);
        return buffer;
    }

    private static void Draw(DrawingContext dc, int pixels, TrayMetricText text, bool lightTaskbar)
    {
        var ink = new SolidColorBrush(lightTaskbar ? Color.FromRgb(0x1A, 0x1A, 0x1A) : Color.FromRgb(0xF5, 0xF5, 0xF5));
        ink.Freeze();
        // A rate icon is two equal lines; a usage icon is a small label over a larger value.
        var isRate = text.Top.Length > 3;
        var topShare = isRate ? 0.5 : 0.42;
        var width = pixels - 1.0;

        var top = Fit(text.Top, isRate ? ValueFace : LabelFace, pixels * topShare * 0.98, width, ink);
        var bottom = Fit(pixels < SmallSlot ? Tighten(text.Bottom) : text.Bottom, ValueFace, pixels * (1 - topShare) * 0.98, width, ink);
        var gap = Math.Max(0, (pixels - top.Height - bottom.Height) / 3);

        dc.DrawText(top, new Point((pixels - top.WidthIncludingTrailingWhitespace) / 2, gap));
        dc.DrawText(bottom, new Point((pixels - bottom.WidthIncludingTrailingWhitespace) / 2, pixels - gap - bottom.Height));
    }

    /// <summary>Below this many pixels (100% scaling) every glyph counts, so values drop what the label already says.</summary>
    private const int SmallSlot = 20;

    /// <summary>"23%" becomes "23" under a CPU label, and "3h42m" becomes "3:42".</summary>
    internal static string Tighten(string value)
    {
        if (value.Length > 1 && value.EndsWith('%'))
        {
            return value[..^1];
        }
        var match = System.Text.RegularExpressions.Regex.Match(value, @"^(\d+)h(\d+)m$");
        return match.Success ? $"{match.Groups[1].Value}:{match.Groups[2].Value.PadLeft(2, '0')}" : value;
    }

    /// <summary>Largest size up to <paramref name="maxHeight"/> at which the text fits <paramref name="maxWidth"/>.</summary>
    private static FormattedText Fit(string value, Typeface face, double maxHeight, double maxWidth, Brush ink)
    {
        // FormattedText's height includes line spacing, roughly 1.33 times the em size for Segoe UI.
        var size = Math.Max(4, maxHeight / 1.33);
        FormattedText formatted;
        do
        {
            formatted = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, ink, 1.0);
            size -= 0.25;
        }
        while (formatted.WidthIncludingTrailingWhitespace > maxWidth && size > 4);
        formatted.LineHeight = Math.Max(1, formatted.Extent);
        return formatted;
    }
}
