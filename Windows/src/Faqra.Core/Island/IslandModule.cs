// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors NotchModule in Sources/Vorssaint/Services/Notch/NotchSupport.swift (lines 7-72)

using Faqra.Core.Features;

namespace Faqra.Core.Island;

/// <summary>
/// The island's sections. Raw values are persisted in the hidden-module and order lists, so
/// members can be added but never renamed.
/// </summary>
public enum IslandModule
{
    Controls, Mixer, Music, Clipboard, Captures, Files, System, Tools, Calendar, Notifications, Timer, Camera, Downloads,
}

public static class IslandModules
{
    public static readonly IReadOnlyList<IslandModule> All = Enum.GetValues<IslandModule>();

    public static string RawValue(this IslandModule module) => module switch
    {
        IslandModule.Controls => "controls",
        IslandModule.Mixer => "mixer",
        IslandModule.Music => "music",
        IslandModule.Clipboard => "clipboard",
        IslandModule.Captures => "captures",
        IslandModule.Files => "files",
        IslandModule.System => "system",
        IslandModule.Tools => "tools",
        IslandModule.Calendar => "calendar",
        IslandModule.Notifications => "notifications",
        IslandModule.Timer => "timer",
        IslandModule.Camera => "camera",
        IslandModule.Downloads => "downloads",
        _ => throw new ArgumentOutOfRangeException(nameof(module), module, null),
    };

    public static IslandModule? FromRawValue(string? raw) =>
        All.Cast<IslandModule?>().FirstOrDefault(module => module!.Value.RawValue() == raw);

    /// <summary>Stable across ordering and languages; every destination has a direct key.</summary>
    public static char ShortcutKey(this IslandModule module) => module switch
    {
        IslandModule.Controls => 'c',
        IslandModule.Mixer => 'v',
        IslandModule.Music => 'm',
        IslandModule.Clipboard => 'b',
        IslandModule.Captures => 's',
        IslandModule.Files => 'f',
        IslandModule.System => 'i',
        IslandModule.Tools => 't',
        IslandModule.Calendar => 'a',
        IslandModule.Notifications => 'n',
        IslandModule.Timer => 'r',
        IslandModule.Camera => 'w',
        IslandModule.Downloads => 'd',
        _ => throw new ArgumentOutOfRangeException(nameof(module), module, null),
    };

    /// <summary>Segoe Fluent Icons glyph, replacing upstream's SF Symbol.</summary>
    public static string Glyph(this IslandModule module) => module switch
    {
        IslandModule.Controls => "",      // Slider
        IslandModule.Mixer => "",         // Volume
        IslandModule.Music => "",         // MusicNote
        IslandModule.Clipboard => "",     // Copy
        IslandModule.Captures => "",      // Camera
        IslandModule.Files => "",         // Tray
        IslandModule.System => "",        // DataSense
        IslandModule.Tools => "",         // AllApps
        IslandModule.Calendar => "",      // Calendar
        IslandModule.Notifications => "", // Ringer
        IslandModule.Timer => "",         // Stopwatch
        IslandModule.Camera => "",        // Webcam
        IslandModule.Downloads => "",     // Download
        _ => throw new ArgumentOutOfRangeException(nameof(module), module, null),
    };

    /// <summary>
    /// Whether the module has anything to show. Controls and Music always do; the rest ride on the
    /// feature that fills them, so an uninstalled feature takes its section out of the island.
    /// </summary>
    public static bool IsAvailable(this IslandModule module, Func<AppFeature, bool> isAvailable) => module switch
    {
        IslandModule.Controls or IslandModule.Music => true,
        IslandModule.Timer => isAvailable(AppFeature.NotchTimer),
        IslandModule.Camera => isAvailable(AppFeature.CameraPreview),
        IslandModule.Downloads => isAvailable(AppFeature.NotchDownloads),
        IslandModule.Notifications => isAvailable(AppFeature.NotchNotifications),
        IslandModule.Calendar => isAvailable(AppFeature.NotchCalendar),
        IslandModule.Mixer => isAvailable(AppFeature.Mixer),
        IslandModule.Tools => isAvailable(AppFeature.QuickLauncher),
        IslandModule.Clipboard => isAvailable(AppFeature.ClipboardHistory),
        IslandModule.Captures => isAvailable(AppFeature.Screenshot) || isAvailable(AppFeature.ScreenRecorder)
            || isAvailable(AppFeature.ScreenOCR) || isAvailable(AppFeature.ColorPicker),
        IslandModule.Files => isAvailable(AppFeature.Shelf),
        IslandModule.System => isAvailable(AppFeature.MonitorCPU) || isAvailable(AppFeature.MonitorGPU)
            || isAvailable(AppFeature.MonitorMemory) || isAvailable(AppFeature.MonitorNetwork)
            || isAvailable(AppFeature.MonitorDisk) || isAvailable(AppFeature.MonitorPower),
        _ => throw new ArgumentOutOfRangeException(nameof(module), module, null),
    };

    /// <summary>
    /// The visible modules in display order: the saved order first, then any not yet listed in
    /// canonical order, minus the hidden ones and the unavailable ones.
    /// </summary>
    public static IReadOnlyList<IslandModule> Visible(
        string? savedOrder,
        string? hiddenList,
        Func<AppFeature, bool> isAvailable)
    {
        var hidden = (hiddenList ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<IslandModule>();
        var ordered = new List<IslandModule>();
        foreach (var raw in (savedOrder ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (FromRawValue(raw) is { } module && seen.Add(module))
            {
                ordered.Add(module);
            }
        }
        foreach (var module in All.Where(module => seen.Add(module)))
        {
            ordered.Add(module);
        }
        return ordered
            .Where(module => !hidden.Contains(module.RawValue()) && module.IsAvailable(isAvailable))
            .ToList();
    }

    /// <summary>The next or previous visible module, wrapping. Drives Ctrl+Tab and Ctrl+Shift+Tab.</summary>
    public static IslandModule? Adjacent(IslandModule current, int delta, IReadOnlyList<IslandModule> visible)
    {
        if (visible.Count == 0)
        {
            return null;
        }
        var index = -1;
        for (var i = 0; i < visible.Count; i++)
        {
            if (visible[i] == current)
            {
                index = i;
                break;
            }
        }
        if (index < 0)
        {
            return visible[0];
        }
        var next = ((index + delta) % visible.Count + visible.Count) % visible.Count;
        return visible[next];
    }
}
