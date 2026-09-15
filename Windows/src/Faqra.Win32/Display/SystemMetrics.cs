// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Faqra.Win32.Native;

namespace Faqra.Win32.Display;

public static class SystemMetrics
{
    /// <summary>The pixel size the shell draws tray icons at, already scaled for the system DPI.</summary>
    public static int SmallIconPixels()
    {
        var size = User32.GetSystemMetrics(User32.SM_CXSMICON);
        return size > 0 ? size : 16;
    }

    public static double SystemScale() => User32.GetDpiForSystem() / 96.0;

    public static ulong UptimeMilliseconds() => Kernel32.GetTickCount64();
}
