// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the Application Support layout of Sources/Vorssaint/Services/PrivateFileStore.swift

namespace Faqra.Core;

/// <summary>Where Faqra keeps its own files. Settings and user material live under one folder.</summary>
public static class AppPaths
{
    /// <summary>%LOCALAPPDATA%\Faqra. Created on demand.</summary>
    public static string LocalDataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.Name);

    public static string SettingsFile => Path.Combine(LocalDataDirectory, "settings.json");

    /// <summary>The relay Claude Code's hooks run. A fixed path, so updates never break the hooks.</summary>
    public static string AgentsRelayFile => Path.Combine(LocalDataDirectory, "bin", "faqra-hook.exe");

    /// <summary>One line per agent event: names and states only, never prompts, commands or paths.</summary>
    public static string AgentsLogFile => Path.Combine(LocalDataDirectory, "agents.log");

    /// <summary>Claude Code's user settings, where its hooks are registered.</summary>
    public static string ClaudeSettingsFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");

    public static string EnsureLocalDataDirectory()
    {
        Directory.CreateDirectory(LocalDataDirectory);
        return LocalDataDirectory;
    }
}
