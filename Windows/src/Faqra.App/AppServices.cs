// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the singletons wired in Sources/Vorssaint/App/AppDelegate.swift and main.swift,
// and of the binding table in App/FeatureRuntime.swift.

using Faqra.App.Island;
using Faqra.Core;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Localization;
using Faqra.Services;
using Faqra.Services.Island;
using Faqra.Services.Media;
using Faqra.Services.Startup;

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

        // The runtime is built last: its bindings capture the services above, and a binding only
        // runs for a feature that is actually installed.
        FeatureRuntime = new FeatureRuntime(store, Bindings());
        Island = new IslandController(store, FeatureRuntime, NowPlaying);
    }

    /// <summary>The live instance. Available after <see cref="Start"/>.</summary>
    public static AppServices Current => s_current ?? throw new InvalidOperationException("AppServices.Start has not run");

    public DefaultsStore Store { get; }

    public FeatureRuntime FeatureRuntime { get; }

    public LaunchAtLogin LaunchAtLogin { get; }

    public NowPlayingService NowPlaying { get; }

    public IslandTimerService Timer { get; }

    public IslandController Island { get; }

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
        FeatureRuntime.SyncAtLaunch();
    }

    /// <summary>
    /// What each feature re-evaluates when its availability changes. A feature with no binding has
    /// nothing running in the background; bindings for later features arrive with them.
    /// </summary>
    private Dictionary<AppFeature, Action> Bindings() => new()
    {
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
        Timer.Dispose();
        NowPlaying.Dispose();
        Store.Dispose();
        s_current = null;
    }
}
