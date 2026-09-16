// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/LaunchAtLogin.swift. macOS registers a login item through
// SMAppService; Windows uses the per-user Run key, with Explorer's StartupApproved value deciding
// whether the entry is actually honored.

using System.Diagnostics;
using System.Security;
using Faqra.Core;
using Faqra.Core.Defaults;
using Faqra.Core.Startup;
using Microsoft.Win32;

namespace Faqra.Services.Startup;

/// <summary>Raised when the OS refused to start the app at login, with a message ready to show.</summary>
public sealed class LaunchAtLoginException(string message) : Exception(message);

/// <summary>
/// Reads and writes the startup entry. The stored wish (<see cref="DefaultsKey.LaunchAtLoginWanted"/>)
/// is the source of truth because an entry can be lost: startup repair re-registers it, but never
/// disables anything by itself.
/// </summary>
public sealed class LaunchAtLogin(ISettingsStore store)
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = AppInfo.Name;

    /// <summary>The first byte of a StartupApproved value: 2 means enabled, 3 means the user switched it off.</summary>
    private const byte ApprovedEnabled = 2;

    public LaunchRegistration Registration
    {
        get
        {
            try
            {
                using var run = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                if (run?.GetValue(ValueName) is not string)
                {
                    return LaunchRegistration.Off;
                }
                using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath);
                if (approved?.GetValue(ValueName) is byte[] { Length: > 0 } state && state[0] != ApprovedEnabled)
                {
                    return LaunchRegistration.NeedsApproval;
                }
                return LaunchRegistration.Enabled;
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
            {
                return LaunchRegistration.Off;
            }
        }
    }

    public bool IsEnabled => Registration == LaunchRegistration.Enabled;

    /// <summary>A copy running from a temp folder or a removable drive cannot be relied on at login.</summary>
    public bool LocationIsUnstable
    {
        get
        {
            var path = ExecutablePath;
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }
            var temp = Path.GetTempPath();
            if (path.StartsWith(temp, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            try
            {
                var root = Path.GetPathRoot(path);
                return root is not null && new DriveInfo(root).DriveType is DriveType.Removable or DriveType.Network;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    public static string ExecutablePath => Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

    /// <summary>
    /// Turns the entry on or off. The stored wish is written first so a failure still leaves the
    /// intent recorded, then corrected to reality if the write failed.
    /// </summary>
    public void SetEnabled(bool enabled, Core.Localization.Strings s)
    {
        if (enabled && LocationIsUnstable)
        {
            throw new LaunchAtLoginException(s.LaunchAtLoginUnstableLocation);
        }
        store.Set(DefaultsKey.LaunchAtLoginWanted, enabled);
        Exception? failure = null;
        try
        {
            if (enabled)
            {
                Register();
            }
            else
            {
                Unregister();
            }
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            failure = ex;
        }

        if (enabled && Registration == LaunchRegistration.NeedsApproval)
        {
            throw new LaunchAtLoginException(s.LaunchAtLoginNeedsApproval);
        }
        if (failure is not null && IsEnabled != enabled)
        {
            store.Set(DefaultsKey.LaunchAtLoginWanted, IsEnabled);
            throw new LaunchAtLoginException(failure.Message);
        }
    }

    /// <summary>Re-registers an entry the system lost. Never disables anything.</summary>
    public void RepairAtStartup()
    {
        var action = LaunchAtLoginSupport.Decide(
            store.Bool(DefaultsKey.LaunchAtLoginWanted), Registration, LocationIsUnstable);
        switch (action)
        {
            case StartupAction.AdoptEnabled:
                store.Set(DefaultsKey.LaunchAtLoginWanted, true);
                break;
            case StartupAction.Register:
                try
                {
                    Register();
                }
                catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
                {
                    // The wish stays stored; the next launch tries again.
                }
                break;
        }
    }

    private void Register()
    {
        using var run = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new LaunchAtLoginException("cannot open the Run key");
        run.SetValue(ValueName, $"\"{ExecutablePath}\" --autostart", RegistryValueKind.String);
    }

    private void Unregister()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        run?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
