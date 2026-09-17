# Faqra design system

Faqra is a Windows 11 re-implementation of the macOS app Vorssaint. Two authorities govern every
visual decision, in this order:

1. **Upstream Vorssaint** owns structure, copy and behavior. Layouts, section order, control order,
   wording and timings are mirrored from `../Sources/Vorssaint/`. Each C# type names the Swift file
   it mirrors in its header comment.
2. **Windows 11 Fluent** owns the look. Where upstream uses a macOS idiom that has a different
   native form on Windows, the Windows form wins and the deviation is recorded below.

There is no third voice. Faqra does not invent an aesthetic of its own.

## Mode and dials

Operate. The user completes a task, so scanability beats expression and the brand lives in precise
details. Variance 3, motion 2, density 7: a symmetric grid, feedback-only motion, dense rows with
tabular numerals for metrics.

## Foundation

**WPF UI 4.3.0** (`WPF-UI` on NuGet) supplies Fluent: `FluentWindow`, Mica backdrop, rounded window
corners, `CardControl`, `ToggleSwitch`, `Button` appearances, and the light and dark theme
dictionaries. It is installed rather than hand-recreated, and it is the only UI system in the
project. Controls consume its theme brushes, never raw hex.

## Tokens

Defined once in `src/Faqra.App/App.xaml`.

| Token | Value | Use |
|---|---|---|
| `FaqraFont` | Segoe UI Variable Text, Segoe UI | Body and controls |
| `FaqraDisplayFont` | Segoe UI Variable Display, Segoe UI | Page and window titles |
| `FaqraMonoFont` | Cascadia Mono, Consolas | Paths, metrics, anything measured |
| `FaqraTitleSize` / `SectionSize` / `BodySize` / `CaptionSize` | 20 / 14 / 13 / 12 | The whole type scale |
| `PageTitle` / `SectionHeader` / `Body` / `Caption` | styles | The four text roles |

The face is the Windows system font on purpose. A downloaded display face would make a native
utility read as a ported Mac app, which is the one thing this port must not do.

Color comes entirely from WPF UI's theme brushes (`TextFillColorPrimaryBrush`,
`CardBackgroundFillColorDefaultBrush`, `AccentFillColorDefaultBrush`, and so on), so light and dark
both work and the accent follows the user's Windows accent color. No component hard-codes a color.

Spacing runs on a 4px scale. Section headers carry more space above than below (20 above, 8 below).

## Motion

Curves and durations come from [transitions.dev](https://transitions.dev), pulled with
`npx transitions-dev add <name>`, not invented here and not copied from upstream's SwiftUI springs,
which read as slow on Windows. `CubicBezierEase` in `src/Faqra.App/Motion.cs` solves the CSS
cubic-bezier directly, because WPF has no such easing function.

| Token | Value | Where it is used |
|---|---|---|
| shared ease | `cubic-bezier(0.22, 1, 0.36, 1)` | every transition below |
| card-resize | 300ms | the island growing; closing runs at 180ms, the dropdown's 60% ratio |
| panel-reveal | 400ms open, 350ms close, 16px travel | the island's expanded content, and module swaps |
| menu-dropdown | 250ms open, 150ms close, 0.97 pre-scale | the island's sections grid |
| toggle | `cubic-bezier(0.34, 1.35, 0.64, 1)` | available for controls that should overshoot |

Two deliberate omissions. panel-reveal's 2px cross-blur is dropped on the island, because a
transparent window is composited in software and a per-frame blur would cost more than it adds.
Every animation is skipped entirely when `SystemParameters.ClientAreaAnimation` is off, which is
how Windows reports reduced motion.

## Deliberate deviations from upstream

| Upstream | Faqra | Why |
|---|---|---|
| Menu bar status item, top right | System tray icon, bottom right, plus the island pill at top center | Windows 11 cannot move the taskbar to the top. |
| Notch off by default, and absent from the Essentials preset | Island on by default, and added to the first-run set | The hover-at-top behavior is the reason this port exists, so it has to be there on the first launch. Upstream's presets themselves are unchanged; the addition lives in `FeaturePresets.FirstRunFeatures`, with a one-time migration for installs that predate it. |
| Combined metrics in one status item | One tray icon per metric | A tray slot renders one small icon, not arbitrary-width text. |
| SF Symbols | Segoe Fluent Icons glyphs | The native icon family; one family throughout. |
| Permissions tab requests macOS grants | Informational only | Windows needs no consent grant for anything Faqra does. |
| Alert with named action buttons | Message box whose body names the action | WPF's message box labels its buttons OK and Cancel. |
| Notch fixed to the built-in screen | The island follows the pointer between monitors, or pins to primary | A Mac's cutout cannot move. Windows has no cutout, so the useful meaning of upstream's "automatic" is to be on the display you are working on. |
| Menu bar popover hanging under the status item, 0.18s fade | Tray flyout above the taskbar, 12px from the taskbar and screen edge, Acrylic, kept out of Alt+Tab; transitions.dev menu-dropdown (250ms in, 150ms out, 0.97 pre-scale from the bottom-right corner) | It is where Windows 11 puts its own tray flyouts (volume, network), and the motion comes from the shared tokens. |
| Panel section titles uppercase at 10pt, cards at radius 10 | Sentence-case titles, Fluent cards at radius 8 with the card stroke | Fluent owns the look. |
| Seven metric hues (cyan, mint, pink, orange and so on) | Accent for the first series, Fluent's success color for the second (upload, write), caution for power | No component hard-codes a color; the theme's semantic brushes carry both light and dark. |
| Combined metrics text, or one status item per metric | One tray icon per metric, label over value, full reading in the tooltip; below 20px the value drops what the label says ("23" under "CPU", "3:42" under "BAT") | A 16px icon cannot fit "100%" or "3h42m" legibly. |
| Memory pressure from the kernel's pressure level | Derived from free physical memory: Caution under 15% free, Critical under 5% | Windows has no pressure signal. The commit charge is not used: on the development PC it sat at 99% of a limit Windows grows on demand, on a healthy machine. |
| "Swap used", "Open Activity Monitor", "on this Mac" | "Page file used", "Open Task Manager", "on this PC" | The Windows name for the same thing. |
| Drag to reorder panel sections and blocks, eye buttons inline | Move up / Move down and switches on the Monitor settings page | Works from the keyboard, and the panel stays a read-only glance. |
| Notch always at the top | The island attaches to the top, left or right edge | Same reason: nothing physically fixes it to the top. The edge lives in `faqraIslandEdge`, a Faqra-prefixed key so it can never collide with an upstream one. |
| Mixer rows at up to 200% with an amber boost state, a per-app output picker | Rows stop at 100%, no boost color, no per-app picker | Windows session volume cannot exceed 100% or reroute one app. |
| Keep-awake status glyphs from SF Symbols | Segoe Fluent Icons: Cafe, View, QuietHours, Lightbulb, drawn on their ink in the chosen tint; the brand style tints the Faqra mark | One icon family throughout. |
| "Connected to power" needs a battery reporting AC | On mains counts as connected, battery or not | A desktop user who switches the automation on expects it to work. |
| Menu bar countdown title | Not offered; the tooltip reads "awake until 15:45" | A tray icon has no text beside it. |
| Keep-awake shortcut ⌃⌥⌘K | Ctrl+Alt+Win+K | The Windows key takes Command's place. |
| Mixer's percent field is a native text field inside the popover | A text box that appears on click; Esc cancels the edit instead of closing the panel | Same behavior, WPF focus model. |

## Verification

`tests/Faqra.App.Tests/UiRenderTests.cs` builds every window and page against an in-memory store,
asserts the content that must be present, and writes a PNG of each surface to `%TEMP%\faqra-ui`
(override with `FAQRA_UI_SHOTS`). It runs in CI, so a layout that fails to build fails the build.
