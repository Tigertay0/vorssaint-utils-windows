// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Runtime.InteropServices;

namespace Faqra.Win32.Native;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public unsafe struct NOTIFYICONDATAW
{
    public uint cbSize;
    public IntPtr hWnd;
    public uint uID;
    public uint uFlags;
    public uint uCallbackMessage;
    public IntPtr hIcon;
    public fixed char szTip[128];
    public uint dwState;
    public uint dwStateMask;
    public fixed char szInfo[256];
    public uint uVersionOrTimeout;
    public fixed char szInfoTitle[64];
    public uint dwInfoFlags;
    public Guid guidItem;
    public IntPtr hBalloonIcon;
}

[StructLayout(LayoutKind.Sequential)]
public struct NOTIFYICONIDENTIFIER
{
    public uint cbSize;
    public IntPtr hWnd;
    public uint uID;
    public Guid guidItem;
}

public static class Shell32
{
    public const uint NIM_ADD = 0x0;
    public const uint NIM_MODIFY = 0x1;
    public const uint NIM_DELETE = 0x2;
    public const uint NIM_SETVERSION = 0x4;

    public const uint NIF_MESSAGE = 0x01;
    public const uint NIF_ICON = 0x02;
    public const uint NIF_TIP = 0x04;
    public const uint NIF_INFO = 0x10;
    public const uint NIF_GUID = 0x20;
    public const uint NIIF_USER = 0x04;
    public const uint NIIF_LARGE_ICON = 0x20;
    public const uint NIF_SHOWTIP = 0x80;

    public const uint NOTIFYICON_VERSION_4 = 4;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("shell32.dll", SetLastError = true)]
    public static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT iconLocation);
}
