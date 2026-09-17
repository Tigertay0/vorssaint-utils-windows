using Faqra.Core.CommandBar;
using Faqra.Core.Defaults;

namespace Faqra.Core.Tests;

// Tests/MetricsTests.swift:25413-25433, 25732-25743 upstream table, per
// digest-commandbar-logic.md section 2.
public class CommandBarUsageTests
{
    [Fact]
    public void Boost_RecentUseScoresHigherThanOldUseAtEqualCount()
    {
        var now = 1_000_000d;
        var recent = new CommandBarUse(5, now - 60);
        var monthOld = new CommandBarUse(5, now - 30 * 86400);

        Assert.True(CommandBarUsage.Boost(recent, now) > CommandBarUsage.Boost(monthOld, now));
    }

    [Fact]
    public void Boost_CountCapsAtFortyEvenWhenStoredCountIsHigher()
    {
        var now = 1_000_000d;
        var use = new CommandBarUse(999, now - 60);

        // min(999, 40) * 12 = 480, well under the 700 an uncapped count would imply.
        Assert.Equal(480, CommandBarUsage.Boost(use, now));
        Assert.True(CommandBarUsage.Boost(use, now) < 700);
    }

    [Fact]
    public void Boost_NullUseIsZero() => Assert.Equal(0, CommandBarUsage.Boost(null, 0));

    [Fact]
    public void Recording_IncrementsCountAndStampsLastUsed()
    {
        var usage = new Dictionary<string, CommandBarUse>();
        var next = CommandBarUsage.Recording(usage, "app.chrome", 100);
        next = CommandBarUsage.Recording(next, "app.chrome", 200);

        Assert.Equal(2, next["app.chrome"].Count);
        Assert.Equal(200, next["app.chrome"].LastUsed);
    }

    [Fact]
    public void Recording_CountCapsAt999()
    {
        IReadOnlyDictionary<string, CommandBarUse> usage =
            new Dictionary<string, CommandBarUse> { ["x"] = new CommandBarUse(999, 1) };

        var next = CommandBarUsage.Recording(usage, "x", 2);

        Assert.Equal(999, next["x"].Count);
    }

    [Fact]
    public void Recording_EvictsOldestByLastUsedPastTheStoredIdLimit()
    {
        IReadOnlyDictionary<string, CommandBarUse> usage = new Dictionary<string, CommandBarUse>();
        for (var i = 0; i < CommandBarUsage.StoredIdLimit; i++)
        {
            usage = CommandBarUsage.Recording(usage, $"id{i}", i);
        }

        // One more entry, recorded most recently, should evict id0 (the oldest lastUsed).
        var next = CommandBarUsage.Recording(usage, "newcomer", CommandBarUsage.StoredIdLimit);

        Assert.Equal(CommandBarUsage.StoredIdLimit, next.Count);
        Assert.False(next.ContainsKey("id0"));
        Assert.True(next.ContainsKey("newcomer"));
    }

    [Fact]
    public void EncodeDecode_RoundTrips()
    {
        IReadOnlyDictionary<string, CommandBarUse> usage =
            new Dictionary<string, CommandBarUse> { ["app.chrome"] = new CommandBarUse(3, 42.5) };

        var raw = CommandBarUsage.Encode(usage);
        var decoded = CommandBarUsage.Decode(raw);

        Assert.Equal(usage["app.chrome"], decoded["app.chrome"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    public void Decode_CorruptOrMissingRawFallsBackToEmpty(string? raw) =>
        Assert.Empty(CommandBarUsage.Decode(raw));

    [Fact]
    public void LoadSave_RoundTripsThroughAnISettingsStore()
    {
        var store = DefaultsStore.InMemory();
        IReadOnlyDictionary<string, CommandBarUse> usage =
            new Dictionary<string, CommandBarUse> { ["app.chrome"] = new CommandBarUse(4, 10) };

        CommandBarUsage.Save(store, usage);
        var loaded = CommandBarUsage.Load(store);

        Assert.Equal(4, loaded["app.chrome"].Count);
        // Saved under the exact upstream-mirroring key so a future settings/backup UI keyed on it works.
        Assert.Equal("commandBarUsage", DefaultsKey.CommandBarUsage);
        Assert.Equal("commandBarUsage", store.Keys.Single());
    }

    [Fact]
    public void SuggestionIds_UsedFirstThenCuratedFillsRemainingSlots()
    {
        IReadOnlyDictionary<string, CommandBarUse> usage = new Dictionary<string, CommandBarUse>
        {
            ["screenshot"] = new CommandBarUse(2, 0),
            ["darkMode"] = new CommandBarUse(1, 0),
        };
        var available = new[] { "screenshot", "darkMode", "colorPicker", "ocr" };
        var curated = new[] { "colorPicker", "gone", "ocr" };

        var result = CommandBarUsage.SuggestionIds(usage, available, curated, 3);

        Assert.Equal(new[] { "screenshot", "darkMode", "colorPicker" }, result);
    }

    [Fact]
    public void SuggestionIds_EmptyUsageFallsBackToCuratedOrderFilteredByAvailability()
    {
        var result = CommandBarUsage.SuggestionIds(
            usage: new Dictionary<string, CommandBarUse>(),
            available: new[] { "a", "b" },
            curated: new[] { "c", "b", "a" },
            limit: 5);

        Assert.Equal(new[] { "b", "a" }, result);
    }

    [Fact]
    public void CategoryIds_UsedRowsLeadUnusedRowsKeepCatalogOrder()
    {
        IReadOnlyDictionary<string, CommandBarUse> usage = new Dictionary<string, CommandBarUse>
        {
            ["emoji.heart"] = new CommandBarUse(3, 20),
            ["emoji.fire"] = new CommandBarUse(3, 10),
            ["emoji.wave"] = new CommandBarUse(1, 5),
        };
        var available = new[] { "emoji.heart", "emoji.fire", "emoji.wave", "emoji.grin", "emoji.star" };

        var result = CommandBarUsage.CategoryIds(usage, available);

        Assert.Equal(
            new[] { "emoji.heart", "emoji.fire", "emoji.wave", "emoji.grin", "emoji.star" },
            result);
    }

    [Fact]
    public void CategoryIds_AllUnusedKeepsOriginalOrder()
    {
        var available = new[] { "a", "b", "c" };
        Assert.Equal(available, CommandBarUsage.CategoryIds(new Dictionary<string, CommandBarUse>(), available));
    }
}
