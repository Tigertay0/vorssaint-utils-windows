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

/// <summary>The real shell. UI thread only, like every tray icon that shares it.</summary>
internal sealed class NotifyIconShell : INotifyIconShell
{
    /// <summary>Long enough for a healthy taskbar, short enough not to stall the UI thread.</summary>
    private const uint ProbeTimeoutMilliseconds = 500;

    /// <summary>How long a taskbar that did not answer is reported busy without asking it again.</summary>
    private const long QuietMilliseconds = 2_000;

    public static readonly NotifyIconShell Instance = new(ProbeTaskbar, () => Environment.TickCount64);

    private readonly Func<bool> _probe;
    private readonly Func<long> _clock;
    private long? _askAgainAt;

    internal NotifyIconShell(Func<bool> probe, Func<long> clock)
    {
        _probe = probe;
        _clock = clock;
    }

    public bool NotifyIcon(uint message, ref NOTIFYICONDATAW data) => Shell32.Shell_NotifyIconW(message, ref data);

    public bool IsTaskbarResponsive()
    {
        var now = _clock();
        if (_askAgainAt is { } askAgainAt && now < askAgainAt)
        {
            return false;
        }
        if (_probe())
        {
            _askAgainAt = null;
            return true;
        }
        // A busy taskbar makes each probe wait the full timeout, and seven metric icons asking in turn
        // would hold the UI thread for seconds.
        _askAgainAt = now + QuietMilliseconds;
        return false;
    }

    private static bool ProbeTaskbar()
    {
        var taskbar = User32.FindWindowW("Shell_TrayWnd", null);
        return taskbar != IntPtr.Zero
            && User32.SendMessageTimeoutW(taskbar, User32.WM_NULL, IntPtr.Zero, IntPtr.Zero,
                User32.SMTO_ABORTIFHUNG, ProbeTimeoutMilliseconds, out _) != IntPtr.Zero;
    }
}
