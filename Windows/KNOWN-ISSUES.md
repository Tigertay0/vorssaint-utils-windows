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

### Crash inside Windows' media session component (root cause found, fix pending)

Seen once, 2026-09-16 21:10:32, on the M5 test build: an access violation (0xc0000005) in
`Windows.Media.MediaControl.dll` (10.0.26100.9278, offset 0x225b8). Dump:
`%LOCALAPPDATA%\CrashDumps\Faqra.exe.61016.dmp`. A crash also ends any keep-awake session.

Native stack, read without WinDbg by a small dbghelp + Microsoft public symbols tool
(`%TEMP%\faqra-live\dumpsym`): the fault is in `GlobalSystemMediaTransportControlsSessionImpl`'s own
playback-update work item on a shell thread-pool thread, while it releases the session's cached
playback-info object (`[session+0x70]`) whose memory had already been freed and reused (vtable read
0x400000000). No Faqra code was on the stack and every other thread was idle.

Why it was freed: `GetPlaybackInfo` reads that same cached pointer and takes a reference without a
lock, and the update work item swaps and releases it without a lock. When a read lands inside a swap,
the caller gets a reference to an object that is freed and immediately reallocated as the new cached
object; when the caller's RCW later releases it, the session's cache holds freed memory and the next
playback update crashes. `NowPlayingService.RefreshAsync` calls `GetPlaybackInfo` from a thread-pool
continuation right after each `PlaybackInfoChanged`/`MediaPropertiesChanged` event, which is
exactly when the next update is likely to run. A Windows race, made likely by Faqra's timing.

Status: hypothesis from the dump and disassembly. A stress repro (`%TEMP%\faqra-live\gsmtcrace`)
compares the current call pattern with the candidate fix (read playback info only inside the
`PlaybackInfoChanged` callback, which runs inside the update work item and so cannot race it).
## Gaps a user will notice

These are scheduled or deliberately out of scope, not broken. Listed because they look like bugs from
the outside.

- **Check for updates is greyed out** in the tray menu. Milestone 7.
- **Command bar pieces upstream has that this build leaves out:** menu commands of the app in front,
  file search, the selected text, emoji, clipboard history, saved links and scripts, the actions panel
  (pin, name, hide, a row's own shortcut), the per-app shortcuts sheet, compact mode, drag to move, the
  Windows Settings panes, Wi-Fi, today's date and time rows, and the learned query habits store (plain
  usage still ranks). An app row always launches; it does not switch to a running copy first.
- **Command bar is not in the first-run set**, like upstream: install it from the Feature Hub. Its
  Alt+Space shortcut is on once installed.
- **Shortcuts page lists only built features** (keep awake, command bar). A shortcut another app
  already holds is reported only when Windows refuses it at registration; Windows has no readable
  list of other apps' shortcuts.
- **Island Controls section still to come:** it shows "Coming in a later update." Music, Timer,
  System and Mixer are built.
- **Mixer pieces upstream has that this build leaves out:** volume above 100% (Windows caps an app's
  session volume at 100%, so a saved boost plays at 100%), a per-app output picker (Windows has no
  per-app routing API), the system sounds and microphone pickers, finer volume steps, and the output
  switcher shortcut. Apps are listed by executable, so two different apps built as the same
  "app.exe" share one row.
- **Keep awake pieces upstream has that this build leaves out:** the "selected apps are running"
  automation (needs an app picker), pointer jiggle, the menu bar countdown (a tray icon has no title;
  the tooltip shows the end time), closed-lid mode (macOS only).
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
