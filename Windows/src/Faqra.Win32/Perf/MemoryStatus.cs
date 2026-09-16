// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of host_statistics64 / vm_statistics in Sources/Vorssaint/Services/SystemMonitor/SystemMonitor.swift.

using System.Runtime.InteropServices;

namespace Faqra.Win32.Perf;

/// <summary>Physical memory and commit charge, in bytes.</summary>
public readonly record struct MemoryReading(
    ulong TotalBytes,
    ulong AvailableBytes,
    uint LoadPercent,
    ulong CommitTotalBytes,
    ulong CommitLimitBytes,
    ulong CacheBytes);

public static class MemoryStatus
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PERFORMANCE_INFORMATION
    {
        public uint cb;
        public nuint CommitTotal;
        public nuint CommitLimit;
        public nuint CommitPeak;
        public nuint PhysicalTotal;
        public nuint PhysicalAvailable;
        public nuint SystemCache;
        public nuint KernelTotal;
        public nuint KernelPaged;
        public nuint KernelNonpaged;
        public nuint PageSize;
        public uint HandleCount;
        public uint ProcessCount;
        public uint ThreadCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION info, uint size);

    /// <summary>The current reading, or null when Windows refuses the query.</summary>
    public static MemoryReading? Read()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status) || status.ullTotalPhys == 0)
        {
            return null;
        }

        ulong commitTotal = 0, commitLimit = 0, cache = 0;
        if (GetPerformanceInfo(out var perf, (uint)Marshal.SizeOf<PERFORMANCE_INFORMATION>()))
        {
            var page = (ulong)perf.PageSize;
            commitTotal = (ulong)perf.CommitTotal * page;
            commitLimit = (ulong)perf.CommitLimit * page;
            cache = (ulong)perf.SystemCache * page;
        }

        return new MemoryReading(
            status.ullTotalPhys,
            status.ullAvailPhys,
            status.dwMemoryLoad,
            commitTotal,
            commitLimit,
            cache);
    }
}
