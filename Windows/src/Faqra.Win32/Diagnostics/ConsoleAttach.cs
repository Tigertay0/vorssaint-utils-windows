// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Faqra.Win32.Native;

namespace Faqra.Win32.Diagnostics;

/// <summary>Lets a WinExe print to the console it was launched from (for --selftest and --uninstall).</summary>
public static class ConsoleAttach
{
    public static bool TryAttachParent()
    {
        if (!Kernel32.AttachConsole(Kernel32.ATTACH_PARENT_PROCESS))
        {
            return false;
        }
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        return true;
    }
}
