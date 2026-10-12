# Faqra Home, Calendar and Shelf: design

Date: 2026-10-12. Status: draft for the owner's review.

Three island sections that make the island the first place the owner looks and the place files wait
on their way somewhere:

- **Home** (H1): what is playing, the next calendar event and the agents, in an order the owner sets.
  The island opens on Home every time it opens on hover.
- **Calendar** (H1): the owner's calendar, read from a private calendar link, in its own section.
- **Shelf** (H2): drag a file to the island and it opens as a depot. Files wait there, and can be
  dragged back out wherever they are needed.

## Sources and licences

| Source | Licence | What Faqra takes |
|---|---|---|
| Upstream Vorssaint (`Sources/Vorssaint/Services/Shelf/*`, `UI/Shelf/*`, `UI/Notch/NotchFilesView.swift`, `NotchCalendarView.swift`) | Same as the rest of the port | The shelf's model (items point at the original file, limits of 200 items, files that moved show as missing), the Files and Calendar sections' place in the island |
| [Ical.Net](https://www.nuget.org/packages/Ical.Net) 5.2.3 | MIT | Reading calendar feeds: repeating events, exceptions, time zones |
| [NodaTime](https://nodatime.org) (Ical.Net's dependency) | Apache 2.0 | Time zones, through Ical.Net |
| NotchNook, boring.notch, Yoink, Dropover | References only | Mechanisms, listed below; no code, art or copy |

Both packages go into `THIRD-PARTY-NOTICES.md`. They are the first NuGet packages Faqra adds since A1.

## Decisions confirmed with the owner

| Topic | Decision |
|---|---|
| Home's content (2026-10-10) | What is playing, the next calendar event and the agents by default; items can be added, removed and reordered |
| Calendar source (2026-10-10) | A private calendar link (ICS) pasted in Settings; read-only, no sign-in |
| Order (2026-10-10, 2026-10-12) | After A3: H1 and H2, then M7 (installer) |
| Hover (2026-10-12) | Hovering always opens Home. Picking a section shows it until the island closes |
| Calendar scope (2026-10-12) | Home shows the next event, and the Calendar section (a placeholder today) shows the days ahead |
| Shelf (2026-10-12, owner's words) | "If I'm dragging a file, the island should open up and act as a depot where I put the files. The files stay there for about 15 minutes. After 15 minutes, if I ever go back up there, the file should be somewhere I can drag it and put it wherever I need to." Read as: files dropped in the last 15 minutes sit up front; older ones move to an Earlier list in the same section, still draggable, until removed |

## Design read

> Reading this as: three new island sections (a glance at the owner's day, their calendar, and a
> file depot) for one owner at a Windows desk, in Operate mode, with Faqra's quiet dark-island
> language, leaning toward the existing WPF UI Fluent system in `Windows/design-system.md`.

- **Mode:** new surfaces inside an established world. The island palette, the system font
  (Segoe UI Variable), the 12 px-radius `Fill` panels, the pill buttons and the motion tokens are
  inherited, with no new faces or colours. One accent (`Accent`), used for the single thing that
  needs the owner.
- **Dials:** variance 3 (aligned rows, one column), motion 3 (hover, press and the island's own
  open and close), density 6 (several items readable in one look).

### References and what they teach

| Reference | Mechanism taken |
|---|---|
| NotchNook ([review](https://www.macwelt.de/article/2407506/notchnook-test-macbook.html), [launch](https://mezha.ua/en/2024/07/22/the-notchnook-app-has-been-released-for-macbook-which-makes-the-camera-cutout-on-top-of-the-screen-functional/amp/)) | The opened notch is a "nook" of widgets the owner can swap and reorder; the calendar can replace media; files dropped on its tray tab stay put |
| boring.notch ([v2.8 notes](https://newreleases.io/project/github/TheBoredTeam/boring.notch/release/v2.8-rc.0)) | Home is music plus calendar plus a timer; a week view; events open meeting links (Meet, Zoom, Teams, Webex) straight from the notch |
| Yoink ([TidBITS](https://tidbits.com/2017/09/05/yoink-takes-mac-drag-and-drop-to-the-next-level/)) | The holding area appears at the screen edge when a drag begins and stays visible while it holds something |
| Dropover ([site](https://dropoverapp.com/)) | Shelves are temporary and appear only when needed; recently closed shelves can be reopened, which is what the Earlier list does here |

Synthesis:

1. One column of rows, each a single job, read top to bottom: no tile grid.
2. The owner orders the rows; Faqra never reorders them by itself.
3. Exactly one thing may wear the accent at a time: an agent waiting, or a meeting about to start.
4. Every row acts in place: play and skip, join a meeting, open the waiting card, drag a file out.
5. The depot appears only while a file is on its way, and is as big as the island while it does.
6. Nothing a drop puts on the shelf is lost silently: old items move aside, they do not vanish.

The rut this avoids: a dashboard of equal widget tiles (the category default), and its opposite, one
oversized now-playing hero that buries everything else.

## Home (H1)

A Faqra-only section, `faqraHome`, first in the island's section order. Opening on hover always shows
Home; a section picked from the section list or a shortcut stays until the island collapses. If the
owner hides Home in Settings → Island, hovering reopens the last section as today.

**Rows**, each a `Fill` panel, in the owner's order:

| Row | Shows | Acts |
|---|---|---|
| Now playing (default) | Artwork 44 px, title, artist; "Nothing playing" when idle | Play or pause, next |
| Next event (default) | When ("in 25 min", or "14:30", or "Tomorrow 09:00"), title, place; all-day events as "Today" | Join, when the event carries a Meet, Zoom, Teams or Webex link; the row opens the Calendar section |
| Agents (default) | Up to 3 sessions: orb, name, status, the most urgent first | A waiting session's row opens its card in the Agents section |
| Shelf | How many files wait, the newest three as thumbnails | Drag a thumbnail out; the row opens the Shelf section |
| Timer | The running timer, or "No timer" | Opens the Timer section |
| System | CPU and memory in one line | Opens the System section |
| Battery | Charge and time left (on PCs with a battery) | None |

**The outlier.** An agent waiting on the owner, or an event starting within 10 minutes, takes the
accent on its row (accent text and the primary pill). If both happen, the agent wins: it is blocking
work. Every other row stays neutral.

**Editing.** An Edit button at the end of Home turns the rows into an editable list: each row gets
Move up, Move down and Remove buttons (the keyboard and single-pointer path), and an Add item list
shows the rows not on Home. Done saves. The order lives in one Faqra-only key, `faqraHomeItems`, a
comma list of raw names; unknown names are dropped, so items can be added later.

**Empty and error states.** No calendar link: the event row says "Add your calendar link" with a
button to Settings. A link that fails: the last good events stay, with "Updated 2 h ago"; a link that
no longer works (401, 403, 404): "This calendar link stopped working. Paste a new one in Settings."
No agents: the row says "No agents running". Each state is a sentence, never a blank row.

## Calendar (H1)

**Settings.** A Calendar page in Settings: one or more private calendar links (Google's "Secret
address in iCal format", Outlook's published ICS link, iCloud's public link). `webcal://` is read as
`https://`; nothing else but `https` is accepted. A link is a password in all but name: Faqra stores
it with Windows' per-user data protection (DPAPI), shows it masked, never logs it, and never sends it
anywhere but its own host. The owner pastes it in the page, never in a chat.

**Reading.** Faqra fetches each link every 15 minutes, on wake and when the network returns, with a
20 s timeout and a 5 MB cap, then expands the next 14 days with Ical.Net (repeats, exceptions, moved
occurrences, time zones, all-day events). Cancelled events are skipped. The last good copy is kept on
disk, protected the same way, so Home has an answer at start-up and offline. The log records only
"calendar refreshed" or the HTTP status, never a title.

**The section.** The Calendar placeholder becomes a week strip (seven days, today marked, arrows for
the previous and next week) above the chosen day's events: time range, title, place, and Join for
meeting links. Upstream's month grid is not ported; the island is too small for a readable month at
its default 440 by 480 px.

## Shelf (H2)

The Files placeholder becomes the Shelf, gated on `AppFeature.Shelf` (ported from "not applicable"
to built).

**Opening as a depot.** Dragging a file to the top centre of the screen opens the island as soon as
the file reaches it, and the whole open island becomes one drop target: "Drop files here". While a
mouse button is held near the top edge, the island's catch area grows so the drop does not need to
hit the small pill. Opening as soon as any drag starts, the way Yoink does on the Mac, has no
documented Windows signal; H2 tries the candidates first (a drag-start window event, the shell's drag
image) and keeps one only if it proves reliable across Explorer, the desktop, browsers and Outlook.

**Items.** Files and folders. Like upstream, the shelf points at the original file and copies
nothing; a file that moved or was deleted shows as "Moved or deleted" with Remove. Each drop is kept
together, so a set of files dragged in together can be dragged out together.

**The 15 minutes.** Items dropped in the last 15 minutes sit up front under "Just now". After that
they move to "Earlier" in the same section, newest first, still draggable, until the owner removes
them. Earlier keeps the 200 newest items; the oldest drop out beyond that.

**Dragging out.** Drag an item, or a whole drop, from the Shelf or from Home's Shelf row to Explorer,
the desktop, a mail or a chat. Windows decides copy or move as usual (Shift moves, Ctrl copies). If
the file was moved, it leaves the shelf; if it was copied, it stays.

**Removing.** Each item has Remove; Earlier has Clear. Nothing is deleted from disk, ever.

**Persistence.** `shelf.json` in Faqra's local data folder: paths, names, sizes and drop times, never
file contents. Thumbnails come from Windows' shell at display time.

## Milestones

| | Builds | Done when |
|---|---|---|
| H1a | The Home section, hover opens Home, the Now playing, Agents, Timer, System and Battery rows, Edit with add, remove and reorder | Hovering opens Home; rows act in place; the order survives a restart |
| H1b | Calendar links in Settings, the reader, the Next event row, the Calendar section | The owner's own link shows their next event and their week, offline included |
| H2 | The Shelf section, the depot on drag, Just now and Earlier, dragging out, Home's Shelf row | Files dragged to the island stay, move to Earlier after 15 minutes, and drag back out to Explorer |

Each milestone gets its own plan and runs as A1 to A3 did. M7 (installer) follows H2.

## Not in v1

Text and links on the shelf (upstream supports both), sharing from the shelf, writing to calendars,
a month grid, calendar colours per event, weather on Home.

## Code layout

| Piece | Where |
|---|---|
| Home items, order and the hover rule | `Faqra.Core/Island/Home*.cs`; `IslandModule.FaqraHome` |
| Calendar model, reading, next-event rule | `Faqra.Core/Calendar/*` (pure, tested with recorded feeds); fetching and the protected store in `Faqra.Services/Calendar/*` |
| Shelf model, the 15-minute rule, limits | `Faqra.Core/Shelf/*`; persistence and file checks in `Faqra.Services/Shelf/*`; thumbnails and drag in `Faqra.Win32` and `Faqra.App/Island/Modules/Shelf*.cs` |
| Views | `Faqra.App/Island/Modules/HomeModule.cs`, `CalendarModule.cs`, `ShelfModule.cs` |
| Settings | `Faqra.App/Settings/Pages/CalendarPage.cs`; Shelf options on the Island page |

## Risks

- **Seeing a drag before it reaches the island.** Undocumented on Windows; the island catching the file
  at the top of the screen is the guaranteed path.
- **Calendar feeds differ.** Google, Outlook and iCloud each write ICS their own way; tests run on
  recorded feeds from all three, and the owner's link is checked live.
- **A leaked calendar link exposes the calendar.** Stored with DPAPI, masked, never logged.
- **Drops on a window that never takes focus.** The island stays non-activating; the drop target and
  drag-out must work without activating it. Checked in H2's first task.
- **Hover opening Home every time** may feel wrong after a section was in use; the owner can hide Home
  to get today's behaviour back.

## Testing

Core rules (row order, the outlier, the next-event rule, recurrence expansion on recorded feeds, the
15-minute grouping, the 200-item limit, missing files) get unit tests first. Views get render tests
(`RenderInk` screenshots at 440 by 480 and at the island's smallest and largest sizes). Each milestone
ends with a live check on Faqra-test, with the owner's OK for anything touching their account: their
calendar link is pasted by them, in Settings.
