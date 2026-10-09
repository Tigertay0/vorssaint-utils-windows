// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Faqra.Win32.Native;

namespace Faqra.Win32.Tray;

/// <summary>
/// The shell calls a <see cref="TrayIcon"/> makes. Tests substitute a fake to play a taskbar that is
/// missing, busy or restarting, which the real shell only does around sign-in, wake and Explorer restarts.
/// </summary>
internal interface INotifyIconShell
{
    bool NotifyIcon(uint message, ref NOTIFYICONDATAW data);

    /// <summary>True when the taskbar window exists and answers a message promptly.</summary>
    bool IsTaskbarResponsive();
}

internal sealed class NotifyIconShell : INotifyIconShell
{
    /// <summary>Long enough for a healthy taskbar, short enough not to stall the UI thread.</summary>
    private const uint ProbeTimeoutMilliseconds = 500;

    public static readonly NotifyIconShell Instance = new();

    public bool NotifyIcon(uint message, ref NOTIFYICONDATAW data) => Shell32.Shell_NotifyIconW(message, ref data);

    public bool IsTaskbarResponsive()
    {
        var taskbar = User32.FindWindowW("Shell_TrayWnd", null);
        return taskbar != IntPtr.Zero
            && User32.SendMessageTimeoutW(taskbar, User32.WM_NULL, IntPtr.Zero, IntPtr.Zero,
                User32.SMTO_ABORTIFHUNG, ProbeTimeoutMilliseconds, out _) != IntPtr.Zero;
    }
}
