// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of NSPopover anchoring under the status item in Sources/Vorssaint/App/AppDelegate.swift,
// and the panel's maxHeight rule in UI/MenuPanel/MenuPanelView.swift (lines 92-96).

namespace Faqra.Core.Panel;

/// <summary>A screen rectangle in physical pixels.</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;

    public int CenterX => Left + Width / 2;

    public int CenterY => Top + Height / 2;
}

public readonly record struct PixelPoint(int X, int Y);

public static class PanelPlacement
{
    /// <summary>
    /// Where the panel's top-left corner goes. It opens against whichever edge the taskbar occupies,
    /// the gap away from it like Windows' own tray flyouts, centered on the tray icon along that edge
    /// and clamped inside the work area. Without an icon rectangle (the icon is in the overflow) it
    /// takes the work area's corner on the taskbar side.
    /// </summary>
    public static PixelPoint Origin(PixelRect? icon, PixelRect monitor, PixelRect workArea, int width, int height, int gap)
    {
        var taskbarBottom = workArea.Bottom < monitor.Bottom;
        var taskbarTop = workArea.Top > monitor.Top;
        var taskbarLeft = workArea.Left > monitor.Left;

        int x, y;
        if (taskbarBottom || taskbarTop || (!taskbarLeft && workArea.Right == monitor.Right))
        {
            // A horizontal taskbar (or none): the panel hangs off it, centered on the icon.
            x = (icon?.CenterX ?? workArea.Right) - width / 2;
            y = taskbarTop ? workArea.Top + gap : workArea.Bottom - gap - height;
        }
        else
        {
            // A vertical taskbar: the panel sits beside it, level with the icon.
            x = taskbarLeft ? workArea.Left + gap : workArea.Right - gap - width;
            y = (icon?.CenterY ?? workArea.Bottom) - height / 2;
        }

        return new PixelPoint(
            Clamp(x, workArea.Left + gap, workArea.Right - gap - width),
            Clamp(y, workArea.Top + gap, workArea.Bottom - gap - height));
    }

    /// <summary>The tallest the panel may grow: the work area less a gap on each side, never below <paramref name="floor"/>.</summary>
    public static int MaxHeight(PixelRect workArea, int gap, int floor) => Math.Max(floor, workArea.Height - gap * 2);

    /// <summary>Clamps toward <paramref name="min"/> when the range is empty, so a huge panel still starts on screen.</summary>
    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(value, max));
}
