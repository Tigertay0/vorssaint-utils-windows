// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/GlobalShortcut.swift (GlobalShortcutModifiers, GlobalShortcut storage
// grammar "modifier+modifier:keyCode", isValid). The key code is a Windows virtual-key code; "command"
// is the Windows key and "option" is Alt. Key caps use Windows' "Ctrl+Alt+K" convention instead of
// macOS glyphs, and a static VK table replaces upstream's keyboard-layout lookup.

using System.Globalization;

namespace Faqra.Core.Shortcuts;

/// <summary>Values match RegisterHotKey's MOD_ALT, MOD_CONTROL, MOD_SHIFT and MOD_WIN.</summary>
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
    private const int FirstFunctionKey = 0x70; // VK_F1
    private const int LastFunctionKey = 0x87;  // VK_F24
    private const int MaxVirtualKey = 0xFE;

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

    /// <summary>Alt+Space: upstream's ⌥Space.</summary>
    public static readonly GlobalShortcut CommandBarDefault = new(0x20, ShortcutModifiers.Alt);

    /// <summary>Ctrl+Alt+Space: the fallback when another app (PowerToys Run) already owns Alt+Space.</summary>
    public static readonly GlobalShortcut CommandBarFallback = new(0x20, ShortcutModifiers.Control | ShortcutModifiers.Alt);

    /// <summary>Ctrl+Alt+Win+S: upstream's ⌃⌥⌘S.</summary>
    public static readonly GlobalShortcut SoundOutputSwitcherDefault = new(0x53, ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Win);

    /// <summary>Control, Alt or Win; Shift alone never counts (GlobalShortcut.swift:23-25).</summary>
    public bool HasPrimaryModifier => (Modifiers & (ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Win)) != 0;

    public bool IsFunctionKey => VirtualKey is >= FirstFunctionKey and <= LastFunctionKey;

    /// <summary>The key has a cap to show and is not itself a modifier.</summary>
    public bool HasPrintableKey => KeyName(VirtualKey) is not null;

    /// <summary>A usable global shortcut: a real key with Ctrl, Alt or Win, or a bare function key.</summary>
    public bool IsValid => HasPrintableKey && (HasPrimaryModifier || IsFunctionKey);

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
            parts.Add(KeyName(VirtualKey) ?? $"0x{VirtualKey:X2}");
            return string.Join('+', parts);
        }
    }

    /// <summary>A stored shortcut, or null when the text is not a valid one (GlobalShortcut.swift:95-111).</summary>
    public static GlobalShortcut? Parse(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }
        var colon = raw.IndexOf(':');
        if (colon < 0
            || !int.TryParse(raw.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var key)
            || key is < 1 or > MaxVirtualKey)
        {
            return null;
        }
        var modifiers = ShortcutModifiers.None;
        if (colon > 0)
        {
            foreach (var name in raw[..colon].Split('+'))
            {
                var match = ModifierNames.FirstOrDefault(m => m.Name == name);
                if (match.Name is null)
                {
                    return null;
                }
                modifiers |= match.Flag;
            }
        }
        var shortcut = new GlobalShortcut(key, modifiers);
        return shortcut.IsValid ? shortcut : null;
    }

    /// <summary>True for Shift, Ctrl, Alt and the Windows keys, which never end a recording on their own.</summary>
    public static bool IsModifierKey(int vk) => vk is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or (>= 0xA0 and <= 0xA5);

    /// <summary>The key cap label, or null for modifiers and keys with no cap.</summary>
    public static string? KeyName(int vk) => vk switch
    {
        >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= FirstFunctionKey and <= LastFunctionKey => $"F{vk - 0x6F}",
        >= 0x60 and <= 0x69 => $"Num {vk - 0x60}",
        0x20 => "Space",
        0x0D => "Enter",
        0x1B => "Esc",
        0x09 => "Tab",
        0x08 => "Backspace",
        0x2E => "Delete",
        0x2D => "Insert",
        0x24 => "Home",
        0x23 => "End",
        0x21 => "Page Up",
        0x22 => "Page Down",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x13 => "Pause",
        0x2C => "Print Screen",
        0x6A => "Num *",
        0x6B => "Num +",
        0x6D => "Num -",
        0x6E => "Num .",
        0x6F => "Num /",
        0xBA => ";",
        0xBB => "=",
        0xBC => ",",
        0xBD => "-",
        0xBE => ".",
        0xBF => "/",
        0xC0 => "`",
        0xDB => "[",
        0xDC => "\\",
        0xDD => "]",
        0xDE => "'",
        _ => null,
    };
}
