using Faqra.App.Tray;
using Faqra.Core.Localization;
using Faqra.Core.Tray;

namespace Faqra.App.Tests;

public class TrayMenuAdapterTests
{
    [Fact]
    public void AssignsUniqueIdsToEveryLeafAndKeepsSeparators()
    {
        var items = ContextMenuBuilder.Build(new TrayMenuState(KeepAwakeAvailable: true, KeepAwakeActive: false), Strings.EnUS);

        var (entries, lookup) = TrayMenuAdapter.ToEntries(items, new HashSet<TrayMenuAction> { TrayMenuAction.About, TrayMenuAction.Quit });

        Assert.Equal(items.Count, entries.Count);
        Assert.Equal(2, entries.Count(e => e.IsSeparator));
        // 1 toggle + 7 durations + settings + about + updates + quit
        Assert.Equal(12, lookup.Count);
        Assert.Equal(lookup.Count, lookup.Keys.Distinct().Count());
        Assert.All(lookup.Keys, id => Assert.True(id > 0));
    }

    [Fact]
    public void GreysOutUnimplementedActionsAndParentsWithoutEnabledChildren()
    {
        var items = ContextMenuBuilder.Build(new TrayMenuState(KeepAwakeAvailable: true, KeepAwakeActive: false), Strings.EnUS);

        var (entries, lookup) = TrayMenuAdapter.ToEntries(items, new HashSet<TrayMenuAction> { TrayMenuAction.About, TrayMenuAction.Quit });

        var about = entries.Single(e => e.Title == "About Faqra");
        var quit = entries.Single(e => e.Title == "Quit Faqra");
        var settings = entries.Single(e => e.Title == "Settings…");
        var activateFor = entries.Single(e => e.Title == "Activate for…");
        var updates = entries.Single(e => e.Title == "Check for updates…");
        Assert.True(about.Enabled);
        Assert.True(quit.Enabled);
        Assert.False(settings.Enabled);
        Assert.False(updates.Enabled);
        Assert.False(activateFor.Enabled);
        Assert.All(activateFor.Children!, child => Assert.False(child.Enabled));
        Assert.Equal(TrayMenuAction.About, lookup[about.Id].Action);
        Assert.Equal(TrayMenuAction.Quit, lookup[quit.Id].Action);
    }
}
