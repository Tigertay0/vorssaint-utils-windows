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

    public static string EnsureLocalDataDirectory()
    {
        Directory.CreateDirectory(LocalDataDirectory);
        return LocalDataDirectory;
    }
}
