// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/Defaults.swift (sanitized* functions, lines 1764-1997) for Stage 1 keys.
// Sanitizers for features not yet ported (clipboard, debounce, disk exclusions, routing) arrive with them.

namespace Faqra.Core.Defaults;

public enum KeepAwakeIconTint { Orange, Green, Blue, Purple, Pink, None }

public enum KeepAwakeActiveIcon { Brand, Coffee, Eye, Moon, Light }

public enum AppAppearance { System, Light, Dark }

public static class DefaultsSanitizers
{
    public static readonly IReadOnlyList<int> AllowedDurations = [0, 15, 30, 60, 120, 240, 480];
    public static readonly IReadOnlyList<int> AllowedBatteryLimits = [0, 5, 10, 15, 20];
    public static readonly IReadOnlyList<int> AllowedJiggleIntervals = [1, 2, 5, 10, 15];
    public static readonly IReadOnlyList<int> AllowedMonitorIntervals = [1, 2, 5];
    public static readonly IReadOnlyList<int> AllowedAlertCooldowns = [2, 5, 15, 30, 60];
    public static readonly IReadOnlyList<string> AllowedMenuBarPresets = ["dense"];
    public static readonly IReadOnlyList<string> AllowedMenuBarSpacings = ["standard", "compact"];
    public static readonly IReadOnlyList<string> AllowedMenuBarAppearances = ["values", "bars"];
    public static readonly IReadOnlyList<string> AllowedMenuBarLabelStyles = ["compact", "classic"];
    public static readonly IReadOnlyList<string> AllowedMenuBarMemoryStyles = ["dot", "percent", "both"];
    public static readonly IReadOnlyList<string> AllowedMonitorMemoryMetrics = ["used", "app"];

    public static int DefaultDuration(int minutes) => AllowedDurations.Contains(minutes) ? minutes : 0;

    public static int BatteryLimit(int percent) => AllowedBatteryLimits.Contains(percent) ? percent : 10;

    public static int KeepAwakeMouseJiggleInterval(int minutes) => AllowedJiggleIntervals.Contains(minutes) ? minutes : 5;

    public static int MonitorInterval(int seconds) => AllowedMonitorIntervals.Contains(seconds) ? seconds : 2;

    public static int MonitorAlertCooldown(int minutes) => AllowedAlertCooldowns.Contains(minutes) ? minutes : 15;

    public static string MenuBarPreset(string? preset) => OneOf(preset, AllowedMenuBarPresets, "dense");

    public static string MenuBarMetricSpacing(string? spacing) => OneOf(spacing, AllowedMenuBarSpacings, "compact");

    public static string MenuBarMetricAppearance(string? appearance) => OneOf(appearance, AllowedMenuBarAppearances, "values");

    public static string MenuBarLabelStyle(string? style) => OneOf(style, AllowedMenuBarLabelStyles, "compact");

    public static string MenuBarMemoryStyle(string? style) => OneOf(style, AllowedMenuBarMemoryStyles, "percent");

    public static string MonitorMemoryMetric(string? metric) => OneOf(metric, AllowedMonitorMemoryMetrics, "used");

    /// <summary>
    /// Keeps only known metric ids, expands the legacy "temperature" token into the three
    /// temperature metrics, de-duplicates, and appends any missing id so the result is always
    /// the full set in a stable order.
    /// </summary>
    public static IReadOnlyList<string> MenuBarMetricOrder(string? raw)
    {
        var tokens = (raw ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(token => token == "temperature" ? ["cpuTemperature", "gpuTemperature", "batteryTemperature"] : new[] { token });
        return PanelItemOrder(tokens, RegisteredDefaults.DefaultMenuBarMetricOrder);
    }

    /// <summary>Keeps only ids in the default order, de-duplicated, then appends missing defaults in default order.</summary>
    public static IReadOnlyList<string> PanelItemOrder(string? raw, IReadOnlyList<string> defaultOrder) =>
        PanelItemOrder((raw ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), defaultOrder);

    private static IReadOnlyList<string> PanelItemOrder(IEnumerable<string> tokens, IReadOnlyList<string> defaultOrder)
    {
        var allowed = defaultOrder.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var token in tokens)
        {
            if (allowed.Contains(token) && seen.Add(token))
            {
                result.Add(token);
            }
        }
        foreach (var id in defaultOrder)
        {
            if (seen.Add(id))
            {
                result.Add(id);
            }
        }
        return result;
    }

    public static int Percent(int value, int fallback, int min, int max) => value >= min && value <= max ? value : fallback;

    /// <summary>Finite and within 0…2 (upstream allows a boost above 1); non-finite falls back to 1.</summary>
    public static double AppVolume(double volume) => double.IsFinite(volume) ? Math.Clamp(volume, 0, 2) : 1;

    public static int MixerHeadphonesDisconnectVolumePercent(int percent) => Math.Clamp(percent, 10, 100);

    public static KeepAwakeIconTint IconTint(string? raw) => raw switch
    {
        "green" => KeepAwakeIconTint.Green,
        "blue" => KeepAwakeIconTint.Blue,
        "purple" => KeepAwakeIconTint.Purple,
        "pink" => KeepAwakeIconTint.Pink,
        "none" => KeepAwakeIconTint.None,
        _ => KeepAwakeIconTint.Orange,
    };

    public static string RawValue(this KeepAwakeIconTint tint) => tint.ToString().ToLowerInvariant();

    /// <summary>The raw value "vorssaint" is upstream's brand glyph; it maps to Faqra's own mark.</summary>
    public static KeepAwakeActiveIcon ActiveIcon(string? raw) => raw switch
    {
        "coffee" => KeepAwakeActiveIcon.Coffee,
        "eye" => KeepAwakeActiveIcon.Eye,
        "moon" => KeepAwakeActiveIcon.Moon,
        "light" => KeepAwakeActiveIcon.Light,
        _ => KeepAwakeActiveIcon.Brand,
    };

    public static string RawValue(this KeepAwakeActiveIcon icon) =>
        icon == KeepAwakeActiveIcon.Brand ? "vorssaint" : icon.ToString().ToLowerInvariant();

    public static AppAppearance Appearance(string? raw) => raw switch
    {
        "light" => AppAppearance.Light,
        "dark" => AppAppearance.Dark,
        _ => AppAppearance.System,
    };

    public static string RawValue(this AppAppearance appearance) => appearance.ToString().ToLowerInvariant();

    private static string OneOf(string? value, IReadOnlyList<string> allowed, string fallback) =>
        value is not null && allowed.Contains(value) ? value : fallback;
}
