# Known issues

Things that are wrong or missing, kept here so they are not rediscovered. Milestone scope lives in
`../docs/superpowers/specs/2026-09-15-faqra-stage-1-design.md`; this file is only for defects and for
gaps a user would notice.

## Defects

### The island shows a stale media session

Reported 2026-09-16. The island displays a YouTube tab watched the day before instead of the Spotify
track playing now. Title, artist and artwork all come from the dead session.

`NowPlayingService` picks its session with
`GlobalSystemMediaTransportControlsSessionManager.GetCurrentSession()`
(`src/Faqra.Services/Media/NowPlayingService.cs:132`). That returns whichever session Windows last
treated as current, which a browser tab can keep long after it stopped playing, and the service only
subscribes to `CurrentSessionChanged`, so it never hears other sessions change.

Probable fix: enumerate `GetSessions()`, prefer a session whose `PlaybackStatus` is `Playing`, fall
back to `GetCurrentSession()` only when none is, and subscribe to `SessionsChanged` as well. Where
several play at once, prefer the most recently updated. Keep swallowing exceptions from sessions that
vanish mid-query. Verify with real playback against a stale browser tab, not only with unit tests.

## Gaps a user will notice

These are scheduled, not broken. Listed because they look like bugs from the outside.

- **Left-clicking the tray icon does nothing.** The popover panel is milestone 4. It is the first
  thing anyone tries.
- **Keep awake and Check for updates are greyed out** in the tray menu. Milestones 5 and 7.
- **The mixer and the six monitor rows show as installed but do nothing.** The Feature Hub answers
  "can Windows run this", not "is it built yet". Their code lands in milestones 4 and 5.
- **Nine of the island's thirteen sections are placeholders.** Music and Timer are built; Controls,
  Mixer and System arrive with their features.
- **No installer, so the app cannot start with Windows properly.** Milestone 7. Launch at login works
  but records whatever path the build currently sits at, which breaks if that folder moves.
- **Only English ships.** The twelve other languages resolve to English by design until their
  catalogs are ported.
