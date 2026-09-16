// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors MenuBarMetric in Sources/Vorssaint/App/MenuBarRenderer.swift (lines 31-119, 463-713) for the
// separate-metrics mode, which is Faqra's only mode: a tray slot holds one small icon, not a text run.
// Temperatures, fan speed and peripheral batteries have no Windows source, so they get no icon.

using System.Globalization;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Localization;
using Faqra.Core.Metrics;
using Faqra.Core.Panel;

namespace Faqra.Core.Tray;

public enum TrayMetric { Cpu, Gpu, Memory, Battery, BatteryTime, Network, DiskUsage, DiskActivity, Power }

/// <summary>What one metric icon draws, top line over bottom line, and its hover text.</summary>
public readonly record struct TrayMetricText(string Top, string Bottom, string Tooltip);

public static class TrayMetrics
{
    private const string Placeholder = "-";

    /// <summary>The metrics switched on, in the saved order, that this PC can actually read.</summary>
    public static IReadOnlyList<TrayMetric> Enabled(
        string? savedOrder,
        Func<string, bool> setting,
        Func<AppFeature, bool> isAvailable,
        bool hasBattery)
    {
        var result = new List<TrayMetric>();
        foreach (var raw in DefaultsSanitizers.MenuBarMetricOrder(savedOrder))
        {
            if (FromRawValue(raw) is not { } metric)
            {
                continue;
            }
            if (setting(metric.DefaultsKey()) && isAvailable(metric.Feature()) && (hasBattery || !metric.NeedsBattery()))
            {
                result.Add(metric);
            }
        }
        return result;
    }

    public static TrayMetric? FromRawValue(string raw) => raw switch
    {
        "cpu" => TrayMetric.Cpu,
        "gpu" => TrayMetric.Gpu,
        "memory" => TrayMetric.Memory,
        "battery" => TrayMetric.Battery,
        "batteryTime" => TrayMetric.BatteryTime,
        "network" => TrayMetric.Network,
        "diskUsage" => TrayMetric.DiskUsage,
        "diskActivity" => TrayMetric.DiskActivity,
        "power" => TrayMetric.Power,
        _ => null,
    };

    public static string DefaultsKey(this TrayMetric metric) => metric switch
    {
        TrayMetric.Cpu => Defaults.DefaultsKey.MenuBarCPU,
        TrayMetric.Gpu => Defaults.DefaultsKey.MenuBarGPU,
        TrayMetric.Memory => Defaults.DefaultsKey.MenuBarMemory,
        TrayMetric.Battery => Defaults.DefaultsKey.MenuBarBattery,
        TrayMetric.BatteryTime => Defaults.DefaultsKey.MenuBarBatteryTime,
        TrayMetric.Network => Defaults.DefaultsKey.MenuBarNetwork,
        TrayMetric.DiskUsage => Defaults.DefaultsKey.MenuBarDiskUsage,
        TrayMetric.DiskActivity => Defaults.DefaultsKey.MenuBarDiskActivity,
        TrayMetric.Power => Defaults.DefaultsKey.MenuBarPower,
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, null),
    };

    public static AppFeature Feature(this TrayMetric metric) => metric switch
    {
        TrayMetric.Cpu => AppFeature.MonitorCPU,
        TrayMetric.Gpu => AppFeature.MonitorGPU,
        TrayMetric.Memory => AppFeature.MonitorMemory,
        TrayMetric.Network => AppFeature.MonitorNetwork,
        TrayMetric.DiskUsage or TrayMetric.DiskActivity => AppFeature.MonitorDisk,
        _ => AppFeature.MonitorPower,
    };

    public static bool NeedsBattery(this TrayMetric metric) => metric is TrayMetric.Battery or TrayMetric.BatteryTime;

    /// <summary>The panel section a click on the icon opens, upstream's metric detail kind mapped to its section.</summary>
    public static PanelSectionId Section(this TrayMetric metric) => metric switch
    {
        TrayMetric.Network => PanelSectionId.Network,
        TrayMetric.DiskUsage or TrayMetric.DiskActivity => PanelSectionId.Disk,
        TrayMetric.Battery or TrayMetric.BatteryTime or TrayMetric.Power => PanelSectionId.Power,
        _ => PanelSectionId.System,
    };

    public static string Title(this TrayMetric metric, MonitorStrings s) => metric switch
    {
        TrayMetric.Cpu => s.Cpu,
        TrayMetric.Gpu => s.Gpu,
        TrayMetric.Memory => s.Memory,
        TrayMetric.Battery => s.Battery,
        TrayMetric.BatteryTime => s.BatteryTimeRemaining,
        TrayMetric.Network => s.NetworkSection,
        TrayMetric.DiskUsage => s.DiskUsage,
        TrayMetric.DiskActivity => s.LiveActivity,
        TrayMetric.Power => s.Power,
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, null),
    };

    /// <summary>
    /// The icon's two lines and tooltip for the current snapshot. A missing reading keeps the icon
    /// with a placeholder instead of removing it, so the other icons do not shuffle around it.
    /// </summary>
    public static TrayMetricText Text(TrayMetric metric, SystemSnapshot snapshot, MonitorStrings s, string? memoryMetric, CultureInfo? culture = null)
    {
        var title = metric.Title(s);
        TrayMetricText Usage(string label, double? fraction) => fraction is { } f
            ? new(label, MetricFormat.Percent(f), $"{title}: {MetricFormat.Percent(f)}")
            : new(label, Placeholder, $"{title}: {s.Measuring}");

        switch (metric)
        {
            case TrayMetric.Cpu:
                return Usage("CPU", snapshot.CpuUsage);
            case TrayMetric.Gpu:
                return Usage("GPU", snapshot.GpuUsage);
            case TrayMetric.Memory:
                var used = MetricFormat.SelectedMemory(snapshot.MemoryUsed, snapshot.MemoryAppUsed, memoryMetric);
                var memory = MetricFormat.MenuBarMemoryPercent(used, snapshot.MemoryTotal);
                return new("RAM", memory, $"{title}: {(memory == "--%" ? s.Measuring : memory)}");
            case TrayMetric.DiskUsage:
                return Usage("DSK", snapshot.PrimaryDisk?.UsedFraction);
            case TrayMetric.Network:
                return Rates(title, "↓", "↑", snapshot.NetDownBytesPerSec, snapshot.NetUpBytesPerSec, culture, s);
            case TrayMetric.DiskActivity:
                var reads = snapshot.Disks.Where(d => d.ReadBytesPerSec is not null).Select(d => d.ReadBytesPerSec!.Value).ToList();
                var writes = snapshot.Disks.Where(d => d.WriteBytesPerSec is not null).Select(d => d.WriteBytesPerSec!.Value).ToList();
                return Rates(title, "R", "W", reads.Count > 0 ? reads.Sum() : null, writes.Count > 0 ? writes.Sum() : null, culture, s);
            case TrayMetric.Battery:
                return snapshot.Power?.ChargePercent is { } charge
                    ? new("BAT", $"{Math.Clamp(charge, 0, 100)}%", $"{title}: {Math.Clamp(charge, 0, 100)}%")
                    : new("BAT", Placeholder, $"{title}: {s.Measuring}");
            case TrayMetric.BatteryTime:
                var remaining = snapshot.Power?.TimeRemainingSeconds is { } seconds ? BatteryTime.Formatted(seconds) : null;
                return remaining is not null
                    ? new("BAT", remaining.Replace(" ", string.Empty, StringComparison.Ordinal), $"{title}: {remaining}")
                    : new("BAT", "...", $"{title}: {s.Calculating}");
            case TrayMetric.Power:
                return snapshot.Power?.SystemWatts is { } watts
                    ? new("PWR", MetricFormat.WattsCompact(watts, culture), $"{title}: {MetricFormat.Watts(watts, culture)}")
                    : new("PWR", Placeholder, $"{title}: {s.Measuring}");
            default:
                throw new ArgumentOutOfRangeException(nameof(metric), metric, null);
        }
    }

    private static TrayMetricText Rates(string title, string first, string second, double? a, double? b, CultureInfo? culture, MonitorStrings s)
    {
        if (a is not { } x || b is not { } y)
        {
            return new($"{first}{Placeholder}", $"{second}{Placeholder}", $"{title}: {s.Measuring}");
        }
        // The icon lines have no room for a space; the tooltip does, and matches upstream's two lines.
        var separator = first == "↓" ? string.Empty : " ";
        return new(
            $"{first}{MetricFormat.BytesPerSecCompact(x, culture)}",
            $"{second}{MetricFormat.BytesPerSecCompact(y, culture)}",
            $"{title}: {first}{separator}{MetricFormat.BytesPerSec(x, culture)} {second}{separator}{MetricFormat.BytesPerSec(y, culture)}");
    }
}
