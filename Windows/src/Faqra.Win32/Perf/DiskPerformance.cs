// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the IOBlockStorageDriver "Statistics" byte counters in
// Sources/Vorssaint/Services/Metrics/DiskSampler.swift (lines 361-420).

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Faqra.Win32.Perf;

public static unsafe class DiskPerformance
{
    private const uint IoctlDiskPerformance = 0x00070020;
    private const uint FileShareReadWrite = 0x00000003;
    private const uint OpenExisting = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct DISK_PERFORMANCE
    {
        public long BytesRead;
        public long BytesWritten;
        public long ReadTime;
        public long WriteTime;
        public long IdleTime;
        public uint ReadCount;
        public uint WriteCount;
        public uint QueueDepth;
        public uint SplitCount;
        public long QueryTime;
        public uint StorageDeviceNumber;
        public fixed char StorageManagerName[8];
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device, uint controlCode, IntPtr inBuffer, uint inBufferSize,
        out DISK_PERFORMANCE outBuffer, uint outBufferSize, out uint bytesReturned, IntPtr overlapped);

    /// <summary>
    /// Cumulative bytes read and written on a volume since boot, e.g. for <c>C:</c>. Opening the
    /// volume with no access rights is enough for this query, so it works without elevation. Null for
    /// volumes whose driver keeps no counters, such as cloud-drive virtual file systems.
    /// </summary>
    public static (ulong Read, ulong Written)? Read(string driveLetter)
    {
        using var handle = CreateFileW(@"\\.\" + driveLetter.TrimEnd('\\'), 0, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return null;
        }
        if (!DeviceIoControl(handle, IoctlDiskPerformance, IntPtr.Zero, 0, out var perf, (uint)sizeof(DISK_PERFORMANCE), out _, IntPtr.Zero))
        {
            return null;
        }
        return perf.BytesRead < 0 || perf.BytesWritten < 0 ? null : ((ulong)perf.BytesRead, (ulong)perf.BytesWritten);
    }
}
