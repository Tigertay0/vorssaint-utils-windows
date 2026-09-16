// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/LaunchAtLoginSupport.swift

namespace Faqra.Core.Startup;

/// <summary>What the OS says about the startup entry.</summary>
public enum LaunchRegistration
{
    /// <summary>The system will open the app at next login.</summary>
    Enabled,
    /// <summary>Registered but switched off by the user (Task Manager › Startup apps); only the user can re-enable it.</summary>
    NeedsApproval,
    /// <summary>No usable registration.</summary>
    Off,
}

public enum StartupAction
{
    None,
    /// <summary>The OS entry is on but the stored wish is off: adopt the OS state.</summary>
    AdoptEnabled,
    /// <summary>The user wants it and the OS lost the entry: register again.</summary>
    Register,
}

/// <summary>
/// Decides what startup repair does. The stored wish is the source of truth because OS records get
/// lost; startup never disables anything, turning it off is always an explicit user action.
/// </summary>
public static class LaunchAtLoginSupport
{
    public static StartupAction Decide(bool wanted, LaunchRegistration registration, bool locationIsUnstable) =>
        (wanted, registration) switch
        {
            (false, LaunchRegistration.Enabled) => StartupAction.AdoptEnabled,
            (true, LaunchRegistration.Off) when !locationIsUnstable => StartupAction.Register,
            _ => StartupAction.None,
        };
}
