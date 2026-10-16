# Faqra Calendar (H1b) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Read the owner's private calendar links and show their next event on Home and their week in the island's Calendar section.

**Architecture:**
- **Core** reads an ICS feed into event occurrences with Ical.Net and holds the pure rules: the next event, how its time reads, "starts within 10 minutes", meeting links and accepted link forms.
- **Win32** wraps Windows' per-user data protection (DPAPI) in two calls.
- **Services** keeps the links (protected, inside Faqra's settings), fetches them every 15 minutes, on wake and when the network returns, keeps each feed's last good copy (protected, on disk), and publishes a snapshot.
- **App** gets a Calendar page in Settings, the island's Calendar section (a week strip over the chosen day's events), and Home's Next event row.

**Tech Stack:** C# / .NET 8, WPF + WPF UI 4.3, xunit, Ical.Net 5.2.3 (MIT) with NodaTime (Apache 2.0), System.Net.Http, P/Invoke to crypt32.

**Spec:** `docs/superpowers/specs/2026-10-12-faqra-home-calendar-shelf-design.md` ("Calendar (H1)", "Home (H1)" Next event row and outlier, "Risks", "Testing").

## Global Constraints

- **Build and files**
  - Run every `dotnet` command from `C:\Users\Tigre\vorssaint-utils-windows\Windows`.
  - New `src/` files carry the two-line SPDX header; test files carry none.
  - Nullable warnings are errors, and the build has no warnings.
- **New packages**
  - Exactly two: `Ical.Net` 5.2.3 and its dependency `NodaTime`. Both go in `Directory.Packages.props` (central management) and are referenced by `Faqra.Core` only.
  - Both get entries in `THIRD-PARTY-NOTICES.md`.
- **The link is a password**
  - Links are stored only DPAPI-protected (current user), as one settings value `faqraCalendarLinks` (bytes).
  - Links are shown masked (host, then `…`, then the last 4 characters).
  - A link is never written to a log, `Trace`, an exception message or a `ToString()`.
  - A link is only ever sent to its own host.
- **Accepted links**
  - Accepted: absolute `https://` URLs, and `webcal://` read as `https://`. Anything else is refused with a sentence.
- **Fetching**
  - Every 15 minutes, when the feature starts, on resume from sleep, and when the network becomes available.
  - Timeout 20 s, at most 5 MB per feed.
  - 401, 403 and 404 mark a link broken. Any other failure keeps the last good copy and marks the link failing.
- **Reading**
  - Occurrences from 14 days back to 14 days ahead, so the previous week the Calendar section shows is real.
  - Repeats, exceptions, moved occurrences, time zones (IANA and Windows names) and all-day events are handled.
  - `STATUS:CANCELLED` is skipped.
- **Last good copy**
  - Kept per feed in `%LOCALAPPDATA%\Faqra\calendar\<16 hex of SHA-256 of the link>.dat`, DPAPI-protected and written atomically (temp file, then move).
- **Next event**
  - The earliest timed event that has not ended. If there is none, an all-day event covering today. If neither, "Nothing in the next 2 weeks".
- **How the time reads**
  - "Now" while it runs.
  - "in N min" under an hour.
  - The local time today.
  - "Tomorrow HH:mm".
  - Otherwise the short weekday and the time.
  - All-day events read "Today", "Tomorrow" or the weekday.
- **Accent (outlier)**
  - The Next event row wears the accent when its event starts within 10 minutes (not yet started), unless an agent waits. The agent wins.
- **Join**
  - Shown only for https links to Google Meet (`meet.google.com`), Zoom (`*.zoom.us/j/`), Teams (`teams.microsoft.com/l/meetup-join`, `teams.live.com/meet`) and Webex (`*.webex.com`).
  - Taken from the location, then the description, then the event URL.
- **Copy**
  - Sentence case, no em or en dashes, no exclamation marks.
  - Time ranges read "09:30 to 10:00".
- **Process**
  - Commits use conventional prefixes, with no `Co-Authored-By` or Claude attribution.
  - WPF tests run in `StaThread.Run(...)`.
  - Subagents run on Sonnet.

## Review Focus

1. **A feed that changes shape keeps the island working.** This covers a garbage response, an HTML login page, an empty calendar, and a huge feed. Each shows the last good events or a sentence, and nothing throws. Pinned in Task 1 (`ANonCalendarIsNoCalendar`) and Task 2 (`AnHtmlPageKeepsTheLastGoodCopy`, `AFeedOverTheLimitIsRefused`).
2. **The link never leaks.** It is not in settings as plain text, not in the cache, and not in `ToString()`. Pinned in Task 2 (`LinksAreStoredProtected`, `TheCacheIsProtected`, `ASnapshotNeverPrintsALink`).
3. **Times are right across time zones and daylight saving.** Outlook's Windows zone names and Google's IANA names both read correctly. Pinned in Task 1 (`ReadsGoogleRepeatsExceptionsMovesAndAllDay`, `ReadsOutlooksWindowsTimeZone`).
4. **Offline at start-up still shows the week from the last good copy.** Pinned in Task 2 (`StartingOfflineShowsTheLastGoodCopy`).
5. **The Next event row never steals the outlier from a waiting agent.** Pinned in Task 5 (`AWaitingAgentKeepsTheAccent`).

---

## File structure

| File | Responsibility |
|---|---|
| `src/Faqra.Core/Calendar/CalendarEvent.cs`, `CalendarFeed.cs`, `CalendarRules.cs` (new) | Occurrences from ICS; next event, time words, meeting links, link forms |
| `src/Faqra.Core/Localization/CalendarStrings.cs`, `CalendarStrings.EnUS.cs` (new) | Calendar words |
| `src/Faqra.Win32/Security/Dpapi.cs` (new) | Protect and unprotect bytes for the current user |
| `src/Faqra.Services/Calendar/CalendarService.cs`, `CalendarSnapshot.cs` (new) | Links, fetching, last good copies, snapshot |
| `src/Faqra.Core/Features/*`, `Defaults/DefaultsMigrations.cs`, `Settings/SettingsPage.cs` (modify) | The feature is built and installed once; Settings page wiring |
| `src/Faqra.App/Settings/Pages/CalendarPage.cs` (new), `SettingsWindow.xaml.cs`, `AppServices.cs` (modify) | Settings page; service lifetime, resume hook |
| `src/Faqra.App/Island/Modules/CalendarModule.cs` (new), `IslandController.cs` (modify) | The Calendar section |
| `src/Faqra.Core/Island/HomeItems.cs`, `src/Faqra.App/Island/Modules/HomeRows.cs`, `HomeModule.cs`, `IslandController.Home.cs` (modify) | Next event row, shared outlier, icon button tooltip |

---

### Task 1: Reading feeds and the calendar rules (Core)

**Files:**
- Modify: `Directory.Packages.props`, `src/Faqra.Core/Faqra.Core.csproj`, `THIRD-PARTY-NOTICES.md`
- Create: `src/Faqra.Core/Calendar/CalendarEvent.cs`, `CalendarFeed.cs`, `CalendarRules.cs`, `src/Faqra.Core/Localization/CalendarStrings.cs`, `CalendarStrings.EnUS.cs`
- Test: `tests/Faqra.Core.Tests/Calendar/CalendarFeedTests.cs`, `CalendarRulesTests.cs`, `tests/Faqra.Core.Tests/Calendar/Data/google.ics`, `outlook.ics` (new; copied to the output folder)

**Interfaces:**
- Produces:
  - `sealed record CalendarEvent(string Title, string? Location, DateTimeOffset Start, DateTimeOffset End, bool AllDay, Uri? JoinLink)`.
  - `static class CalendarFeed`:
    - `IReadOnlyList<CalendarEvent>? Read(string ics, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo local)`: null when the text is not a calendar.
  - `static partial class CalendarRules`:
    - `const int SoonMinutes = 10`.
    - `CalendarEvent? Next(IReadOnlyList<CalendarEvent> events, DateTimeOffset now, TimeZoneInfo local)`.
    - `bool StartsSoon(CalendarEvent e, DateTimeOffset now)`.
    - `string When(CalendarEvent e, DateTimeOffset now, TimeZoneInfo local, CalendarStrings s, CultureInfo culture)`.
    - `string Range(CalendarEvent e, TimeZoneInfo local, CalendarStrings s, CultureInfo culture)`.
    - `Uri? JoinLink(params string?[] texts)`.
    - `Uri? FeedLink(string? input)`.
    - `string Mask(Uri link)`.
  - `CalendarStrings`, with the members listed in Step 4.

- [ ] **Step 1: Add the packages**

In `Directory.Packages.props`, add next to the other `PackageVersion` lines:

```xml
    <PackageVersion Include="Ical.Net" Version="5.2.3" />
```

In `src/Faqra.Core/Faqra.Core.csproj`, add an `ItemGroup` with `<PackageReference Include="Ical.Net" />`. NodaTime comes with it. Run `dotnet restore`. If restore reports that NodaTime needs pinning under central management, add `<PackageVersion Include="NodaTime" Version="<the version restore names>" />` too.

In `THIRD-PARTY-NOTICES.md`, add entries in the file's existing style:
- Ical.Net 5.2.3, MIT, https://github.com/ical-org/ical.net
- NodaTime, Apache License 2.0, https://nodatime.org

- [ ] **Step 2: Write the fixtures and the failing tests**

`tests/Faqra.Core.Tests/Calendar/Data/google.ics` (Google's shape: IANA zones, a weekly repeat with an excluded day, a moved occurrence, an all-day event, a cancelled event, a Zoom link in the description):

```
BEGIN:VCALENDAR
PRODID:-//Google Inc//Google Calendar 70.9054//EN
VERSION:2.0
CALSCALE:GREGORIAN
X-WR-CALNAME:Owner
X-WR-TIMEZONE:America/Toronto
BEGIN:VEVENT
DTSTART;TZID=America/Toronto:20261019T093000
DTEND;TZID=America/Toronto:20261019T100000
RRULE:FREQ=WEEKLY;BYDAY=MO,WE,FR
EXDATE;TZID=America/Toronto:20261021T093000
DTSTAMP:20261015T120000Z
UID:standup@google.com
SUMMARY:Standup
LOCATION:https://meet.google.com/abc-defg-hij
STATUS:CONFIRMED
END:VEVENT
BEGIN:VEVENT
DTSTART;TZID=America/Toronto:20261023T093000
DTEND;TZID=America/Toronto:20261023T103000
RECURRENCE-ID;TZID=America/Toronto:20261023T093000
DTSTAMP:20261015T120000Z
UID:standup@google.com
SUMMARY:Standup (moved)
STATUS:CONFIRMED
END:VEVENT
BEGIN:VEVENT
DTSTART;VALUE=DATE:20261020
DTEND;VALUE=DATE:20261021
DTSTAMP:20261015T120000Z
UID:holiday@google.com
SUMMARY:Day off
END:VEVENT
BEGIN:VEVENT
DTSTART:20261022T180000Z
DTEND:20261022T190000Z
DTSTAMP:20261015T120000Z
UID:cancelled@google.com
SUMMARY:Cancelled call
STATUS:CANCELLED
END:VEVENT
BEGIN:VEVENT
DTSTART:20261022T150000Z
DTEND:20261022T160000Z
DTSTAMP:20261015T120000Z
UID:review@google.com
SUMMARY:Design review
DESCRIPTION:Join Zoom Meeting\nhttps://us02web.zoom.us/j/123456789
END:VEVENT
END:VCALENDAR
```

`tests/Faqra.Core.Tests/Calendar/Data/outlook.ics` (Outlook's shape: a Windows zone name with its own VTIMEZONE, a Teams link):

```
BEGIN:VCALENDAR
METHOD:PUBLISH
PRODID:Microsoft Exchange Server 2010
VERSION:2.0
X-WR-CALNAME:Calendar
BEGIN:VTIMEZONE
TZID:Eastern Standard Time
BEGIN:STANDARD
DTSTART:16010101T020000
TZOFFSETFROM:-0400
TZOFFSETTO:-0500
RRULE:FREQ=YEARLY;INTERVAL=1;BYDAY=1SU;BYMONTH=11
END:STANDARD
BEGIN:DAYLIGHT
DTSTART:16010101T020000
TZOFFSETFROM:-0500
TZOFFSETTO:-0400
RRULE:FREQ=YEARLY;INTERVAL=1;BYDAY=2SU;BYMONTH=3
END:DAYLIGHT
END:VTIMEZONE
BEGIN:VEVENT
DESCRIPTION:Microsoft Teams meeting\nJoin: https://teams.microsoft.com/l/meetup-join/19%3ameeting_abc/0?context=x
UID:040000008200E00074C5B7101A82E00800000000
SUMMARY:Planning
DTSTART;TZID=Eastern Standard Time:20261020T140000
DTEND;TZID=Eastern Standard Time:20261020T150000
DTSTAMP:20261015T120000Z
STATUS:CONFIRMED
LOCATION:Microsoft Teams Meeting
END:VEVENT
END:VCALENDAR
```

In `tests/Faqra.Core.Tests/Faqra.Core.Tests.csproj`, copy them to the output: `<None Update="Calendar\Data\*.ics" CopyToOutputDirectory="PreserveNewest" />`. The existing `Agents/Data` folder may already show the pattern; follow it.

`tests/Faqra.Core.Tests/Calendar/CalendarFeedTests.cs`:

```csharp
using Faqra.Core.Calendar;

namespace Faqra.Core.Tests.Calendar;

public class CalendarFeedTests
{
    private static readonly TimeZoneInfo Toronto = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Calendar", "Data", name));

    private static readonly DateTimeOffset From = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReadsGoogleRepeatsExceptionsMovesAndAllDay()
    {
        var events = CalendarFeed.Read(Fixture("google.ics"), From, To, Toronto)!;
        Assert.Equal(
            ["Standup", "Day off", "Design review", "Standup (moved)"],
            events.Select(e => e.Title));

        var standup = events[0];
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 13, 30, 0, TimeSpan.Zero), standup.Start.ToUniversalTime());
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 14, 0, 0, TimeSpan.Zero), standup.End.ToUniversalTime());
        Assert.Equal(new Uri("https://meet.google.com/abc-defg-hij"), standup.JoinLink);

        var dayOff = events[1];
        Assert.True(dayOff.AllDay);
        Assert.Equal(new DateTime(2026, 10, 20), dayOff.Start.Date);
        Assert.Equal(new DateTime(2026, 10, 21), dayOff.End.Date);

        Assert.Equal(new Uri("https://us02web.zoom.us/j/123456789"), events[2].JoinLink);
        var moved = events[3];
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 14, 30, 0, TimeSpan.Zero), moved.End.ToUniversalTime());
        Assert.DoesNotContain(events, e => e.Title == "Cancelled call");
        Assert.DoesNotContain(events, e => e.Start.ToUniversalTime().Date == new DateTime(2026, 10, 21));
    }

    [Fact]
    public void ReadsOutlooksWindowsTimeZone()
    {
        var planning = Assert.Single(CalendarFeed.Read(Fixture("outlook.ics"), From, To, Toronto)!);
        Assert.Equal("Planning", planning.Title);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 18, 0, 0, TimeSpan.Zero), planning.Start.ToUniversalTime());
        Assert.Equal("teams.microsoft.com", planning.JoinLink!.Host);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a calendar")]
    [InlineData("<html><body>Sign in</body></html>")]
    public void ANonCalendarIsNoCalendar(string text) => Assert.Null(CalendarFeed.Read(text, From, To, Toronto));

    [Fact]
    public void AnEmptyCalendarHasNoEvents() =>
        Assert.Empty(CalendarFeed.Read("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:x\r\nEND:VCALENDAR\r\n", From, To, Toronto)!);
}
```

`tests/Faqra.Core.Tests/Calendar/CalendarRulesTests.cs`:

```csharp
using System.Globalization;
using Faqra.Core.Calendar;
using Faqra.Core.Localization;

namespace Faqra.Core.Tests.Calendar;

public class CalendarRulesTests
{
    private static readonly TimeZoneInfo Toronto = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
    private static readonly CalendarStrings S = CalendarStrings.EnUS;
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-CA");

    // Monday 19 October 2026, 09:00 in Toronto (13:00 UTC).
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 13, 0, 0, TimeSpan.Zero);

    private static CalendarEvent At(string title, int minutesFromNow, int lengthMinutes = 30) =>
        new(title, null, Now.AddMinutes(minutesFromNow), Now.AddMinutes(minutesFromNow + lengthMinutes), false, null);

    private static CalendarEvent AllDay(string title, int daysFromToday)
    {
        var day = new DateTimeOffset(2026, 10, 19, 0, 0, 0, Toronto.GetUtcOffset(new DateTime(2026, 10, 19))).AddDays(daysFromToday);
        return new(title, null, day, day.AddDays(1), true, null);
    }

    [Fact]
    public void TheNextEventIsTheEarliestThatHasNotEnded()
    {
        IReadOnlyList<CalendarEvent> events = [At("Ended", -60), AllDay("Day off", 0), At("Later", 120), At("Running", -10, 30)];
        Assert.Equal("Running", CalendarRules.Next(events, Now, Toronto)!.Title);
        Assert.Equal("Day off", CalendarRules.Next([AllDay("Day off", 0), At("Ended", -60)], Now, Toronto)!.Title);
        Assert.Null(CalendarRules.Next([AllDay("Tomorrow off", 1), At("Ended", -60)], Now, Toronto));
        Assert.Null(CalendarRules.Next([], Now, Toronto));
    }

    [Fact]
    public void TheTimeReadsTheWayAPersonSaysIt()
    {
        Assert.Equal(S.Now, CalendarRules.When(At("x", -5), Now, Toronto, S, En));
        Assert.Equal("in 25 min", CalendarRules.When(At("x", 25), Now, Toronto, S, En));
        Assert.Equal("14:30", CalendarRules.When(At("x", 330), Now, Toronto, S, CultureInfo.GetCultureInfo("fr-CA")));
        Assert.Equal("Tomorrow 09:30", CalendarRules.When(At("x", 24 * 60 + 30), Now, Toronto, S, CultureInfo.GetCultureInfo("fr-CA")));
        Assert.Equal(S.Today, CalendarRules.When(AllDay("x", 0), Now, Toronto, S, En));
        Assert.Equal(S.Tomorrow, CalendarRules.When(AllDay("x", 1), Now, Toronto, S, En));
        Assert.Equal("09:00 to 09:30", CalendarRules.Range(At("x", 0), Toronto, S, CultureInfo.GetCultureInfo("fr-CA")));
        Assert.Equal(S.AllDay, CalendarRules.Range(AllDay("x", 0), Toronto, S, En));
    }

    [Fact]
    public void SoonMeansStartingWithinTenMinutes()
    {
        Assert.True(CalendarRules.StartsSoon(At("x", 10), Now));
        Assert.True(CalendarRules.StartsSoon(At("x", 1), Now));
        Assert.False(CalendarRules.StartsSoon(At("x", 11), Now));
        Assert.False(CalendarRules.StartsSoon(At("x", -1), Now));
        Assert.False(CalendarRules.StartsSoon(AllDay("x", 0), Now));
    }

    [Theory]
    [InlineData("https://meet.google.com/abc-defg-hij", "meet.google.com")]
    [InlineData("Join: https://us02web.zoom.us/j/123456789?pwd=x then", "us02web.zoom.us")]
    [InlineData("https://teams.microsoft.com/l/meetup-join/19%3ameeting/0", "teams.microsoft.com")]
    [InlineData("https://teams.live.com/meet/9876", "teams.live.com")]
    [InlineData("https://acme.webex.com/meet/owner", "acme.webex.com")]
    public void FindsMeetingLinks(string text, string host) => Assert.Equal(host, CalendarRules.JoinLink(null, text)!.Host);

    [Theory]
    [InlineData("http://meet.google.com/abc")]
    [InlineData("https://example.com/meet")]
    [InlineData("https://zoom.us.evil.com/j/1")]
    [InlineData("Microsoft Teams Meeting")]
    [InlineData(null)]
    public void IgnoresAnythingElse(string? text) => Assert.Null(CalendarRules.JoinLink(text));

    [Theory]
    [InlineData("https://calendar.google.com/calendar/ical/x/private-abc/basic.ics", "https://calendar.google.com/calendar/ical/x/private-abc/basic.ics")]
    [InlineData("  webcal://p01-caldav.icloud.com/published/2/abc  ", "https://p01-caldav.icloud.com/published/2/abc")]
    public void AcceptsHttpsAndWebcalLinks(string input, string expected) => Assert.Equal(new Uri(expected), CalendarRules.FeedLink(input));

    [Theory]
    [InlineData("http://calendar.google.com/x.ics")]
    [InlineData("file:///C:/x.ics")]
    [InlineData("calendar.google.com/x.ics")]
    [InlineData("")]
    [InlineData(null)]
    public void RefusesOtherLinks(string? input) => Assert.Null(CalendarRules.FeedLink(input));

    [Theory]
    [InlineData("https://calendar.google.com/calendar/ical/x/private-abc9/basic.ics", "calendar.google.com…abc9")]
    [InlineData("https://p01-caldav.icloud.com/published/2/XYZ12", "p01-caldav.icloud.com…YZ12")]
    [InlineData("https://outlook.office365.com/owa/calendar/a@b.com/f00d/calendar.ics", "outlook.office365.com…f00d")]
    public void AMaskedLinkShowsOnlyTheHostAndTheEnd(string link, string masked) =>
        Assert.Equal(masked, CalendarRules.Mask(new Uri(link)));
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~Calendar"`
Expected: build FAILS with "The type or namespace name 'Calendar' does not exist in the namespace 'Faqra.Core'".

- [ ] **Step 4: Write the model, the feed reader, the rules and the words**

`src/Faqra.Core/Calendar/CalendarEvent.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

namespace Faqra.Core.Calendar;

/// <summary>
/// One occurrence of a calendar event. Start and End are instants; for an all-day event they are local midnights and
/// End is the day after the last day.
/// </summary>
public sealed record CalendarEvent(string Title, string? Location, DateTimeOffset Start, DateTimeOffset End, bool AllDay, Uri? JoinLink);
```

`src/Faqra.Core/Calendar/CalendarFeed.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Ical.Net.DataTypes;
using IcalCalendar = Ical.Net.Calendar;
using IcalEvent = Ical.Net.CalendarComponents.CalendarEvent;

namespace Faqra.Core.Calendar;

/// <summary>An ICS feed read into the occurrences of a window: repeats, exceptions, moved occurrences, time zones, all-day events.</summary>
public static class CalendarFeed
{
    /// <summary>The occurrences that overlap [from, to), oldest first; null when the text is not a calendar.</summary>
    public static IReadOnlyList<CalendarEvent>? Read(string ics, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo local)
    {
        if (!ics.Contains("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        IcalCalendar? calendar;
        try
        {
            calendar = IcalCalendar.Load(ics);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
        if (calendar is null)
        {
            return null;
        }
        var events = new List<CalendarEvent>();
        var start = new CalDateTime(from.UtcDateTime, "UTC");
        var end = new CalDateTime(to.UtcDateTime, "UTC");
        try
        {
            foreach (var occurrence in calendar.GetOccurrences(start).TakeWhileBefore(end))
            {
                if (occurrence.Source is not IcalEvent source || string.Equals(source.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                events.Add(ToEvent(source, occurrence.Period, local));
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A rule Ical.Net cannot evaluate keeps what it read so far.
            System.Diagnostics.Trace.TraceWarning($"Faqra calendar: {ex.GetType().Name}");
        }
        return events.Where(e => e.End > from).OrderBy(e => e.Start).ToList();
    }

    private static CalendarEvent ToEvent(IcalEvent source, Period period, TimeZoneInfo local)
    {
        var title = string.IsNullOrWhiteSpace(source.Summary) ? "(No title)" : source.Summary.Trim();
        var join = CalendarRules.JoinLink(source.Location, source.Description, source.Url?.ToString());
        if (source.IsAllDay)
        {
            // Unspecified kind: a Local-kind DateTime with another zone's offset would make DateTimeOffset throw.
            var day = DateTime.SpecifyKind(period.StartTime.Date, DateTimeKind.Unspecified);
            var days = Math.Max(1, (int)Math.Round(((period.EffectiveEndTime?.Date ?? day.AddDays(1)) - day).TotalDays));
            var startLocal = new DateTimeOffset(day, local.GetUtcOffset(day));
            var endDay = day.AddDays(days);
            return new CalendarEvent(title, source.Location, startLocal, new DateTimeOffset(endDay, local.GetUtcOffset(endDay)), true, join);
        }
        var startUtc = new DateTimeOffset(period.StartTime.AsUtc, TimeSpan.Zero);
        var endUtc = period.EffectiveEndTime is { } endTime ? new DateTimeOffset(endTime.AsUtc, TimeSpan.Zero) : startUtc;
        return new CalendarEvent(title, source.Location, startUtc, endUtc, false, join);
    }
}
```

(Ical.Net 5.2.3's names may differ in small ways: `Period.EffectiveEndTime`, `CalDateTime.AsUtc`, `CalDateTime.Date`, the `GetOccurrences(CalDateTime)` overload and `TakeWhileBefore`. Check them against the installed package. Use the equivalents, keep the behaviour, and say in the report what you used. Ical.Net already reads the UTC instant of an occurrence in its own zone, including Windows zone names from a VTIMEZONE, and the tests pin that.)

`src/Faqra.Core/Calendar/CalendarRules.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Globalization;
using System.Text.RegularExpressions;
using Faqra.Core.Localization;

namespace Faqra.Core.Calendar;

/// <summary>The calendar's rules: which event is next, how its time reads, when it is soon, and which links count.</summary>
public static partial class CalendarRules
{
    public const int SoonMinutes = 10;

    /// <summary>The earliest timed event that has not ended; else an all-day event covering today; else null.</summary>
    public static CalendarEvent? Next(IReadOnlyList<CalendarEvent> events, DateTimeOffset now, TimeZoneInfo local)
    {
        var timed = events.Where(e => !e.AllDay && e.End > now).OrderBy(e => e.Start).FirstOrDefault();
        if (timed is not null)
        {
            return timed;
        }
        var today = TimeZoneInfo.ConvertTime(now, local).Date;
        return events.FirstOrDefault(e => e.AllDay && e.Start.Date <= today && e.End.Date > today);
    }

    /// <summary>True when a timed event starts within <see cref="SoonMinutes"/> and has not started yet.</summary>
    public static bool StartsSoon(CalendarEvent e, DateTimeOffset now) =>
        !e.AllDay && e.Start > now && e.Start - now <= TimeSpan.FromMinutes(SoonMinutes);

    /// <summary>When the event is, the way a person says it: Now, in 25 min, 14:30, Tomorrow 09:30, Thu 10:00, Today.</summary>
    public static string When(CalendarEvent e, DateTimeOffset now, TimeZoneInfo local, CalendarStrings s, CultureInfo culture)
    {
        var today = TimeZoneInfo.ConvertTime(now, local).Date;
        if (e.AllDay)
        {
            var day = e.Start.Date;
            return day <= today ? s.Today : day == today.AddDays(1) ? s.Tomorrow : day.ToString("ddd", culture);
        }
        if (e.Start <= now && now < e.End)
        {
            return s.Now;
        }
        var minutes = (int)Math.Ceiling((e.Start - now).TotalMinutes);
        if (minutes is > 0 and < 60)
        {
            return string.Format(culture, s.InMinutesFormat, minutes);
        }
        var start = TimeZoneInfo.ConvertTime(e.Start, local);
        var time = start.ToString("HH:mm", culture);
        return start.Date == today ? time
            : start.Date == today.AddDays(1) ? string.Format(culture, s.TomorrowFormat, time)
            : $"{start.ToString("ddd", culture)} {time}";
    }

    /// <summary>"09:30 to 10:00", or "All day".</summary>
    public static string Range(CalendarEvent e, TimeZoneInfo local, CalendarStrings s, CultureInfo culture) => e.AllDay
        ? s.AllDay
        : string.Format(culture, s.RangeFormat,
            TimeZoneInfo.ConvertTime(e.Start, local).ToString("HH:mm", culture),
            TimeZoneInfo.ConvertTime(e.End, local).ToString("HH:mm", culture));

    /// <summary>The first meeting link in the texts: https to Google Meet, Zoom, Teams or Webex only.</summary>
    public static Uri? JoinLink(params string?[] texts)
    {
        foreach (var text in texts)
        {
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }
            foreach (Match match in UrlPattern().Matches(text))
            {
                if (Uri.TryCreate(match.Value.TrimEnd('.', ',', ')', ';'), UriKind.Absolute, out var uri) && IsMeeting(uri))
                {
                    return uri;
                }
            }
        }
        return null;
    }

    /// <summary>A link the owner pasted, as Faqra will fetch it: https, or webcal read as https; null for anything else.</summary>
    public static Uri? FeedLink(string? input)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        if (text.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
        {
            text = "https://" + text["webcal://".Length..];
        }
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0 ? uri : null;
    }

    /// <summary>
    /// A link as the owner may see it: its host, then the last four characters before a trailing "something.ics" file
    /// name (Google and Outlook end every link the same way, so the file name would not tell two links apart).
    /// </summary>
    public static string Mask(Uri link)
    {
        var text = link.AbsoluteUri.TrimEnd('/');
        var slash = text.LastIndexOf('/');
        if (text.EndsWith(".ics", StringComparison.OrdinalIgnoreCase) && slash > link.GetLeftPart(UriPartial.Authority).Length)
        {
            text = text[..slash];
        }
        return $"{link.Host}…{text[^Math.Min(4, text.Length)..]}";
    }

    private static bool IsMeeting(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }
        var host = uri.Host.ToLowerInvariant();
        var path = uri.AbsolutePath.ToLowerInvariant();
        return host == "meet.google.com"
            || ((host == "zoom.us" || host.EndsWith(".zoom.us", StringComparison.Ordinal)) && path.StartsWith("/j/", StringComparison.Ordinal))
            || (host == "teams.microsoft.com" && path.StartsWith("/l/meetup-join", StringComparison.Ordinal))
            || (host == "teams.live.com" && path.StartsWith("/meet", StringComparison.Ordinal))
            || host == "webex.com" || host.EndsWith(".webex.com", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();
}
```

`src/Faqra.Core/Localization/CalendarStrings.cs` and `CalendarStrings.EnUS.cs`: mirror `HomeStrings`, a partial class with `required string` members, `For(AppLanguage)` and `EnUS` in the second file. Members and English words:

| Member | English |
|---|---|
| `Title` | `Calendar` |
| `Now` | `Now` |
| `Today` | `Today` |
| `Tomorrow` | `Tomorrow` |
| `AllDay` | `All day` |
| `InMinutesFormat` | `in {0} min` |
| `TomorrowFormat` | `Tomorrow {0}` |
| `RangeFormat` | `{0} to {1}` |
| `Join` | `Join` |
| `NextEvent` | `Next event` |
| `NothingSoon` | `Nothing in the next 2 weeks` |
| `NothingThisDay` | `Nothing on this day` |
| `AddLinkPrompt` | `Add your calendar link` |
| `OpenSettings` | `Open settings` |
| `LinkBroken` | `This calendar link stopped working. Paste a new one in Settings.` |
| `UpdatedFormat` | `Updated {0}` |
| `PreviousWeek` | `Previous week` |
| `NextWeek` | `Next week` |
| `PageCaption` | `Paste a private calendar link to see your events on the island. Faqra only reads it, keeps it encrypted on this PC, and sends it nowhere but to your calendar.` |
| `LinksSection` | `Calendar links` |
| `LinkPlaceholder` | `Private calendar link (https or webcal)` |
| `AddLink` | `Add` |
| `RemoveLink` | `Remove` |
| `RefreshNow` | `Refresh now` |
| `LinkRefused` | `That isn't a calendar link Faqra can read. Use the https or webcal address of a private calendar.` |
| `LinkHelp` | `Google Calendar: Settings, your calendar, Secret address in iCal format. Outlook: Settings, Calendar, Shared calendars, Publish a calendar, ICS link. iCloud: Calendar, Share, Public calendar.` |
| `StateWaiting` | `Not read yet` |
| `StateOk` | `Working` |
| `StateFailing` | `Can't reach it right now. Showing the last copy.` |
| `StateBroken` | `Stopped working. Paste a new link.` |

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.Core.Tests`
Expected: PASS (every Core test).

- [ ] **Step 6: Commit**

```bash
git add Directory.Packages.props THIRD-PARTY-NOTICES.md src/Faqra.Core tests/Faqra.Core.Tests
git commit -m "feat(windows): read calendar feeds and the next-event rules"
```

---

### Task 2: Protected links, fetching and last good copies (Win32 + Services)

**Files:**
- Create: `src/Faqra.Win32/Security/Dpapi.cs`, `src/Faqra.Services/Calendar/CalendarSnapshot.cs`, `src/Faqra.Services/Calendar/CalendarService.cs`
- Modify: `src/Faqra.Core/Defaults/DefaultsKey.Stage1.cs` (key `FaqraCalendarLinks`), `src/Faqra.Core/AppPaths.cs` (`CalendarCacheDirectory`)
- Test: `tests/Faqra.Services.Tests/Calendar/CalendarServiceTests.cs` (new)

**Interfaces:**
- Consumes: `CalendarFeed.Read`, `CalendarRules.FeedLink`, `CalendarRules.Mask`, `CalendarEvent` (Task 1); `ISettingsStore` (`Data`, `Set(string, byte[]?)`).
- Produces:
  - `static class Dpapi` with `byte[] Protect(byte[] data)` and `byte[]? Unprotect(byte[] blob)`.
  - `DefaultsKey.FaqraCalendarLinks = "faqraCalendarLinks"`.
  - `AppPaths.CalendarCacheDirectory`.
  - `enum FeedState { Waiting, Ok, Failing, Broken }`.
  - `sealed record FeedStatus(string Masked, FeedState State, DateTimeOffset? UpdatedAt)`.
  - `sealed record CalendarSnapshot(IReadOnlyList<CalendarEvent> Events, IReadOnlyList<FeedStatus> Feeds)`, with `bool HasLinks`, `bool AllBroken`, `DateTimeOffset? UpdatedAt` and `static Empty`.
  - `sealed class CalendarService : IDisposable`:
    - Constructor `(ISettingsStore store, string cacheDirectory, Func<DateTimeOffset> now, TimeZoneInfo local, HttpMessageHandler? handler = null, bool useTimer = true)`.
    - `event Action? Changed`.
    - `CalendarSnapshot Snapshot`.
    - `IReadOnlyList<Uri> Links`.
    - `bool AddLink(string input)` (false when refused) and `void RemoveLink(Uri link)`.
    - `void SetRunning(bool running)`.
    - `Task RefreshAsync()`.

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.Services.Tests/Calendar/CalendarServiceTests.cs`:

```csharp
using System.Net;
using System.Text;
using Faqra.Core.Defaults;
using Faqra.Services.Calendar;

namespace Faqra.Services.Tests.Calendar;

public class CalendarServiceTests
{
    private static readonly TimeZoneInfo Toronto = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private const string Link = "https://calendar.google.com/calendar/ical/owner/private-secret123/basic.ics";

    private const string Ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:x\r\nBEGIN:VEVENT\r\nUID:a\r\nDTSTAMP:20261015T120000Z\r\nDTSTART:20261019T150000Z\r\nDTEND:20261019T160000Z\r\nSUMMARY:Design review\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";

    /// <summary>Answers every request with the next scripted response (the last repeats); records each request's URL.</summary>
    private sealed class Feed(params Func<HttpResponseMessage>[] answers) : HttpMessageHandler
    {
        private int _next;
        public List<Uri> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Add(request.RequestUri!);
            var answer = answers[Math.Min(_next++, answers.Length - 1)];
            return Task.FromResult(answer());
        }
    }

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/calendar") };

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code) { Content = new StringContent("") };

    private static string TempDir() => Directory.CreateTempSubdirectory("faqra-cal-").FullName;

    private static CalendarService Service(DefaultsStore store, string dir, HttpMessageHandler handler) =>
        new(store, dir, () => Now, Toronto, handler, useTimer: false);

    [Fact]
    public async Task ALinkIsReadIntoEvents()
    {
        var store = DefaultsStore.InMemory();
        var dir = TempDir();
        var feed = new Feed(() => Ok(Ics));
        using var service = Service(store, dir, feed);
        Assert.True(service.AddLink("  webcal://calendar.google.com/calendar/ical/owner/private-secret123/basic.ics "));
        await service.RefreshAsync();

        Assert.Equal(new Uri(Link), Assert.Single(feed.Asked));
        Assert.Equal("Design review", Assert.Single(service.Snapshot.Events).Title);
        var status = Assert.Single(service.Snapshot.Feeds);
        Assert.Equal((FeedState.Ok, (DateTimeOffset?)Now), (status.State, status.UpdatedAt));
        Directory.Delete(dir, true);
    }

    [Fact]
    public void OnlyHttpsAndWebcalLinksAreKept()
    {
        using var service = Service(DefaultsStore.InMemory(), TempDir(), new Feed(() => Ok(Ics)));
        Assert.False(service.AddLink("http://calendar.google.com/x.ics"));
        Assert.False(service.AddLink("not a link"));
        Assert.Empty(service.Links);
    }

    [Fact]
    public void LinksAreStoredProtected()
    {
        var store = DefaultsStore.InMemory();
        var dir = TempDir();
        using (var service = Service(store, dir, new Feed(() => Ok(Ics))))
        {
            service.AddLink(Link);
        }
        var stored = store.Data(Faqra.Core.Defaults.DefaultsKey.FaqraCalendarLinks)!;
        Assert.DoesNotContain("private-secret123", Encoding.UTF8.GetString(stored));
        Assert.DoesNotContain("private-secret123", Encoding.Unicode.GetString(stored));
        using var again = Service(store, dir, new Feed(() => Ok(Ics)));
        Assert.Equal(new Uri(Link), Assert.Single(again.Links));
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task TheCacheIsProtected()
    {
        var dir = TempDir();
        using var service = Service(DefaultsStore.InMemory(), dir, new Feed(() => Ok(Ics)));
        service.AddLink(Link);
        await service.RefreshAsync();
        var file = Assert.Single(Directory.GetFiles(dir));
        Assert.DoesNotContain("Design review", Encoding.UTF8.GetString(File.ReadAllBytes(file)));
        Assert.DoesNotContain("private-secret123", Path.GetFileName(file));
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task StartingOfflineShowsTheLastGoodCopy()
    {
        var store = DefaultsStore.InMemory();
        var dir = TempDir();
        using (var first = Service(store, dir, new Feed(() => Ok(Ics))))
        {
            first.AddLink(Link);
            await first.RefreshAsync();
        }
        using var offline = Service(store, dir, new Feed(() => throw new HttpRequestException("offline")));
        await offline.RefreshAsync();
        Assert.Equal("Design review", Assert.Single(offline.Snapshot.Events).Title);
        Assert.Equal(FeedState.Failing, Assert.Single(offline.Snapshot.Feeds).State);
        Directory.Delete(dir, true);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task AGoneLinkIsBroken(HttpStatusCode code)
    {
        using var service = Service(DefaultsStore.InMemory(), TempDir(), new Feed(() => Status(code)));
        service.AddLink(Link);
        await service.RefreshAsync();
        Assert.Equal(FeedState.Broken, Assert.Single(service.Snapshot.Feeds).State);
        Assert.True(service.Snapshot.AllBroken);
    }

    [Fact]
    public async Task AnHtmlPageKeepsTheLastGoodCopy()
    {
        var feed = new Feed(() => Ok(Ics), () => Ok("<html><body>Sign in</body></html>"));
        using var service = Service(DefaultsStore.InMemory(), TempDir(), feed);
        service.AddLink(Link);
        await service.RefreshAsync();
        await service.RefreshAsync();
        Assert.Equal("Design review", Assert.Single(service.Snapshot.Events).Title);
        Assert.Equal(FeedState.Failing, Assert.Single(service.Snapshot.Feeds).State);
    }

    [Fact]
    public async Task AFeedOverTheLimitIsRefused()
    {
        var huge = "BEGIN:VCALENDAR\r\n" + new string('x', CalendarService.MaxFeedBytes + 10);
        using var service = Service(DefaultsStore.InMemory(), TempDir(), new Feed(() => Ok(huge)));
        service.AddLink(Link);
        await service.RefreshAsync();
        Assert.Empty(service.Snapshot.Events);
        Assert.Equal(FeedState.Failing, Assert.Single(service.Snapshot.Feeds).State);
    }

    [Fact]
    public async Task RemovingALinkForgetsItsEventsAndCopy()
    {
        var dir = TempDir();
        using var service = Service(DefaultsStore.InMemory(), dir, new Feed(() => Ok(Ics)));
        service.AddLink(Link);
        await service.RefreshAsync();
        service.RemoveLink(new Uri(Link));
        Assert.Empty(service.Links);
        Assert.Empty(service.Snapshot.Events);
        Assert.False(service.Snapshot.HasLinks);
        Assert.Empty(Directory.GetFiles(dir));
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task ASnapshotNeverPrintsALink()
    {
        using var service = Service(DefaultsStore.InMemory(), TempDir(), new Feed(() => Ok(Ics)));
        service.AddLink(Link);
        await service.RefreshAsync();
        Assert.DoesNotContain("private-secret123", service.Snapshot.ToString());
        Assert.StartsWith("calendar.google.com…", Assert.Single(service.Snapshot.Feeds).Masked);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Services.Tests --filter "FullyQualifiedName~CalendarServiceTests"`
Expected: build FAILS with "The type or namespace name 'Calendar' does not exist in the namespace 'Faqra.Services'".

- [ ] **Step 3: DPAPI, the key and the cache folder**

`src/Faqra.Win32/Security/Dpapi.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Runtime.InteropServices;

namespace Faqra.Win32.Security;

/// <summary>Windows' per-user data protection: what one user protects, only that user on this PC can read back.</summary>
public static class Dpapi
{
    private static readonly byte[] Entropy = "Faqra calendar"u8.ToArray();

    public static byte[] Protect(byte[] data) =>
        Run(data, protect: true) ?? throw new InvalidOperationException("Windows could not protect the data.");

    /// <summary>The original bytes, or null when the blob is not one this user protected.</summary>
    public static byte[]? Unprotect(byte[] blob) => Run(blob, protect: false);

    private static byte[]? Run(byte[] input, bool protect)
    {
        var inBlob = new DataBlob();
        var entropyBlob = new DataBlob();
        var outBlob = new DataBlob();
        var inHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
        var entropyHandle = GCHandle.Alloc(Entropy, GCHandleType.Pinned);
        try
        {
            inBlob = new DataBlob { cbData = input.Length, pbData = inHandle.AddrOfPinnedObject() };
            entropyBlob = new DataBlob { cbData = Entropy.Length, pbData = entropyHandle.AddrOfPinnedObject() };
            var ok = protect
                ? CryptProtectData(ref inBlob, null, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref outBlob)
                : CryptUnprotectData(ref inBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref outBlob);
            if (!ok)
            {
                return null;
            }
            var output = new byte[outBlob.cbData];
            Marshal.Copy(outBlob.pbData, output, 0, outBlob.cbData);
            return output;
        }
        finally
        {
            inHandle.Free();
            entropyHandle.Free();
            if (outBlob.pbData != IntPtr.Zero)
            {
                LocalFree(outBlob.pbData);
            }
        }
    }

    private const int UiForbidden = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DataBlob dataIn, string? description, ref DataBlob entropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, ref DataBlob entropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);
}
```

(If the project uses `LibraryImport` source-generated interop elsewhere in `Faqra.Win32`, follow that style instead.)

In `DefaultsKey.Stage1.cs`, add after `FaqraHomeItems`: `public const string FaqraCalendarLinks = "faqraCalendarLinks";`. It has no registered default; absent means no links.

In `src/Faqra.Core/AppPaths.cs`, add next to `AgentsLogFile`: `public static string CalendarCacheDirectory => Path.Combine(LocalDataDirectory, "calendar");`.

- [ ] **Step 4: The snapshot and the service**

`src/Faqra.Services/Calendar/CalendarSnapshot.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Faqra.Core.Calendar;

namespace Faqra.Services.Calendar;

public enum FeedState { Waiting, Ok, Failing, Broken }

/// <summary>One link as the owner may see it: masked, never whole.</summary>
public sealed record FeedStatus(string Masked, FeedState State, DateTimeOffset? UpdatedAt);

/// <summary>What the calendar knows: every link's events in one list, oldest first, and each link's state.</summary>
public sealed record CalendarSnapshot(IReadOnlyList<CalendarEvent> Events, IReadOnlyList<FeedStatus> Feeds)
{
    public static readonly CalendarSnapshot Empty = new([], []);

    public bool HasLinks => Feeds.Count > 0;

    public bool AllBroken => Feeds.Count > 0 && Feeds.All(feed => feed.State == FeedState.Broken);

    /// <summary>The oldest successful read among the links that have one.</summary>
    public DateTimeOffset? UpdatedAt => Feeds.Where(feed => feed.UpdatedAt is not null).Select(feed => feed.UpdatedAt).Min();
}
```

`src/Faqra.Services/Calendar/CalendarService.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faqra.Core.Calendar;
using Faqra.Core.Defaults;
using Faqra.Win32.Security;

namespace Faqra.Services.Calendar;

/// <summary>
/// The owner's private calendar links: kept protected in Faqra's settings, read every 15 minutes (and when asked), each
/// link's last good copy kept protected on disk, and the next two weeks published as a snapshot. A link is a password:
/// it never reaches a log, a trace or an exception, and only its own host ever sees it.
/// </summary>
public sealed class CalendarService : IDisposable
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(20);
    public const int MaxFeedBytes = 5 * 1024 * 1024;
    // Back covers the whole previous week the Calendar section can show; Ahead covers the next one.
    private static readonly TimeSpan Back = TimeSpan.FromDays(14);
    private static readonly TimeSpan Ahead = TimeSpan.FromDays(14);

    private readonly ISettingsStore _store;
    private readonly string _cacheDirectory;
    private readonly Func<DateTimeOffset> _now;
    private readonly TimeZoneInfo _local;
    private readonly HttpClient _http;
    private readonly bool _useTimer;
    private readonly SemaphoreSlim _refreshing = new(1, 1);
    private readonly object _gate = new();
    private readonly Dictionary<string, (FeedState State, DateTimeOffset? UpdatedAt)> _states = new(StringComparer.Ordinal);
    private Timer? _timer;
    private bool _running;
    private volatile CalendarSnapshot _snapshot = CalendarSnapshot.Empty;

    public CalendarService(ISettingsStore store, string cacheDirectory, Func<DateTimeOffset> now, TimeZoneInfo local, HttpMessageHandler? handler = null, bool useTimer = true)
    {
        _store = store;
        _cacheDirectory = cacheDirectory;
        _now = now;
        _local = local;
        _useTimer = useTimer;
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = FetchTimeout;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Faqra-Calendar/1.0");
        Links = ReadLinks();
    }

    /// <summary>Raised off the UI thread after the snapshot changes.</summary>
    public event Action? Changed;

    public CalendarSnapshot Snapshot => _snapshot;

    public IReadOnlyList<Uri> Links { get; private set; }

    /// <summary>Keeps a pasted link (https, or webcal read as https); false when it is not a link Faqra can read.</summary>
    public bool AddLink(string input)
    {
        if (CalendarRules.FeedLink(input) is not { } link)
        {
            return false;
        }
        if (!Links.Contains(link))
        {
            Links = [.. Links, link];
            SaveLinks();
            Publish();
            if (_running)
            {
                _ = RefreshAsync();
            }
        }
        return true;
    }

    public void RemoveLink(Uri link)
    {
        Links = Links.Where(other => other != link).ToList();
        SaveLinks();
        lock (_gate)
        {
            _states.Remove(Key(link));
        }
        try
        {
            File.Delete(CachePath(link));
        }
        catch (IOException ex)
        {
            Trace.TraceWarning($"Faqra calendar: {ex.GetType().Name}");
        }
        Publish();
    }

    /// <summary>Starts or stops the 15-minute reads; starting shows the last good copies at once, then reads.</summary>
    public void SetRunning(bool running)
    {
        if (running == _running)
        {
            return;
        }
        _running = running;
        if (running)
        {
            NetworkChange.NetworkAvailabilityChanged += OnNetwork;
            Publish();
            if (_useTimer)
            {
                _timer = new Timer(_ => _ = RefreshAsync(), null, TimeSpan.Zero, RefreshInterval);
            }
        }
        else
        {
            NetworkChange.NetworkAvailabilityChanged -= OnNetwork;
            _timer?.Dispose();
            _timer = null;
        }
    }

    /// <summary>Reads every link once; a read already running makes this one a no-op. Never throws.</summary>
    public async Task RefreshAsync()
    {
        if (!await _refreshing.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }
        try
        {
            foreach (var link in Links.ToList())
            {
                var (state, text) = await FetchAsync(link).ConfigureAwait(false);
                if (state == FeedState.Ok && text is not null)
                {
                    WriteCache(link, text);
                }
                lock (_gate)
                {
                    var previous = _states.TryGetValue(Key(link), out var known) ? known.UpdatedAt : null;
                    _states[Key(link)] = (state, state == FeedState.Ok ? _now() : previous);
                }
            }
            Trace.TraceInformation($"Faqra calendar: refreshed {Links.Count} link(s)");
            Publish();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Trace.TraceWarning($"Faqra calendar: {ex.GetType().Name}");
        }
        finally
        {
            _refreshing.Release();
        }
    }

    public void Dispose()
    {
        SetRunning(false);
        _http.Dispose();
        _refreshing.Dispose();
    }

    private async Task<(FeedState State, string? Text)> FetchAsync(Uri link)
    {
        try
        {
            using var response = await _http.GetAsync(link, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            {
                Trace.TraceInformation($"Faqra calendar: HTTP {(int)response.StatusCode}");
                return (FeedState.Broken, null);
            }
            if (!response.IsSuccessStatusCode)
            {
                Trace.TraceInformation($"Faqra calendar: HTTP {(int)response.StatusCode}");
                return (FeedState.Failing, null);
            }
            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk).ConfigureAwait(false)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxFeedBytes)
                {
                    return (FeedState.Failing, null);
                }
            }
            var text = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
            return CalendarFeed.Read(text, _now() - Back, _now() + Ahead, _local) is null ? (FeedState.Failing, null) : (FeedState.Ok, text);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            Trace.TraceInformation($"Faqra calendar: {ex.GetType().Name}");
            return (FeedState.Failing, null);
        }
    }

    /// <summary>Rebuilds the snapshot from the last good copies on disk and the known states.</summary>
    private void Publish()
    {
        var now = _now();
        var events = new List<CalendarEvent>();
        var feeds = new List<FeedStatus>();
        foreach (var link in Links)
        {
            if (ReadCache(link) is { } text && CalendarFeed.Read(text, now - Back, now + Ahead, _local) is { } read)
            {
                events.AddRange(read);
            }
            (FeedState State, DateTimeOffset? UpdatedAt) state;
            lock (_gate)
            {
                state = _states.TryGetValue(Key(link), out var known) ? known : (FeedState.Waiting, null);
            }
            feeds.Add(new FeedStatus(CalendarRules.Mask(link), state.State, state.UpdatedAt));
        }
        _snapshot = new CalendarSnapshot(events.OrderBy(e => e.Start).ToList(), feeds);
        Changed?.Invoke();
    }

    private void OnNetwork(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (e.IsAvailable)
        {
            _ = RefreshAsync();
        }
    }

    private IReadOnlyList<Uri> ReadLinks()
    {
        if (_store.Data(DefaultsKey.FaqraCalendarLinks) is not { Length: > 0 } blob || Dpapi.Unprotect(blob) is not { } json)
        {
            return [];
        }
        try
        {
            return (JsonSerializer.Deserialize<string[]>(json) ?? [])
                .Select(CalendarRules.FeedLink).OfType<Uri>().Distinct().ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void SaveLinks()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(Links.Select(link => link.AbsoluteUri).ToArray());
        _store.Set(DefaultsKey.FaqraCalendarLinks, Links.Count == 0 ? null : Dpapi.Protect(json));
    }

    private string? ReadCache(Uri link)
    {
        try
        {
            var path = CachePath(link);
            return File.Exists(path) && Dpapi.Unprotect(File.ReadAllBytes(path)) is { } bytes ? Encoding.UTF8.GetString(bytes) : null;
        }
        catch (IOException ex)
        {
            Trace.TraceWarning($"Faqra calendar: {ex.GetType().Name}");
            return null;
        }
    }

    private void WriteCache(Uri link, string text)
    {
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            var path = CachePath(link);
            var temp = path + ".tmp";
            File.WriteAllBytes(temp, Dpapi.Protect(Encoding.UTF8.GetBytes(text)));
            File.Move(temp, path, overwrite: true);
        }
        catch (IOException ex)
        {
            Trace.TraceWarning($"Faqra calendar: {ex.GetType().Name}");
        }
    }

    private string CachePath(Uri link) => Path.Combine(_cacheDirectory, Key(link) + ".dat");

    /// <summary>A name for a link that does not reveal it: the first 16 hex characters of its SHA-256.</summary>
    private static string Key(Uri link) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(link.AbsoluteUri)))[..16].ToLowerInvariant();
}
```

(Check `ISettingsStore`'s byte accessor name, which the explorer reported as `Data`, and its `Set(string, byte[]?)` overload. If `Faqra.Services` cannot reference `Faqra.Win32` for some reason, report BLOCKED; it already references it today.)

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.Services.Tests --filter "FullyQualifiedName~CalendarServiceTests"`, then `dotnet test tests/Faqra.Services.Tests`.
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Faqra.Win32/Security src/Faqra.Services/Calendar src/Faqra.Core tests/Faqra.Services.Tests/Calendar
git commit -m "feat(windows): calendar links kept protected, read every 15 minutes with a last good copy"
```

---

### Task 3: The feature, its Settings page and its lifetime (Core + App)

**Files:**
- Modify:
  - `src/Faqra.Core/Features/FeatureWindowsSupport.cs`, `FeaturePreset.cs`, `FeaturePermissions.cs` (if it gates on an OS permission)
  - `src/Faqra.Core/Defaults/DefaultsMigrations.cs`
  - `src/Faqra.Core/Settings/SettingsPage.cs`
  - `src/Faqra.Core/Localization/Strings.EnUS.cs` (page title)
  - `src/Faqra.App/Settings/SettingsWindow.xaml.cs`
  - `src/Faqra.App/AppServices.cs`
- Create: `src/Faqra.App/Settings/Pages/CalendarPage.cs`
- Test: `tests/Faqra.Core.Tests/Calendar/CalendarRegistrationTests.cs` (new), `tests/Faqra.App.Tests/CalendarPageTests.cs` (new)

**Interfaces:**
- Consumes: `CalendarService` (Task 2), `CalendarStrings` (Task 1).
- Produces:
  - `AppFeature.NotchCalendar` is supported and built, among first-run features, and installed once for existing owners (marker `faqraCalendarInstalled`).
  - `SettingsPage.Calendar`, gated on and the destination of `NotchCalendar`.
  - `AppServices.Calendar` (`CalendarService`), running while the feature is on and refreshed on resume.
  - `CalendarPage`.

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.Core.Tests/Calendar/CalendarRegistrationTests.cs` (model on `tests/Faqra.Core.Tests/Agents/AgentsRegistrationTests.cs`):

```csharp
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Settings;

namespace Faqra.Core.Tests.Calendar;

public class CalendarRegistrationTests
{
    [Fact]
    public void TheCalendarIsBuiltOnWindows()
    {
        Assert.Equal(WindowsSupport.Supported, FeatureWindowsSupport.Classify(AppFeature.NotchCalendar));
        Assert.True(FeatureWindowsSupport.IsBuilt(AppFeature.NotchCalendar));
        Assert.Contains(AppFeature.NotchCalendar, FeaturePreset.FirstRunFeatures);
    }

    [Fact]
    public void ExistingOwnersGetTheCalendarOnce()
    {
        var store = DefaultsStore.InMemory();
        DefaultsMigrations.Run(store, "1.0.0");
        Assert.True(store.Bool(DefaultsKey.FeatureAvailable(AppFeature.NotchCalendar.RawValue())));
        store.Set(DefaultsKey.FeatureAvailable(AppFeature.NotchCalendar.RawValue()), false);
        DefaultsMigrations.Run(store, "1.0.0");
        Assert.False(store.Bool(DefaultsKey.FeatureAvailable(AppFeature.NotchCalendar.RawValue())));
    }

    [Fact]
    public void TheCalendarHasItsOwnSettingsPage() =>
        Assert.Equal(SettingsPage.Calendar, SettingsPages.Destination(AppFeature.NotchCalendar));
}
```

(Use the real names from `AgentsRegistrationTests.cs` and `FeaturePresetTests.cs`. `FirstRunFeatures`, `RawValue()`, `DefaultsKey.FeatureAvailable(...)` and the `Destination` method's home may be spelled differently. Copy their forms.)

`tests/Faqra.App.Tests/CalendarPageTests.cs`:

```csharp
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Faqra.App.Settings.Pages;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;

namespace Faqra.App.Tests;

public class CalendarPageTests
{
    private static readonly CalendarStrings S = CalendarStrings.EnUS;

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

    [Fact]
    public void ALinkIsAddedMaskedAndRemoved() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        var page = new CalendarPage();
        AgentsRenderTests.RenderInk(page, "settings-calendar", 840, 700);
        var box = All<PasswordBox>(page).Single();
        Assert.Equal(S.LinkPlaceholder, AutomationProperties.GetName(box));

        box.Password = "https://calendar.google.com/calendar/ical/owner/private-secret123/basic.ics";
        var add = All<Button>(page).Single(b => AutomationProperties.GetName(b) == S.AddLink);
        add.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        var text = AgentsRenderTests.AllText(page);
        Assert.Contains("calendar.google.com…", text);
        Assert.DoesNotContain("private-secret123", text);
        Assert.Equal(string.Empty, All<PasswordBox>(page).Single().Password);

        All<Button>(page).Single(b => AutomationProperties.GetName(b)?.StartsWith(S.RemoveLink, StringComparison.Ordinal) == true)
            .RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Empty(services.Calendar.Links);
    });

    [Fact]
    public void ALinkThatIsNotACalendarIsRefusedWithASentence() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        var page = new CalendarPage();
        All<PasswordBox>(page).Single().Password = "http://example.com/x.ics";
        All<Button>(page).Single(b => AutomationProperties.GetName(b) == S.AddLink).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Contains(S.LinkRefused, AgentsRenderTests.AllText(page));
        Assert.Empty(services.Calendar.Links);
    });
}
```

(In tests, `AppServices.StartWith` builds a `CalendarService` over the in-memory store. No test may touch the owner's folder or the network. Give `AppServices` a test-only way to pass the cache directory (a fresh temp folder) and an `HttpMessageHandler` (one that answers 404 to everything) the way other test overrides in `AppServices` are handled, and use it in every App test that starts services. Say in the report what you used.)

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~CalendarRegistrationTests"`
Expected: FAIL (NotchCalendar is still NotApplicable).

- [ ] **Step 3: Register the feature**

Mirror exactly how A1 registered `FaqraAgents`:
- **`FeatureWindowsSupport.cs`:** add `or AppFeature.NotchCalendar` to the Supported arm of `Classify` and to `IsBuilt`.
- **`FeaturePreset.cs`:** add `AppFeature.NotchCalendar` to the first-run list, next to `AppFeature.FaqraAgents`.
- **`DefaultsMigrations.cs`:** add `InstallCalendarOnce(store)`, the same shape as `InstallAgentsOnce` with the marker `faqraCalendarInstalled` and feature raw name `notchCalendar` (use `AppFeature.NotchCalendar`'s real raw value). Call it last in `Run`.
- **`FeaturePermissions.cs`:** if `NotchCalendar` maps to `AppPermission.Calendar` and that keeps the feature unavailable on Windows until an OS permission is granted, remove the mapping for Windows. A private link needs no permission. Say what you found.
- **`SettingsPage.cs`:**
  - add `Calendar` to the enum (after `Agents`);
  - add an `AllPages` row in the Agents row's section with glyph `"\uE787"` (Calendar);
  - add `SettingsPage.Calendar => [AppFeature.NotchCalendar]` to `Gate`;
  - in `Destination`, change `AppFeature.NotchCalendar` to `SettingsPage.Calendar`.
- **`Strings.EnUS.cs`:** add the page title `Calendar` next to the Agents title, line 77 or wherever the titles map is.
- **`SettingsWindow.xaml.cs`:** add `SettingsPage.Calendar` to the shown pages (line 21) and `SettingsPage.Calendar => new CalendarPage(),` to `CreatePage`.
- **`AppServices.cs`:**
  - add `public CalendarService Calendar { get; }`, built with `new CalendarService(store, AppPaths.CalendarCacheDirectory, () => DateTimeOffset.Now, TimeZoneInfo.Local)` (with the test override above);
  - add a `Bindings()` entry `[AppFeature.NotchCalendar] = () => { ...; Calendar.SetRunning(on); Island.SyncWithPreferences() if Notch is available; }`, modelled on the FaqraAgents entry;
  - call `_ = Calendar.RefreshAsync()` from the existing `SystemEventsWindow.PowerChanged` subscription (find where `KeepAwakeManager` gets it, or subscribe next to it);
  - dispose `Calendar` with the other services.

- [ ] **Step 4: Write `CalendarPage`**

`src/Faqra.App/Settings/Pages/CalendarPage.cs`, styled like `AgentsPage`: same `Text(..., "PageTitle" | "SectionHeader" | "Caption" | "Body")` helpers and `CardControl` rows. Behaviour:
- **Header:** the page title, `PageCaption` as a wrapped caption, then `LinkHelp` as a second wrapped caption.
- **Links section:** header `LinksSection`. Under it, one `CardControl` per link in `AppServices.Current.Calendar.Snapshot.Feeds`, in order. Each card has:
  - the masked text as its header;
  - under the header, a caption with the state: `StateWaiting`, `StateOk` with `Format(UpdatedFormat, time)` when updated, `StateFailing` or `StateBroken`;
  - a `Remove` secondary button whose automation name is `"{RemoveLink} {masked}"`. It calls `RemoveLink` with the matching `Links[i]`.
- **Add row:**
  - a `PasswordBox` (MinWidth 360) with automation name `LinkPlaceholder` and a placeholder label beside or above it;
  - an `Add` primary button with automation name `AddLink`. When clicked, it calls `AddLink(box.Password)`.
  - On true it clears the box and rebuilds the list. On false it shows `LinkRefused` in a caption under the row, in the island's error colour or the theme's critical text brush.
- **Refresh button:** a `Refresh now` secondary button calls `_ = Calendar.RefreshAsync()`.
- **Live updates:** the page rebuilds its list on `Calendar.Changed`, marshalled with `Dispatcher.BeginInvoke`. It unsubscribes on `Unloaded`.
- **Secrecy:** the link's full text must never be placed in any `TextBlock`, tooltip or automation name.

Write it as one class of about 150 lines, following `AgentsPage`'s structure (fields, `Build()`, helpers).

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test` (whole solution).
Expected: PASS, no warnings. Tests that count features or settings pages may need their counts moved by one; change only those counts, and list each one.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "feat(windows): calendar feature, its settings page and lifetime"
```

---

### Task 4: The Calendar section (App)

**Files:**
- Create: `src/Faqra.App/Island/Modules/CalendarModule.cs`
- Modify: `src/Faqra.App/Island/IslandController.cs` (`BuildModule`, `ModuleTitle`)
- Test: `tests/Faqra.App.Tests/CalendarModuleTests.cs` (new)

**Interfaces:**
- Consumes:
  - `CalendarService.Snapshot` and `Changed` (Task 2);
  - `CalendarRules.Range`, `When`, `StartsSoon` and `CalendarStrings` (Task 1);
  - `AgentCardParts`, `MusicModule.RoundButtonTemplate` and `App.ShowSettings(SettingsPage)` (existing).
- Produces: `public sealed class CalendarModule : UserControl`, built as `CalendarModule(CalendarService calendar, Func<DateTimeOffset> now, TimeZoneInfo local)`, with `DateTime SelectedDay`, `DateTime WeekStart`, `void ShowDay(DateTime day)`, `void ShiftWeek(int weeks)`.

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.App.Tests/CalendarModuleTests.cs`:
- Build a `CalendarService` over an in-memory store and a temp cache directory, with a fake `HttpMessageHandler` returning a small ICS. The ICS has two events today and one on Thursday, in local time. Use the same `Feed` handler shape as Task 2's tests.
- Add one link and await `RefreshAsync()`.
- `now` is fixed at a Monday 09:00 local.

Tests:
- `TheWeekStartsOnMondayAndTodayIsChosen`:
  - `WeekStart` is that Monday and `SelectedDay` is today.
  - The strip has 7 day buttons with automation names like "Monday 19 October", via `ToString("dddd d MMMM", culture)`.
  - Today's two events are listed with `Range` text and titles. Render `island-calendar.png` at 412 by 400.
- `ChoosingADayShowsItsEvents`: click Thursday's button; that day's event shows. Click an empty day; `NothingThisDay` shows.
- `TheArrowsMoveByAWeek`: invoke the "Next week" button (automation name `NextWeek`). `WeekStart` moves 7 days and the selected day moves with it, and "Next week" is now disabled (the feed only covers two weeks). Then "Previous week" twice reaches last week, where "Previous week" is disabled.
- `WithoutALinkTheSectionSaysHowToAddOne`: with no links, the text contains `AddLinkPrompt` and there is a button named `OpenSettings`.
- `ABrokenLinkSaysSo`: with a handler returning 404, the text contains `LinkBroken`.
- `JoinShowsOnlyForMeetingLinks`: an event with a Meet link in its location has a `Join` button. One without has none.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.App.Tests --filter "FullyQualifiedName~CalendarModuleTests"`
Expected: build FAILS with "The type or namespace name 'CalendarModule' could not be found".

- [ ] **Step 3: Write `CalendarModule`**

`src/Faqra.App/Island/Modules/CalendarModule.cs`.

**Layout**, top to bottom, with 12 px side margins like `HomeModule`:
1. **Week strip.** A `Grid` with a 28 px "previous week" icon button (`\uE76B`), seven equal day columns, and a 28 px "next week" icon button (`\uE76C`). The arrows use the island's round template, with automation names and tooltips `PreviousWeek`/`NextWeek`.
2. **Day buttons.** Each shows the short weekday (`ddd`, 11 px, `Tertiary`) over the day number (14 px).
   - Today's number is `Primary` semibold.
   - The selected day has a `FillStrong` background. Other days are transparent.
   - Each has the automation name `dddd d MMMM`.
   - Clicking one calls `ShowDay`.
3. **Under the strip** (12 px gap), the selected day's events, oldest first. Each is an `AgentCardParts.Panel`:
   - `Range` as a `Tertiary` 11 px line;
   - the title as 13 px semibold;
   - the location as a 12 px `Secondary` line, skipped when it is a meeting link or empty;
   - a right-aligned `Join` secondary pill when `JoinLink` is set. It opens the link with `Process.Start(new ProcessStartInfo(link.AbsoluteUri) { UseShellExecute = true })` inside a try/catch that traces only the exception type.
4. **Empty and status states**, all as sentences:
   - No links: `AddLinkPrompt`, plus an `OpenSettings` quiet button that calls `App.ShowSettings(Core.Settings.SettingsPage.Calendar)`.
   - `AllBroken`: `LinkBroken`.
   - The day has nothing: `NothingThisDay`.
   - When any feed is `Failing` and `UpdatedAt` is known: a `Tertiary` caption `Format(UpdatedFormat, AgentsText.Elapsed(now - updated, agentsStrings))`, or a simple "N min ago" or "N h ago" if `Elapsed` doesn't fit.
5. **Wrapping.** Everything sits in a `ScrollViewer`.

**Behaviour:**
- `SelectedDay` starts as today (local). `WeekStart` is the Monday on or before it.
- `ShowDay(day)` selects a day. `ShiftWeek(n)` moves `WeekStart` and the selected day by `7n` days, kept between last week and next week (the weeks the feed covers). An arrow at its limit is disabled.
- The module subscribes to `Calendar.Changed`, marshalled with `Dispatcher.BeginInvoke`, and rebuilds only the day list (the strip keeps its buttons; update its backgrounds).
- Subscribe and unsubscribe idempotently on Loaded, Unloaded and `IsVisibleChanged`, as `HomeModule` does.
- A minute `DispatcherTimer` re-renders the day list so "Now" and the soon state stay right.

In `IslandController.cs`:
- `BuildModule`: add `IslandModule.Calendar => new CalendarModule(AppServices.Current.Calendar, () => DateTimeOffset.Now, TimeZoneInfo.Local),`.
- `ModuleTitle`: add `IslandModule.Calendar => CalendarStrings.For(L10n.Shared.Language).Title,`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Faqra.App.Tests`
Expected: PASS. Open `%TEMP%\faqra-ui\island-calendar.png` and describe it in the report.

- [ ] **Step 5: Commit**

```bash
git add src/Faqra.App tests/Faqra.App.Tests
git commit -m "feat(windows): island Calendar section with a week strip and the day's events"
```

---

### Task 5: Home's Next event row (Core + App)

**Files:**
- Modify:
  - `src/Faqra.Core/Island/HomeItems.cs`
  - `src/Faqra.Core/Localization/HomeStrings.cs` and `HomeStrings.EnUS.cs` (`ItemNextEvent`)
  - `src/Faqra.App/Island/Modules/HomeRows.cs`
  - `src/Faqra.App/Island/Modules/HomeModule.cs`
  - `src/Faqra.App/Island/IslandController.Home.cs`
- Test: `tests/Faqra.Core.Tests/HomeItemsTests.cs` (add), `tests/Faqra.App.Tests/HomeModuleTests.cs` (add)

**Interfaces:**
- Consumes: `CalendarService` (Task 2); `CalendarRules.Next`, `When`, `StartsSoon` and `CalendarStrings` (Task 1); `HomeModule` and `HomeRow` (H1a).
- Produces:
  - `HomeItem.NextEvent` (raw `nextEvent`, appended to the enum).
  - `HomeItems.Accent(IReadOnlyList<HomeItem> items, bool agentWaiting, bool eventSoon)`, where the agent wins.
  - `HomeModule` gains a `CalendarService` constructor parameter, after `monitor`.
  - The play and next icon buttons get tooltips.

- [ ] **Step 1: Write the failing tests**

In `tests/Faqra.Core.Tests/HomeItemsTests.cs`:
- Update `RawNamesAreStableAndRoundTrip`'s expected list to end with `"nextEvent"`. Remove the `Assert.Null(HomeItems.FromRawValue("nextEvent"))` line.
- Update `HomeStartsWithMusicAndTheAgents` to expect `[HomeItem.NowPlaying, HomeItem.NextEvent, HomeItem.Agents]`.
- Replace `OnlyAWaitingAgentWearsTheAccent` with:

```csharp
    [Fact]
    public void TheAccentGoesToAWaitingAgentThenASoonEvent()
    {
        IReadOnlyList<HomeItem> items = [HomeItem.NowPlaying, HomeItem.NextEvent, HomeItem.Agents];
        Assert.Equal(HomeItem.Agents, HomeItems.Accent(items, agentWaiting: true, eventSoon: true));
        Assert.Equal(HomeItem.NextEvent, HomeItems.Accent(items, agentWaiting: false, eventSoon: true));
        Assert.Null(HomeItems.Accent(items, agentWaiting: false, eventSoon: false));
        Assert.Equal(HomeItem.NextEvent, HomeItems.Accent([HomeItem.NextEvent], agentWaiting: true, eventSoon: true));
        Assert.True(HomeItems.Waits(AgentState.Approval));
        Assert.False(HomeItems.Waits(AgentState.Working));
    }
```

In `tests/Faqra.App.Tests/HomeModuleTests.cs`:
- Update the `Home(...)` helper to pass a `CalendarService`. Use one over the in-memory store, a temp cache directory, and a fake handler (Task 2's `Feed` shape), with `useTimer: false`.
- Add tests:
  - **`TheNextEventRowSaysWhatIsNext`:** with an event 25 minutes from `now`, the row reads `Next event`, `in 25 min` and the title.
  - **`AnEventStartingSoonWearsTheAccent`:** with an event 5 minutes away and no waiting agent, exactly one TextBlock has the `Accent` foreground, and it reads `Next event`.
  - **`AWaitingAgentKeepsTheAccent`:** an event 5 minutes away, plus a PermissionRequest session on the hub. Exactly one TextBlock has the `Accent` foreground, and it reads `Agents`.
  - **`WithoutALinkTheRowSaysHowToAddOne`:** the row text contains `AddLinkPrompt`.
  - **`IconButtonsHaveTooltips`:** the play and next buttons' `ToolTip` equals their automation names.
- `HomeModule` needs a clock for "now" in tests. Add an optional constructor parameter `Func<DateTimeOffset>? now = null`, defaulting to `() => DateTimeOffset.Now`, and pass it to the row.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~HomeItemsTests"`
Expected: FAIL (no `NextEvent` member; `Accent` has two parameters).

- [ ] **Step 3: Core**

In `HomeItems.cs`:
- Change the enum to `public enum HomeItem { NowPlaying, Agents, Timer, System, Battery, NextEvent }`.
- Add `HomeItem.NextEvent => "nextEvent",` to `RawValue`.
- Replace `Accent` with:

```csharp
    /// <summary>The single row that wears the accent: Agents while an agent waits; else Next event while it starts soon.</summary>
    public static HomeItem? Accent(IReadOnlyList<HomeItem> items, bool agentWaiting, bool eventSoon) =>
        agentWaiting && items.Contains(HomeItem.Agents) ? HomeItem.Agents
        : eventSoon && items.Contains(HomeItem.NextEvent) ? HomeItem.NextEvent
        : null;
```

In `HomeStrings`, add `ItemNextEvent` with the English `Next event`.

- [ ] **Step 4: The row and the shared outlier (App)**

In `HomeRows.cs`:
- In `IconButton`, add `button.ToolTip = name;` after `AutomationProperties.SetName(button, name);`.
- Add `NextEventRow : HomeRow`, as a whole-row button (`RowButton`) that opens the Calendar section. It shows:
  - the `ItemNextEvent` label, which is a `Label` and turns `Accent` when the module says so;
  - a line with `When` and the title, 13 px `Primary`;
  - the location, or nothing;
  - a right-aligned `Join` secondary pill when the event has a link.
- The row takes `(CalendarService calendar, HomeStrings s, CalendarStrings c, Func<DateTimeOffset> now, Action openCalendar, Action openSettings)`, and exposes `public bool Soon` and `public void SetAccent(bool on)`.
- `Update()` reads `calendar.Snapshot` and shows:
  - with no links: `AddLinkPrompt`, plus an `OpenSettings` quiet button;
  - with `AllBroken`: `LinkBroken`;
  - when `Next(...)` is null: `NothingSoon`;
  - otherwise the event.
- The `Join` pill opens the link as in Task 4, inside a try/catch.

In `HomeModule.cs`:
- Add the `CalendarService calendar` constructor parameter (after `monitor`) and the optional `now`.
- Subscribe and unsubscribe `calendar.Changed` like the other sources, marshalled with `BeginInvoke(() => Refresh(HomeItem.NextEvent))`.
- Add `HomeItem.NextEvent => new NextEventRow(...)` to `CreateRow`, with `openCalendar` raising `SectionRequested(IslandModule.Calendar)` and `openSettings` calling `App.ShowSettings(Core.Settings.SettingsPage.Calendar)`.
- Add `HomeItem.NextEvent => _s.ItemNextEvent` to `ItemName`.
- After any refresh of the Agents or NextEvent rows, apply the shared outlier: compute `HomeItems.Accent(Items, agentsRow?.Waiting == true, nextRow?.Soon == true)`. Call `nextRow.SetAccent(accent == HomeItem.NextEvent)`, and make the Agents row's title accent depend on `accent == HomeItem.Agents`; its Open pill rule stays as is.
- Refresh NextEvent on the existing battery-style minute tick too, so "in N min" and the soon state stay right. Add a 30 s `DispatcherTimer`, or reuse one.

In `IslandController.Home.cs`:
- Pass `AppServices.Current.Calendar` to `HomeModule`.
- Add `HomeItem.NextEvent => IslandModule.Calendar.IsAvailable(_runtime.IsAvailable),` to `HomeItemAvailable`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test` (whole solution).
Expected: PASS, no warnings. Render `island-home-calendar.png` (Home with the Next event row, an event 25 minutes away) and describe it.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "feat(windows): Home's Next event row, sharing the outlier with a waiting agent"
```

---

### Task 6: Live check and docs (controller, with the owner)

- [ ] **Step 1: Full suite.** Run `dotnet test`, then three `DOTNET_PROCESSOR_COUNT=2` Services runs.
- [ ] **Step 2: Publish Faqra-test** (the owner approved the H1b live check) and start it. Confirm the Calendar section is in the island and `Settings > Calendar` shows.
- [ ] **Step 3: The owner adds their link.** Open `Settings > Calendar` for them with `App.ShowSettings`, or tell them where it is. They paste their private link into the box themselves; it never goes in the chat. Within about 20 s the link shows as `Working`.
- [ ] **Step 4: Check by UI Automation:**
  - Home's Next event row names an event, or "Nothing in the next 2 weeks".
  - The Calendar section shows this week with today chosen.
  - The next week button works.
  - Restarting Faqra-test offline is optional; skip it.
- [ ] **Step 5: Confirm nothing leaked.**
  - `settings.json` contains no `calendar.google.com` text.
  - `%LOCALAPPDATA%\Faqra\calendar\*.dat` files hold no readable titles (`Select-String` finds nothing).
- [ ] **Step 6: Docs.**
  - `design-system.md`: a Calendar row (week strip, day list, Join pill, states) and the Next event row.
  - `KNOWN-ISSUES.md`: Calendar notes (read-only; a link is a password; offline shows the last copy).
  - Spec milestone table: H1b done.
- [ ] **Step 7:** Commit (`docs(windows): calendar checked live`), push, update memory, and fast-forward local `main`.
