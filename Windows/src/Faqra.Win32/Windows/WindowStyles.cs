// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the NSPanel flags in Sources/Vorssaint/Services/Notch/NotchWindowHost.swift
// (borderless, nonactivating, above status items, on every Space, out of app cycling).

using Faqra.Win32.Native;

namespace Faqra.Win32.Windows;

/// <summary>Turns an ordinary WPF window into a non-activating always-on-top overlay.</summary>
public static class WindowStyles
{
    /// <summary>
    /// Applies the overlay styles: clicking it never activates the app, it stays above other
    /// windows, and it is absent from Alt+Tab and the taskbar.
    /// </summary>
    public static void MakeNonActivatingOverlay(IntPtr hwnd)
    {
        var exStyle = User32.GetWindowLongPtrW(hwnd, User32.GWL_EXSTYLE).ToInt64();
        exStyle |= User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW | User32.WS_EX_TOPMOST;
        exStyle &= ~(long)User32.WS_EX_APPWINDOW;
        User32.SetWindowLongPtrW(hwnd, User32.GWL_EXSTYLE, new IntPtr(exStyle));
    }

    /// <summary>Keeps an activating window out of Alt+Tab and the taskbar.</summary>
    public static void MakeToolWindow(IntPtr hwnd)
    {
        var exStyle = User32.GetWindowLongPtrW(hwnd, User32.GWL_EXSTYLE).ToInt64();
        exStyle = (exStyle | User32.WS_EX_TOOLWINDOW) & ~(long)User32.WS_EX_APPWINDOW;
        User32.SetWindowLongPtrW(hwnd, User32.GWL_EXSTYLE, new IntPtr(exStyle));
    }

    /// <summary>Re-asserts topmost without moving, resizing or activating the window.</summary>
    public static void BringToTop(IntPtr hwnd) =>
        User32.SetWindowPos(hwnd, User32.HWND_TOPMOST, 0, 0, 0, 0,
            User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);

    /// <summary>Moves and resizes in one call, without activating.</summary>
    public static void SetBounds(IntPtr hwnd, int x, int y, int width, int height) =>
        User32.SetWindowPos(hwnd, User32.HWND_TOPMOST, x, y, width, height, User32.SWP_NOACTIVATE);

    /// <summary>
    /// Turns the no-activate flag on or off. A hover-opened island keeps it on so it never steals
    /// focus; a click-opened one drops it for as long as it needs the keyboard, then restores it.
    /// </summary>
    public static void SetNonActivating(IntPtr hwnd, bool nonActivating)
    {
        var exStyle = User32.GetWindowLongPtrW(hwnd, User32.GWL_EXSTYLE).ToInt64();
        exStyle = nonActivating
            ? exStyle | User32.WS_EX_NOACTIVATE
            : exStyle & ~(long)User32.WS_EX_NOACTIVATE;
        User32.SetWindowLongPtrW(hwnd, User32.GWL_EXSTYLE, new IntPtr(exStyle));
    }

    public static IntPtr ForegroundWindow() => User32.GetForegroundWindow();

    public static void Focus(IntPtr hwnd) => User32.SetForegroundWindow(hwnd);

    /// <summary>Lets clicks pass through to whatever is underneath.</summary>
    public static void SetClickThrough(IntPtr hwnd, bool clickThrough)
    {
        var exStyle = User32.GetWindowLongPtrW(hwnd, User32.GWL_EXSTYLE).ToInt64();
        exStyle = clickThrough ? exStyle | User32.WS_EX_TRANSPARENT : exStyle & ~(long)User32.WS_EX_TRANSPARENT;
        User32.SetWindowLongPtrW(hwnd, User32.GWL_EXSTYLE, new IntPtr(exStyle));
    }

    /// <summary>Shows the window without taking focus away from whatever the user is using.</summary>
    public static void ShowWithoutActivating(IntPtr hwnd) => User32.ShowWindow(hwnd, User32.SW_SHOWNOACTIVATE);

    public static void Hide(IntPtr hwnd) => User32.ShowWindow(hwnd, User32.SW_HIDE);
}
