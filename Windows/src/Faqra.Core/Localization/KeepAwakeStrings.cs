// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the keep-awake entries of Sources/Vorssaint/Core/Localization.swift and Core/KeepAwakeStrings.swift
// (automation and display-sleep strings). Where upstream names the Mac, the Windows name is used.

namespace Faqra.Core.Localization;

/// <summary>Every string the keep-awake panel section, the Energy settings page and its notifications show.</summary>
public sealed partial class KeepAwakeStrings
{
    // Panel section
    public required string EndsIn { get; init; }
    public required string UntilDisabled { get; init; }
    public required string NormalRules { get; init; }
    public required string Options { get; init; }
    public required string Duration { get; init; }
    public required string Indefinite { get; init; }
    public required string ExtendFormat { get; init; }

    // Options and settings
    public required string AllowDisplaySleep { get; init; }
    public required string AllowDisplaySleepCaption { get; init; }
    public required string AutoStart { get; init; }
    public required string AutoStartCaption { get; init; }
    public required string SessionSection { get; init; }
    public required string DefaultDuration { get; init; }
    public required string RightClickToggle { get; init; }
    public required string RightClickToggleCaption { get; init; }

    // Automation
    public required string AutomationSection { get; init; }
    public required string AutomationCaption { get; init; }
    public required string AutomationOff { get; init; }
    public required string ExternalDisplayToggle { get; init; }
    public required string ExternalDisplayActive { get; init; }
    public required string PowerToggle { get; init; }
    public required string PowerActive { get; init; }
    public required string RunningAppsActive { get; init; }
    public required string AutomationActive { get; init; }
    public required string PauseWhenLocked { get; init; }
    public required string PauseWhenLockedCaption { get; init; }

    // Battery protection
    public required string BatteryProtectionSection { get; init; }
    public required string BatteryDisableBelow { get; init; }
    public required string BatteryNever { get; init; }
    public required string BatteryProtectionCaption { get; init; }

    // Tray icon
    public required string ActiveIconLabel { get; init; }
    public required string ActiveIconBrand { get; init; }
    public required string ActiveIconCoffee { get; init; }
    public required string ActiveIconEye { get; init; }
    public required string ActiveIconMoon { get; init; }
    public required string ActiveIconLight { get; init; }
    public required string IconTintLabel { get; init; }
    public required string TintOrange { get; init; }
    public required string TintGreen { get; init; }
    public required string TintBlue { get; init; }
    public required string TintPurple { get; init; }
    public required string TintPink { get; init; }
    public required string TintNone { get; init; }

    // Global shortcut
    public required string GlobalHotkeySection { get; init; }
    public required string HotkeyToggle { get; init; }
    public required string HotkeyCaption { get; init; }
    public required string ShortcutUnavailable { get; init; }

    // Notifications
    public required string SessionEndedTitle { get; init; }
    public required string SessionEndedBody { get; init; }
    public required string BatteryTitle { get; init; }
    public required string BatteryBody { get; init; }

    public static KeepAwakeStrings For(AppLanguage language) => language switch
    {
        // Translations pending: every language resolves to English for now (see Strings.Aliases.cs).
        _ => EnUS,
    };

    public static KeepAwakeStrings EnUS { get; } = new()
    {
        EndsIn = "Ends in",
        UntilDisabled = "Active until you turn it off",
        NormalRules = "The PC follows its normal power plan",
        Options = "Options",
        Duration = "Duration",
        Indefinite = "Indefinite",
        ExtendFormat = "+{0} min",

        AllowDisplaySleep = "Allow the display to sleep",
        AllowDisplaySleepCaption = "Keeps the PC awake while the display follows its normal sleep timer.",
        AutoStart = $"Keep Awake when {AppInfo.Name} opens",
        AutoStartCaption = "Starts a session with the default duration.",
        SessionSection = "Session",
        DefaultDuration = "Default duration",
        RightClickToggle = "Right-click the tray icon to toggle Keep Awake",
        RightClickToggleCaption = "Replaces the right-click context menu.",

        AutomationSection = "Automation",
        AutomationCaption = "Starts when any selected condition is active.",
        AutomationOff = "Off",
        ExternalDisplayToggle = "External display",
        ExternalDisplayActive = "Active while an external display is connected",
        PowerToggle = "Power",
        PowerActive = "Active while connected to power",
        RunningAppsActive = "Active while a selected app is running",
        AutomationActive = "Active because an automatic condition is met",
        PauseWhenLocked = "Pause while the PC is locked",
        PauseWhenLockedCaption = "Follows normal sleep rules while locked and resumes the remaining session after you unlock.",

        BatteryProtectionSection = "Battery protection",
        BatteryDisableBelow = "Disable when battery drops below",
        BatteryNever = "Never",
        BatteryProtectionCaption = "Keeps a forgotten session from draining the laptop battery.",

        ActiveIconLabel = "Active icon",
        ActiveIconBrand = AppInfo.Name,
        ActiveIconCoffee = "Coffee",
        ActiveIconEye = "Eye",
        ActiveIconMoon = "Moon",
        ActiveIconLight = "Lightbulb",
        IconTintLabel = "Active icon color",
        TintOrange = "Orange",
        TintGreen = "Green",
        TintBlue = "Blue",
        TintPurple = "Purple",
        TintPink = "Pink",
        TintNone = "No color",

        GlobalHotkeySection = "Global shortcut",
        HotkeyToggle = "Enable shortcut for \"Keep awake\"",
        HotkeyCaption = "Works in any app, no extra permissions.",
        ShortcutUnavailable = "Windows rejected this shortcut. Another app may be using it.",

        SessionEndedTitle = "Session ended",
        SessionEndedBody = "Time is up. The PC will sleep normally again.",
        BatteryTitle = $"{AppInfo.Name} disabled",
        BatteryBody = "Low battery. Normal sleep was restored to protect the charge.",
    };
}
