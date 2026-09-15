// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of BlackHoleGlyph in Sources/Vorssaint/App/StatusItemController.swift, with
// Faqra's own mark (upstream's logo is trademarked and may not be reused).

using System.Windows;
using System.Windows.Media;

namespace Faqra.App.Tray;

/// <summary>
/// Draws the Faqra mark: a rounded frame with a small island pill hanging from its top edge.
/// The idle state adapts to the taskbar theme; the keep-awake state uses the upstream orange tint.
/// </summary>
public static class GlyphPainter
{
    public static readonly Color ActiveTint = Color.FromRgb(0xFF, 0x9F, 0x0A);

    public static void Draw(DrawingContext dc, int pixels, bool active, bool lightTaskbar)
    {
        var ink = active ? ActiveTint : (lightTaskbar ? Colors.Black : Colors.White);
        var brush = new SolidColorBrush(ink);
        brush.Freeze();

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
}
