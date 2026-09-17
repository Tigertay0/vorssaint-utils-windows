// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of NSWorkspace.icon(forFile:) as used by ResponsibleProcess.icon in
// Sources/Vorssaint/Services/ResponsibleProcess.swift.

using System.Runtime.InteropServices;

namespace Faqra.Win32.Icons;

public static class ShellIcons
{
    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFOW
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    /// <summary>The 32 px shell icon of a file, as Explorer shows it; null when the shell has none.</summary>
    public static NativeIcon? LargeIconFor(string path)
    {
        var info = new SHFILEINFOW();
        var result = SHGetFileInfoW(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFOW>(), SHGFI_ICON | SHGFI_LARGEICON);
        return result == IntPtr.Zero || info.hIcon == IntPtr.Zero ? null : new NativeIcon(info.hIcon);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfoW(string path, uint fileAttributes, ref SHFILEINFOW info, uint size, uint flags);
}
