// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the singletons wired in Sources/Vorssaint/App/AppDelegate.swift and main.swift,
// and of the binding table in App/FeatureRuntime.swift.

using Faqra.App.Island;
using Faqra.Core;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Localization;
using System.Windows.Threading;
using Faqra.Services;
using Faqra.Services.Audio;
using Faqra.Services.KeepAwake;
using Faqra.Services.Island;
using Faqra.Services.Media;
using Faqra.Services.Monitor;
using Faqra.Services.Shortcuts;
using Faqra.Services.Startup;
using Faqra.Win32.Windows;

namespace Faqra.App;

/// <summary>
/// The app's composition root: one settings store, one feature runtime, and the services the
/// installed features need. Created once at startup so everything shares the same store, exactly
/// like upstream's shared singletons.
/// </summary>
public sealed class AppServices : IDisposable
{
    private static AppServices? s_current;

    private AppServices(DefaultsStore store)
    {
        Store = store;
        LaunchAtLogin = new LaunchAtLogin(store);
        NowPlaying = new NowPlayingService();
        Timer = new IslandTimerService();
        // The readers open their counters now, but nothing is sampled until a surface asks for a metric.
        Monitor = new SystemMonitor(new WindowsMetricReaders(), store, feature => store.Bool(feature.AvailabilityKey()));
        // Neither touches Windows until StartFeatures: the mixer opens Core Audio on its own thread when
        // the feature is installed, and keep awake only sets the execution state for a session.
        Mixer = new AppVolumeMixer(store, () => store.Bool(AppFeature.Mixer.AvailabilityKey()), () => new WasapiAudioBackend(), new AudioThread(), Environment.ProcessId);
        KeepAwake = new KeepAwakeManager(store, () => store.Bool(AppFeature.KeepAwake.AvailabilityKey()), new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

        // The runtime is built last: its bindings capture the services above, and a binding only
        // runs for a feature that is actually installed.
        FeatureRuntime = new FeatureRuntime(store, Bindings());
        Island = new IslandController(store, FeatureRuntime, NowPlaying, Monitor, Mixer);
    }

    /// <summary>The live instance. Available after <see cref="Start"/>.</summary>
    public static AppServices Current => s_current ?? throw new InvalidOperationException("AppServices.Start has not run");

    public DefaultsStore Store { get; }

    public FeatureRuntime FeatureRuntime { get; }

    public LaunchAtLogin LaunchAtLogin { get; }

    public NowPlayingService NowPlaying { get; }

    public IslandTimerService Timer { get; }

    public SystemMonitor Monitor { get; }

    public IslandController Island { get; }

    public AppVolumeMixer Mixer { get; }

    public KeepAwakeManager KeepAwake { get; }

    /// <summary>Created by <see cref="StartFeatures"/>; null in tests that never start them.</summary>
    public HotKeyRegistry? HotKeys { get; private set; }

    /// <summary>The command bar. Created by <see cref="StartFeatures"/>.</summary>
    public CommandBar.CommandBarController? CommandBar { get; private set; }

    /// <summary>Lock, power, display and hot key messages. Created by <see cref="StartFeatures"/>.</summary>
    public SystemEventsWindow? SystemEvents { get; private set; }

    public Strings S => L10n.Shared.S;

    /// <summary>
    /// Loads settings, runs migrations, seeds a clean install with the Essential preset, restores
    /// the language, repairs the startup entry, then starts the installed features. Upstream's order.
    /// </summary>
    public static AppServices Start() => StartWith(DefaultsStore.Open(AppPaths.SettingsFile));

    /// <summary>Same startup sequence over a caller-supplied store, so tests can run in memory.</summary>
    internal static AppServices StartWith(DefaultsStore store)
    {
        DefaultsMigrations.Run(store, AppInfo.Version);
        FeaturePresets.PrepareFirstRunAvailability(store);

        var services = new AppServices(store);
        s_current = services;

        L10n.Shared.Language = AppLanguages.FromRawValue(store.String(DefaultsKey.Language)) ?? AppLanguages.SystemDefault();
        L10n.Shared.Changed += (_, _) => store.Set(DefaultsKey.Language, L10n.Shared.Language.RawValue());

        services.LaunchAtLogin.RepairAtStartup();
        return services;
    }

    /// <summary>
    /// Brings the installed features to life. Separate from <see cref="Start"/> so tests can build
    /// the object graph without opening windows or reading the media session.
    /// </summary>
    public void StartFeatures()
    {
        // Reading the system media session is async and may find nothing; the island falls back to
        // its battery or blank idle, so nothing waits on it.
        _ = NowPlaying.StartAsync();
        SystemEvents = new SystemEventsWindow();
        HotKeys = new HotKeyRegistry(Store, FeatureRuntime.IsAvailable, SystemEvents);
        HotKeys.Bind(Core.Shortcuts.GlobalShortcutRole.KeepAwake, KeepAwake.Session.Toggle);
        CommandBar = new CommandBar.CommandBarController(this);
        HotKeys.Bind(Core.Shortcuts.GlobalShortcutRole.CommandBar, CommandBar.Toggle);
        FeatureRuntime.SyncAtLaunch();
        KeepAwake.Start(SystemEvents);
        // Tray metrics switched on in a previous session need sampling from the first second.
        Monitor.PlanDidChange();
    }

    /// <summary>
    /// What each feature re-evaluates when its availability changes. A feature with no binding has
    /// nothing running in the background; bindings for later features arrive with them.
    /// </summary>
    private Dictionary<AppFeature, Action> Bindings() => new()
    {
        [AppFeature.Mixer] = () => Mixer.SyncWithPreferences(),
        [AppFeature.KeepAwake] = () =>
        {
            KeepAwake.SyncWithFeatures();
            HotKeys?.Sync();
        },
        [AppFeature.CommandBar] = () =>
        {
            HotKeys?.Sync();
            if (!FeatureRuntime.IsAvailable(AppFeature.CommandBar))
            {
                CommandBar?.Hide(restoreFocus: false);
            }
            CommandBar?.Prepare();
        },
        [AppFeature.Notch] = () => Island.SyncWithPreferences(),
        // The sub-features only change what the island shows, so they re-sync it when installed and
        // stop their own work when not.
        [AppFeature.NotchTimer] = () =>
        {
            if (FeatureRuntime.IsAvailable(AppFeature.Notch))
            {
                Island.SyncWithPreferences();
            }
            if (!FeatureRuntime.IsAvailable(AppFeature.NotchTimer))
            {
                Timer.Reset();
            }
        },
    };

    public bool HasOnboarded => Store.Bool(DefaultsKey.HasOnboarded);

    /// <summary>Marks first run complete, mirroring AppDelegate.markOnboardingComplete().</summary>
    public void MarkOnboardingComplete()
    {
        Store.Set(DefaultsKey.HasOnboarded, true);
        Store.Set(DefaultsKey.OnboardingStep, 0);
        Store.Set(DefaultsKey.LastUpdateIntroVersion, AppInfo.Version);
    }

    public void Dispose()
    {
        Island.Dispose();
        HotKeys?.Dispose();
        CommandBar?.Dispose();
        KeepAwake.Dispose();
        SystemEvents?.Dispose();
        Mixer.Dispose();
        Monitor.Dispose();
        Timer.Dispose();
        NowPlaying.Dispose();
        Store.Dispose();
        s_current = null;
    }
}
