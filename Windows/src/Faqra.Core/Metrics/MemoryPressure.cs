// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors MemoryPressure in Sources/Vorssaint/Services/SystemMonitor/SystemMonitor.swift (lines 12-23).

namespace Faqra.Core.Metrics;

public enum MemoryPressure { Normal, Warning, Critical, Unknown }

public static class MemoryPressures
{
    private const double WarningFreeFraction = 0.15;
    private const double CriticalFreeFraction = 0.05;

    /// <summary>
    /// macOS reports a kernel pressure level; Windows has no such signal, so pressure is derived
    /// from free physical memory, the point where Windows starts paging. The commit charge is not an
    /// input: with a system-managed page file Windows grows the limit on demand, so a commit charge
    /// near its limit is routine on a healthy machine.
    /// </summary>
    public static MemoryPressure FromMemory(ulong totalBytes, ulong availableBytes)
    {
        if (totalBytes == 0)
        {
            return MemoryPressure.Unknown;
        }
        var free = (double)availableBytes / totalBytes;
        if (free < CriticalFreeFraction)
        {
            return MemoryPressure.Critical;
        }
        return free < WarningFreeFraction ? MemoryPressure.Warning : MemoryPressure.Normal;
    }
}
