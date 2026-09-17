// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of BlackHoleGlyph in Sources/Vorssaint/App/StatusItemController.swift, with
// Faqra's own mark (upstream's logo is trademarked and may not be reused).

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Faqra.Core.Defaults;

namespace Faqra.App.Tray;

/// <summary>
/// Draws the Faqra mark: a rounded frame with a small island pill hanging from its top edge.
/// The idle state adapts to the taskbar theme; the keep-awake state uses the upstream orange tint.
/// </summary>
public static class GlyphPainter
{
    public static readonly Color ActiveTint = Color.FromRgb(0xFF, 0x9F, 0x0A);

    private static readonly Typeface IconFace = new(new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    /// <summary>Upstream's system tints (systemOrange, systemGreen and so on), as the dark-appearance values.</summary>
    public static Color? TintColor(KeepAwakeIconTint tint) => tint switch
    {
        KeepAwakeIconTint.Orange => ActiveTint,
        KeepAwakeIconTint.Green => Color.FromRgb(0x30, 0xD1, 0x58),
        KeepAwakeIconTint.Blue => Color.FromRgb(0x0A, 0x84, 0xFF),
        KeepAwakeIconTint.Purple => Color.FromRgb(0xBF, 0x5A, 0xF2),
        KeepAwakeIconTint.Pink => Color.FromRgb(0xFF, 0x37, 0x5F),
        _ => null,
    };

    /// <summary>Segoe Fluent Icons stand-ins for upstream's SF Symbols; the brand style draws the Faqra mark.</summary>
    public static string? IconGlyph(KeepAwakeActiveIcon icon) => icon switch
    {
        KeepAwakeActiveIcon.Coffee => "",  // Cafe (cup.and.saucer.fill)
        KeepAwakeActiveIcon.Eye => "",     // View (eye.fill)
        KeepAwakeActiveIcon.Moon => "",    // QuietHours (moon.fill)
        KeepAwakeActiveIcon.Light => "",   // Lightbulb (lightbulb.fill)
        _ => null,
    };

    public static void Draw(DrawingContext dc, int pixels, bool active, bool lightTaskbar) =>
        Draw(dc, pixels, active, lightTaskbar, KeepAwakeActiveIcon.Brand, KeepAwakeIconTint.Orange);

    public static void Draw(DrawingContext dc, int pixels, bool active, bool lightTaskbar, KeepAwakeActiveIcon icon, KeepAwakeIconTint tint)
    {
        var mono = lightTaskbar ? Colors.Black : Colors.White;
        var ink = active ? TintColor(tint) ?? mono : mono;
        var brush = new SolidColorBrush(ink);
        brush.Freeze();

        if (active && IconGlyph(icon) is { } glyph)
        {
            DrawSymbol(dc, pixels, glyph, brush);
            return;
        }

        // Keep at least one clear pixel on every side so the mark never touches the slot edge.
        var inset = Math.Max(1.5, pixels * 0.125);
        var stroke = Math.Max(1.5, pixels / 8.0);
        var half = stroke / 2;
        var frame = new Rect(inset + half, inset + half, pixels - 2 * (inset + half), pixels - 2 * (inset + half));
        var radius = pixels * 0.22;

        var pen = new Pen(brush, stroke) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        dc.DrawRoundedRectangle(null, pen, frame, radius, radius);

        var pillWidth = pixels * 0.38;
        var pillHeight = Math.Max(2.0, pixels * 0.16);
        var pill = new Rect((pixels - pillWidth) / 2, frame.Top + half, pillWidth, pillHeight);
        dc.DrawRoundedRectangle(brush, null, pill, pillHeight / 2, pillHeight / 2);
    }

    /// <summary>A symbol centered on its ink, a pixel clear of every edge.</summary>
    private static void DrawSymbol(DrawingContext dc, int pixels, string glyph, Brush brush)
    {
        var text = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, IconFace, pixels * 0.72, brush, 1.0);
        var geometry = text.BuildGeometry(new Point(0, 0));
        var bounds = geometry.Bounds;
        if (bounds.IsEmpty)
        {
            return;
        }
        var available = pixels - 2 * Math.Max(1.5, pixels * 0.125);
        var scale = Math.Min(1, available / Math.Max(bounds.Width, bounds.Height));
        var group = new TransformGroup();
        group.Children.Add(new TranslateTransform(-bounds.X - bounds.Width / 2, -bounds.Y - bounds.Height / 2));
        group.Children.Add(new ScaleTransform(scale, scale));
        group.Children.Add(new TranslateTransform(pixels / 2.0, pixels / 2.0));
        geometry.Transform = group;
        dc.DrawGeometry(brush, null, geometry);
    }
}
