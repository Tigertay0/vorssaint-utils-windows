# Known issues

Things that are wrong or missing, kept here so they are not rediscovered. Milestone scope lives in
`../docs/superpowers/specs/2026-09-15-faqra-stage-1-design.md`; this file is only for defects and for
gaps a user would notice.

## Defects

### The island can show a paused session instead of the one playing (deferred)

Reported 2026-09-16 as "the island shows yesterday's YouTube tab instead of Spotify". Investigated
with a probe of every system media session: Spotify was not registered with Windows' media controls
at all (a Spotify setting was off; the user turned it on and Spotify now appears and becomes current).

A weaker version of the problem remains and the user chose to defer it. `NowPlayingService` takes
`GetCurrentSession()` (`src/Faqra.Services/Media/NowPlayingService.cs:132`), which Windows can leave on
a paused browser tab while another app plays, and it only listens to `CurrentSessionChanged`. The fix,
when it is picked up: enumerate `GetSessions()`, prefer a `Playing` session (most recently updated
first), fall back to `GetCurrentSession()`, subscribe to `SessionsChanged`, and keep swallowing
exceptions from sessions that vanish mid-query.

## Gaps a user will notice

These are scheduled or deliberately out of scope, not broken. Listed because they look like bugs from
the outside.

- **Keep awake and Check for updates are greyed out** in the tray menu. Milestones 5 and 7.
- **The mixer, keep awake and command bar are not built yet.** The Feature Hub now labels them
  "Coming in a later update"; the popover panel hides their tabs until they exist.
- **Island sections still to come:** Controls and Mixer show "Coming in a later update." Music,
  Timer and System are built.
- **Monitor readings upstream has that Windows does not show:** temperatures and fan speed (no
  Windows API without vendor drivers), SMART data, disk eject and tools, the speed test, per-app
  CPU/GPU/memory/network/energy lists, battery health and cycle count, adapter wattage. The panel's
  in-section edit mode (drag blocks, hide inline) is replaced by the Monitor settings page.
- **Island System cards are not clickable.** Upstream opens a metric detail view; that view is not
  ported.
- **Tray metric icons are tight at 100% display scaling.** A 16px icon fits "23" under "CPU" but
  rates such as "↑320K" are small; the tooltip always carries the full reading.
- **No installer, so the app cannot start with Windows properly.** Milestone 7. Launch at login works
  but records whatever path the build currently sits at, which breaks if that folder moves.
- **Only English ships.** The twelve other languages resolve to English by design until their
  catalogs are ported.
