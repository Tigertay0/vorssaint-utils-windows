// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the Stage 1 subset of CommandBarFeatureStrings in Sources/Vorssaint/Core/CommandBarStrings.swift
// (enUS, lines 191-349). Where upstream names the Mac, Finder or Vorssaint, the Windows or Faqra name is
// used; format strings use {0} instead of %@ and %d.

namespace Faqra.Core.Localization;

/// <summary>Every string the command bar window and its settings page show.</summary>
public sealed class CommandBarStrings
{
    public required string PageTitle { get; init; }
    public required string HubDescription { get; init; }
    public required string SettingsCaption { get; init; }
    public required string OpenButton { get; init; }
    public required string SearchPlaceholder { get; init; }
    public required string SuggestionsLabel { get; init; }
    public required string EverythingTitle { get; init; }
    public required string NoResultsTitle { get; init; }
    public required string NoResultsAction { get; init; }
    public required string ArgumentRangeFormat { get; init; }
    public required string ArgumentHint { get; init; }
    public required string ConfirmHint { get; init; }
    public required string ConfirmButton { get; init; }
    public required string CancelButton { get; init; }
    public required string CopyHint { get; init; }
    public required string TryTheseLabel { get; init; }
    public required string StateOn { get; init; }
    public required string NeedsSetupFormat { get; init; }
    public required string PrivacyNote { get; init; }
    public required string ShortcutToggle { get; init; }
    public required string ShortcutFallbackFormat { get; init; }
    public required string ForgetAllButton { get; init; }

    // Row subtitles
    public required string KindApp { get; init; }
    public required string KindWindow { get; init; }
    public required string KindAnswer { get; init; }
    public required string KindFolder { get; init; }

    // Actions
    public required string KeepAwakeForFormat { get; init; }
    public required string VolumeTitle { get; init; }
    public required string SoundMute { get; init; }
    public required string SoundUnmute { get; init; }
    public required string ActionOpenSettings { get; init; }
    public required string OpenInBrowser { get; init; }
    public required string TurnOnFormat { get; init; }
    public required string TurnOffFormat { get; init; }
    public required string PowerSleep { get; init; }
    public required string PowerRestart { get; init; }
    public required string PowerRestartConfirm { get; init; }
    public required string PowerShutDown { get; init; }
    public required string PowerShutDownConfirm { get; init; }
    public required string PowerLogOut { get; init; }
    public required string PowerLogOutConfirm { get; init; }
    public required string PowerLock { get; init; }
    public required string AnswerDateLabel { get; init; }
    public required string AnswerTimeLabel { get; init; }

    // Sources
    public required string SourcesTitle { get; init; }
    public required string SourcesCaption { get; init; }
    public required string SourceActions { get; init; }
    public required string SourceApps { get; init; }
    public required string SourceWindows { get; init; }
    public required string SourceSettingsPages { get; init; }
    public required string SourceFolders { get; init; }
    public required string SourceCalculator { get; init; }

    public static CommandBarStrings For(AppLanguage language) => language switch
    {
        // Translations pending: every language resolves to English for now (see Strings.Aliases.cs).
        _ => EnUS,
    };

    public static CommandBarStrings EnUS { get; } = new()
    {
        PageTitle = "Command Bar",
        HubDescription = "One field that finds and runs everything the app does",
        SettingsCaption = "One shortcut opens a field over whatever you are doing. Type a few letters, press Enter and it happens. Nothing you type is saved.",
        OpenButton = "Open the bar now",
        SearchPlaceholder = "Type what you want to do",
        SuggestionsLabel = "Suggestions",
        EverythingTitle = "Everything it can do",
        NoResultsTitle = "Nothing here by that name.",
        NoResultsAction = "See suggestions",
        ArgumentRangeFormat = "{0} to {1}",
        ArgumentHint = "Enter applies · Esc goes back",
        ConfirmHint = "Enter confirms · Esc cancels",
        ConfirmButton = "Confirm",
        CancelButton = "Cancel",
        CopyHint = "Enter copies",
        TryTheseLabel = "Try",
        StateOn = "on",
        NeedsSetupFormat = "Turn on {0} in Settings",
        PrivacyNote = "Everything happens on this PC: no account, no cloud, nothing sent anywhere.",
        ShortcutToggle = "Global shortcut to open the bar",
        ShortcutFallbackFormat = "Another app already uses Alt+Space, so the bar opens with {0}.",
        ForgetAllButton = "Forget what I use most",

        KindApp = "App",
        KindWindow = "Window",
        KindAnswer = "Answer",
        KindFolder = "Folder",

        KeepAwakeForFormat = "Keep awake for {0}",
        VolumeTitle = "Volume",
        SoundMute = "Mute the sound",
        SoundUnmute = "Turn the sound back on",
        ActionOpenSettings = "Open Settings",
        OpenInBrowser = "Open in browser",
        TurnOnFormat = "Turn on {0}",
        TurnOffFormat = "Turn off {0}",
        PowerSleep = "Sleep",
        PowerRestart = "Restart",
        PowerRestartConfirm = "Restart the PC?",
        PowerShutDown = "Shut down",
        PowerShutDownConfirm = "Shut down the PC?",
        PowerLogOut = "Sign out",
        PowerLogOutConfirm = "Sign out?",
        PowerLock = "Lock the PC",
        AnswerDateLabel = "Today",
        AnswerTimeLabel = "Time now",

        SourcesTitle = "What the bar searches",
        SourcesCaption = "Turn off what you never want to see. Your own actions always stay.",
        SourceActions = $"{AppInfo.Name} actions",
        SourceApps = "Apps",
        SourceWindows = "Open windows",
        SourceSettingsPages = "Settings pages",
        SourceFolders = "Folders",
        SourceCalculator = "Sums and conversions",
    };
}

/// <summary>The shortcut recorder and the Shortcuts settings page (Localization.swift:3011-3056, ShortcutSettingsStrings.swift).</summary>
public sealed class ShortcutStrings
{
    public required string PageCaption { get; init; }
    public required string Recording { get; init; }
    public required string PressKeys { get; init; }
    public required string EscapeHint { get; init; }
    public required string Reset { get; init; }
    public required string Invalid { get; init; }
    public required string ConflictFormat { get; init; }
    public required string Reserved { get; init; }
    public required string Unavailable { get; init; }
    public required string Active { get; init; }
    public required string Inactive { get; init; }

    public static ShortcutStrings For(AppLanguage language) => language switch
    {
        _ => EnUS,
    };

    public static ShortcutStrings EnUS { get; } = new()
    {
        PageCaption = "Edit every global shortcut from the features installed on this PC. Inactive shortcuts stay saved but do not run.",
        Recording = "Press the new shortcut",
        PressKeys = "Press keys",
        EscapeHint = "Escape cancels.",
        Reset = "Reset",
        Invalid = "Use at least Ctrl, Alt or Win with a key.",
        ConflictFormat = "This shortcut is already used by {0}.",
        Reserved = "Windows keeps this shortcut for itself. Choose another one.",
        Unavailable = "Windows or another app already uses this shortcut. Choose another one.",
        Active = "Active",
        Inactive = "Inactive",
    };
}
