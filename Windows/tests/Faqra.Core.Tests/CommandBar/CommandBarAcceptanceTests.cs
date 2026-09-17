using Faqra.Core.CommandBar;

namespace Faqra.Core.Tests;

// The Stage 1 acceptance case from the porting brief: entries for "Google Chrome",
// "Character Map", "Chrome Remote Desktop" and "Control Panel" (kind app), query "chr".
//
// The brief asked for "Google Chrome" first, but instructed: if the verbatim algorithm does not
// rank it first, report exactly what it ranks first and why instead of bending the algorithm.
// Verbatim CommandBarSearch DOES NOT rank "Google Chrome" first here - it ranks "Chrome Remote
// Desktop" first. Traced through CommandBarSupport.swift's own rules (matchTier is compared
// before score, CommandBarSupport.swift:307-328):
//   - "Chrome Remote Desktop".normalized() STARTS WITH "chr" -> matchTier 4 (title has query as
//     prefix, +900 whole-query bonus) + a word-prefix token score (80) = 980.
//   - "Google Chrome".normalized() only CONTAINS "chr" (inside "chrome") -> matchTier 3 (+700
//     bonus) + the same word-prefix token score (80) = 780.
//   - "Character Map" only reaches "chr" as a subsequence ("CHaracter" has no literal "chr"
//     substring) -> matchTier 1, score 24.
//   - "Control Panel" contains no "h" at all, so no tier of CommandBarSearch.Score matches it -
//     it is excluded entirely, matching the brief's own expectation.
// Because all four entries share kind App and hence the same CommandBarSource.Apps rank bias,
// the bias cannot break the Chrome-Remote-Desktop-vs-Google-Chrome tie: bias only nudges within
// a tier, and tier 4 always beats tier 3 regardless of score or boost (CommandBarCandidate.Boost
// only feeds the score component, never the tier). Nor can a CommandBarUsage boost fix it, for
// the same reason: usage feeds Boost (score), not Priority, and Priority is the only channel
// that outranks tier. Only an explicit alias or a learned query-habit choice (both out of Stage
// 1's scope; CommandBarQueryHabits is explicitly deferred per the porting brief) arrives as
// Priority and could override this. This is a property of the upstream algorithm itself, not a
// porting bug - a real "Chrome Remote Desktop" row genuinely outranks "Google Chrome" for "chr"
// on text quality alone, until either app has been run enough to earn a learned-choice priority.
public class CommandBarAcceptanceTests
{
    private static readonly CommandBarEntry GoogleChrome = new(
        "app.google-chrome", "Google Chrome", "Application", "", CommandBarEntryKind.App, CommandBarSource.Apps);

    private static readonly CommandBarEntry CharacterMap = new(
        "app.character-map", "Character Map", "Application", "", CommandBarEntryKind.App, CommandBarSource.Apps);

    private static readonly CommandBarEntry ChromeRemoteDesktop = new(
        "app.chrome-remote-desktop", "Chrome Remote Desktop", "Application", "", CommandBarEntryKind.App, CommandBarSource.Apps);

    private static readonly CommandBarEntry ControlPanel = new(
        "app.control-panel", "Control Panel", "Application", "", CommandBarEntryKind.App, CommandBarSource.Apps);

    private static readonly IReadOnlyList<CommandBarEntry> Entries =
        new[] { GoogleChrome, CharacterMap, ChromeRemoteDesktop, ControlPanel };

    [Fact]
    public void Chr_RanksChromeRemoteDesktopFirst_NotGoogleChrome_PerVerbatimUpstreamRules()
    {
        var ranked = CommandBarRanking.Rank(Entries, "chr");

        Assert.Equal(
            new[] { ChromeRemoteDesktop, GoogleChrome, CharacterMap },
            ranked);
        Assert.DoesNotContain(ControlPanel, ranked);
    }

    [Fact]
    public void Chr_GoogleChromeRanksFirstOnceItHasEarnedAnExplicitPriority()
    {
        // Demonstrates the fix the brief asks about: NOT a usage boost (which cannot cross a tier
        // boundary) but an explicit priority - the same channel an alias or a learned query
        // choice would use once CommandBarQueryHabits ships.
        var withAlias = new[]
        {
            GoogleChrome with { Priority = 1 },
            CharacterMap,
            ChromeRemoteDesktop,
            ControlPanel,
        };

        var ranked = CommandBarRanking.Rank(withAlias, "chr");

        Assert.Equal(GoogleChrome.Title, ranked[0].Title);
    }

    [Fact]
    public void Chr_AUsageBoostAloneCannotFixTheOrderBecauseItNeverCrossesATierBoundary()
    {
        var now = 1_000_000d;
        var usage = new Dictionary<string, CommandBarUse>
        {
            // Heavily used, very recently: the maximum possible CommandBarUsage boost (480).
            [GoogleChrome.Id] = new CommandBarUse(999, now - 1),
        };

        var ranked = CommandBarRanking.Rank(Entries, "chr", usage, now: now);

        // Chrome Remote Desktop (tier 4) still leads Google Chrome (tier 3, even after the
        // maximum possible usage boost is folded into its score) because RankedIndexes compares
        // tier before score, and CommandBarUsage.Boost only ever feeds score.
        Assert.Equal(ChromeRemoteDesktop.Title, ranked[0].Title);
    }

    [Fact]
    public void KindLimits_CapsAppsAtFiveAndDropsTheRest()
    {
        var apps = Enumerable.Range(0, 8)
            .Select(i => new CommandBarEntry($"app.{i}", $"App {i}", "", "", CommandBarEntryKind.App, CommandBarSource.Apps))
            .ToArray();

        var capped = CommandBarKindLimits.Apply(apps, maxResults: 20);

        Assert.Equal(5, capped.Count);
        Assert.Equal(apps.Take(5), capped);
    }

    [Fact]
    public void KindLimits_UncappedKindsPassThroughUntilTheTotalCap()
    {
        var actions = Enumerable.Range(0, 15)
            .Select(i => new CommandBarEntry($"action.{i}", $"Action {i}", "", "", CommandBarEntryKind.Action, CommandBarSource.Actions))
            .ToArray();

        var capped = CommandBarKindLimits.Apply(actions);

        Assert.Equal(CommandBarKindLimits.DefaultMaxResults, capped.Count);
    }
}
