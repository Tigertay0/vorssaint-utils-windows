// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of URLResourceValues volume capacity keys in Sources/Vorssaint/Services/SystemMonitor/SystemMonitor.swift.

using System.Runtime.InteropServices;

namespace Faqra.Win32.Perf;

/// <summary>A volume's size and free space, in bytes.</summary>
public readonly record struct DiskSpaceReading(ulong TotalBytes, ulong FreeBytes)
{
    public ulong UsedBytes => TotalBytes - FreeBytes;
}

public static class DiskSpace
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceExW(
        string directoryName,
        out ulong freeBytesAvailableToCaller,
        out ulong totalNumberOfBytes,
        out ulong totalNumberOfFreeBytes);

    /// <summary>
    /// Size and free space of the volume holding <paramref name="root"/>, or null when it does not
    /// exist or is not ready. Free space is the volume's, not what quotas leave this user, matching
    /// Explorer's "free of" line.
    /// </summary>
    public static DiskSpaceReading? Read(string root)
    {
        if (!GetDiskFreeSpaceExW(root, out _, out var total, out var free) || total == 0)
        {
            return null;
        }
        return new DiskSpaceReading(total, Math.Min(free, total));
    }
}
