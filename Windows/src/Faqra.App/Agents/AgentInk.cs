// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Windows.Media;
using Faqra.App.Island.Modules;
using Faqra.Core.Agents;

namespace Faqra.App.Agents;

/// <summary>
/// The orb's ink on the island's fixed dark surface. Caution, critical and success are Windows 11's
/// dark-theme semantic colours; the rest come from <see cref="IslandPalette"/>.
/// </summary>
public static class AgentInk
{
    public static readonly Brush Caution = Frozen(0xFC, 0xE1, 0x00);
    public static readonly Brush Critical = Frozen(0xFF, 0x99, 0xA4);
    public static readonly Brush Success = Frozen(0x6C, 0xCB, 0x5F);

    public static Brush For(AgentTone tone) => tone switch
    {
        AgentTone.Caution => Caution,
        AgentTone.Critical => Critical,
        AgentTone.Success => Success,
        AgentTone.Accent => IslandPalette.Accent,
        AgentTone.Secondary => IslandPalette.Secondary,
        _ => IslandPalette.Primary,
    };

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
