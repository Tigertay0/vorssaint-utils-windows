using Faqra.Core.Defaults;
using Faqra.Core.Shortcuts;

namespace Faqra.Core.Tests;

// The storage grammar of Sources/Vorssaint/Core/GlobalShortcut.swift with Windows virtual-key codes:
// command is the Windows key, option is Alt.
public class GlobalShortcutTests
{
    [Fact]
    public void Parse_KeepAwakeDefault()
    {
        var shortcut = GlobalShortcut.Parse("control+option+command:75");
        Assert.NotNull(shortcut);
        Assert.Equal(75, shortcut.Value.VirtualKey);
        Assert.Equal(ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Win, shortcut.Value.Modifiers);
    }

    [Theory]
    [InlineData("option:32", ShortcutModifiers.Alt, 32)]
    [InlineData("command+shift+control:112", ShortcutModifiers.Win | ShortcutModifiers.Shift | ShortcutModifiers.Control, 112)]
    [InlineData("shift+option:65", ShortcutModifiers.Shift | ShortcutModifiers.Alt, 65)]
    public void Parse_AnyModifierOrder(string raw, ShortcutModifiers modifiers, int key)
    {
        var shortcut = GlobalShortcut.Parse(raw)!.Value;
        Assert.Equal(modifiers, shortcut.Modifiers);
        Assert.Equal(key, shortcut.VirtualKey);
    }

    // GlobalShortcut.swift:95-111, 254-257: a stored shortcut needs Control, Option or Command, unless
    // the key is a bare function key.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("control+option")]
    [InlineData(":75")]
    [InlineData("shift:65")]
    [InlineData("control:")]
    [InlineData("control:abc")]
    [InlineData("control:-5")]
    [InlineData("control:0")]
    [InlineData("control:256")]
    [InlineData("hyper:75")]
    [InlineData("Control:75")]
    [InlineData("control+option+command:75:99")]
    public void Parse_RejectsMalformedOrInvalid(string? raw) => Assert.Null(GlobalShortcut.Parse(raw));

    [Theory]
    [InlineData(":112")]
    [InlineData(":135")]
    [InlineData("shift:113")]
    public void Parse_AcceptsBareFunctionKeys(string raw) => Assert.NotNull(GlobalShortcut.Parse(raw));

    [Fact]
    public void StorageValue_RoundTrips()
    {
        var shortcut = new GlobalShortcut(75, ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Win);
        Assert.Equal("control+option+command:75", shortcut.StorageValue);
        Assert.Equal(shortcut, GlobalShortcut.Parse(shortcut.StorageValue));
    }

    [Fact]
    public void StorageValue_BareFunctionKeyHasLeadingColon() =>
        Assert.Equal(":113", new GlobalShortcut(113, ShortcutModifiers.None).StorageValue);

    [Theory]
    [InlineData("control+option+command:75", "Ctrl+Alt+Win+K")]
    [InlineData("option:32", "Alt+Space")]
    [InlineData("control+shift:112", "Ctrl+Shift+F1")]
    [InlineData("control+option:48", "Ctrl+Alt+0")]
    [InlineData("control+option:190", "Ctrl+Alt+.")]
    [InlineData("control+option:36", "Ctrl+Alt+Home")]
    public void DisplayText_UsesWindowsNames(string raw, string expected) =>
        Assert.Equal(expected, GlobalShortcut.Parse(raw)!.Value.DisplayText);

    [Theory]
    [InlineData(65, ShortcutModifiers.Control, true)]
    [InlineData(65, ShortcutModifiers.Shift, false)]
    [InlineData(65, ShortcutModifiers.None, false)]
    [InlineData(112, ShortcutModifiers.None, true)]
    [InlineData(0x10, ShortcutModifiers.Control, false)] // Shift itself is not a key
    [InlineData(0x5B, ShortcutModifiers.Control, false)] // left Windows key
    public void IsValid(int key, ShortcutModifiers modifiers, bool expected) =>
        Assert.Equal(expected, new GlobalShortcut(key, modifiers).IsValid);

    // The registered defaults were copied with macOS key codes (kVK_Space = 49, kVK_ANSI_S = 1, kVK_ANSI_M = 46).
    [Theory]
    [InlineData(DefaultsKey.KeepAwakeShortcut, "control+option+command:75")]
    [InlineData(DefaultsKey.CommandBarShortcut, "option:32")]
    [InlineData(DefaultsKey.SoundOutputSwitcherShortcut, "control+option+command:83")]
    [InlineData(DefaultsKey.MicMuteShortcut, "control+option+command:77")]
    public void RegisteredDefaults_UseWindowsKeys(string key, string expected) =>
        Assert.Equal(expected, DefaultsStore.InMemory().String(key));
}
