using System.Globalization;
using Faqra.Core.CommandBar;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Localization;

namespace Faqra.Core.Tests.CommandBar;

// CommandBarCatalog.toggleEntries, answerEntry, openURLEntry and CommandBarService.suggestionRows
// (Sources/Vorssaint/Services/CommandBar/CommandBarCatalog.swift:205-268, 1478-1527; CommandBarService.swift:1153-1258).
public class CommandBarCatalogSupportTests
{
    private static readonly CommandBarStrings Bar = CommandBarStrings.EnUS;
    private static readonly FeatureHubStrings Hub = FeatureHubStrings.EnUS;
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Toggles_OneRowPerFeatureWithExactlyOneSwitch()
    {
        var store = DefaultsStore.InMemory();
        store.Set(DefaultsKey.NotchTimerEnabled, true);
        var features = new[] { AppFeature.NotchTimer, AppFeature.Notch, AppFeature.ScrollInverter, AppFeature.KeepAwake };

        var toggles = CommandBarCatalogSupport.Toggles(features, store, Bar, Hub);

        Assert.Equal(["toggle.notchTimer", "toggle.notch"], toggles.Select(t => t.Entry.Id));
        var timer = toggles[0];
        Assert.True(timer.IsOn);
        Assert.Equal(DefaultsKey.NotchTimerEnabled, timer.Key);
        Assert.Equal(string.Format(Bar.TurnOffFormat, Hub.FeatureTitles[AppFeature.NotchTimer]), timer.Entry.Title);
        Assert.Equal(Hub.GroupTitles[AppFeature.NotchTimer.Group()], timer.Entry.Subtitle);
        Assert.Equal(CommandBarEntryKind.Toggle, timer.Entry.Kind);
    }

    [Theory]
    [InlineData("2^10", "math.result", "1,024")]
    [InlineData("10 km in mi", "units.result", null)]
    [InlineData("next friday", "date.result", null)]
    public void Answer_TriesMathThenUnitsThenDates(string query, string id, string? title)
    {
        var answer = CommandBarCatalogSupport.Answer(query, Now, TimeZoneInfo.Utc, CultureInfo.GetCultureInfo("en-US"), Bar);
        Assert.NotNull(answer);
        Assert.Equal(id, answer.Entry.Id);
        Assert.False(answer.Entry.CountsUsage);
        Assert.Equal(answer.Value, answer.Entry.Title);
        if (title is not null)
        {
            Assert.Equal(title, answer.Value);
        }
    }

    [Theory]
    [InlineData("chr")]
    [InlineData("3")]
    [InlineData("")]
    public void Answer_NullForOrdinaryQueries(string query) =>
        Assert.Null(CommandBarCatalogSupport.Answer(query, Now, TimeZoneInfo.Utc, CultureInfo.GetCultureInfo("en-US"), Bar));

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("  http://example.com/a?b=c  ", true)]
    [InlineData("example.com", false)]
    [InlineData("https://exa mple.com", false)]
    [InlineData("ftp://example.com", false)]
    [InlineData("chrome", false)]
    public void TypedUrl_OnlyWholeHttpAddresses(string query, bool expected) =>
        Assert.Equal(expected, CommandBarCatalogSupport.TypedUrl(query) is not null);

    [Fact]
    public void Home_SuggestionsThenGroupsWithoutAppsOrWindows()
    {
        var pool = new List<CommandBarEntry>
        {
            new("action.keepAwake", "Enable keep awake", "Keep awake", "", CommandBarEntryKind.Action, CommandBarSource.Actions),
            new("action.power.sleep", "Sleep", "Power", "", CommandBarEntryKind.Action, CommandBarSource.Actions),
            new("action.power.restart", "Restart", "Power", "", CommandBarEntryKind.Action, CommandBarSource.Actions),
            new("app.Chrome", "Google Chrome", "App", "", CommandBarEntryKind.App, CommandBarSource.Apps),
            new("window.1", "Inbox", "Window", "", CommandBarEntryKind.Window, CommandBarSource.Windows),
        };
        var usage = new Dictionary<string, CommandBarUse> { ["action.power.restart"] = new(3, Now.ToUnixTimeSeconds()) };

        var sections = CommandBarCatalogSupport.Home(pool, usage, curated: ["action.keepAwake"], Bar);

        Assert.Equal(Bar.SuggestionsLabel, sections[0].Title);
        Assert.Equal(["action.power.restart", "action.keepAwake"], sections[0].Entries.Select(e => e.Id));
        Assert.Equal("Power", sections[1].Title);
        Assert.Equal(["action.power.sleep"], sections[1].Entries.Select(e => e.Id));
        Assert.DoesNotContain(sections, s => s.Entries.Any(e => e.Kind is CommandBarEntryKind.App or CommandBarEntryKind.Window));
    }

    [Fact]
    public void Home_CapsEachGroupAtTwelve()
    {
        var pool = Enumerable.Range(0, 20)
            .Select(i => new CommandBarEntry($"folder.{i}", $"Folder {i}", "Folder", "", CommandBarEntryKind.Folder, CommandBarSource.Folders))
            .ToList();
        var sections = CommandBarCatalogSupport.Home(pool, new Dictionary<string, CommandBarUse>(), [], Bar);
        Assert.Equal(12, sections.Single(s => s.Title == "Folder").Entries.Count);
    }
}
