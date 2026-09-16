// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the monitor, panel and menu bar metric entries of Sources/Vorssaint/Core/Localization.swift
// (English block from line 2186), Core/BatteryTimeStrings.swift and the notch system strings.

namespace Faqra.Core.Localization;

/// <summary>Every string the popover panel, the tray metric icons, the island's System module and the Monitor page show.</summary>
public sealed partial class MonitorStrings
{
    // Panel chrome
    public required string SystemSection { get; init; }
    public required string NetworkSection { get; init; }
    public required string DiskSection { get; init; }
    public required string PowerSection { get; init; }
    public required string PanelSettings { get; init; }
    public required string PanelQuit { get; init; }
    public required string Measuring { get; init; }

    // System
    public required string HardwareUsage { get; init; }
    public required string Cpu { get; init; }
    public required string Gpu { get; init; }
    public required string Memory { get; init; }
    public required string Pressure { get; init; }
    public required string PressureNormal { get; init; }
    public required string PressureWarning { get; init; }
    public required string PressureCritical { get; init; }
    public required string MemoryCompressed { get; init; }
    public required string MemoryCachedFiles { get; init; }
    public required string MemorySwapUsed { get; init; }
    public required string UptimeFormat { get; init; }
    public required string Uptime { get; init; }
    public required string OpenTaskManager { get; init; }

    // Network
    public required string Download { get; init; }
    public required string Upload { get; init; }
    public required string ThisSession { get; init; }
    public required string LiveSpeed { get; init; }
    public required string SessionTotals { get; init; }

    // Disks
    public required string SelectDisk { get; init; }
    public required string DiskUsage { get; init; }
    public required string LiveActivity { get; init; }
    public required string Read { get; init; }
    public required string Write { get; init; }
    public required string UsedFormat { get; init; }
    public required string AvailableFormat { get; init; }
    public required string Available { get; init; }
    public required string Internal { get; init; }
    public required string External { get; init; }
    public required string NoDisks { get; init; }

    // Power
    public required string Battery { get; init; }
    public required string Charge { get; init; }
    public required string Charging { get; init; }
    public required string OnBattery { get; init; }
    public required string PluggedIn { get; init; }
    public required string SystemPower { get; init; }
    public required string BatteryTimeRemaining { get; init; }
    public required string SystemEstimate { get; init; }
    public required string Calculating { get; init; }
    public required string PowerUnavailable { get; init; }
    public required string Power { get; init; }

    // Island
    public required string SensorsUnavailable { get; init; }

    // Monitor settings page
    public required string TraySection { get; init; }
    public required string TrayCaption { get; init; }
    public required string UpdateEvery { get; init; }
    public required string OneSecond { get; init; }
    public required string SecondsFormat { get; init; }
    public required string InThePanel { get; init; }
    public required string InThePanelCaption { get; init; }
    public required string ShowInPanel { get; init; }
    public required string Graphs { get; init; }
    public required string GraphsCaption { get; init; }
    public required string PanelOrder { get; init; }
    public required string PanelOrderCaption { get; init; }
    public required string MoveUp { get; init; }
    public required string MoveDown { get; init; }

    // Island placeholder for sections whose feature is not ported yet
    public required string ComingLater { get; init; }

    public static MonitorStrings For(AppLanguage language) => language switch
    {
        // Translations pending: every language resolves to English for now (see Strings.Aliases.cs).
        _ => EnUS,
    };
}
