// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/Metrics/MetricFormat.swift (lines 8-11, 58-61, 68-392, 405-408).
// The macOS page-count memory math (memoryUsed, appMemory, compressedMemory, cachedFiles) is left
// behind: Windows reports memory in bytes, not VM pages.

using System.Globalization;

namespace Faqra.Core.Metrics;

/// <summary>Cumulative network bytes since boot.</summary>
public readonly record struct NetworkCounters(ulong Received, ulong Sent);

/// <summary>Cumulative disk bytes.</summary>
public readonly record struct DiskIOCounters(ulong Read, ulong Written);

public enum TemperatureUnit { Celsius, Fahrenheit }

public static class TemperatureUnits
{
    public static string RawValue(this TemperatureUnit unit) => unit == TemperatureUnit.Fahrenheit ? "fahrenheit" : "celsius";

    /// <summary>Anything but "fahrenheit" reads as Celsius, as upstream's renderer does.</summary>
    public static TemperatureUnit FromRawValue(string? raw) => raw == "fahrenheit" ? TemperatureUnit.Fahrenheit : TemperatureUnit.Celsius;
}

/// <summary>
/// Every metric string the panel, tray and island show. Numbers follow the Windows regional format
/// (<see cref="CultureInfo.CurrentCulture"/>), not the app language, exactly as upstream follows the
/// region rather than the UI language. Rounding reproduces Swift: <c>rounded()</c> is half away
/// from zero, and <c>String(format: "%.1f")</c> is C printf, which rounds half to even.
/// </summary>
public static class MetricFormat
{
    private static readonly string[] ByteUnits = ["B", "KB", "MB", "GB", "TB", "PB"];
    private static readonly string[] CompactUnits = ["B", "K", "M", "G", "T", "P"];

    // MARK: sizes and rates

    public static string Bytes(ulong bytes, CultureInfo? culture = null)
    {
        var (value, unit) = Scale(bytes);
        return $"{Number(value, unit, culture)} {unit}";
    }

    public static string BytesPerSec(double bytesPerSecond, CultureInfo? culture = null)
    {
        var (value, unit) = Scale(bytesPerSecond);
        return $"{Number(value, unit, culture)} {unit}/s";
    }

    /// <summary>Volumes are sized in powers of 1000, matching how drives are sold and how Explorer labels them.</summary>
    public static string DiskBytes(ulong bytes, CultureInfo? culture = null)
    {
        var (value, index) = ScaleDecimal(bytes);
        var unit = ByteUnits[index];
        if (index == 0)
        {
            return $"{Fixed(value, 0, culture)} B";
        }
        return value < 10 ? $"{Fixed(value, 1, culture)} {unit}" : $"{Fixed(value, 0, culture)} {unit}";
    }

    public static string DiskBytesPrecise(ulong bytes, CultureInfo? culture = null)
    {
        var (value, index) = ScaleDecimal(bytes);
        var unit = ByteUnits[index];
        if (index == 0)
        {
            return $"{Fixed(value, 0, culture)} B";
        }
        if (unit is "TB" or "PB")
        {
            return $"{Fixed(value, 2, culture)} {unit}";
        }
        return value < 10 ? $"{Fixed(value, 1, culture)} {unit}" : $"{Fixed(value, 0, culture)} {unit}";
    }

    /// <summary>
    /// The narrow tray form, e.g. "320K" or "1.2M". A value that would round up to 1024 moves to the
    /// next unit first, so "1024B" and "1024K" never appear.
    /// </summary>
    public static string BytesPerSecCompact(double bytesPerSecond, CultureInfo? culture = null)
    {
        var value = double.IsFinite(bytesPerSecond) ? SwiftMax(0, bytesPerSecond) : 0;
        var index = 0;
        while (value >= 1024 && index < CompactUnits.Length - 1)
        {
            value /= 1024;
            index++;
        }
        while (index < CompactUnits.Length - 1 && (index == 0 || value >= 10) && RoundAway(value) >= 1024)
        {
            value /= 1024;
            index++;
        }
        if (index == 0)
        {
            return $"{(long)RoundAway(value)}B";
        }
        if (value < 10)
        {
            var rounded = RoundAway(value * 10) / 10;
            return rounded >= 10
                ? $"{(long)RoundAway(rounded)}{CompactUnits[index]}"
                : $"{Fixed(rounded, 1, culture)}{CompactUnits[index]}";
        }
        return $"{(long)RoundAway(value)}{CompactUnits[index]}";
    }

    // MARK: power and fractions

    public static string Watts(double watts, CultureInfo? culture = null) =>
        Math.Abs(watts) < 10 ? $"{Fixed(watts, 1, culture)} W" : $"{Fixed(watts, 0, culture)} W";

    public static string WattsCompact(double watts, CultureInfo? culture = null) => $"{Fixed(RoundAway(watts), 0, culture)}W";

    /// <summary>The measured system draw, or while running on battery the battery's discharge rate.</summary>
    public static double? SystemPowerWatts(double? measured, double? batteryWatts, bool externalConnected)
    {
        if (measured is { } m)
        {
            return m;
        }
        return !externalConnected && batteryWatts is { } b && b < 0 ? -b : null;
    }

    public static string Percent(double fraction) => $"{(long)RoundAway(SwiftMax(0, SwiftMin(1, fraction)) * 100)}%";

    public static double BoundedPercentage(double percentage) =>
        double.IsFinite(percentage) ? Math.Max(0, Math.Min(100, percentage)) : 0;

    public static string MenuBarMemoryPercent(ulong? used, ulong? total) =>
        used is { } u && total is { } t && t > 0 ? Percent((double)u / t) : "--%";

    public static T SelectedMemory<T>(T used, T app, string? metric) => metric == "app" ? app : used;

    /// <summary>
    /// Smooths the raw GPU reading: a rise is capped at 20 points per sample, and a fall eases toward
    /// the new value, so one busy frame does not spike the graph.
    /// </summary>
    public static double StabilizedGpuUsage(double? previous, double current)
    {
        var value = double.IsFinite(current) ? Math.Clamp(current, 0, 1) : 0;
        if (previous is not { } p || !double.IsFinite(p))
        {
            return value;
        }
        var baseline = Math.Clamp(p, 0, 1);
        return value > baseline ? Math.Min(value, baseline + 0.20) : baseline * 0.35 + value * 0.65;
    }

    // MARK: temperature and time

    public static string Temperature(double celsius, TemperatureUnit unit, CultureInfo? culture = null) =>
        $"{Fixed(Convert(celsius, unit), 0, culture)} {TemperatureUnitSuffix(unit)}";

    public static string TemperatureCompact(double celsius, TemperatureUnit unit, CultureInfo? culture = null) =>
        $"{Fixed(Convert(celsius, unit), 0, culture)}°";

    public static string TemperatureUnitSuffix(TemperatureUnit unit) => unit == TemperatureUnit.Fahrenheit ? "°F" : "°C";

    public static string Uptime(long seconds)
    {
        var total = Math.Max(0, seconds);
        var days = total / 86_400;
        var hours = total % 86_400 / 3_600;
        var minutes = total % 3_600 / 60;
        if (days > 0)
        {
            return $"{days}d {hours}h";
        }
        return hours > 0 ? $"{hours}h {minutes}min" : $"{minutes}min";
    }

    // MARK: rate math

    /// <summary>Bytes per second between two counter readings; a counter that went backwards reads 0.</summary>
    public static (double Down, double Up) NetSpeed(NetworkCounters previous, NetworkCounters current, double elapsed)
    {
        if (elapsed <= 0)
        {
            return (0, 0);
        }
        return (Delta(previous.Received, current.Received) / elapsed, Delta(previous.Sent, current.Sent) / elapsed);
    }

    public static (double Read, double Write) DiskSpeed(DiskIOCounters previous, DiskIOCounters current, double elapsed)
    {
        if (elapsed <= 0)
        {
            return (0, 0);
        }
        return (Delta(previous.Read, current.Read) / elapsed, Delta(previous.Written, current.Written) / elapsed);
    }

    // MARK: helpers

    private static double Delta(ulong previous, ulong current) => current >= previous ? current - previous : 0;

    private static (double Value, string Unit) Scale(double bytes)
    {
        var value = SwiftMax(0, bytes);
        var index = 0;
        while (value >= 1024 && index < ByteUnits.Length - 1)
        {
            value /= 1024;
            index++;
        }
        return (value, ByteUnits[index]);
    }

    private static (double Value, int Index) ScaleDecimal(ulong bytes)
    {
        double value = bytes;
        var index = 0;
        while (value >= 1000 && index < ByteUnits.Length - 1)
        {
            value /= 1000;
            index++;
        }
        return (value, index);
    }

    private static string Number(double value, string unit, CultureInfo? culture) =>
        unit == "B" ? Fixed(value, 0, culture) : Fixed(value, value < 10 ? 1 : 0, culture);

    private static double Convert(double celsius, TemperatureUnit unit) =>
        unit == TemperatureUnit.Fahrenheit ? celsius * 9 / 5 + 32 : celsius;

    /// <summary>C printf's <c>%.Nf</c>: half-to-even, then the region's decimal separator.</summary>
    private static string Fixed(double value, int digits, CultureInfo? culture) =>
        Math.Round(value, digits, MidpointRounding.ToEven).ToString("F" + digits, culture ?? CultureInfo.CurrentCulture);

    /// <summary>Swift's <c>rounded()</c>.</summary>
    private static double RoundAway(double value) => Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>Swift's <c>max</c>, which returns the first argument when the second is NaN.</summary>
    private static double SwiftMax(double x, double y) => y >= x ? y : x;

    /// <summary>Swift's <c>min</c>, which returns the first argument when the second is NaN.</summary>
    private static double SwiftMin(double x, double y) => y < x ? y : x;
}
