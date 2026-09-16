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

## Deliberate deviations from upstream

| Upstream | Faqra | Why |
|---|---|---|
| Menu bar status item, top right | System tray icon, bottom right, plus the island pill at top center | Windows 11 cannot move the taskbar to the top. |
| Notch off by default | Island on by default | The hover-at-top behavior is the reason this port exists. |
| Combined metrics in one status item | One tray icon per metric | A tray slot renders one small icon, not arbitrary-width text. |
| SF Symbols | Segoe Fluent Icons glyphs | The native icon family; one family throughout. |
| Permissions tab requests macOS grants | Informational only | Windows needs no consent grant for anything Faqra does. |
| Alert with named action buttons | Message box whose body names the action | WPF's message box labels its buttons OK and Cancel. |

## Verification

`tests/Faqra.App.Tests/UiRenderTests.cs` builds every window and page against an in-memory store,
asserts the content that must be present, and writes a PNG of each surface to `%TEMP%\faqra-ui`
(override with `FAQRA_UI_SHOTS`). It runs in CI, so a layout that fails to build fails the build.
