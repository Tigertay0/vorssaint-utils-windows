using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Shortcuts;
using Faqra.Services.Shortcuts;
using Faqra.Win32.Windows;

namespace Faqra.Services.Tests;

public class HotKeyRegistryTests
{
    private sealed class FakeHost : IHotKeyHost
    {
        public readonly Dictionary<int, (uint Modifiers, uint Key)> Registered = new();
        public readonly HashSet<(uint Modifiers, uint Key)> TakenByOtherApps = new();

        public event Action<int>? HotKeyPressed;

        public bool RegisterHotKey(int id, uint modifiers, uint virtualKey)
        {
            if (Registered.ContainsKey(id) || TakenByOtherApps.Contains((modifiers, virtualKey))
                || Registered.Values.Contains((modifiers, virtualKey)))
            {
                return false;
            }
            Registered[id] = (modifiers, virtualKey);
            return true;
        }

        public void UnregisterHotKey(int id) => Registered.Remove(id);

        public void Press(int id) => HotKeyPressed?.Invoke(id);

        public int? IdFor(GlobalShortcut shortcut) =>
            Registered.Where(r => r.Value == ((uint)shortcut.Modifiers, (uint)shortcut.VirtualKey)).Select(r => (int?)r.Key).FirstOrDefault();
    }

    private static (HotKeyRegistry Registry, FakeHost Host, DefaultsStore Store) Create(Func<AppFeature, bool>? installed = null)
    {
        var store = DefaultsStore.InMemory();
        var host = new FakeHost();
        var registry = new HotKeyRegistry(store, installed ?? (_ => true), host);
        return (registry, host, store);
    }

    [Fact]
    public void KeepAwakeKeepsHotKeyIdOne()
    {
        var (registry, host, _) = Create();
        var toggles = 0;
        registry.Bind(GlobalShortcutRole.KeepAwake, () => toggles++);
        registry.Sync();

        Assert.Equal(1, host.IdFor(GlobalShortcut.KeepAwakeDefault));
        host.Press(1);
        Assert.Equal(1, toggles);
    }

    [Fact]
    public void RegistersOnlyActiveRolesOfInstalledFeatures()
    {
        var (registry, host, store) = Create(f => f != AppFeature.SoundOutputSwitcher);
        foreach (var role in Enum.GetValues<GlobalShortcutRole>())
        {
            registry.Bind(role, () => { });
        }
        store.Set(DefaultsKey.CommandBarShortcutEnabled, false);
        store.Set(DefaultsKey.SoundOutputSwitcherEnabled, true);
        registry.Sync();

        Assert.Single(host.Registered);
        Assert.NotNull(host.IdFor(GlobalShortcut.KeepAwakeDefault));
    }

    [Fact]
    public void UnboundRolesAreNeverRegistered()
    {
        var (registry, host, store) = Create();
        store.Set(DefaultsKey.CommandBarShortcutEnabled, true);
        registry.Sync();
        Assert.Empty(host.Registered);
    }

    [Fact]
    public void ReportsFailureWhenAnotherAppOwnsTheShortcut()
    {
        var (registry, host, _) = Create();
        var k = GlobalShortcut.KeepAwakeDefault;
        host.TakenByOtherApps.Add(((uint)k.Modifiers, (uint)k.VirtualKey));
        registry.Bind(GlobalShortcutRole.KeepAwake, () => { });
        var changes = 0;
        registry.Changed += () => changes++;
        registry.Sync();

        Assert.True(registry.State(GlobalShortcutRole.KeepAwake).Failed);
        Assert.Null(registry.State(GlobalShortcutRole.KeepAwake).Registered);
        Assert.True(changes > 0);
    }

    // Design doc B.8: Alt+Space falls back to Ctrl+Alt+Space when PowerToys Run or similar holds it.
    [Fact]
    public void CommandBarFallsBackToCtrlAltSpace()
    {
        var (registry, host, store) = Create();
        var altSpace = GlobalShortcut.CommandBarDefault;
        host.TakenByOtherApps.Add(((uint)altSpace.Modifiers, (uint)altSpace.VirtualKey));
        store.Set(DefaultsKey.CommandBarShortcutEnabled, true);
        registry.Bind(GlobalShortcutRole.CommandBar, () => { });
        registry.Sync();

        var state = registry.State(GlobalShortcutRole.CommandBar);
        Assert.Equal(GlobalShortcut.CommandBarFallback, state.Registered);
        Assert.True(state.UsingFallback);
        Assert.False(state.Failed);
    }

    [Fact]
    public void CustomCommandBarShortcutHasNoFallback()
    {
        var (registry, host, store) = Create();
        var custom = new GlobalShortcut(0x4A, ShortcutModifiers.Control | ShortcutModifiers.Alt);
        host.TakenByOtherApps.Add(((uint)custom.Modifiers, (uint)custom.VirtualKey));
        store.Set(DefaultsKey.CommandBarShortcutEnabled, true);
        store.Set(DefaultsKey.CommandBarShortcut, custom.StorageValue);
        registry.Bind(GlobalShortcutRole.CommandBar, () => { });
        registry.Sync();

        Assert.True(registry.State(GlobalShortcutRole.CommandBar).Failed);
        Assert.Empty(host.Registered);
    }

    [Fact]
    public void ChangingTheStoredShortcutReRegisters()
    {
        var (registry, host, store) = Create();
        registry.Bind(GlobalShortcutRole.KeepAwake, () => { });
        registry.Sync();
        var next = new GlobalShortcut(0x4A, ShortcutModifiers.Control | ShortcutModifiers.Shift);
        store.Set(DefaultsKey.KeepAwakeShortcut, next.StorageValue);

        Assert.Equal(1, host.IdFor(next));
        Assert.Null(host.IdFor(GlobalShortcut.KeepAwakeDefault));
    }

    [Fact]
    public void DisablingTheRoleUnregisters()
    {
        var (registry, host, store) = Create();
        registry.Bind(GlobalShortcutRole.KeepAwake, () => { });
        registry.Sync();
        store.Set(DefaultsKey.HotkeyEnabled, false);
        Assert.Empty(host.Registered);
        Assert.False(registry.State(GlobalShortcutRole.KeepAwake).Failed);
    }

    // ShortcutCapture.swift: every shortcut goes quiet while a recorder listens, then comes back.
    [Fact]
    public void SuspendAndResume()
    {
        var (registry, host, store) = Create();
        var presses = 0;
        registry.Bind(GlobalShortcutRole.KeepAwake, () => presses++);
        registry.Sync();

        registry.Suspend();
        Assert.Empty(host.Registered);
        host.Press(1);
        Assert.Equal(0, presses);

        // A shortcut saved while suspended is picked up on resume, not registered early.
        var next = new GlobalShortcut(0x4A, ShortcutModifiers.Control | ShortcutModifiers.Shift);
        store.Set(DefaultsKey.KeepAwakeShortcut, next.StorageValue);
        Assert.Empty(host.Registered);

        registry.Resume();
        Assert.Equal(1, host.IdFor(next));
        host.Press(1);
        Assert.Equal(1, presses);
    }

    [Fact]
    public void SuspendIsCounted()
    {
        var (registry, host, _) = Create();
        registry.Bind(GlobalShortcutRole.KeepAwake, () => { });
        registry.Sync();
        registry.Suspend();
        registry.Suspend();
        registry.Resume();
        Assert.Empty(host.Registered);
        registry.Resume();
        Assert.Single(host.Registered);
    }

    [Fact]
    public void DisposeUnregistersEverything()
    {
        var (registry, host, store) = Create();
        store.Set(DefaultsKey.CommandBarShortcutEnabled, true);
        registry.Bind(GlobalShortcutRole.KeepAwake, () => { });
        registry.Bind(GlobalShortcutRole.CommandBar, () => { });
        registry.Sync();
        Assert.Equal(2, host.Registered.Count);
        registry.Dispose();
        Assert.Empty(host.Registered);
    }
}
