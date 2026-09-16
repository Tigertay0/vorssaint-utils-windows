// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/Metrics/BatteryTimeSupport.swift

namespace Faqra.Core.Metrics;

public static class BatteryTime
{
    /// <summary>A week, the upper bound for a believable estimate; sentinel values sit above it.</summary>
    private const int MaxMinutes = 7 * 24 * 60;

    /// <summary>
    /// Seconds of battery left, or null while plugged in, charging, or when the system has no
    /// believable estimate (Windows reports -1 while it is still calculating).
    /// </summary>
    public static double? RemainingSeconds(int? timeToEmptyMinutes, bool externalConnected, bool isCharging)
    {
        if (externalConnected || isCharging || timeToEmptyMinutes is not { } minutes || minutes < 1 || minutes >= MaxMinutes)
        {
            return null;
        }
        return minutes * 60d;
    }

    /// <summary>"3h 42m"; anything under a minute still reads as one minute.</summary>
    public static string? Formatted(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0)
        {
            return null;
        }
        var totalMinutes = Math.Max(1, (long)(seconds / 60));
        return $"{totalMinutes / 60}h {totalMinutes % 60}m";
    }
}
