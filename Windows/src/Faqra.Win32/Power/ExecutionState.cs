// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of IOPMAssertionCreateWithName in Sources/Vorssaint/Services/KeepAwakeManager.swift

using Faqra.Win32.Native;

namespace Faqra.Win32.Power;

/// <summary>
/// Thread execution state. The flag belongs to the calling thread, so every call must come from
/// the same long-lived thread (the UI thread) and be cleared with ES_CONTINUOUS alone.
/// </summary>
public static class ExecutionState
{
    /// <summary>Keeps the system (and optionally the display) awake until <see cref="Clear"/>.</summary>
    public static bool KeepAwake(bool keepDisplayOn)
    {
        var flags = Kernel32.ES_CONTINUOUS | Kernel32.ES_SYSTEM_REQUIRED;
        if (keepDisplayOn)
        {
            flags |= Kernel32.ES_DISPLAY_REQUIRED;
        }
        return Kernel32.SetThreadExecutionState(flags) != 0;
    }

    public static bool Clear() => Kernel32.SetThreadExecutionState(Kernel32.ES_CONTINUOUS) != 0;

    /// <summary>Requests then releases the state; false when the OS refused the request.</summary>
    public static bool RoundTrip()
    {
        var granted = KeepAwake(keepDisplayOn: false);
        Clear();
        return granted;
    }
}
