using Faqra.Core.Localization;
using Faqra.Core.Tray;

namespace Faqra.Core.Tests;

public class ContextMenuBuilderTests
{
    private static readonly Strings S = Strings.EnUS;

    private static string[] Titles(IReadOnlyList<TrayMenuItem> items) =>
        items.Select(i => i.IsSeparator ? "---" : i.Title).ToArray();

    [Fact]
    public void KeepAwakeInactive_ProducesUpstreamOrder()
    {
        var items = ContextMenuBuilder.Build(new TrayMenuState(KeepAwakeAvailable: true, KeepAwakeActive: false), S);

        Assert.Equal(
            ["Enable keep awake", "Activate for…", "---", "Settings…", "About Faqra", "Check for updates…", "---", "Quit Faqra"],
            Titles(items));
    }

    [Fact]
    public void KeepAwakeActive_ShowsDisableAndNoDurations()
    {
        var items = ContextMenuBuilder.Build(new TrayMenuState(KeepAwakeAvailable: true, KeepAwakeActive: true), S);

        Assert.Equal("Disable keep awake", items[0].Title);
        Assert.DoesNotContain(items, i => i.Title == "Activate for…");
    }

    [Fact]
    public void KeepAwakeUnavailable_StartsWithSettingsAndNoLeadingSeparator()
    {
        var items = ContextMenuBuilder.Build(new TrayMenuState(KeepAwakeAvailable: false, KeepAwakeActive: false), S);

        Assert.Equal(["Settings…", "About Faqra", "Check for updates…", "---", "Quit Faqra"], Titles(items));
    }

    [Fact]
    public void Durations_MatchUpstreamPresets()
    {
        var items = ContextMenuBuilder.Build(new TrayMenuState(KeepAwakeAvailable: true, KeepAwakeActive: false), S);
        var submenu = items.Single(i => i.Title == "Activate for…").Children!;

        Assert.Equal([15, 30, 60, 120, 240, 480, 0], submenu.Select(i => i.Minutes));
        Assert.Equal(["15 minutes", "30 minutes", "1 hour", "2 hours", "4 hours", "8 hours", "Indefinitely"], Titles(submenu));
        Assert.All(submenu, i => Assert.Equal(TrayMenuAction.ActivateKeepAwake, i.Action));
    }

    [Fact]
    public void OptionalItems_AppearOnlyWhenAvailable()
    {
        var state = new TrayMenuState(
            KeepAwakeAvailable: false, KeepAwakeActive: false,
            CleaningModeAvailable: true, UninstallerAvailable: true, ShelfAvailable: true, ShelfEnabled: true);

        var items = ContextMenuBuilder.Build(state, S);

        Assert.Equal(
            ["Cleaning Mode", "---", "Settings…", "About Faqra", "Uninstall an app…", "Open shelf", "Check for updates…", "---", "Quit Faqra"],
            Titles(items));
    }

    [Fact]
    public void Shelf_RequiresBothAvailableAndEnabled()
    {
        var state = new TrayMenuState(KeepAwakeAvailable: false, KeepAwakeActive: false, ShelfAvailable: true, ShelfEnabled: false);

        var items = ContextMenuBuilder.Build(state, S);

        Assert.DoesNotContain(items, i => i.Action == TrayMenuAction.Shelf);
    }

    [Fact]
    public void ActionsAreWiredToTheExpectedRows()
    {
        var items = ContextMenuBuilder.Build(new TrayMenuState(KeepAwakeAvailable: true, KeepAwakeActive: false), S);

        Assert.Equal(TrayMenuAction.ToggleKeepAwake, items[0].Action);
        Assert.Equal(TrayMenuAction.OpenSettings, items.Single(i => i.Title == "Settings…").Action);
        Assert.Equal(TrayMenuAction.About, items.Single(i => i.Title == "About Faqra").Action);
        Assert.Equal(TrayMenuAction.CheckUpdates, items.Single(i => i.Title == "Check for updates…").Action);
        Assert.Equal(TrayMenuAction.Quit, items[^1].Action);
    }
}
