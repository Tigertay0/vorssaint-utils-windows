// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of FileManager.urls(for: .downloadsDirectory) in CommandBarExtras.standardFolders
// (Sources/Vorssaint/Services/CommandBar/CommandBarExtras.swift); .NET's SpecialFolder has no Downloads.

using System.Runtime.InteropServices;

namespace Faqra.Win32.Shell;

public static class KnownFolders
{
    private static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

    /// <summary>The user's Downloads folder, wherever they moved it; null when the shell has none.</summary>
    public static string? Downloads()
    {
        var id = DownloadsId;
        if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out var pointer) != 0)
        {
            return null;
        }
        try
        {
            return Marshal.PtrToStringUni(pointer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);
}
