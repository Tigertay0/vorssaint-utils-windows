# Faqra Home (H1a) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the island a Faqra-only Home section that opens on hover: what is playing, the agents, the timer, the system and the battery, in an order the owner edits on Home itself.

**Architecture:** Home is a new island section (`IslandModule.FaqraHome`) placed first, and the island's "open on nothing in particular" choice becomes Home whenever Home is shown. A small Core model (`HomeItems`) owns the rows: their raw names, the owner's order in one settings key, editing operations and the single-accent rule. The WPF `HomeModule` builds one row per item once and updates each in place from its source, so buttons keep their state and orbs keep moving; Edit swaps the rows for an editable list.

**Tech Stack:** C# / .NET 8, WPF + WPF UI 4.3, xunit. No new NuGet packages.

**Spec:** `docs/superpowers/specs/2026-10-12-faqra-home-calendar-shelf-design.md` ("Home (H1)", "Decisions confirmed with the owner", "Design read"). Next event and Shelf rows come with H1b and H2.

## Global Constraints

- Solution lives in `Windows/`; run every `dotnet` command from `C:\Users\Tigre\vorssaint-utils-windows\Windows`.
- Every new source file under `src/` starts with `// SPDX-License-Identifier: GPL-3.0-or-later` and `// Copyright (C) 2026 Faqra contributors`. Test files have no header. Nullable warnings are errors; the build has no warnings. No new NuGet packages.
- Raw names, never renamed: section `faqraHome`; rows `nowPlaying`, `agents`, `timer`, `system`, `battery` (H1b adds `nextEvent`, H2 adds `shelf`).
- Settings key `faqraHomeItems`, default `nowPlaying,nextEvent,agents`. Unknown names are skipped when reading and kept when saving, so `nextEvent` appears by itself once H1b ships.
- Hover always opens Home while Home is shown; a section picked from the list, a shortcut or a row stays until the island collapses. With Home hidden, the island opens as before (the last section, else the first).
- Home is first in the section order unless the owner placed it elsewhere. Its shortcut letter is `h`; its glyph is Segoe Fluent `\uE80F` (Home).
- Look: the island palette, the system font, `AgentCardParts.Panel` rows (12 px radius, 12 px padding, 10 px apart). Only one thing wears `Accent`: the Agents row while an agent waits on the owner (Approval, Question or AwaitingReply).
- Every empty state is a sentence, never a blank row. Copy: sentence case, no em or en dashes, no exclamation marks.
- Editing has a keyboard and single-pointer path: Move up, Move down and Remove buttons per row, Add buttons for missing rows; every button has an automation name naming its row.
- Commits: conventional prefixes, no `Co-Authored-By` or Claude attribution line. WPF tests run in `StaThread.Run(...)`. Subagents run on Sonnet.

## Review Focus

1. A saved order with unknown, repeated or unavailable names (an older or newer Faqra, a desktop without a battery) shows the rest in order and never throws; saving keeps the unknown names. Pinned in Task 1 (`ReadsWhatItKnowsAndKeepsTheRest`).
2. The island opens on Home on hover even after the owner used another section, but a section picked while open stays. Pinned in Task 1 (`HomeOpensWheneverItIsShown`) and Task 3 (live).
3. Rows update in place: pressing play or a session row while events arrive hits the same button. Pinned in Task 2 (`RowsAreUpdatedInPlace`).
4. Removing every row leaves a sentence and a way back. Pinned in Task 2 (`AnEmptyHomeSaysHowToFillIt`).
5. Exactly one accent: a waiting agent turns the Agents row's title accent; nothing else is accent. Pinned in Task 2 (`OnlyAWaitingAgentWearsTheAccent`).

---

## File structure

| File | Responsibility |
|---|---|
| `src/Faqra.Core/Island/HomeItems.cs` (new) | Home's rows, the saved order, edits, the accent rule |
| `src/Faqra.Core/Island/IslandModule.cs` (modify) | `FaqraHome`, Home first, `Opening` |
| `src/Faqra.Core/Localization/HomeStrings.cs`, `HomeStrings.EnUS.cs` (new) | Home's words |
| `src/Faqra.Core/Defaults/DefaultsKey.Stage1.cs`, `RegisteredDefaults.Stage1.cs` (modify) | `faqraHomeItems` |
| `src/Faqra.App/Island/Modules/HomeRows.cs` (new) | The five rows, each built once and updated in place |
| `src/Faqra.App/Island/Modules/HomeModule.cs` (new) | Home: rows in order, Edit, events to the controller |
| `src/Faqra.App/Island/IslandController.Home.cs` (new), `IslandController.cs`, `Modules/SectionPicker.cs` (modify) | Building Home, opening on Home, row targets, system sampling |

---

### Task 1: Home's model and the opening rule (Core)

**Files:**
- Create: `src/Faqra.Core/Island/HomeItems.cs`, `src/Faqra.Core/Localization/HomeStrings.cs`, `src/Faqra.Core/Localization/HomeStrings.EnUS.cs`
- Modify: `src/Faqra.Core/Island/IslandModule.cs`, `src/Faqra.Core/Defaults/DefaultsKey.Stage1.cs`, `src/Faqra.Core/Defaults/RegisteredDefaults.Stage1.cs`
- Test: `tests/Faqra.Core.Tests/HomeItemsTests.cs` (new), `tests/Faqra.Core.Tests/IslandModuleTests.cs` (modify)

**Interfaces:**
- Produces: `enum HomeItem { NowPlaying, Agents, Timer, System, Battery }`; `static class HomeItems` with `All`, `const string DefaultOrder = "nowPlaying,nextEvent,agents"`, `RawValue(this HomeItem)`, `HomeItem? FromRawValue(string?)`, `IReadOnlyList<HomeItem> Parse(string? saved, Func<HomeItem, bool> isAvailable)`, `string Saved(IReadOnlyList<HomeItem> items, string? previous)`, `IReadOnlyList<HomeItem> Addable(IReadOnlyList<HomeItem> current, Func<HomeItem, bool> isAvailable)`, `Move(items, item, delta)`, `Remove(items, item)`, `Add(items, item)`, `HomeItem? Accent(IReadOnlyList<HomeItem> items, bool agentWaiting)`, `bool Waits(AgentState state)`; `IslandModule.FaqraHome` (raw `faqraHome`, key `h`, glyph `\uE80F`, always available); `IslandModules.Opening(IslandModule current, IReadOnlyList<IslandModule> visible)` (`IslandModule?`); `DefaultsKey.FaqraHomeItems`; `HomeStrings` (members listed in Step 4).

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.Core.Tests/HomeItemsTests.cs`:

```csharp
using Faqra.Core.Agents;
using Faqra.Core.Defaults;
using Faqra.Core.Island;

namespace Faqra.Core.Tests;

public class HomeItemsTests
{
    private static bool Everything(HomeItem _) => true;

    [Fact]
    public void RawNamesAreStableAndRoundTrip()
    {
        Assert.Equal(["nowPlaying", "agents", "timer", "system", "battery"], HomeItems.All.Select(item => item.RawValue()));
        foreach (var item in HomeItems.All)
        {
            Assert.Equal(item, HomeItems.FromRawValue(item.RawValue()));
        }
        Assert.Null(HomeItems.FromRawValue("nextEvent"));
    }

    [Fact]
    public void HomeStartsWithMusicAndTheAgents()
    {
        Assert.Equal(HomeItems.DefaultOrder, DefaultsStore.InMemory().String(DefaultsKey.FaqraHomeItems));
        Assert.Equal([HomeItem.NowPlaying, HomeItem.Agents], HomeItems.Parse(HomeItems.DefaultOrder, Everything));
    }

    [Fact]
    public void ReadsWhatItKnowsAndKeepsTheRest()
    {
        var items = HomeItems.Parse(" battery , agents,agents,shelf,,timer ", item => item != HomeItem.Battery);
        Assert.Equal([HomeItem.Agents, HomeItem.Timer], items);
        Assert.Empty(HomeItems.Parse(null, Everything));

        // Names this version does not know survive a save, so a later Faqra still finds them.
        Assert.Equal("timer,agents,nextEvent,shelf", HomeItems.Saved([HomeItem.Timer, HomeItem.Agents], "agents,nextEvent,timer,shelf,timer"));
        Assert.Equal("agents", HomeItems.Saved([HomeItem.Agents], null));
    }

    [Fact]
    public void EditsReturnNewListsAndStayInBounds()
    {
        IReadOnlyList<HomeItem> items = [HomeItem.NowPlaying, HomeItem.Agents, HomeItem.Timer];
        Assert.Equal([HomeItem.Agents, HomeItem.NowPlaying, HomeItem.Timer], HomeItems.Move(items, HomeItem.Agents, -1));
        Assert.Equal([HomeItem.NowPlaying, HomeItem.Timer, HomeItem.Agents], HomeItems.Move(items, HomeItem.Agents, 1));
        Assert.Same(items, HomeItems.Move(items, HomeItem.NowPlaying, -1));
        Assert.Same(items, HomeItems.Move(items, HomeItem.Timer, 1));
        Assert.Same(items, HomeItems.Move(items, HomeItem.Battery, 1));
        Assert.Equal([HomeItem.NowPlaying, HomeItem.Timer], HomeItems.Remove(items, HomeItem.Agents));
        Assert.Equal([HomeItem.NowPlaying, HomeItem.Agents, HomeItem.Timer, HomeItem.System], HomeItems.Add(items, HomeItem.System));
        Assert.Same(items, HomeItems.Add(items, HomeItem.Timer));
        Assert.Equal([HomeItem.NowPlaying, HomeItem.Agents, HomeItem.Timer], items);
    }

    [Fact]
    public void OnlyAvailableRowsNotOnHomeCanBeAdded() =>
        Assert.Equal([HomeItem.System], HomeItems.Addable([HomeItem.NowPlaying, HomeItem.Agents, HomeItem.Timer], item => item != HomeItem.Battery));

    [Fact]
    public void OnlyAWaitingAgentWearsTheAccent()
    {
        IReadOnlyList<HomeItem> items = [HomeItem.NowPlaying, HomeItem.Agents];
        Assert.Equal(HomeItem.Agents, HomeItems.Accent(items, agentWaiting: true));
        Assert.Null(HomeItems.Accent(items, agentWaiting: false));
        Assert.Null(HomeItems.Accent([HomeItem.NowPlaying], agentWaiting: true));
        Assert.True(HomeItems.Waits(AgentState.Approval));
        Assert.True(HomeItems.Waits(AgentState.Question));
        Assert.True(HomeItems.Waits(AgentState.AwaitingReply));
        Assert.False(HomeItems.Waits(AgentState.Working));
        Assert.False(HomeItems.Waits(AgentState.Finished));
    }
}
```

In `tests/Faqra.Core.Tests/IslandModuleTests.cs`:

1. Rename `FourteenModulesWithUniqueRawValuesAndShortcutKeys` to `FifteenModulesWithUniqueRawValuesAndShortcutKeys` and change its three `14`s to `15`.
2. In `ControlsAndMusicAreAlwaysAvailable`, add `Assert.True(IslandModule.FaqraHome.IsAvailable(_ => false));`.
3. Replace `VisibleFollowsTheSavedOrderThenCanonicalOrder`'s assertions with:

```csharp
        // Home leads unless the owner placed it; then the saved order, then the canonical order.
        Assert.Equal(IslandModule.FaqraHome, visible[0]);
        Assert.Equal(IslandModule.System, visible[1]);
        Assert.Equal(IslandModule.Controls, visible[2]);
        Assert.Equal(15, visible.Count);
        Assert.Equal(15, visible.Distinct().Count());
```

4. Replace `UnknownAndRepeatedTokensInASavedOrderAreIgnored`'s assertions with:

```csharp
        Assert.Equal(IslandModule.FaqraHome, visible[0]);
        Assert.Equal(IslandModule.Controls, visible[1]);
        Assert.Equal(IslandModule.Timer, visible[2]);
        Assert.Equal(15, visible.Count);
```

5. Add inside the class:

```csharp
    [Fact]
    public void HomeIsWhereTheOwnerPutsIt()
    {
        Assert.Equal("faqraHome", IslandModule.FaqraHome.RawValue());
        Assert.Equal('h', IslandModule.FaqraHome.ShortcutKey());
        var visible = IslandModules.Visible("system,faqraHome", hiddenList: null, _ => true);
        Assert.Equal([IslandModule.System, IslandModule.FaqraHome], visible.Take(2));
        Assert.DoesNotContain(IslandModule.FaqraHome, IslandModules.Visible(null, "faqraHome", _ => true));
    }

    [Fact]
    public void HomeOpensWheneverItIsShown()
    {
        IReadOnlyList<IslandModule> withHome = [IslandModule.FaqraHome, IslandModule.Music, IslandModule.System];
        IReadOnlyList<IslandModule> withoutHome = [IslandModule.Music, IslandModule.System];
        Assert.Equal(IslandModule.FaqraHome, IslandModules.Opening(IslandModule.System, withHome));
        Assert.Equal(IslandModule.System, IslandModules.Opening(IslandModule.System, withoutHome));
        Assert.Equal(IslandModule.Music, IslandModules.Opening(IslandModule.Timer, withoutHome));
        Assert.Null(IslandModules.Opening(IslandModule.Timer, []));
    }
```

Other Core tests that count island modules (search `tests/Faqra.Core.Tests` for `IslandModules.All` and `Visible(`) move from 14 to 15 the same way; change only the counts.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~HomeItemsTests|FullyQualifiedName~IslandModuleTests"`
Expected: build FAILS with "The type or namespace name 'HomeItem' could not be found".

- [ ] **Step 3: Write `HomeItems`**

`src/Faqra.Core/Island/HomeItems.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Faqra.Core.Agents;

namespace Faqra.Core.Island;

/// <summary>A row on the island's Home. Raw values are saved in <c>faqraHomeItems</c>: add members, never rename them.</summary>
public enum HomeItem { NowPlaying, Agents, Timer, System, Battery }

/// <summary>Home's rows: their saved order, the owner's edits, and the one row that may wear the accent.</summary>
public static class HomeItems
{
    public static readonly IReadOnlyList<HomeItem> All = Enum.GetValues<HomeItem>();

    /// <summary>Home's first order. "nextEvent" arrives with the calendar and is skipped until then.</summary>
    public const string DefaultOrder = "nowPlaying,nextEvent,agents";

    public static string RawValue(this HomeItem item) => item switch
    {
        HomeItem.NowPlaying => "nowPlaying",
        HomeItem.Agents => "agents",
        HomeItem.Timer => "timer",
        HomeItem.System => "system",
        HomeItem.Battery => "battery",
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null),
    };

    public static HomeItem? FromRawValue(string? raw) =>
        All.Cast<HomeItem?>().FirstOrDefault(item => item!.Value.RawValue() == raw);

    /// <summary>The rows to show, in the saved order; unknown names, repeats and rows with nothing to show are skipped.</summary>
    public static IReadOnlyList<HomeItem> Parse(string? saved, Func<HomeItem, bool> isAvailable)
    {
        var seen = new HashSet<HomeItem>();
        var items = new List<HomeItem>();
        foreach (var raw in Names(saved))
        {
            if (FromRawValue(raw) is { } item && isAvailable(item) && seen.Add(item))
            {
                items.Add(item);
            }
        }
        return items;
    }

    /// <summary>What to save: the rows in their new order, then any names from <paramref name="previous"/> this version does not know.</summary>
    public static string Saved(IReadOnlyList<HomeItem> items, string? previous)
    {
        var unknown = Names(previous).Where(raw => FromRawValue(raw) is null).Distinct(StringComparer.Ordinal);
        return string.Join(",", items.Select(item => item.RawValue()).Concat(unknown));
    }

    /// <summary>Rows that can be added: available and not on Home yet, in canonical order.</summary>
    public static IReadOnlyList<HomeItem> Addable(IReadOnlyList<HomeItem> current, Func<HomeItem, bool> isAvailable) =>
        All.Where(item => isAvailable(item) && !current.Contains(item)).ToList();

    /// <summary>The list with <paramref name="item"/> moved by <paramref name="delta"/>; the same list when it cannot move.</summary>
    public static IReadOnlyList<HomeItem> Move(IReadOnlyList<HomeItem> items, HomeItem item, int delta)
    {
        var moved = items.ToList();
        var at = moved.IndexOf(item);
        var to = at + delta;
        if (at < 0 || to < 0 || to >= moved.Count)
        {
            return items;
        }
        moved.RemoveAt(at);
        moved.Insert(to, item);
        return moved;
    }

    public static IReadOnlyList<HomeItem> Remove(IReadOnlyList<HomeItem> items, HomeItem item) =>
        items.Where(other => other != item).ToList();

    public static IReadOnlyList<HomeItem> Add(IReadOnlyList<HomeItem> items, HomeItem item) =>
        items.Contains(item) ? items : [.. items, item];

    /// <summary>The single row that wears the accent: Agents, while an agent waits on the owner.</summary>
    public static HomeItem? Accent(IReadOnlyList<HomeItem> items, bool agentWaiting) =>
        agentWaiting && items.Contains(HomeItem.Agents) ? HomeItem.Agents : null;

    /// <summary>A session in this state waits on the owner.</summary>
    public static bool Waits(AgentState state) =>
        state is AgentState.Approval or AgentState.Question or AgentState.AwaitingReply;

    private static IEnumerable<string> Names(string? list) =>
        (list ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
```

- [ ] **Step 4: Register Home and its words**

In `src/Faqra.Core/Island/IslandModule.cs`:

1. Add `FaqraHome` at the end of the enum (after `FaqraAgents`).
2. Add arms: `IslandModule.FaqraHome => "faqraHome",` in `RawValue`; `IslandModule.FaqraHome => 'h',` in `ShortcutKey`; `IslandModule.FaqraHome => "\uE80F",      // Home` in `Glyph`.
3. In `IsAvailable`, change the first arm to `IslandModule.Controls or IslandModule.Music or IslandModule.FaqraHome => true,`.
4. In `Visible`, replace the canonical-order loop with:

```csharp
        foreach (var module in All.Where(module => seen.Add(module)))
        {
            // Home leads unless the owner placed it themselves.
            if (module == IslandModule.FaqraHome)
            {
                ordered.Insert(0, module);
            }
            else
            {
                ordered.Add(module);
            }
        }
```

5. Add after `Visible`:

```csharp
    /// <summary>
    /// The section the island opens on when nothing asks for one: Home while it is shown, else the current section,
    /// else the first. Null when no section is shown.
    /// </summary>
    public static IslandModule? Opening(IslandModule current, IReadOnlyList<IslandModule> visible) =>
        visible.Count == 0 ? null
        : visible.Contains(IslandModule.FaqraHome) ? IslandModule.FaqraHome
        : visible.Contains(current) ? current
        : visible[0];
```

In `src/Faqra.Core/Defaults/DefaultsKey.Stage1.cs`, add after `FaqraAgentsReplyWindowSeconds`:

```csharp
    public const string FaqraHomeItems = "faqraHomeItems";
```

In `src/Faqra.Core/Defaults/RegisteredDefaults.Stage1.cs`, add after the `FaqraAgentsReplyWindowSeconds` entry:

```csharp
        [DefaultsKey.FaqraHomeItems] = Island.HomeItems.DefaultOrder,
```

(Use the namespace qualification the file needs; `HomeItems` is in `Faqra.Core.Island`.)

`src/Faqra.Core/Localization/HomeStrings.cs` (follow `AgentsStrings.cs`'s shape: a sealed class of `required string` properties and a `For(AppLanguage)` that resolves to `EnUS`):

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

namespace Faqra.Core.Localization;

/// <summary>The island Home's words.</summary>
public sealed partial class HomeStrings
{
    public required string Title { get; init; }
    public required string ItemNowPlaying { get; init; }
    public required string ItemAgents { get; init; }
    public required string ItemTimer { get; init; }
    public required string ItemSystem { get; init; }
    public required string ItemBattery { get; init; }
    public required string NothingPlaying { get; init; }
    public required string PlayPause { get; init; }
    public required string NextTrack { get; init; }
    public required string NoAgents { get; init; }
    public required string MoreAgentsFormat { get; init; }
    public required string Open { get; init; }
    public required string NoTimer { get; init; }
    public required string TimerLeftFormat { get; init; }
    public required string TimerPaused { get; init; }
    public required string CpuFormat { get; init; }
    public required string MemoryFormat { get; init; }
    public required string BatteryCharging { get; init; }
    public required string BatteryOnBattery { get; init; }
    public required string Edit { get; init; }
    public required string Done { get; init; }
    public required string MoveUpFormat { get; init; }
    public required string MoveDownFormat { get; init; }
    public required string RemoveFormat { get; init; }
    public required string AddFormat { get; init; }
    public required string AddHeader { get; init; }
    public required string Empty { get; init; }

    public static HomeStrings For(AppLanguage language) => language switch
    {
        // Translations pending: every language resolves to English for now (see Strings.Aliases.cs).
        _ => EnUS,
    };
}
```

`src/Faqra.Core/Localization/HomeStrings.EnUS.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

namespace Faqra.Core.Localization;

public sealed partial class HomeStrings
{
    public static readonly HomeStrings EnUS = new()
    {
        Title = "Home",
        ItemNowPlaying = "Now playing",
        ItemAgents = "Agents",
        ItemTimer = "Timer",
        ItemSystem = "System",
        ItemBattery = "Battery",
        NothingPlaying = "Nothing playing",
        PlayPause = "Play or pause",
        NextTrack = "Next track",
        NoAgents = "No agents running",
        MoreAgentsFormat = "{0} more in Agents",
        Open = "Open",
        NoTimer = "No timer running",
        TimerLeftFormat = "{0} left",
        TimerPaused = "Paused",
        CpuFormat = "CPU {0}",
        MemoryFormat = "Memory {0}",
        BatteryCharging = "Charging",
        BatteryOnBattery = "On battery",
        Edit = "Edit Home",
        Done = "Done",
        MoveUpFormat = "Move {0} up",
        MoveDownFormat = "Move {0} down",
        RemoveFormat = "Remove {0}",
        AddFormat = "Add {0}",
        AddHeader = "Add to Home",
        Empty = "Nothing on Home yet. Edit Home to add rows.",
    };
}
```

(If `AgentsStrings` is not split into partial files this way, mirror whatever it does; the members and the English words are what matter.)

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.Core.Tests`
Expected: PASS (every Core test).

- [ ] **Step 6: Commit**

```bash
git add src/Faqra.Core tests/Faqra.Core.Tests
git commit -m "feat(windows): Home section model and opening on Home"
```

---

### Task 2: The Home section (App)

**Files:**
- Create: `src/Faqra.App/Island/Modules/HomeRows.cs`, `src/Faqra.App/Island/Modules/HomeModule.cs`
- Test: `tests/Faqra.App.Tests/HomeModuleTests.cs` (new)

**Interfaces:**
- Consumes: `HomeItem`, `HomeItems`, `HomeStrings`, `DefaultsKey.FaqraHomeItems` (Task 1); `NowPlayingService` (`Current`, `Changed`, `TogglePlayPauseAsync`, `NextAsync`), `IslandTimerService` (`HasSession`, `IsRunning`, `Remaining`, `Format`, `Changed`), `SystemMonitor` (`Snapshot`, `SnapshotChanged`), `Power.BatteryStatus()`, `AgentHub` (`Board.Ordered`, `Changed`), `OrbView`, `AgentOrbStyles`, `AgentInk`, `AgentsText.Status`, `AgentCardParts`, `IdleMusicView.Artwork`, `MusicModule.RoundButtonTemplate`, `MetricFormat.Percent`.
- Produces: `public sealed class HomeModule : UserControl` with `HomeModule(ISettingsStore store, NowPlayingService nowPlaying, AgentHub agents, IslandTimerService timer, SystemMonitor monitor, Func<HomeItem, bool> isAvailable)`, events `Action<IslandModule>? SectionRequested` and `Action<string>? SessionRequested`, `IReadOnlyList<HomeItem> Items`, `bool Editing`, `void BeginEdit()`, `void EndEdit()`, `bool Shows(HomeItem item)`.

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.App.Tests/HomeModuleTests.cs`:

```csharp
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Faqra.App.Island.Modules;
using Faqra.Core.Defaults;
using Faqra.Core.Island;
using Faqra.Core.Localization;

namespace Faqra.App.Tests;

public class HomeModuleTests
{
    private static readonly HomeStrings S = HomeStrings.EnUS;

    private static IEnumerable<T> All<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            foreach (var nested in All<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static Button Named(DependencyObject root, string name) =>
        All<Button>(root).Single(button => AutomationProperties.GetName(button) == name);

    private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static HomeModule Home(AppServices services, Faqra.Services.Agents.AgentHub? hub = null) =>
        new(services.Store, services.NowPlaying, hub ?? services.Agents, services.Timer, services.Monitor, _ => true);

    [Fact]
    public void HomeShowsItsDefaultRowsInOrder() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        var home = Home(services);
        AgentsRenderTests.RenderInk(new Border { Background = IslandPalette.Surface, Child = home, Width = 412, Height = 400 }, "island-home", 412, 400);

        Assert.Equal([HomeItem.NowPlaying, HomeItem.Agents], home.Items);
        var text = AgentsRenderTests.AllText(home);
        Assert.Contains(S.NothingPlaying, text);
        Assert.Contains(S.NoAgents, text);
        Assert.True(text.IndexOf(S.NothingPlaying, StringComparison.Ordinal) < text.IndexOf(S.NoAgents, StringComparison.Ordinal));
    });

    [Fact]
    public void EditingReordersRemovesAddsAndRemembers() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        var home = Home(services);
        Click(Named(home, S.Edit));
        Assert.True(home.Editing);
        AgentsRenderTests.RenderInk(new Border { Background = IslandPalette.Surface, Child = home, Width = 412, Height = 400 }, "island-home-edit", 412, 400);

        Click(Named(home, string.Format(S.MoveUpFormat, S.ItemAgents)));
        Assert.Equal("agents,nowPlaying,nextEvent", services.Store.String(DefaultsKey.FaqraHomeItems));
        Click(Named(home, string.Format(S.RemoveFormat, S.ItemNowPlaying)));
        Click(Named(home, string.Format(S.AddFormat, S.ItemTimer)));
        Assert.Equal([HomeItem.Agents, HomeItem.Timer], home.Items);
        Assert.Equal("agents,timer,nextEvent", services.Store.String(DefaultsKey.FaqraHomeItems));

        Click(Named(home, S.Done));
        Assert.False(home.Editing);
        Assert.Contains(S.NoTimer, AgentsRenderTests.AllText(home));
    });

    [Fact]
    public void AnEmptyHomeSaysHowToFillIt() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        services.Store.Set(DefaultsKey.FaqraHomeItems, "nextEvent");
        var home = Home(services);
        Assert.Empty(home.Items);
        Assert.Contains(S.Empty, AgentsRenderTests.AllText(home));
        Assert.NotNull(Named(home, S.Edit));
    });

    [Fact]
    public void OnlyAWaitingAgentWearsTheAccent() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Read\"}");
        var home = Home(services, hub);
        Assert.DoesNotContain(All<TextBlock>(home), block => ReferenceEquals(block.Foreground, IslandPalette.Accent));

        hub.ReplaceBoardForTests(hub.Board.Apply(Faqra.Core.Agents.AgentEvent.TryParse("{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}")!, DateTimeOffset.Now));
        var accented = All<TextBlock>(home).Where(block => ReferenceEquals(block.Foreground, IslandPalette.Accent)).ToList();
        Assert.Single(accented);
        Assert.Equal(S.ItemAgents, AgentsRenderTests.Words(accented[0]));
        AgentsRenderTests.RenderInk(new Border { Background = IslandPalette.Surface, Child = home, Width = 412, Height = 400 }, "island-home-waiting", 412, 400);
    });

    [Fact]
    public void RowsOpenTheirSectionOrSession() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        services.Store.Set(DefaultsKey.FaqraHomeItems, "agents,timer,system,battery");
        using var hub = AgentsRenderTests.HubWith("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Read\"}");
        var home = Home(services, hub);
        var sections = new List<IslandModule>();
        var sessions = new List<string>();
        home.SectionRequested += sections.Add;
        home.SessionRequested += sessions.Add;

        Click(Named(home, S.ItemTimer));
        Click(Named(home, S.ItemSystem));
        Click(All<Button>(home).First(button => AutomationProperties.GetName(button).StartsWith("faqra", StringComparison.Ordinal)));

        Assert.Equal([IslandModule.Timer, IslandModule.System], sections);
        Assert.Equal(["s1"], sessions);
    });

    [Fact]
    public void RowsAreUpdatedInPlace() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Read\"}");
        var home = Home(services, hub);
        var play = Named(home, S.PlayPause);
        var session = All<Button>(home).First(button => AutomationProperties.GetName(button).StartsWith("faqra", StringComparison.Ordinal));

        hub.ReplaceBoardForTests(hub.Board.Apply(Faqra.Core.Agents.AgentEvent.TryParse("{\"hook_event_name\":\"PostToolUse\",\"session_id\":\"s1\",\"tool_name\":\"Read\"}")!, DateTimeOffset.Now));

        Assert.Same(play, Named(home, S.PlayPause));
        Assert.Same(session, All<Button>(home).First(button => AutomationProperties.GetName(button).StartsWith("faqra", StringComparison.Ordinal)));
    });
}
```

(`AppServices` exposes `Store`, `NowPlaying`, `Agents`, `Timer` and `Monitor`; check the real property names in `src/Faqra.App/AppServices.cs` and use them. A session row's automation name is `"<session name>, <status>"`, the way `AgentsModule.SessionRow` names its rows, so a session in `C:\code\faqra` starts with `faqra`.)

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.App.Tests --filter "FullyQualifiedName~HomeModuleTests"`
Expected: build FAILS with "The type or namespace name 'HomeModule' could not be found".

- [ ] **Step 3: Write the rows**

`src/Faqra.App/Island/Modules/HomeRows.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Faqra.App.Agents;
using Faqra.Core.Agents;
using Faqra.Core.Island;
using Faqra.Core.Localization;
using Faqra.Core.Metrics;
using Faqra.Services.Agents;
using Faqra.Services.Island;
using Faqra.Services.Media;
using Faqra.Services.Monitor;
using Faqra.Win32.Power;

namespace Faqra.App.Island.Modules;

/// <summary>One row on Home: built once, then refreshed in place so its buttons and orbs keep their state.</summary>
internal abstract class HomeRow
{
    protected HomeRow(HomeItem item) => Item = item;

    public HomeItem Item { get; }

    public abstract FrameworkElement View { get; }

    public abstract void Update();

    protected static string Format(string format, object value) => string.Format(CultureInfo.CurrentCulture, format, value);

    protected static TextBlock Label(string text) => new()
    {
        Text = text,
        FontFamily = AgentCardParts.TextFont,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = IslandPalette.Tertiary,
    };

    protected static TextBlock Line(double size, Brush ink) => new()
    {
        FontFamily = AgentCardParts.TextFont,
        FontSize = size,
        Foreground = ink,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    /// <summary>A whole-row button: the row's panel look, hover and press from the island's round template.</summary>
    protected static Button RowButton(UIElement content, string name, Action click)
    {
        var button = new Button
        {
            Content = content,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 10),
            Background = IslandPalette.Fill,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Template = MusicModule.RoundButtonTemplate(12),
        };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>A round icon button for actions inside a row.</summary>
    protected static Button IconButton(string glyph, string name, Action click)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = IslandPalette.Primary,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Width = 32,
            Height = 32,
            Margin = new Thickness(6, 0, 0, 0),
            Background = IslandPalette.FillStrong,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Template = MusicModule.RoundButtonTemplate(16),
        };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>Runs a media command without letting a failure reach the dispatcher.</summary>
    protected static async void Fire(Func<Task> command)
    {
        try
        {
            await command();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Trace.TraceWarning($"Faqra home: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

/// <summary>What is playing: artwork, title and artist, play or pause, next.</summary>
internal sealed class NowPlayingRow : HomeRow
{
    private readonly NowPlayingService _service;
    private readonly HomeStrings _s;
    private readonly Border _art = new() { Width = 44, Height = 44, CornerRadius = new CornerRadius(8) };
    private readonly TextBlock _title = Line(13, IslandPalette.Primary);
    private readonly TextBlock _artist = Line(12, IslandPalette.Secondary);
    private readonly TextBlock _playGlyph;
    private readonly Button _play;
    private readonly Button _next;

    public NowPlayingRow(NowPlayingService service, HomeStrings s) : base(HomeItem.NowPlaying)
    {
        _service = service;
        _s = s;
        _title.FontWeight = FontWeights.SemiBold;
        _play = IconButton("\uE768", s.PlayPause, () => Fire(_service.TogglePlayPauseAsync));
        _playGlyph = (TextBlock)_play.Content;
        _next = IconButton("\uE893", s.NextTrack, () => Fire(_service.NextAsync));

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var words = new StackPanel { Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(_title);
        words.Children.Add(_artist);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        buttons.Children.Add(_play);
        buttons.Children.Add(_next);
        Grid.SetColumn(words, 1);
        Grid.SetColumn(buttons, 2);
        grid.Children.Add(_art);
        grid.Children.Add(words);
        grid.Children.Add(buttons);
        View = AgentCardParts.Panel(grid);
        Update();
    }

    public override FrameworkElement View { get; }

    public override void Update()
    {
        var track = _service.Current;
        _title.Text = track.HasTrack ? track.Title : _s.NothingPlaying;
        _artist.Text = track.HasTrack ? track.Artist : string.Empty;
        _art.Background = IdleMusicView.Artwork(track.Artwork) is { } art
            ? new ImageBrush(art) { Stretch = Stretch.UniformToFill }
            : IslandPalette.FillStrong;
        _playGlyph.Text = track.IsPlaying ? "\uE769" : "\uE768";
        _play.IsEnabled = track.HasTrack;
        _next.IsEnabled = track.HasTrack;
    }
}

/// <summary>Up to three sessions, most urgent first; a waiting one turns the row's title to the accent.</summary>
internal sealed class AgentsRow : HomeRow
{
    private const int Slots = 3;
    private readonly AgentHub _hub;
    private readonly HomeStrings _s;
    private readonly AgentsStrings _words;
    private readonly TextBlock _label;
    private readonly TextBlock _empty;
    private readonly Button _more;
    private readonly Slot[] _slots;

    public AgentsRow(AgentHub hub, HomeStrings s, AgentsStrings words, Action<string> openSession, Action openSection) : base(HomeItem.Agents)
    {
        _hub = hub;
        _s = s;
        _words = words;
        _label = Label(s.ItemAgents);
        _empty = Line(12, IslandPalette.Secondary);
        _empty.Text = s.NoAgents;
        _empty.Margin = new Thickness(0, 6, 0, 0);
        _slots = Enumerable.Range(0, Slots).Select(_ => new Slot(s, openSession)).ToArray();
        _more = AgentCardParts.Quiet(string.Empty, (_, _) => openSection());

        var stack = new StackPanel();
        stack.Children.Add(_label);
        stack.Children.Add(_empty);
        foreach (var slot in _slots)
        {
            stack.Children.Add(slot.Button);
        }
        stack.Children.Add(_more);
        View = AgentCardParts.Panel(stack);
        Update();
    }

    public override FrameworkElement View { get; }

    /// <summary>True while the most urgent session waits on the owner.</summary>
    public bool Waiting => _hub.Board.Ordered.FirstOrDefault() is { } first && HomeItems.Waits(first.State);

    public override void Update()
    {
        var sessions = _hub.Board.Ordered;
        _label.Foreground = Waiting ? IslandPalette.Accent : IslandPalette.Tertiary;
        _empty.Visibility = sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        for (var i = 0; i < Slots; i++)
        {
            _slots[i].Show(i < sessions.Count ? sessions[i] : null, _words);
        }
        _more.Content = sessions.Count > Slots ? Format(_s.MoreAgentsFormat, sessions.Count - Slots) : null;
        _more.Visibility = sessions.Count > Slots ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>One session line: orb, name, status, and an Open pill when it waits. Reused as sessions come and go.</summary>
    private sealed class Slot
    {
        private readonly OrbView _orb = new() { Diameter = 16, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _name = Line(13, IslandPalette.Primary);
        private readonly TextBlock _status = Line(12, IslandPalette.Secondary);
        private readonly Border _open;
        private string? _sessionId;

        public Slot(HomeStrings s, Action<string> openSession)
        {
            _name.FontWeight = FontWeights.SemiBold;
            _open = new Border
            {
                Background = IslandPalette.Accent,
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(10, 3, 10, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = s.Open, FontFamily = AgentCardParts.TextFont, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = IslandPalette.Surface },
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var words = new StackPanel { Margin = new Thickness(10, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            words.Children.Add(_name);
            words.Children.Add(_status);
            Grid.SetColumn(words, 1);
            Grid.SetColumn(_open, 2);
            grid.Children.Add(_orb);
            grid.Children.Add(words);
            grid.Children.Add(_open);
            Button = new Button
            {
                Content = grid,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 6, 0, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Template = MusicModule.RoundButtonTemplate(8),
            };
            Button.Click += (_, _) =>
            {
                if (_sessionId is { } id)
                {
                    openSession(id);
                }
            };
        }

        public Button Button { get; }

        public void Show(AgentSession? session, AgentsStrings words)
        {
            _sessionId = session?.Id;
            Button.Visibility = session is null ? Visibility.Collapsed : Visibility.Visible;
            if (session is null)
            {
                return;
            }
            var style = AgentOrbStyles.For(session.State);
            _orb.Apply(style, AgentInk.For(style.Tone));
            _name.Text = session.Name;
            _status.Text = AgentsText.Status(session, words);
            _open.Visibility = HomeItems.Waits(session.State) ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetName(Button, $"{_name.Text}, {_status.Text}");
        }
    }
}

/// <summary>The running timer, or that none runs; the row opens the Timer section.</summary>
internal sealed class TimerRow : HomeRow
{
    private readonly IslandTimerService _timer;
    private readonly HomeStrings _s;
    private readonly TextBlock _value = Line(13, IslandPalette.Primary);

    public TimerRow(IslandTimerService timer, HomeStrings s, Action open) : base(HomeItem.Timer)
    {
        _timer = timer;
        _s = s;
        var stack = new StackPanel();
        stack.Children.Add(Label(s.ItemTimer));
        _value.Margin = new Thickness(0, 4, 0, 0);
        stack.Children.Add(_value);
        View = RowButton(stack, s.ItemTimer, open);
        Update();
    }

    public override FrameworkElement View { get; }

    public override void Update() => _value.Text = !_timer.HasSession
        ? _s.NoTimer
        : _timer.IsRunning
            ? Format(_s.TimerLeftFormat, IslandTimerService.Format(_timer.Remaining))
            : $"{Format(_s.TimerLeftFormat, IslandTimerService.Format(_timer.Remaining))}, {_s.TimerPaused.ToLower(CultureInfo.CurrentCulture)}";
}

/// <summary>Processor and memory use in one line; the row opens the System section.</summary>
internal sealed class SystemRow : HomeRow
{
    private readonly SystemMonitor _monitor;
    private readonly HomeStrings _s;
    private readonly TextBlock _cpu = Line(13, IslandPalette.Primary);
    private readonly TextBlock _memory = Line(13, IslandPalette.Primary);

    public SystemRow(SystemMonitor monitor, HomeStrings s, Action open) : base(HomeItem.System)
    {
        _monitor = monitor;
        _s = s;
        var values = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        _memory.Margin = new Thickness(20, 0, 0, 0);
        values.Children.Add(_cpu);
        values.Children.Add(_memory);
        var stack = new StackPanel();
        stack.Children.Add(Label(s.ItemSystem));
        stack.Children.Add(values);
        View = RowButton(stack, s.ItemSystem, open);
        Update();
    }

    public override FrameworkElement View { get; }

    public override void Update()
    {
        var snapshot = _monitor.Snapshot;
        _cpu.Text = Format(_s.CpuFormat, snapshot.CpuUsage is { } cpu && double.IsFinite(cpu) ? MetricFormat.Percent(cpu) : "…");
        _memory.Text = Format(_s.MemoryFormat,
            snapshot.MemoryUsed is { } used && snapshot.MemoryTotal is { } total && total > 0 ? MetricFormat.Percent((double)used / total) : "…");
    }
}

/// <summary>Charge and whether it charges.</summary>
internal sealed class BatteryRow : HomeRow
{
    private readonly HomeStrings _s;
    private readonly TextBlock _value = Line(13, IslandPalette.Primary);

    public BatteryRow(HomeStrings s) : base(HomeItem.Battery)
    {
        _s = s;
        var stack = new StackPanel();
        stack.Children.Add(Label(s.ItemBattery));
        _value.Margin = new Thickness(0, 4, 0, 0);
        stack.Children.Add(_value);
        View = AgentCardParts.Panel(stack);
        Update();
    }

    public override FrameworkElement View { get; }

    public override void Update()
    {
        var status = Power.BatteryStatus();
        var percent = status.Percent >= 0 ? $"{status.Percent}%" : "…";
        _value.Text = $"{percent}, {(status.Charging ? _s.BatteryCharging : _s.BatteryOnBattery).ToLower(CultureInfo.CurrentCulture)}";
    }
}
```

(Check `SystemSnapshot.CpuUsage`'s scale against `SystemModule`'s use of `MetricFormat.Percent`: it takes a fraction from 0 to 1. If `CpuUsage` is already a percentage, divide by 100.)

- [ ] **Step 4: Write `HomeModule`**

`src/Faqra.App/Island/Modules/HomeModule.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Faqra.Core.Defaults;
using Faqra.Core.Island;
using Faqra.Core.Localization;
using Faqra.Core.Metrics;
using Faqra.Services.Agents;
using Faqra.Services.Island;
using Faqra.Services.Media;
using Faqra.Services.Monitor;

namespace Faqra.App.Island.Modules;

/// <summary>
/// The island's Home: one row per item in the owner's order, each acting in place or opening its section. Edit turns
/// the rows into a list to reorder, remove and add, saved at once. Rows are built when the order changes and refreshed
/// in place from their sources the rest of the time.
/// </summary>
public sealed class HomeModule : UserControl
{
    private static readonly TimeSpan BatteryInterval = TimeSpan.FromSeconds(30);

    private readonly ISettingsStore _store;
    private readonly NowPlayingService _nowPlaying;
    private readonly AgentHub _agents;
    private readonly IslandTimerService _timer;
    private readonly SystemMonitor _monitor;
    private readonly Func<HomeItem, bool> _isAvailable;
    private readonly HomeStrings _s = HomeStrings.For(L10n.Shared.Language);
    private readonly AgentsStrings _agentWords = AgentsStrings.For(L10n.Shared.Language);
    private readonly StackPanel _body = new() { Margin = new Thickness(12, 0, 12, 12) };
    private readonly DispatcherTimer _battery = new() { Interval = BatteryInterval };
    private readonly List<HomeRow> _rows = [];

    public HomeModule(ISettingsStore store, NowPlayingService nowPlaying, AgentHub agents, IslandTimerService timer, SystemMonitor monitor, Func<HomeItem, bool> isAvailable)
    {
        _store = store;
        _nowPlaying = nowPlaying;
        _agents = agents;
        _timer = timer;
        _monitor = monitor;
        _isAvailable = isAvailable;
        Content = new ScrollViewer { Content = _body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Items = HomeItems.Parse(_store.String(DefaultsKey.FaqraHomeItems), _isAvailable);
        Build();

        _nowPlaying.Changed += OnNowPlaying;
        _agents.Changed += OnAgents;
        _timer.Changed += OnTimer;
        _monitor.SnapshotChanged += OnSnapshot;
        _battery.Tick += (_, _) => Refresh(HomeItem.Battery);
        Loaded += (_, _) => _battery.Start();
        Unloaded += (_, _) =>
        {
            _battery.Stop();
            _nowPlaying.Changed -= OnNowPlaying;
            _agents.Changed -= OnAgents;
            _timer.Changed -= OnTimer;
            _monitor.SnapshotChanged -= OnSnapshot;
        };
    }

    /// <summary>A row asked for its section (Timer, System, Agents).</summary>
    public event Action<IslandModule>? SectionRequested;

    /// <summary>A session row asked for that session's card in the Agents section.</summary>
    public event Action<string>? SessionRequested;

    public IReadOnlyList<HomeItem> Items { get; private set; }

    public bool Editing { get; private set; }

    /// <summary>Whether a row is on Home, so the island knows to sample the system.</summary>
    public bool Shows(HomeItem item) => Items.Contains(item);

    public void BeginEdit()
    {
        Editing = true;
        Build();
    }

    public void EndEdit()
    {
        Editing = false;
        Build();
    }

    private void Build()
    {
        _body.Children.Clear();
        _rows.Clear();
        if (Editing)
        {
            BuildEditor();
            return;
        }
        if (Items.Count == 0)
        {
            _body.Children.Add(AgentCardParts.Caption(_s.Empty));
        }
        foreach (var item in Items)
        {
            var row = CreateRow(item);
            _rows.Add(row);
            _body.Children.Add(row.View);
        }
        var edit = AgentCardParts.Quiet(_s.Edit, (_, _) => BeginEdit());
        AutomationProperties.SetName(edit, _s.Edit);
        _body.Children.Add(edit);
    }

    private HomeRow CreateRow(HomeItem item) => item switch
    {
        HomeItem.NowPlaying => new NowPlayingRow(_nowPlaying, _s),
        HomeItem.Agents => new AgentsRow(_agents, _s, _agentWords, id => SessionRequested?.Invoke(id), () => SectionRequested?.Invoke(IslandModule.FaqraAgents)),
        HomeItem.Timer => new TimerRow(_timer, _s, () => SectionRequested?.Invoke(IslandModule.Timer)),
        HomeItem.System => new SystemRow(_monitor, _s, () => SectionRequested?.Invoke(IslandModule.System)),
        HomeItem.Battery => new BatteryRow(_s),
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null),
    };

    /// <summary>The rows as a list with Move up, Move down and Remove, then the rows that can be added, then Done.</summary>
    private void BuildEditor()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var name = Name(item);
            var line = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(EditButton("\uE70E", Format(_s.MoveUpFormat, name), i > 0, () => Change(HomeItems.Move(Items, item, -1))));
            buttons.Children.Add(EditButton("\uE70D", Format(_s.MoveDownFormat, name), i < Items.Count - 1, () => Change(HomeItems.Move(Items, item, 1))));
            buttons.Children.Add(EditButton("\uE711", Format(_s.RemoveFormat, name), true, () => Change(HomeItems.Remove(Items, item))));
            DockPanel.SetDock(buttons, Dock.Right);
            line.Children.Add(buttons);
            var label = AgentCardParts.Title(name);
            label.VerticalAlignment = VerticalAlignment.Center;
            line.Children.Add(label);
            _body.Children.Add(AgentCardParts.Panel(line));
        }
        var addable = HomeItems.Addable(Items, _isAvailable);
        if (addable.Count > 0)
        {
            var header = AgentCardParts.Caption(_s.AddHeader);
            header.Margin = new Thickness(0, 6, 0, 0);
            _body.Children.Add(header);
            var adds = new WrapPanel { Margin = new Thickness(-8, 0, 0, 0) };
            foreach (var item in addable)
            {
                var add = AgentCardParts.Action(Format(_s.AddFormat, Name(item)), primary: false, (_, _) => Change(HomeItems.Add(Items, item)));
                add.Margin = new Thickness(8, 8, 0, 0);
                AutomationProperties.SetName(add, Format(_s.AddFormat, Name(item)));
                adds.Children.Add(add);
            }
            _body.Children.Add(adds);
        }
        var done = AgentCardParts.Action(_s.Done, primary: true, (_, _) => EndEdit());
        AutomationProperties.SetName(done, _s.Done);
        _body.Children.Add(AgentCardParts.Footer(AgentCardParts.Actions(done), quiet: null));
    }

    private Button EditButton(string glyph, string name, bool enabled, Action click)
    {
        var button = AgentCardParts.Action(glyph, primary: false, (_, _) => click());
        button.FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        button.MinWidth = 32;
        button.Width = 32;
        button.Padding = new Thickness(0);
        button.Margin = new Thickness(6, 0, 0, 0);
        button.IsEnabled = enabled;
        AutomationProperties.SetName(button, name);
        return button;
    }

    private void Change(IReadOnlyList<HomeItem> items)
    {
        _store.Set(DefaultsKey.FaqraHomeItems, HomeItems.Saved(items, _store.String(DefaultsKey.FaqraHomeItems)));
        Items = items;
        Build();
    }

    private string Name(HomeItem item) => item switch
    {
        HomeItem.NowPlaying => _s.ItemNowPlaying,
        HomeItem.Agents => _s.ItemAgents,
        HomeItem.Timer => _s.ItemTimer,
        HomeItem.System => _s.ItemSystem,
        HomeItem.Battery => _s.ItemBattery,
        _ => item.RawValue(),
    };

    private static string Format(string format, object value) => string.Format(CultureInfo.CurrentCulture, format, value);

    private void Refresh(HomeItem item)
    {
        foreach (var row in _rows.Where(row => row.Item == item))
        {
            row.Update();
        }
    }

    // Sources may raise their events off the UI thread; every refresh runs on this control's dispatcher.
    private void OnNowPlaying() => Dispatcher.BeginInvoke(() => Refresh(HomeItem.NowPlaying));

    private void OnAgents() => Refresh(HomeItem.Agents);

    private void OnTimer() => Dispatcher.BeginInvoke(() => Refresh(HomeItem.Timer));

    private void OnSnapshot(SystemSnapshot _) => Dispatcher.BeginInvoke(() => Refresh(HomeItem.System));
}
```

(`AgentHub.Changed` is raised on the island's thread, as `AgentsModule` relies on. Check `NowPlayingService.Changed`'s and `IslandTimerService.Changed`'s signatures: if either passes an argument, match it in the handler.)

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.App.Tests --filter "FullyQualifiedName~HomeModuleTests"`, then `dotnet test tests/Faqra.App.Tests`.
Expected: PASS. Open `island-home.png`, `island-home-edit.png` and `island-home-waiting.png` in `%TEMP%\faqra-ui` and describe them in the report: rows 10 px apart in `Fill` panels, the edit list's buttons right-aligned with names left, only the Agents title in the accent when a session waits, nothing cut at 412 px.

- [ ] **Step 6: Commit**

```bash
git add src/Faqra.App/Island/Modules/HomeRows.cs src/Faqra.App/Island/Modules/HomeModule.cs tests/Faqra.App.Tests/HomeModuleTests.cs
git commit -m "feat(windows): island Home section with editable rows"
```

---

### Task 3: The island opens on Home

**Files:**
- Create: `src/Faqra.App/Island/IslandController.Home.cs`
- Modify: `src/Faqra.App/Island/IslandController.cs`, `src/Faqra.App/Island/Modules/SectionPicker.cs`
- Test: `tests/Faqra.App.Tests/HomeModuleTests.cs` (add)

**Interfaces:**
- Consumes: `IslandModules.Opening`, `IslandModule.FaqraHome`, `HomeItem` (Task 1); `HomeModule` and its events (Task 2); the controller's `Expand`, `_agentsFocus`, `VisibleModules`, `_runtime`, `_systemMonitor`, `_store`, `_nowPlaying`, `_agents` (A1 to A3).
- Produces: hover and click open Home while it is shown; Home rows open their section, and a session row opens that session's card; the system is sampled while Home shows a System row.

- [ ] **Step 1: Write the failing test**

Add to `tests/Faqra.App.Tests/HomeModuleTests.cs` (inside the class):

```csharp
    [Fact]
    public void TheSectionListNamesHome() => StaThread.Run(() =>
    {
        var picker = new SectionPicker([IslandModule.FaqraHome, IslandModule.Music], _ => { });
        Assert.Contains("Home", AgentsRenderTests.AllText(picker));
    });
```

(Check `SectionPicker`'s constructor; A1's code builds it with `new SectionPicker(VisibleModules(), module => …)`.)

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Faqra.App.Tests --filter "FullyQualifiedName~TheSectionListNamesHome"`
Expected: FAIL (the tile reads `faqraHome`).

- [ ] **Step 3: Wire Home into the island**

1. In `src/Faqra.App/Island/Modules/SectionPicker.cs`'s `Title` switch, add `IslandModule.FaqraHome => "Home",`.

2. Create `src/Faqra.App/Island/IslandController.Home.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Faqra.App.Island.Modules;
using Faqra.Core.Features;
using Faqra.Core.Island;

namespace Faqra.App.Island;

/// <summary>The island's side of Home: building it, where its rows lead, and which rows Windows can fill.</summary>
public sealed partial class IslandController
{
    private HomeModule? _homeModule;

    private HomeModule BuildHomeModule()
    {
        var module = new HomeModule(_store, _nowPlaying, _agents, AppServices.Current.Timer, _systemMonitor, HomeItemAvailable);
        module.SectionRequested += target => Expand(takeFocus: false, target);
        module.SessionRequested += sessionId =>
        {
            _agentsFocus = sessionId;
            Expand(takeFocus: false, IslandModule.FaqraAgents);
        };
        _homeModule = module;
        return module;
    }

    /// <summary>A Home row is offered only when its source exists on this PC.</summary>
    private bool HomeItemAvailable(HomeItem item) => item switch
    {
        HomeItem.NowPlaying => true,
        HomeItem.Agents => IslandModule.FaqraAgents.IsAvailable(_runtime.IsAvailable),
        HomeItem.Timer => IslandModule.Timer.IsAvailable(_runtime.IsAvailable),
        HomeItem.System => IslandModule.System.IsAvailable(_runtime.IsAvailable),
        HomeItem.Battery => _systemMonitor.HasBattery,
        _ => false,
    };

    /// <summary>Home is open with a System row, so the monitor should sample as it does for the System section.</summary>
    private bool HomeShowsSystem() => _module == IslandModule.FaqraHome && _homeModule?.Shows(HomeItem.System) == true;
}
```

3. In `src/Faqra.App/Island/IslandController.cs`:
   - In `ModuleTitle`, add `IslandModule.FaqraHome => Faqra.Core.Localization.HomeStrings.For(L10n.Shared.Language).Title,`.
   - In `BuildModule`, add `_homeModule = null;` next to `_agentsModule = null;`, and the arm `IslandModule.FaqraHome => BuildHomeModule(),`.
   - Replace `DefaultModule()`'s body with `return IslandModules.Opening(_module, VisibleModules()) ?? IslandModule.Controls;` and its summary (if any) with `/// <summary>Home while it is shown, else the section in use, else the first.</summary>`.
   - Change the sampling line (`_systemMonitor.SetNotchVisible(expanded && _module == IslandModule.System && !_sectionsOpen);`) to:

```csharp
        _systemMonitor.SetNotchVisible(expanded && !_sectionsOpen && (_module == IslandModule.System || HomeShowsSystem()));
```

(`SetNotchVisible` is called during `Render`, which runs after `BuildModule`, so `_homeModule` is current.)

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test` (whole solution).
Expected: PASS, no build warnings. App tests that count section tiles or assume the first section is Controls move by one (Home is first now); change only those counts and firsts, and list each in the report.

- [ ] **Step 5: Commit**

```bash
git add src/Faqra.App/Island tests/Faqra.App.Tests
git commit -m "feat(windows): island opens on Home and Home's rows lead to their sections"
```

---

### Task 4: Live check and docs (controller, with the owner)

Run by the controller. Ask before replacing Faqra-test.

- [ ] **Step 1:** Full suite: `dotnet test`, PASS with no warnings.
- [ ] **Step 2:** Publish Faqra-test (ask first; same commands as A3), start it.
- [ ] **Step 3:** Check by UI Automation and screenshots:
  - hovering the island opens Home;
  - picking Music from the section list shows Music, and after collapsing, hovering opens Home again;
  - Edit Home moves a row and the order survives restarting Faqra-test;
  - with the Agents row on Home, a waiting agent turns its title to the accent.
- [ ] **Step 4:** Docs. In `design-system.md`, add a row for Home: rows in `Fill` panels 10 px apart, the Agents title in `Accent` only while an agent waits, Edit as a quiet text button under the rows, the edit list with Move up, Move down and Remove icon buttons. In the H1/H2 spec's milestone table, mark H1a done.
- [ ] **Step 5:** Commit (`docs(windows): Home checked live`), push, update the memory file: H1a done, next H1b.
