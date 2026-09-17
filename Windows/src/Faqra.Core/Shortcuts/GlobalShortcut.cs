// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the storage grammar of Sources/Vorssaint/Core/GlobalShortcut.swift ("modifier+modifier:keyCode").
// The key code is a Windows virtual-key code; "command" is the Windows key and "option" is Alt. The
// recorder, conflict table and the rest of the shortcut model arrive with milestone 6.

using System.Globalization;

namespace Faqra.Core.Shortcuts;

[Flags]
public enum ShortcutModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

public readonly record struct GlobalShortcut(int VirtualKey, ShortcutModifiers Modifiers)
{
    // Upstream's order, so a stored value written here reads the same as one written on a Mac.
    private static readonly (string Name, ShortcutModifiers Flag, string Display)[] ModifierNames =
    [
        ("control", ShortcutModifiers.Control, "Ctrl"),
        ("option", ShortcutModifiers.Alt, "Alt"),
        ("shift", ShortcutModifiers.Shift, "Shift"),
        ("command", ShortcutModifiers.Win, "Win"),
    ];

    /// <summary>Ctrl+Alt+Win+K: upstream's ⌃⌥⌘K with the Windows key in place of Command.</summary>
    public static readonly GlobalShortcut KeepAwakeDefault = new(0x4B, ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Win);

    public string StorageValue
    {
        get
        {
            var modifiers = this.Modifiers;
            var names = ModifierNames.Where(m => modifiers.HasFlag(m.Flag)).Select(m => m.Name);
            return $"{string.Join('+', names)}:{VirtualKey.ToString(CultureInfo.InvariantCulture)}";
        }
    }

    public string DisplayText
    {
        get
        {
            var modifiers = this.Modifiers;
            var parts = ModifierNames.Where(m => modifiers.HasFlag(m.Flag)).Select(m => m.Display).ToList();
            parts.Add(KeyName(VirtualKey));
            return string.Join('+', parts);
        }
    }

    /// <summary>A stored shortcut, or null when the text is not one.</summary>
    public static GlobalShortcut? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        var colon = raw.LastIndexOf(':');
        if (colon <= 0
            || !int.TryParse(raw.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var key)
            || key is < 1 or > 255)
        {
            return null;
        }
        var modifiers = ShortcutModifiers.None;
        foreach (var name in raw[..colon].Split('+'))
        {
            var match = ModifierNames.FirstOrDefault(m => m.Name == name);
            if (match.Name is null)
            {
                return null;
            }
            modifiers |= match.Flag;
        }
        return new GlobalShortcut(key, modifiers);
    }

    private static string KeyName(int vk) => vk switch
    {
        >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",
        0x20 => "Space",
        0x0D => "Enter",
        0x1B => "Esc",
        0x09 => "Tab",
        0x08 => "Backspace",
        0x2E => "Delete",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        _ => $"0x{vk:X2}",
    };
}
