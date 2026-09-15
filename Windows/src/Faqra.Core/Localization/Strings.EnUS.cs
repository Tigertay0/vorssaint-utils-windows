// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the English catalog in Sources/Vorssaint/Core/Localization.swift

namespace Faqra.Core.Localization;

public sealed partial class Strings
{
    public static Strings EnUS { get; } = new()
    {
        StatusIdleTooltip = $"{AppInfo.Name}: normal sleep",
        StatusActiveUntil = $"{AppInfo.Name}: awake until",
        StatusActiveIndefinite = $"{AppInfo.Name}: awake indefinitely",

        MenuEnableAwake = "Enable keep awake",
        MenuDisableAwake = "Disable keep awake",
        MenuActivateFor = "Activate for…",
        MenuSettings = "Settings…",
        MenuAbout = $"About {AppInfo.Name}",
        MenuQuit = $"Quit {AppInfo.Name}",
        MenuCheckUpdates = "Check for updates…",
        CleaningMenuItem = "Cleaning Mode",
        UninstallerMenuItem = "Uninstall an app…",
        ShelfMenuItem = "Open shelf",

        Minutes15 = "15 minutes",
        Minutes30 = "30 minutes",
        Hour1 = "1 hour",
        Hours2 = "2 hours",
        Hours4 = "4 hours",
        Hours8 = "8 hours",
        Indefinitely = "Indefinitely",
        KeepAwakeTitle = "Keep awake",

        AboutDescription = "A utility hub for your PC.\nEnergy, system monitor, mixer and a command bar, right in the system tray.",
        VersionPrefix = "Version",
        ViewOnGitHub = "View on GitHub",
        BetaBadgeLabel = "Beta",
        ReviewIntro = "Review introduction",
        ReviewHighlights = "Review highlights",
    };
}
