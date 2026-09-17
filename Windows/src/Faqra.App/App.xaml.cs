// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the accessory-app lifecycle of Sources/Vorssaint/App/AppDelegate.swift

using System.Windows;
using Faqra.App.Onboarding;
using Faqra.App.Panel;
using Faqra.App.Settings;
using Faqra.App.Tray;
using Faqra.Core.Defaults;
using Wpf.Ui.Appearance;

namespace Faqra.App;

/// <summary>Tray-only application: no main window, lives until Quit is chosen.</summary>
public partial class App : Application
{
    private AppServices? _services;
    private StatusItemController? _statusItem;
    private MenuPanelController? _panel;
    private TrayMetricsController? _trayMetrics;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _services = AppServices.Start();
        ApplyAppearance();
        _services.Store.Changed += OnSettingChanged;

        _statusItem = new StatusItemController(_services.Store, _services.KeepAwake, () => _services.FeatureRuntime.IsAvailable(Core.Features.AppFeature.KeepAwake));
        _statusItem.Show();
        _panel = new MenuPanelController(_services.Store, _services.FeatureRuntime, _services.Monitor, _services.Mixer, _services.KeepAwake, _statusItem.IconRect);
        _statusItem.Selected += _panel.Toggle;
        _trayMetrics = new TrayMetricsController(_services.Store, _services.FeatureRuntime, _services.Monitor);
        _trayMetrics.SectionRequested += _panel.Show;
        _trayMetrics.Sync();
        _services.StartFeatures();

        if (!_services.HasOnboarded)
        {
            OnboardingWindow.ShowSingleton();
        }
    }

    private void OnSettingChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.Key == DefaultsKey.Appearance)
        {
            Dispatcher.Invoke(ApplyAppearance);
        }
    }

    /// <summary>Applies the stored appearance. Mirrors AppAppearanceController in upstream.</summary>
    internal void ApplyAppearance()
    {
        var appearance = DefaultsSanitizers.Appearance(AppServices.Current.Store.String(DefaultsKey.Appearance));
        var theme = appearance switch
        {
            AppAppearance.Light => ApplicationTheme.Light,
            AppAppearance.Dark => ApplicationTheme.Dark,
            _ => ApplicationThemeManager.GetSystemTheme() == SystemTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark,
        };
        ApplicationThemeManager.Apply(theme, Wpf.Ui.Controls.WindowBackdropType.Mica, updateAccent: true);
        _statusItem?.Refresh();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        if (_services is not null)
        {
            _services.Store.Changed -= OnSettingChanged;
        }
        _trayMetrics?.Dispose();
        _trayMetrics = null;
        _panel?.Dispose();
        _panel = null;
        _statusItem?.Dispose();
        _statusItem = null;
        _services?.Dispose();
        _services = null;
    }

    /// <summary>Opens Settings, or brings it to the front, on the given page.</summary>
    public static void ShowSettings(Core.Settings.SettingsPage page = Core.Settings.SettingsPage.General) =>
        SettingsWindow.ShowSingleton(page);
}
