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
    [InlineData("shift:65", ShortcutModifiers.Shift, 65)]
    [InlineData("option:32", ShortcutModifiers.Alt, 32)]
    [InlineData("command+shift+control:112", ShortcutModifiers.Win | ShortcutModifiers.Shift | ShortcutModifiers.Control, 112)]
    public void Parse_AnyModifierOrder(string raw, ShortcutModifiers modifiers, int key)
    {
        var shortcut = GlobalShortcut.Parse(raw)!.Value;
        Assert.Equal(modifiers, shortcut.Modifiers);
        Assert.Equal(key, shortcut.VirtualKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("control+option")]
    [InlineData(":75")]
    [InlineData("control:")]
    [InlineData("control:abc")]
    [InlineData("control:0")]
    [InlineData("control:256")]
    [InlineData("hyper:75")]
    public void Parse_RejectsMalformed(string? raw) => Assert.Null(GlobalShortcut.Parse(raw));

    [Fact]
    public void StorageValue_RoundTrips()
    {
        var shortcut = new GlobalShortcut(75, ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Win);
        Assert.Equal("control+option+command:75", shortcut.StorageValue);
        Assert.Equal(shortcut, GlobalShortcut.Parse(shortcut.StorageValue));
    }

    [Theory]
    [InlineData("control+option+command:75", "Ctrl+Alt+Win+K")]
    [InlineData("option:32", "Alt+Space")]
    [InlineData("control+shift:112", "Ctrl+Shift+F1")]
    [InlineData("control+option:48", "Ctrl+Alt+0")]
    public void DisplayText_UsesWindowsNames(string raw, string expected) =>
        Assert.Equal(expected, GlobalShortcut.Parse(raw)!.Value.DisplayText);
}
