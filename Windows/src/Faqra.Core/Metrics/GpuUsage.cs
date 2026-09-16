// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of readGPUUsage (IOAccelerator "Device Utilization %") in
// Sources/Vorssaint/Services/SystemMonitor/SystemMonitor.swift (lines 1086-1112).

namespace Faqra.Core.Metrics;

public static class GpuUsage
{
    private const string EngineMarker = "luid_";

    /// <summary>
    /// GPU usage as a 0…1 fraction from the <c>\GPU Engine(*)\Utilization Percentage</c> instances,
    /// computed the way Task Manager does: each engine's load is the sum over the processes using it,
    /// and the GPU reads as its busiest engine. Null when there are no instances at all.
    /// </summary>
    /// <param name="instances">Instance names like <c>pid_1234_luid_0x0_0x1_phys_0_eng_0_engtype_3D</c>.</param>
    public static double? FromEngineInstances(IEnumerable<(string Name, double Value)> instances)
    {
        var perEngine = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in instances)
        {
            if (!double.IsFinite(value))
            {
                continue;
            }
            var start = name.IndexOf(EngineMarker, StringComparison.OrdinalIgnoreCase);
            var engine = start >= 0 ? name[start..] : name;
            perEngine[engine] = perEngine.GetValueOrDefault(engine) + Math.Max(0, value);
        }
        if (perEngine.Count == 0)
        {
            return null;
        }
        return Math.Clamp(perEngine.Values.Max() / 100, 0, 1);
    }
}
