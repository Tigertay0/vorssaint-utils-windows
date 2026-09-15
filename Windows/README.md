# Faqra (Windows)

Faqra is a Windows 11 re-implementation of [Vorssaint](https://github.com/vorssaint/vorssaint-utils),
the free and open-source macOS menu bar toolkit. The Swift sources in `../Sources/` are the
specification; every C# type that mirrors a Swift type says so in its header comment.

The code is GPL-3.0-or-later like upstream. Per upstream's `TRADEMARKS.md`, this build uses its
own name, icon and update feed and is not an official Vorssaint release.

## Build

Requires the .NET 8 SDK.

```powershell
cd Windows
dotnet build
dotnet test
dotnet run --project src/Faqra.App
.\src\Faqra.App\bin\Debug\net8.0-windows10.0.19041.0\Faqra.exe --selftest
```

## Layout

| Project | Role |
|---|---|
| `Faqra.Core` | OS-independent logic: settings registry, feature catalog, shortcuts, metrics formatting, layouts, command bar ranking, localization. Fully unit-tested. |
| `Faqra.Win32` | P/Invoke and COM interop only: tray icon, hotkeys, window styles, DPI, performance counters. No business logic. |
| `Faqra.Services` | Live singletons mirroring upstream `Services/`: keep awake, system monitor, mixer, command bar, self-test, uninstaller. |
| `Faqra.App` | WPF shell: tray controller, popover panel, island pill, command bar, settings, onboarding. |
