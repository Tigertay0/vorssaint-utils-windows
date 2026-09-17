// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors CommandBarCatalog.build and the app/window entry factories in
// Sources/Vorssaint/Services/CommandBar/CommandBarCatalog.swift, limited to what Faqra runs on Windows today:
// keep awake, volume, power, folders, settings pages, generated toggles, apps and open windows.

using System.Diagnostics;
using System.IO;
using System.Media;
using Faqra.App.Settings;
using Faqra.Core.CommandBar;
using Faqra.Core.Features;
using Faqra.Core.Localization;
using Faqra.Core.Settings;
using Faqra.Core.Shortcuts;
using Faqra.Win32.Power;
using Faqra.Win32.Shell;
using Faqra.Win32.Windows;
using Wpf.Ui.Controls;

namespace Faqra.App.CommandBar;

internal sealed class CommandBarCatalog(AppServices services)
{
    /// <summary>Worth discovering on an empty bar before anything has been used (CommandBarCatalog.swift:163-167, Windows subset).</summary>
    public static readonly IReadOnlyList<string> CuratedSuggestionIds = ["action.keepAwake", "action.volume", "action.power.lock", "action.openSettings"];

    private static CommandBarStrings Bar => CommandBarStrings.For(L10n.Shared.Language);

    private bool Installed(AppFeature feature) => services.FeatureRuntime.IsAvailable(feature);

    /// <summary>Everything the bar can run except apps and windows, rebuilt on every opening so titles reflect live state.</summary>
    public List<CommandBarRow> Build()
    {
        var s = L10n.Shared.S;
        var hub = FeatureHubStrings.For(L10n.Shared.Language);
        var rows = new List<CommandBarRow>();
        AddKeepAwake(rows, s, hub);
        AddSound(rows);
        AddPower(rows);
        AddFolders(rows);
        foreach (var toggle in CommandBarCatalogSupport.Toggles(AppFeatures.All.Where(f => Installed(f) && FeatureWindowsSupport.IsBuilt(f)), services.Store, Bar, hub))
        {
            rows.Add(new CommandBarRow(toggle.Entry, new CommandBarIcon.Symbol(SymbolRegular.ToggleLeft24), _ =>
            {
                services.Store.Set(toggle.Key, !toggle.IsOn);
                services.FeatureRuntime.Sync([toggle.Feature]);
            }, IsActive: toggle.IsOn));
        }
        AddSettingsPages(rows, s);
        rows.Add(Action("action.openSettings", Bar.ActionOpenSettings, s.SettingsTitle, SymbolRegular.Settings24, _ => App.ShowSettings()));
        return rows;
    }

    private void AddKeepAwake(List<CommandBarRow> rows, Strings s, FeatureHubStrings hub)
    {
        if (!Installed(AppFeature.KeepAwake))
        {
            return;
        }
        var session = services.KeepAwake.Session;
        var area = hub.FeatureTitles[AppFeature.KeepAwake];
        var shortcut = GlobalShortcutRole.KeepAwake.IsActive(services.Store) ? GlobalShortcutRole.KeepAwake.Saved(services.Store).DisplayText : null;
        // A typed number is a duration in minutes; without one the row is the plain on and off switch.
        rows.Add(new CommandBarRow(
            Entry("action.keepAwake", session.IsActive ? s.MenuDisableAwake : s.MenuEnableAwake, area, s.KeepAwakeTitle),
            new CommandBarIcon.Symbol(SymbolRegular.Flash24),
            minutes =>
            {
                if (minutes is { } m)
                {
                    session.Activate(m);
                }
                else
                {
                    session.Toggle();
                }
            },
            IsActive: session.IsActive,
            Argument: new CommandBarArgument(1, 480, Optional: true),
            ShortcutText: shortcut));
        foreach (var (id, label, minutes) in new[] { ("action.keepAwake.30", s.Minutes30, 30), ("action.keepAwake.60", s.Hour1, 60), ("action.keepAwake.120", s.Hours2, 120) })
        {
            rows.Add(new CommandBarRow(Entry(id, string.Format(Bar.KeepAwakeForFormat, label), area, s.KeepAwakeTitle),
                new CommandBarIcon.Symbol(SymbolRegular.Timer24), _ => session.Activate(minutes)));
        }
    }

    private void AddSound(List<CommandBarRow> rows)
    {
        if (!Installed(AppFeature.Mixer))
        {
            return;
        }
        var mixer = services.Mixer;
        rows.Add(new CommandBarRow(Entry("action.volume", Bar.VolumeTitle, string.Format(Bar.ArgumentRangeFormat, 0, 100)),
            new CommandBarIcon.Symbol(SymbolRegular.Speaker224), value =>
            {
                if (value is { } v)
                {
                    mixer.SetOutputVolume(v / 100.0);
                }
            },
            Argument: new CommandBarArgument(0, 100, Optional: false)));
        if (mixer.Snapshot.OutputMuted is { } muted)
        {
            rows.Add(new CommandBarRow(Entry("action.soundMute", muted ? Bar.SoundUnmute : Bar.SoundMute, Bar.VolumeTitle),
                new CommandBarIcon.Symbol(muted ? SymbolRegular.SpeakerMute24 : SymbolRegular.Speaker224), _ => mixer.ToggleOutputMute(), IsActive: muted));
        }
    }

    private static void AddPower(List<CommandBarRow> rows)
    {
        var general = L10n.Shared.S.SettingsPageTitles[SettingsPage.General];
        // What people try on day one. Everything that ends the session confirms on the row first.
        rows.Add(Power(PowerAction.Sleep, Bar.PowerSleep, null, SymbolRegular.WeatherMoon24));
        rows.Add(Power(PowerAction.Lock, Bar.PowerLock, null, SymbolRegular.LockClosed24));
        rows.Add(Power(PowerAction.Restart, Bar.PowerRestart, Bar.PowerRestartConfirm, SymbolRegular.ArrowClockwise24));
        rows.Add(Power(PowerAction.ShutDown, Bar.PowerShutDown, Bar.PowerShutDownConfirm, SymbolRegular.Power24));
        rows.Add(Power(PowerAction.SignOut, Bar.PowerLogOut, Bar.PowerLogOutConfirm, SymbolRegular.SignOut24));

        CommandBarRow Power(PowerAction action, string title, string? confirm, SymbolRegular glyph) =>
            new(Entry($"action.power.{action.ToString().ToLowerInvariant()}", title, general), new CommandBarIcon.Symbol(glyph),
                _ => Beep(PowerActions.Run(action)), ConfirmationPrompt: confirm);
    }

    private static void AddFolders(List<CommandBarRow> rows)
    {
        // The folders every PC has: a fixed set of destinations, never a search through anyone's files.
        var folders = new (Environment.SpecialFolder? Special, string? Path)[]
        {
            (null, KnownFolders.Downloads()),
            (Environment.SpecialFolder.MyDocuments, null),
            (Environment.SpecialFolder.DesktopDirectory, null),
            (Environment.SpecialFolder.MyPictures, null),
            (Environment.SpecialFolder.MyMusic, null),
            (Environment.SpecialFolder.MyVideos, null),
            (Environment.SpecialFolder.UserProfile, null),
        };
        foreach (var (special, known) in folders)
        {
            var path = known ?? Environment.GetFolderPath(special!.Value);
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                continue;
            }
            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
            var entry = new CommandBarEntry($"folder.{path}", name, Bar.KindFolder, Bar.KindFolder, CommandBarEntryKind.Folder, CommandBarSource.Folders);
            rows.Add(new CommandBarRow(entry, new CommandBarIcon.File(path), _ => Beep(ShellOpen(path)), ActivatesAnotherWindow: true));
        }
    }

    private static void AddSettingsPages(List<CommandBarRow> rows, Strings s)
    {
        foreach (var page in SettingsWindow.ImplementedPages.Where(p => p != SettingsPage.General))
        {
            var title = s.SettingsPageTitles[page];
            var entry = new CommandBarEntry($"settings.{page}", title, s.SettingsTitle, string.Empty, CommandBarEntryKind.Settings, CommandBarSource.SettingsPages);
            rows.Add(new CommandBarRow(entry, new CommandBarIcon.Symbol(SymbolRegular.Settings24), _ => App.ShowSettings(page)));
        }
    }

    /// <summary>One row per app in the Start menu's All apps list.</summary>
    public static IEnumerable<CommandBarRow> Apps(IReadOnlyList<InstalledApp> apps) =>
        apps.Select(app => new CommandBarRow(
            new CommandBarEntry($"app.{app.ParsingName}", app.Name, Bar.KindApp, string.Empty, CommandBarEntryKind.App, CommandBarSource.Apps),
            new CommandBarIcon.App(app),
            _ => Beep(AppsFolder.Launch(app)),
            ActivatesAnotherWindow: true));

    /// <summary>
    /// One row per switchable window, subtitled with its app. Windows are not learned from: a window
    /// is gone tomorrow (countsUsage false upstream).
    /// </summary>
    public static IEnumerable<CommandBarRow> Windows(IReadOnlyList<OpenWindow> windows) =>
        windows.Select(window =>
        {
            var app = window.ExecutablePath is { } path ? AppName(path) : Bar.KindWindow;
            var entry = new CommandBarEntry($"window.{window.Handle}", window.Title, app, app, CommandBarEntryKind.Window, CommandBarSource.Windows, CountsUsage: false);
            return new CommandBarRow(entry,
                window.ExecutablePath is { } exe ? new CommandBarIcon.File(exe) : new CommandBarIcon.Symbol(SymbolRegular.Window24),
                _ => Beep(OpenWindows.Activate(window.Handle)),
                ActivatesAnotherWindow: true);
        });

    private static string AppName(string path)
    {
        try
        {
            var description = FileVersionInfo.GetVersionInfo(path).FileDescription;
            return string.IsNullOrWhiteSpace(description) ? Path.GetFileNameWithoutExtension(path) : description.Trim();
        }
        catch (IOException)
        {
            return Path.GetFileNameWithoutExtension(path);
        }
    }

    private static CommandBarEntry Entry(string id, string title, string subtitle, string keywords = "") =>
        new(id, title, subtitle, keywords, CommandBarEntryKind.Action, CommandBarSource.Actions);

    private static CommandBarRow Action(string id, string title, string subtitle, SymbolRegular glyph, Action<int?> run) =>
        new(Entry(id, title, subtitle), new CommandBarIcon.Symbol(glyph), run);

    private static bool ShellOpen(string target)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Never fail silently: a refused action beeps (upstream's NSSound.beep()).</summary>
    private static void Beep(bool succeeded)
    {
        if (!succeeded)
        {
            SystemSounds.Beep.Play();
        }
    }

    /// <summary>Opens what was typed as a web address.</summary>
    public static CommandBarRow OpenUrl(Uri url, string typed) =>
        new(new CommandBarEntry("action.openURL", typed, Bar.OpenInBrowser, string.Empty, CommandBarEntryKind.Action, CommandBarSource.Actions, CountsUsage: false),
            new CommandBarIcon.Symbol(SymbolRegular.Globe24), _ => Beep(ShellOpen(url.AbsoluteUri)), ActivatesAnotherWindow: true);
}
