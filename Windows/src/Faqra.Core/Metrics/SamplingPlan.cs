// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors SamplingPlan and currentPlan in Sources/Vorssaint/Services/SystemMonitor/SystemMonitor.swift
// (lines 84-121, 463-575, 614-626). Temperatures, fans, peripheral batteries and alerts have no
// Windows sampler yet, so their needs are left out rather than kept as permanently false flags.

using Faqra.Core.Defaults;
using Faqra.Core.Features;

namespace Faqra.Core.Metrics;

/// <summary>Which panel sections are on screen right now.</summary>
public readonly record struct PanelNeeds(bool System = false, bool Network = false, bool Disk = false, bool Power = false)
{
    public bool Any => System || Network || Disk || Power;
}

/// <summary>The readings one sampling tick must take, and whether a full surface is watching.</summary>
public readonly record struct SamplingPlan(
    bool NeedCpu,
    bool NeedMemory,
    bool NeedNetwork,
    bool NeedDisk,
    bool NeedPower,
    bool NeedGpu,
    bool Foreground)
{
    public bool Any => NeedCpu || NeedMemory || NeedNetwork || NeedDisk || NeedPower || NeedGpu;

    /// <summary>
    /// Builds the plan from what is visible and what the user switched on. A panel section or the
    /// island's System module makes sampling foreground (every second); tray metrics alone keep it
    /// in the background, where the expensive readings slow down.
    /// </summary>
    public static SamplingPlan Build(
        PanelNeeds panel,
        bool notchVisible,
        Func<string, bool> setting,
        Func<AppFeature, bool> isAvailable,
        bool hasBattery)
    {
        var panelCpu = (panel.System && setting(DefaultsKey.MonitorSysCPU)) || notchVisible;
        var panelGpu = (panel.System && setting(DefaultsKey.MonitorSysGPU)) || notchVisible;
        var panelMemory = (panel.System && setting(DefaultsKey.MonitorSysMemory)) || notchVisible;
        var panelNetwork = panel.Network || notchVisible;
        // Upstream's island asks for disk through its detail needs; the System module's "Available" card needs it.
        var panelDisk = panel.Disk || notchVisible;
        var panelPower = panel.Power || notchVisible;
        var panelBattery = hasBattery && panel.Power && setting(DefaultsKey.MonitorSysBattery);

        var needCpu = panelCpu || setting(DefaultsKey.MenuBarCPU);
        var needMemory = panelMemory || setting(DefaultsKey.MenuBarMemory);
        var needNetwork = panelNetwork || setting(DefaultsKey.MenuBarNetwork);
        var needDisk = panelDisk || setting(DefaultsKey.MenuBarDiskUsage) || setting(DefaultsKey.MenuBarDiskActivity);
        var needPower = panelPower || panelBattery || setting(DefaultsKey.MenuBarPower)
            || (hasBattery && (setting(DefaultsKey.MenuBarBattery) || setting(DefaultsKey.MenuBarBatteryTime)));
        var needGpu = panelGpu || setting(DefaultsKey.MenuBarGPU);

        return new SamplingPlan(
            needCpu && isAvailable(AppFeature.MonitorCPU),
            needMemory && isAvailable(AppFeature.MonitorMemory),
            needNetwork && isAvailable(AppFeature.MonitorNetwork),
            needDisk && isAvailable(AppFeature.MonitorDisk),
            needPower && isAvailable(AppFeature.MonitorPower),
            needGpu && isAvailable(AppFeature.MonitorGPU),
            Foreground: panel.Any || notchVisible);
    }

    /// <summary>The kinds this plan samples, in upstream's order; drives the timer cadence.</summary>
    public IReadOnlyList<MonitorSamplingKind> NeededKinds()
    {
        var kinds = new List<MonitorSamplingKind>(6);
        if (NeedCpu) kinds.Add(MonitorSamplingKind.Cpu);
        if (NeedMemory) kinds.Add(MonitorSamplingKind.Memory);
        if (NeedNetwork) kinds.Add(MonitorSamplingKind.Network);
        if (NeedDisk) kinds.Add(MonitorSamplingKind.Disk);
        if (NeedPower) kinds.Add(MonitorSamplingKind.Power);
        if (NeedGpu) kinds.Add(MonitorSamplingKind.GpuUsage);
        return kinds;
    }
}
