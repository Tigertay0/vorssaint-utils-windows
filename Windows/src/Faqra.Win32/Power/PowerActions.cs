// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of CommandBarExtras.PowerAction in Sources/Vorssaint/Services/CommandBar/CommandBarExtras.swift
// (pmset sleepnow, System Events restart / shut down / log out) with the Windows equivalents.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Faqra.Win32.Power;

public enum PowerAction
{
    Sleep,
    Lock,
    Restart,
    ShutDown,
    SignOut,
}

public static class PowerActions
{
    /// <summary>Runs the action; false when Windows refused. Restart and shut down let apps save first.</summary>
    public static bool Run(PowerAction action) => action switch
    {
        PowerAction.Sleep => SetSuspendState(false, false, false),
        PowerAction.Lock => LockWorkStation(),
        // shutdown.exe asks Windows the same way Start does, without Faqra holding the shutdown privilege.
        PowerAction.Restart => Shutdown("/r /t 0"),
        PowerAction.ShutDown => Shutdown("/s /t 0"),
        PowerAction.SignOut => Shutdown("/l"),
        _ => false,
    };

    private static bool Shutdown(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("shutdown.exe", arguments) { UseShellExecute = false, CreateNoWindow = true });
            return process is not null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool SetSuspendState([MarshalAs(UnmanagedType.U1)] bool hibernate, [MarshalAs(UnmanagedType.U1)] bool forceCritical, [MarshalAs(UnmanagedType.U1)] bool disableWakeEvent);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();
}
