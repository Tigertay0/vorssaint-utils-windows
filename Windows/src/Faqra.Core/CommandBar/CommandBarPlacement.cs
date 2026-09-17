// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors CommandBarPreferences.clampedPanelOrigin (Sources/Vorssaint/Services/CommandBar/CommandBarPreferences.swift:343-355)
// in Windows' top-down pixels. Upstream's origin is the bottom-left corner 72% up the visible frame,
// which puts the top edge 28% down; the list then grows downward from that edge.

using Faqra.Core.Panel;

namespace Faqra.Core.CommandBar;

public static class CommandBarPlacement
{
    private const double TopFraction = 0.28;

    public static PixelPoint Origin(PixelRect work, int width, int height, int margin)
    {
        var wantedX = work.Left + (work.Right - work.Left - width) / 2;
        var wantedY = work.Top + (int)Math.Floor((work.Bottom - work.Top) * TopFraction);
        var minX = work.Left + margin;
        var maxX = Math.Max(minX, work.Right - width - margin);
        var minY = work.Top + margin;
        var maxY = Math.Max(minY, work.Bottom - height - margin);
        return new PixelPoint(Math.Clamp(wantedX, minX, maxX), Math.Clamp(wantedY, minY, maxY));
    }
}
