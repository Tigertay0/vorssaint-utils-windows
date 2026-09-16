// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the English catalog in Sources/Vorssaint/Core/Localization.swift. Where upstream names a
// macOS thing (Activity Monitor, swap, "on this Mac"), the Windows name is used instead.

namespace Faqra.Core.Localization;

public sealed partial class MonitorStrings
{
    public static MonitorStrings EnUS { get; } = new()
    {
        SystemSection = "System",
        NetworkSection = "Network",
        DiskSection = "Disks",
        PowerSection = "Power",
        PanelSettings = "Settings",
        PanelQuit = "Quit",
        Measuring = "Measuring…",

        HardwareUsage = "Hardware usage",
        Cpu = "CPU",
        Gpu = "GPU",
        Memory = "Memory",
        Pressure = "Pressure",
        PressureNormal = "Normal",
        PressureWarning = "Caution",
        PressureCritical = "Critical",
        MemoryCompressed = "Compressed",
        MemoryCachedFiles = "Cached files",
        MemorySwapUsed = "Page file used",
        UptimeFormat = "Up for {0}",
        Uptime = "Uptime",
        OpenTaskManager = "Open Task Manager",

        Download = "Download",
        Upload = "Upload",
        ThisSession = "This session",
        LiveSpeed = "Live speed",
        SessionTotals = "Session totals",

        SelectDisk = "Select disk",
        DiskUsage = "Disk usage",
        LiveActivity = "Live activity",
        Read = "Read",
        Write = "Write",
        UsedFormat = "{0} used",
        AvailableFormat = "{0} available",
        Available = "Available",
        Internal = "Internal",
        External = "External",
        NoDisks = "No disks found.",

        Battery = "Battery",
        Charge = "Charge",
        Charging = "Charging",
        OnBattery = "On battery",
        PluggedIn = "Plugged in",
        SystemPower = "System",
        BatteryTimeRemaining = "Battery time remaining",
        SystemEstimate = "System estimate",
        Calculating = "Calculating…",
        PowerUnavailable = "Power metrics unavailable on this PC",
        Power = "Power",

        SensorsUnavailable = "Sensors unavailable on this PC",

        TraySection = "In the system tray",
        TrayCaption = "Each reading you turn on gets its own icon in the system tray.",
        UpdateEvery = "Update every",
        OneSecond = "1 second",
        SecondsFormat = "{0} seconds",
        InThePanel = "In the panel",
        InThePanelCaption = "Choose what each panel section shows.",
        ShowInPanel = "Show in panel",
        Graphs = "Graphs",
        GraphsCaption = "Choose which metrics show a graph over time.",
        PanelOrder = "Panel sections",
        PanelOrderCaption = "Move a section up or down to change its place in the panel.",
        MoveUp = "Move up",
        MoveDown = "Move down",

        ComingLater = "Coming in a later update.",
    };
}
