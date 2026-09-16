// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors FeatureRuntime.relaunchApp() in Sources/Vorssaint/App/FeatureRuntime.swift (lines 43-64)

using System.Diagnostics;
using System.Windows;
using Faqra.Services.Startup;

namespace Faqra.App;

/// <summary>
/// Restarts the app in place so uninstalled features leave memory. A detached helper waits for this
/// process to exit before reopening, because quitting flushes pending settings writes first.
/// </summary>
public static class AppRelauncher
{
    public static void Relaunch()
    {
        var exe = LaunchAtLogin.ExecutablePath;
        if (string.IsNullOrEmpty(exe))
        {
            return;
        }
        var pid = Environment.ProcessId;
        // Waits for the PID to disappear, then starts the app again; gives up after ~20 seconds so a
        // quit that never happens cannot reopen the app long afterwards.
        var command = $"/c for /l %i in (1,1,100) do (tasklist /fi \"PID eq {pid}\" | find \"{pid}\" >nul || (start \"\" \"{exe}\" & exit)) & timeout /t 1 /nobreak >nul";
        var started = Process.Start(new ProcessStartInfo("cmd.exe", command)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        });
        if (started is not null)
        {
            Application.Current.Shutdown();
        }
    }
}
