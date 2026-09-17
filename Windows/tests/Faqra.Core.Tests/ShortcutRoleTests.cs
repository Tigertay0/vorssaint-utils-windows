using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Shortcuts;

namespace Faqra.Core.Tests;

// GlobalShortcutRole (Sources/Vorssaint/Core/GlobalShortcut.swift:687-1003), limited to the roles whose
// features exist in Stage 1.
public class ShortcutRoleTests
{
    private static readonly GlobalShortcut CtrlAltWinK = new(75, ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Win);
    private static readonly GlobalShortcut AltSpace = new(32, ShortcutModifiers.Alt);

    [Fact]
    public void Roles_KeysDefaultsAndFeatures()
    {
        Assert.Equal(DefaultsKey.KeepAwakeShortcut, GlobalShortcutRole.KeepAwake.StorageKey());
        Assert.Equal(CtrlAltWinK, GlobalShortcutRole.KeepAwake.DefaultShortcut());
        Assert.Equal(AppFeature.KeepAwake, GlobalShortcutRole.KeepAwake.Feature());
        Assert.Equal([DefaultsKey.HotkeyEnabled], GlobalShortcutRole.KeepAwake.RequiredEnableKeys());

        Assert.Equal(DefaultsKey.CommandBarShortcut, GlobalShortcutRole.CommandBar.StorageKey());
        Assert.Equal(AltSpace, GlobalShortcutRole.CommandBar.DefaultShortcut());
        Assert.Equal(AppFeature.CommandBar, GlobalShortcutRole.CommandBar.Feature());
        Assert.Equal([DefaultsKey.CommandBarShortcutEnabled], GlobalShortcutRole.CommandBar.RequiredEnableKeys());

        Assert.Equal(AppFeature.SoundOutputSwitcher, GlobalShortcutRole.SoundOutputSwitcher.Feature());
        Assert.Equal([DefaultsKey.SoundOutputSwitcherEnabled], GlobalShortcutRole.SoundOutputSwitcher.RequiredEnableKeys());
    }

    [Fact]
    public void DefaultShortcut_MatchesRegisteredDefault()
    {
        var store = DefaultsStore.InMemory();
        foreach (var role in Enum.GetValues<GlobalShortcutRole>())
        {
            Assert.Equal(role.DefaultShortcut().StorageValue, store.String(role.StorageKey()));
        }
    }

    [Fact]
    public void Saved_FallsBackToDefaultWhenInvalid()
    {
        var store = DefaultsStore.InMemory();
        store.Set(DefaultsKey.KeepAwakeShortcut, "shift:65");
        Assert.Equal(CtrlAltWinK, GlobalShortcutRole.KeepAwake.Saved(store));
        store.Set(DefaultsKey.KeepAwakeShortcut, "control+shift:74");
        Assert.Equal(new GlobalShortcut(74, ShortcutModifiers.Control | ShortcutModifiers.Shift), GlobalShortcutRole.KeepAwake.Saved(store));
    }

    [Fact]
    public void IsActive_RequiresEveryEnableKey()
    {
        var store = DefaultsStore.InMemory();
        store.Set(DefaultsKey.HotkeyEnabled, true);
        Assert.True(GlobalShortcutRole.KeepAwake.IsActive(store));
        store.Set(DefaultsKey.HotkeyEnabled, false);
        Assert.False(GlobalShortcutRole.KeepAwake.IsActive(store));
    }

    // GlobalShortcut.swift:842-853
    [Fact]
    public void Conflict_FindsAnotherRoleUsingTheShortcut()
    {
        var store = DefaultsStore.InMemory();
        store.Set(DefaultsKey.CommandBarShortcut, CtrlAltWinK.StorageValue);
        store.Set(DefaultsKey.CommandBarShortcutEnabled, true);
        bool Installed(AppFeature f) => true;

        Assert.Equal(GlobalShortcutRole.CommandBar, GlobalShortcutRole.KeepAwake.Conflict(CtrlAltWinK, store, Installed, includeInactive: false));
        Assert.Equal(GlobalShortcutRole.KeepAwake, GlobalShortcutRole.CommandBar.Conflict(CtrlAltWinK, store, Installed, includeInactive: false));
        Assert.Null(GlobalShortcutRole.CommandBar.Conflict(AltSpace, store, Installed, includeInactive: true));
    }

    [Fact]
    public void Conflict_InactiveRolesCountOnlyWhenAsked()
    {
        var store = DefaultsStore.InMemory();
        store.Set(DefaultsKey.CommandBarShortcut, CtrlAltWinK.StorageValue);
        store.Set(DefaultsKey.CommandBarShortcutEnabled, false);
        bool Installed(AppFeature f) => true;

        Assert.Null(GlobalShortcutRole.KeepAwake.Conflict(CtrlAltWinK, store, Installed, includeInactive: false));
        Assert.Equal(GlobalShortcutRole.CommandBar, GlobalShortcutRole.KeepAwake.Conflict(CtrlAltWinK, store, Installed, includeInactive: true));
    }

    [Fact]
    public void Conflict_IgnoresRolesOfUninstalledFeatures()
    {
        var store = DefaultsStore.InMemory();
        store.Set(DefaultsKey.CommandBarShortcut, CtrlAltWinK.StorageValue);
        store.Set(DefaultsKey.CommandBarShortcutEnabled, true);
        Assert.Null(GlobalShortcutRole.KeepAwake.Conflict(CtrlAltWinK, store, f => f != AppFeature.CommandBar, includeInactive: true));
    }
}

public class WindowsReservedShortcutsTests
{
    [Theory]
    [InlineData(0x4C, ShortcutModifiers.Win)]                                          // Win+L
    [InlineData(0x2E, ShortcutModifiers.Control | ShortcutModifiers.Alt)]              // Ctrl+Alt+Delete
    [InlineData(0x09, ShortcutModifiers.Alt)]                                          // Alt+Tab
    [InlineData(0x09, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
    [InlineData(0x09, ShortcutModifiers.Win)]                                          // Win+Tab
    [InlineData(0x1B, ShortcutModifiers.Control | ShortcutModifiers.Shift)]            // Ctrl+Shift+Esc
    [InlineData(0x73, ShortcutModifiers.Alt)]                                          // Alt+F4
    public void Reserved(int key, ShortcutModifiers modifiers) =>
        Assert.True(WindowsReservedShortcuts.IsReserved(new GlobalShortcut(key, modifiers)));

    [Theory]
    [InlineData(0x4C, ShortcutModifiers.Win | ShortcutModifiers.Control)]
    [InlineData(0x20, ShortcutModifiers.Alt)]
    [InlineData(0x4B, ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Win)]
    public void NotReserved(int key, ShortcutModifiers modifiers) =>
        Assert.False(WindowsReservedShortcuts.IsReserved(new GlobalShortcut(key, modifiers)));
}

// ShortcutRecorderButton.handleRecordingKey (UI/ShortcutRecorderButton.swift:211-255) without AppKit.
public class ShortcutRecordingTests
{
    [Fact]
    public void EscapeWithoutPrimaryModifierCancels()
    {
        Assert.Equal(RecordingOutcome.Cancel, ShortcutRecording.Handle(0x1B, ShortcutModifiers.None).Outcome);
        Assert.Equal(RecordingOutcome.Cancel, ShortcutRecording.Handle(0x1B, ShortcutModifiers.Shift).Outcome);
        Assert.Equal(RecordingOutcome.Captured, ShortcutRecording.Handle(0x1B, ShortcutModifiers.Control | ShortcutModifiers.Alt).Outcome);
    }

    [Theory]
    [InlineData(0x10)] [InlineData(0x11)] [InlineData(0x12)] [InlineData(0x5B)] [InlineData(0x5C)]
    [InlineData(0xA0)] [InlineData(0xA3)] [InlineData(0xA5)]
    public void ModifierKeysWait(int key) =>
        Assert.Equal(RecordingOutcome.Waiting, ShortcutRecording.Handle(key, ShortcutModifiers.Control).Outcome);

    [Fact]
    public void BareDeleteIsIgnoredForRoles()
    {
        Assert.Equal(RecordingOutcome.Waiting, ShortcutRecording.Handle(0x2E, ShortcutModifiers.None).Outcome);
        Assert.Equal(RecordingOutcome.Waiting, ShortcutRecording.Handle(0x08, ShortcutModifiers.Shift).Outcome);
    }

    [Fact]
    public void InvalidCombinationIsReported()
    {
        Assert.Equal(RecordingOutcome.Invalid, ShortcutRecording.Handle(0x41, ShortcutModifiers.None).Outcome);
        Assert.Equal(RecordingOutcome.Invalid, ShortcutRecording.Handle(0x41, ShortcutModifiers.Shift).Outcome);
    }

    [Fact]
    public void ValidCombinationIsCaptured()
    {
        var result = ShortcutRecording.Handle(0x4A, ShortcutModifiers.Control | ShortcutModifiers.Shift);
        Assert.Equal(RecordingOutcome.Captured, result.Outcome);
        Assert.Equal(new GlobalShortcut(0x4A, ShortcutModifiers.Control | ShortcutModifiers.Shift), result.Shortcut);
    }

    [Fact]
    public void ReservedCombinationIsRejected() =>
        Assert.Equal(RecordingOutcome.Reserved, ShortcutRecording.Handle(0x4C, ShortcutModifiers.Win).Outcome);
}
