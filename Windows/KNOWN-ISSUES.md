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

### Crash inside Windows' media session component (open, root cause unknown)

Seen once, 2026-09-16 21:10:32, on the M5 test build: an access violation (0xc0000005) in
`Windows.Media.MediaControl.dll` (10.0.26100.9278, offset 0x225b8) ended the process. That DLL backs
the GlobalSystemMediaTransportControls API that `NowPlayingService` uses for the island's music idle
view (milestone 3). The crash dump is `%LOCALAPPDATA%\CrashDumps\Faqra.exe.61016.dmp`. `dotnet-dump`
shows no managed exception: the fault is on a native WinRT callback thread, and the UI thread was
inside `OutsideClickMonitor.OnMouseEvent` (so the island or panel was open). No earlier Faqra crash in
three days of event logs. Next step: open the dump in WinDbg with Microsoft symbols to get the native
stack, then check `NowPlayingService` for a session released while its `MediaPropertiesChanged` /
`PlaybackInfoChanged` callbacks are in flight (it detaches and drops the session RCW on every
`CurrentSessionChanged`). A crash also ends any keep-awake session, since Windows drops a dead
process's power request.

## Gaps a user will notice

These are scheduled or deliberately out of scope, not broken. Listed because they look like bugs from
the outside.

- **Check for updates is greyed out** in the tray menu. Milestone 7.
- **The command bar is not built yet.** The Feature Hub labels it "Coming in a later update".
- **Island Controls section still to come:** it shows "Coming in a later update." Music, Timer,
  System and Mixer are built.
- **Mixer pieces upstream has that this build leaves out:** volume above 100% (Windows caps an app's
  session volume at 100%, so a saved boost plays at 100%), a per-app output picker (Windows has no
  per-app routing API), the system sounds and microphone pickers, finer volume steps, and the output
  switcher shortcut. Apps are listed by executable, so two different apps built as the same
  "app.exe" share one row.
- **Keep awake pieces upstream has that this build leaves out:** the "selected apps are running"
  automation (needs an app picker), pointer jiggle, the menu bar countdown (a tray icon has no title;
  the tooltip shows the end time), closed-lid mode (macOS only). The shortcut is fixed at
  Ctrl+Alt+Win+K until the shortcut recorder lands in milestone 6.
- **"External display" automation guesses what is built in.** Windows has no built-in flag; a
  display counts as built in when it is connected internally (a laptop panel). On a desktop every
  monitor is external, so the automation is on whenever it is switched on.
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
