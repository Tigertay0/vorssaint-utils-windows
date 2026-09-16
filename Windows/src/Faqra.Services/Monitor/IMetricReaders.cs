// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the reader seams in Sources/Vorssaint/Services/SystemMonitor/SystemMonitor.swift
// (readCPUUsage, readGPUUsage, SystemInfo.memoryUsage) and the NetworkSampler, DiskSampler and
// PowerSampler readers, so the sampling loop can be tested without the live machine.

using Faqra.Core.Metrics;

namespace Faqra.Services.Monitor;

/// <summary>
/// Raw readings from the operating system. Every method returns null (or empty) instead of throwing
/// when a source is missing, and is called from the monitor's sampling thread only.
/// </summary>
public interface IMetricReaders : IDisposable
{
    bool HasBattery { get; }

    /// <summary>Processor utility, 0…1. Null until the counter has two samples.</summary>
    double? ReadCpu();

    /// <summary>Raw GPU usage, 0…1, before stabilization.</summary>
    double? ReadGpu();

    MemorySample? ReadMemory();

    /// <summary>Cumulative bytes over the physical network adapters.</summary>
    NetworkCounters? ReadNetwork();

    IReadOnlyList<VolumeSample> ReadVolumes();

    PowerReading? ReadPower();

    long UptimeSeconds();
}
