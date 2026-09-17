// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of CGGetOnlineDisplayList + CGDisplayIsBuiltin in Sources/Vorssaint/Services/KeepAwakeManager.swift
// (hasExternalDisplay, 468-477).

using System.Runtime.InteropServices;

namespace Faqra.Win32.Display;

public static class DisplayTopology
{
    private const uint QDC_ONLY_ACTIVE_PATHS = 2;
    private const int PathInfoSize = 72;
    private const int ModeInfoSize = 64;
    private const int OutputTechnologyOffset = 36;
    private const int ErrorInsufficientBuffer = 122;

    private const uint InternalTechnology = 0x80000000;
    private const uint DisplayPortEmbedded = 11;
    private const uint UdiEmbedded = 13;

    /// <summary>
    /// Whether each active display is built into the PC (a laptop panel), or null when Windows would
    /// not say. Windows has no "built-in" flag; the connector type is the closest signal.
    /// </summary>
    public static bool[]? BuiltInFlags()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount) != 0)
            {
                return null;
            }
            var paths = new byte[pathCount * PathInfoSize];
            var modes = new byte[modeCount * ModeInfoSize];
            var result = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (result == ErrorInsufficientBuffer)
            {
                continue;
            }
            if (result != 0)
            {
                return null;
            }
            var flags = new bool[pathCount];
            for (var i = 0; i < pathCount; i++)
            {
                var technology = BitConverter.ToUInt32(paths, i * PathInfoSize + OutputTechnologyOffset);
                flags[i] = technology is InternalTechnology or DisplayPortEmbedded or UdiEmbedded;
            }
            return flags;
        }
        return null;
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] byte[] pathArray, ref uint numModeInfoArrayElements, [Out] byte[] modeInfoArray, IntPtr currentTopologyId);
}
