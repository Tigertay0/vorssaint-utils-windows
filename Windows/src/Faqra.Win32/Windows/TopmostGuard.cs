// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the window level and collectionBehavior in
// Sources/Vorssaint/Services/Notch/NotchWindowHost.swift: macOS keeps a panel above everything by
// its level alone, while Windows demotes a topmost window whenever another app claims the front.

using Faqra.Win32.Native;

namespace Faqra.Win32.Windows;

/// <summary>
/// Keeps an overlay on top. Re-asserts topmost whenever another window becomes foreground, and on
/// a slow heartbeat for the cases no event covers. Hook callbacks arrive on the UI thread because
/// the hook is registered with no process or thread filter from that thread.
/// </summary>
public sealed class TopmostGuard : IDisposable
{
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(5);

    private readonly IntPtr _hwnd;
    private readonly WinEventProc _callback;
    private readonly Timer _timer;
    private IntPtr _hook;

    public TopmostGuard(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _callback = OnForegroundChanged;
        _hook = User32.SetWinEventHook(
            User32.EVENT_SYSTEM_FOREGROUND, User32.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _callback, 0, 0,
            User32.WINEVENT_OUTOFCONTEXT | User32.WINEVENT_SKIPOWNPROCESS);
        _timer = new Timer(_ => Assert(), null, Heartbeat, Heartbeat);
    }

    /// <summary>Raised when the foreground window changed, so the owner can re-check its state.</summary>
    public event Action? ForegroundChanged;

    public void Assert()
    {
        if (_hwnd != IntPtr.Zero)
        {
            WindowStyles.BringToTop(_hwnd);
        }
    }

    private void OnForegroundChanged(
        IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        Assert();
        ForegroundChanged?.Invoke();
    }

    public void Dispose()
    {
        _timer.Dispose();
        if (_hook != IntPtr.Zero)
        {
            User32.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
