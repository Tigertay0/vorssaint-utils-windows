// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Windows.Media;

namespace Faqra.App.Island.Modules;

/// <summary>
/// The island is a black surface floating over the desktop, not a themed window, so its colors are
/// fixed rather than taken from the light or dark theme. One place defines them.
/// </summary>
public static class IslandPalette
{
    public static readonly Brush Surface = Frozen(Color.FromRgb(0x0A, 0x0A, 0x0A));
    public static readonly Brush Primary = Frozen(Color.FromRgb(0xF2, 0xF2, 0xF2));
    public static readonly Brush Secondary = Frozen(Color.FromRgb(0xB9, 0xB9, 0xB9));
    public static readonly Brush Tertiary = Frozen(Color.FromRgb(0x8A, 0x8A, 0x8A));
    public static readonly Brush Fill = Frozen(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
    public static readonly Brush FillStrong = Frozen(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
    public static readonly Brush Accent = Frozen(Color.FromRgb(0x64, 0xD2, 0xFF));

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
