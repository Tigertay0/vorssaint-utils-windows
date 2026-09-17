// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the notification observers in Sources/Vorssaint/Services/KeepAwakeManager.swift
// (com.apple.screenIsLocked, didChangeScreenParametersNotification, IOPSNotificationCreateRunLoopSource)
// and of the Carbon hot key target in Services/HotkeyManager.swift.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Faqra.Win32.Native;

namespace Faqra.Win32.Windows;

/// <summary>
/// Hidden top-level window for the system broadcasts a background app needs: session lock, power
/// source, display topology and global hot keys. Top-level because WM_POWERBROADCAST and
/// WM_DISPLAYCHANGE are not delivered to message-only windows. UI thread only.
/// </summary>
public sealed class SystemEventsWindow : IDisposable
{
    private const string ClassName = "FaqraSystemEventsWindow";
    private const uint WM_DISPLAYCHANGE = 0x007E;
    private const uint WM_POWERBROADCAST = 0x0218;
    private const uint WM_WTSSESSION_CHANGE = 0x02B1;
    private const uint WM_HOTKEY = 0x0312;
    private const int PBT_APMPOWERSTATUSCHANGE = 0x000A;
    private const int PBT_APMRESUMEAUTOMATIC = 0x0012;
    private const int WTS_SESSION_LOCK = 0x7;
    private const int WTS_SESSION_UNLOCK = 0x8;
    private const int NOTIFY_FOR_THIS_SESSION = 0;
    private const uint MOD_NOREPEAT = 0x4000;

    private static readonly WndProc StaticProc = StaticWndProc;
    private static readonly IntPtr StaticProcPointer = Marshal.GetFunctionPointerForDelegate(StaticProc);
    private static readonly Dictionary<IntPtr, SystemEventsWindow> Instances = new();
    private static bool s_classRegistered;

    private readonly bool _sessionNotifications;

    public IntPtr Handle { get; private set; }

    /// <summary>True on lock, false on unlock.</summary>
    public event Action<bool>? SessionLockChanged;

    /// <summary>The power source or battery level changed, or the PC resumed.</summary>
    public event Action? PowerChanged;

    public event Action? DisplayChanged;

    public event Action<int>? HotKeyPressed;

    public SystemEventsWindow()
    {
        EnsureClassRegistered();
        Handle = User32.CreateWindowExW(0, ClassName, "Faqra system events", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, Kernel32.GetModuleHandleW(null), IntPtr.Zero);
        if (Handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWindowExW failed for the system events window");
        }
        Instances[Handle] = this;
        _sessionNotifications = WTSRegisterSessionNotification(Handle, NOTIFY_FOR_THIS_SESSION);
    }

    /// <summary>Registers a global hot key; false when another app already owns the combination.</summary>
    public bool RegisterHotKey(int id, uint modifiers, uint virtualKey) =>
        Handle != IntPtr.Zero && RegisterHotKeyNative(Handle, id, modifiers | MOD_NOREPEAT, virtualKey);

    public void UnregisterHotKey(int id)
    {
        if (Handle != IntPtr.Zero)
        {
            UnregisterHotKeyNative(Handle, id);
        }
    }

    private static void EnsureClassRegistered()
    {
        if (s_classRegistered)
        {
            return;
        }
        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = StaticProcPointer,
            hInstance = Kernel32.GetModuleHandleW(null),
            lpszClassName = ClassName,
        };
        if (User32.RegisterClassExW(ref wc) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "RegisterClassExW failed for the system events window");
        }
        s_classRegistered = true;
    }

    private static IntPtr StaticWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (Instances.TryGetValue(hWnd, out var window))
        {
            switch (msg)
            {
                case WM_WTSSESSION_CHANGE when (int)wParam == WTS_SESSION_LOCK:
                    window.SessionLockChanged?.Invoke(true);
                    return IntPtr.Zero;
                case WM_WTSSESSION_CHANGE when (int)wParam == WTS_SESSION_UNLOCK:
                    window.SessionLockChanged?.Invoke(false);
                    return IntPtr.Zero;
                case WM_POWERBROADCAST when (int)wParam is PBT_APMPOWERSTATUSCHANGE or PBT_APMRESUMEAUTOMATIC:
                    window.PowerChanged?.Invoke();
                    return (IntPtr)1;
                case WM_DISPLAYCHANGE:
                    window.DisplayChanged?.Invoke();
                    return IntPtr.Zero;
                case WM_HOTKEY:
                    window.HotKeyPressed?.Invoke((int)wParam);
                    return IntPtr.Zero;
            }
        }
        return User32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (Handle == IntPtr.Zero)
        {
            return;
        }
        if (_sessionNotifications)
        {
            WTSUnRegisterSessionNotification(Handle);
        }
        Instances.Remove(Handle);
        User32.DestroyWindow(Handle);
        Handle = IntPtr.Zero;
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, int dwFlags);

    [DllImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "RegisterHotKey", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKeyNative(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", EntryPoint = "UnregisterHotKey")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKeyNative(IntPtr hWnd, int id);
}
