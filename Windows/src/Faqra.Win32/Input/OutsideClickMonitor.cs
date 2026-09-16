// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the global mouse-down monitor in
// Sources/Vorssaint/Services/Notch/NotchService.swift (lines 1109-1155) and the popover's
// outside-click monitor in App/AppDelegate.swift: a non-activating window gets no Deactivated
// event, so a low-level hook is the only way to learn about a click elsewhere.

using System.Runtime.InteropServices;
using Faqra.Win32.Native;

namespace Faqra.Win32.Input;

/// <summary>
/// Reports mouse-down anywhere on the screen while it is running. Installed only while a surface
/// is open, because a low-level hook runs for every mouse event in the session.
/// </summary>
public sealed class OutsideClickMonitor : IDisposable
{
    private readonly LowLevelMouseProc _callback;
    private IntPtr _hook;

    /// <summary>Screen coordinates of the press, in physical pixels.</summary>
    public event Action<POINT>? Pressed;

    public OutsideClickMonitor()
    {
        _callback = OnMouseEvent;
    }

    public bool IsRunning => _hook != IntPtr.Zero;

    public void Start()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }
        _hook = User32.SetWindowsHookExW(User32.WH_MOUSE_LL, _callback, Kernel32.GetModuleHandleW(null), 0);
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero)
        {
            return;
        }
        User32.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr OnMouseEvent(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && IsButtonDown((uint)wParam))
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            Pressed?.Invoke(data.pt);
        }
        // Never swallow the event: the click belongs to whatever the user aimed at.
        return User32.CallNextHookEx(_hook, code, wParam, lParam);
    }

    private static bool IsButtonDown(uint message) =>
        message is User32.WM_LBUTTONDOWN or User32.WM_RBUTTONDOWN or User32.WM_MBUTTONDOWN or User32.WM_NCXBUTTONDOWN;

    public void Dispose() => Stop();
}
