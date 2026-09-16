// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the singletons wired in Sources/Vorssaint/App/AppDelegate.swift and main.swift.

using Faqra.Core;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Localization;
using Faqra.Services;
using Faqra.Services.Startup;

namespace Faqra.App;

/// <summary>
/// The app's composition root: one settings store, one feature runtime, one startup manager.
/// Created once at startup so services share the same store, exactly like upstream's shared singletons.
/// </summary>
public sealed class AppServices : IDisposable
{
    private static AppServices? s_current;

    private AppServices(DefaultsStore store)
    {
        Store = store;
        LaunchAtLogin = new LaunchAtLogin(store);
        FeatureRuntime = new FeatureRuntime(store, Bindings());
    }

    /// <summary>The live instance. Available after <see cref="Start"/>.</summary>
    public static AppServices Current => s_current ?? throw new InvalidOperationException("AppServices.Start has not run");

    public DefaultsStore Store { get; }

    public FeatureRuntime FeatureRuntime { get; }

    public LaunchAtLogin LaunchAtLogin { get; }

    public Strings S => L10n.Shared.S;

    /// <summary>
    /// Loads settings, runs migrations, seeds a clean install with the Essential preset, restores the
    /// language, repairs the startup entry, then starts the installed features. Upstream's launch order.
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
        services.FeatureRuntime.SyncAtLaunch();
        return services;
    }

    /// <summary>
    /// What each feature re-evaluates when its availability changes. A feature with no binding has
    /// nothing running in the background; bindings for ported features are added by their milestone.
    /// </summary>
    private static Dictionary<AppFeature, Action> Bindings() => new();

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
        Store.Dispose();
        s_current = null;
    }
}
