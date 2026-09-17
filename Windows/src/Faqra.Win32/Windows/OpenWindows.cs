// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of Sources/Vorssaint/Services/WindowEnumerator.swift and WindowActivator.swift for the
// command bar's window rows: the windows Alt+Tab would show, and bringing one to the front.

using System.Runtime.InteropServices;
using System.Text;
using Faqra.Win32.Diagnostics;

namespace Faqra.Win32.Windows;

public sealed record OpenWindow(IntPtr Handle, string Title, int ProcessId, string? ExecutablePath);

public static class OpenWindows
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x80;
    private const long WS_EX_APPWINDOW = 0x40000;
    private const long WS_EX_NOACTIVATE = 0x08000000;
    private const uint GW_OWNER = 4;
    private const int DWMWA_CLOAKED = 14;
    private const int SW_RESTORE = 9;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>Top-level windows a user can switch to, front to back, excluding this process's own.</summary>
    public static IReadOnlyList<OpenWindow> Enumerate()
    {
        var own = Environment.ProcessId;
        var windows = new List<OpenWindow>();
        var paths = new Dictionary<int, string?>();
        EnumWindows((hWnd, _) =>
        {
            if (!IsSwitchable(hWnd))
            {
                return true;
            }
            GetWindowThreadProcessId(hWnd, out var pid);
            if ((int)pid == own)
            {
                return true;
            }
            var title = TitleOf(hWnd);
            if (title.Length == 0)
            {
                return true;
            }
            if (!paths.TryGetValue((int)pid, out var path))
            {
                path = ProcessImage.PathOf((int)pid);
                paths[(int)pid] = path;
            }
            windows.Add(new OpenWindow(hWnd, title, (int)pid, path));
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    /// <summary>Restores a minimized window and brings it to the front. Needs this process to be foreground.</summary>
    public static bool Activate(IntPtr hWnd)
    {
        if (!IsWindow(hWnd))
        {
            return false;
        }
        if (IsIconic(hWnd))
        {
            Native.User32.ShowWindow(hWnd, SW_RESTORE);
        }
        return Native.User32.SetForegroundWindow(hWnd);
    }

    // Raymond Chen's Alt+Tab rule, plus DWM cloaking (windows on other virtual desktops, suspended UWP frames).
    private static bool IsSwitchable(IntPtr hWnd)
    {
        if (!IsWindowVisible(hWnd))
        {
            return false;
        }
        var exStyle = (long)Native.User32.GetWindowLongPtrW(hWnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_TOOLWINDOW) != 0 || ((exStyle & WS_EX_NOACTIVATE) != 0 && (exStyle & WS_EX_APPWINDOW) == 0))
        {
            return false;
        }
        if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero && (exStyle & WS_EX_APPWINDOW) == 0)
        {
            return false;
        }
        return !(DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 && cloaked != 0);
    }

    private static string TitleOf(IntPtr hWnd)
    {
        var length = GetWindowTextLengthW(hWnd);
        if (length <= 0)
        {
            return string.Empty;
        }
        var buffer = new StringBuilder(length + 1);
        GetWindowTextW(hWnd, buffer, buffer.Capacity);
        return buffer.ToString().Trim();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLengthW(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);
}
