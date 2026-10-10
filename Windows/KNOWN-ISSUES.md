# Known issues

Things that are wrong or missing, kept here so they are not rediscovered. Milestone scope lives in
`../docs/superpowers/specs/2026-09-15-faqra-stage-1-design.md`; this file is only for defects and for
gaps a user would notice.

## Defects

### Tray icons ended Faqra after the display woke (fixed, desktop check pending)

Two crashes on the M6 test build, 2026-09-24 20:16 and 2026-09-26 13:23, each after more than a day
of uptime and each 8 seconds after the display woke from keyboard or mouse input (Kernel-Power 566,
reason InputHid), just before Windows re-enumerated the displays. Stack: `TrayMetricsController.Draw`
→ `TrayIcon.Update` → `NIM_MODIFY` refused → `Add` → both `NIM_ADD` calls refused →
`Win32Exception` (1008), unhandled on the dispatcher. Faqra survived seven other wakes in those days,
so the taskbar turns calls away only some of the time.

Fixed in `TrayIcon`: refusals return false instead of throwing; an icon that outlived a refused update
is adopted with `NIM_MODIFY` rather than added a second time; nothing is sent while the taskbar does
not answer a 500 ms `WM_NULL`, because a call queued to a hung taskbar can run later and leave a
duplicate. The metric icons stay undrawn after a refusal so the next reading retries; the main icon
retries every 2 seconds. `TrayIconTests` reproduces the crash against a fake shell. Not yet checked on
the desktop: with the fixed build running, restart Explorer, then freeze it for about 8 seconds; Faqra
should stay up and every icon should come back exactly once.

Left as is (rare): a delete the shell refuses is not retried. Turning a metric off during the few
seconds after a wake can leave its icon in the tray until Faqra quits.

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

### Crash inside Windows' media session component (seen once, not reproduced)

Seen once, 2026-09-16 21:10:32, on the M5 test build: an access violation (0xc0000005) in
`Windows.Media.MediaControl.dll` (10.0.26100.9278, offset 0x225b8). Dump:
`%LOCALAPPDATA%\CrashDumps\Faqra.exe.61016.dmp`. A crash also ends any keep-awake session.

Native stack, read without WinDbg by a small dbghelp + Microsoft public symbols tool
(`%TEMP%\faqra-tools\dumpsym`): the fault is in `GlobalSystemMediaTransportControlsSessionImpl`'s own
playback-update work item on a shell thread-pool thread, while it releases the session's cached
playback-info object (`[session+0x70]`) whose memory had already been freed and reused (vtable read
0x400000000). No Faqra code was on the stack and every other thread was idle.

The first suspect, not confirmed by the stress runs below: `GetPlaybackInfo` reads that same cached
pointer and takes a reference without a lock, and the update work item swaps and releases it without
a lock, so a read landing inside a swap could over-release the object. `NowPlayingService.RefreshAsync`
calls `GetPlaybackInfo` from a thread-pool continuation right after each playback or media event.


Status: not reproduced, so no fix is applied. A stress tool (`%TEMP%\faqra-tools\gsmtcrace`) runs a
silent media session flipping between playing and paused about 65 times a second and tried three
triggers on 2026-09-17, all surviving: NowPlayingService's exact pattern (90 s, 5,901 reads), four
threads calling `GetPlaybackInfo` in a tight loop (90 s, 312 million reads over 5,788 swaps, which
argues against the simple read-during-swap race above), and acquiring, subscribing to and dropping
the session with forced garbage collection (240 s, 10,359 cycles). What the tool does not reproduce
is a real app's session: Spotify or a browser sending artwork and timeline updates, several sessions
changing at once, and the current session switching between apps. If it happens again, keep the
new dump and note what was playing.

A second crash in the same family, 2026-09-28 05:05:12, after 30 hours of uptime, no dump:
`NullReferenceException` in `WinRT.IObjectReference.Finalize()` (WER CLR20r3: WinRT.Runtime 2.2.0.0,
method token 0x060007bf, IL offset 0; nothing else on the stack). The finalizer's `Dispose()` and,
through tier-1 guarded devirtualization, `ObjectReference<T>.Release()` and .NET 8's managed
`Marshal.Release` (`*(*(void***)pUnk + 2)`) can all be inlined into that one frame, so a near-null
fault there fits releasing a native object whose memory was already freed and zeroed: the same
over-release the 2026-09-16 dump showed from Windows' side. `NowPlayingService` is the only WinRT
user, and every `RefreshAsync` leaves media properties, playback info and buffers to the finalizer,
so the object cannot be named without a full dump. No fix applied. CsWinRT issue #2532 (finalizer
release into an uninitializing apartment) was checked and does not match: Faqra never tears down an
apartment, and that stack keeps `Release()` as its own frame. Next step: turn on full WER dumps for
`Faqra.exe` on the test machine (needs one admin prompt) so the next crash names the object.

Related bug found while reading, not a crash: `ReadThumbnailAsync` ignores the buffer that
`stream.ReadAsync` returns and reads its own, which Windows may leave empty, so artwork can be missing.

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
- **Replying to Claude comes in A3.** The island approves tools, answers questions, opens when a turn
  ends and goes to the session's window (A2), but the "Has answered" card has no reply box yet, and
  Claude's words show as plain text with their Markdown marks.
- **A waiting card holds Claude Code's own prompt.** While a card waits (up to 108 s), Claude Code
  waits on Faqra's hook. Esc, the collapse button or "Answer in Claude Code" let it go at once;
  clicking elsewhere keeps the card behind the pill. A huge request (over about 1 MB once escaped)
  gets no card and is answered in Claude Code.
- **Questions need an interactive session.** Claude Code offers AskUserQuestion only in sessions
  with a permission host, so `claude -p` runs never show a question card. Free-text answers (the
  "Your own answer" box) go back as the answer itself; A2's live check could not try this in a
  headless run.
- **Always allow is offered only when Claude Code suggests a rule.** Faqra keeps only "allow" rules
  (saved to the project's `.claude/settings.local.json`) and "accept edits" for the session. A
  command that writes outside the working folders, for example, comes with a "add this folder"
  suggestion instead, which Faqra drops, so its card has no Always allow.
- **A session shows its folder until Claude names it.** Claude Code writes the conversation's title
  into the transcript after the first exchange; a name you give with `/rename` wins.
- **A busy session that goes quiet for 30 minutes rests.** A session left Working, Thinking or in an
  error with no hook event for 30 minutes shows as Idle, since a crashed or killed Claude Code never
  sends SessionEnd. Sessions waiting on an approval or a question are kept.
- **Agents hooks outlive the feature.** Uninstalling Agents in the Feature Hub stops the island
  listening but leaves the hooks in `~/.claude/settings.json`; each hook then gives up in about 74 ms
  (about 78 ms when Faqra is listening, budget 80 ms). Remove them from Settings, Agents. The M7
  uninstaller should offer the same.
- **The relay ships only with the dual publish.** `faqra-hook.exe` is copied to
  `%LOCALAPPDATA%\Faqra\bin` from the folder Faqra runs in, so a build published without it cannot
  install hooks. M7 packaging carries it.
