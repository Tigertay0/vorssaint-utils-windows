// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of NSScreen selection in Sources/Vorssaint/Services/Notch/NotchSupport.swift
// (lines 606-616): pick the display that hosts the island.

using Faqra.Win32.Native;

namespace Faqra.Win32.Display;

/// <summary>One display, in physical pixels, with the scale needed to convert to DIPs.</summary>
public sealed record MonitorGeometry(RECT Bounds, RECT WorkArea, double Scale, bool IsPrimary)
{
    /// <summary>Full bounds in device-independent pixels.</summary>
    public double WidthDip => Bounds.Width / Scale;

    public double HeightDip => Bounds.Height / Scale;

    /// <summary>Converts a DIP offset inside this display to a physical screen coordinate.</summary>
    public (int X, int Y) ToScreenPixels(double dipX, double dipY) =>
        ((int)Math.Round(Bounds.Left + dipX * Scale), (int)Math.Round(Bounds.Top + dipY * Scale));

    public int ToPixels(double dip) => (int)Math.Round(dip * Scale);
}

public static class MonitorInfo
{
    private const uint MONITORINFOF_PRIMARY = 1;

    /// <summary>The primary display, which is where the island sits by default.</summary>
    public static MonitorGeometry Primary()
    {
        var origin = new POINT { X = 0, Y = 0 };
        return For(User32.MonitorFromPoint(origin, User32.MONITOR_DEFAULTTOPRIMARY));
    }

    /// <summary>The display a window is mostly on.</summary>
    public static MonitorGeometry ForWindow(IntPtr hwnd) =>
        For(User32.MonitorFromWindow(hwnd, User32.MONITOR_DEFAULTTONEAREST));

    private static MonitorGeometry For(IntPtr monitor)
    {
        var info = new MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!User32.GetMonitorInfoW(monitor, ref info))
        {
            // No monitor information: fall back to a 1080p primary at 100%, so the island still shows.
            var fallback = new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
            return new MonitorGeometry(fallback, fallback, 1.0, IsPrimary: true);
        }
        var scale = 1.0;
        if (Shcore.GetDpiForMonitor(monitor, Shcore.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
        {
            scale = dpiX / 96.0;
        }
        return new MonitorGeometry(info.rcMonitor, info.rcWork, scale, (info.dwFlags & MONITORINFOF_PRIMARY) != 0);
    }

    /// <summary>
    /// True while a program owns the screen in a way an overlay must not cover: exclusive
    /// fullscreen Direct3D, or a presentation the user asked not to be interrupted during.
    /// </summary>
    public static bool FullscreenAppIsRunning()
    {
        if (Shcore.SHQueryUserNotificationState(out var state) != 0)
        {
            return false;
        }
        return state is UserNotificationState.RunningD3DFullScreen or UserNotificationState.PresentationMode;
    }
}
