// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors GlobalShortcutRole in Sources/Vorssaint/Core/GlobalShortcut.swift (lines 687-1003). Only the
// roles whose features exist on Windows in Stage 1; the others join as their features are ported.

using Faqra.Core.Defaults;
using Faqra.Core.Features;

namespace Faqra.Core.Shortcuts;

public enum GlobalShortcutRole
{
    KeepAwake,
    SoundOutputSwitcher,
    CommandBar,
}

public static class GlobalShortcutRoleExtensions
{
    public static string StorageKey(this GlobalShortcutRole role) => role switch
    {
        GlobalShortcutRole.KeepAwake => DefaultsKey.KeepAwakeShortcut,
        GlobalShortcutRole.SoundOutputSwitcher => DefaultsKey.SoundOutputSwitcherShortcut,
        GlobalShortcutRole.CommandBar => DefaultsKey.CommandBarShortcut,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    public static GlobalShortcut DefaultShortcut(this GlobalShortcutRole role) => role switch
    {
        GlobalShortcutRole.KeepAwake => GlobalShortcut.KeepAwakeDefault,
        GlobalShortcutRole.SoundOutputSwitcher => GlobalShortcut.SoundOutputSwitcherDefault,
        GlobalShortcutRole.CommandBar => GlobalShortcut.CommandBarDefault,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    public static AppFeature Feature(this GlobalShortcutRole role) => role switch
    {
        GlobalShortcutRole.KeepAwake => AppFeature.KeepAwake,
        GlobalShortcutRole.SoundOutputSwitcher => AppFeature.SoundOutputSwitcher,
        GlobalShortcutRole.CommandBar => AppFeature.CommandBar,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    /// <summary>Every key must be on for the shortcut to run.</summary>
    public static IReadOnlyList<string> RequiredEnableKeys(this GlobalShortcutRole role) => role switch
    {
        GlobalShortcutRole.KeepAwake => [DefaultsKey.HotkeyEnabled],
        GlobalShortcutRole.SoundOutputSwitcher => [DefaultsKey.SoundOutputSwitcherEnabled],
        GlobalShortcutRole.CommandBar => [DefaultsKey.CommandBarShortcutEnabled],
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    /// <summary>The shortcut in effect: the stored one, or the default when the stored text is not valid.</summary>
    public static GlobalShortcut Saved(this GlobalShortcutRole role, ISettingsStore store) =>
        GlobalShortcut.Parse(store.String(role.StorageKey())) ?? role.DefaultShortcut();

    public static bool IsActive(this GlobalShortcutRole role, ISettingsStore store) =>
        role.RequiredEnableKeys().All(store.Bool);

    /// <summary>
    /// Another role of an installed feature already using the shortcut; inactive roles count when
    /// <paramref name="includeInactive"/> (the Shortcuts page passes true so a switched-off shortcut still blocks reuse).
    /// </summary>
    public static GlobalShortcutRole? Conflict(this GlobalShortcutRole role, GlobalShortcut shortcut, ISettingsStore store,
        Func<AppFeature, bool> isInstalled, bool includeInactive)
    {
        foreach (var other in Enum.GetValues<GlobalShortcutRole>())
        {
            if (other != role
                && isInstalled(other.Feature())
                && (includeInactive || other.IsActive(store))
                && other.Saved(store) == shortcut)
            {
                return other;
            }
        }
        return null;
    }
}
