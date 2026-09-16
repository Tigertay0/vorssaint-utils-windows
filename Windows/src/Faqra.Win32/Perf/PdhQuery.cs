// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of host_processor_info and IOReport sampling in Sources/Vorssaint/Services/SystemMonitor/SystemMonitor.swift.

using System.Runtime.InteropServices;

namespace Faqra.Win32.Perf;

/// <summary>One instance of a wildcard counter, e.g. a single GPU engine.</summary>
public readonly record struct CounterInstance(string Name, double Value);

/// <summary>
/// A Performance Data Helper query. Rate counters need two collections before they have a value,
/// so callers collect on every tick and read after the second. Counters are added by their English
/// path, which works on every display language. Not thread-safe: use from one thread.
/// </summary>
public sealed unsafe class PdhQuery : IDisposable
{
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhFmtNoCap100 = 0x00008000;
    private const uint PdhMoreData = 0x800007D2;
    private const uint PdhCstatusValidData = 0x00000000;
    private const uint PdhCstatusNewData = 0x00000001;

    [StructLayout(LayoutKind.Explicit)]
    private struct PDH_FMT_COUNTERVALUE
    {
        [FieldOffset(0)] public uint CStatus;
        [FieldOffset(8)] public double DoubleValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PDH_FMT_COUNTERVALUE_ITEM_W
    {
        public IntPtr szName;
        public PDH_FMT_COUNTERVALUE FmtValue;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQueryW(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out PDH_FMT_COUNTERVALUE value);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr buffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);

    private readonly List<IntPtr> _counters = [];
    private IntPtr _query;

    private PdhQuery(IntPtr query)
    {
        _query = query;
    }

    /// <summary>A new, empty query, or null when PDH is unavailable.</summary>
    public static PdhQuery? Open() =>
        PdhOpenQueryW(null, IntPtr.Zero, out var query) == 0 ? new PdhQuery(query) : null;

    /// <summary>Adds a counter by English path and returns its index, or -1 when the counter does not exist.</summary>
    public int AddCounter(string englishPath)
    {
        if (_query == IntPtr.Zero || PdhAddEnglishCounterW(_query, englishPath, IntPtr.Zero, out var counter) != 0)
        {
            return -1;
        }
        _counters.Add(counter);
        return _counters.Count - 1;
    }

    /// <summary>Samples every counter in the query. False when the query has no data yet.</summary>
    public bool Collect() => _query != IntPtr.Zero && PdhCollectQueryData(_query) == 0;

    /// <summary>A single-instance counter's latest value, or null until it has two samples.</summary>
    public double? ReadDouble(int counter)
    {
        if (!IsValid(counter)
            || PdhGetFormattedCounterValue(_counters[counter], PdhFmtDouble | PdhFmtNoCap100, out _, out var value) != 0
            || !IsGoodStatus(value.CStatus)
            || !double.IsFinite(value.DoubleValue))
        {
            return null;
        }
        return value.DoubleValue;
    }

    /// <summary>Every instance of a wildcard counter; empty until it has two samples.</summary>
    public IReadOnlyList<CounterInstance> ReadInstances(int counter)
    {
        if (!IsValid(counter))
        {
            return [];
        }
        var handle = _counters[counter];
        uint size = 0;
        var status = PdhGetFormattedCounterArrayW(handle, PdhFmtDouble | PdhFmtNoCap100, ref size, out _, IntPtr.Zero);
        if (status != PdhMoreData || size == 0)
        {
            return [];
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArrayW(handle, PdhFmtDouble | PdhFmtNoCap100, ref size, out var count, buffer) != 0)
            {
                return [];
            }
            var items = (PDH_FMT_COUNTERVALUE_ITEM_W*)buffer;
            var result = new List<CounterInstance>((int)count);
            for (var i = 0; i < count; i++)
            {
                var item = items[i];
                if (IsGoodStatus(item.FmtValue.CStatus) && double.IsFinite(item.FmtValue.DoubleValue))
                {
                    result.Add(new CounterInstance(Marshal.PtrToStringUni(item.szName) ?? string.Empty, item.FmtValue.DoubleValue));
                }
            }
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private bool IsValid(int counter) => _query != IntPtr.Zero && counter >= 0 && counter < _counters.Count;

    private static bool IsGoodStatus(uint status) => status is PdhCstatusValidData or PdhCstatusNewData;

    public void Dispose()
    {
        if (_query == IntPtr.Zero)
        {
            return;
        }
        // Closing the query also frees every counter in it.
        PdhCloseQuery(_query);
        _query = IntPtr.Zero;
        _counters.Clear();
    }
}
