// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Runtime.InteropServices;

namespace Faqra.Win32.Agents;

public static class PipeClient
{
    /// <summary>The process on the other end of a server pipe, as Windows recorded it when it connected; null if unknown.</summary>
    public static int? ProcessId(SafeHandle pipe) =>
        GetNamedPipeClientProcessId(pipe, out var id) && id != 0 ? (int)id : null;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafeHandle pipe, out uint clientProcessId);
}
