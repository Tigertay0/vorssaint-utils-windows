// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors FeatureHubSettings in Sources/Vorssaint/UI/Settings/FeatureHubSettings.swift (lines 12-249)

using System.Windows;
using System.Windows.Controls;
using Faqra.Core.Features;
using Faqra.Core.Localization;

namespace Faqra.App.Settings.Pages;

public partial class FeatureHubPage : UserControl
{
    public FeatureHubPage()
    {
        InitializeComponent();
        Populate();
    }

    private void Populate()
    {
        var s = L10n.Shared.S;
        var hub = FeatureHubStrings.For(L10n.Shared.Language);
        var runtime = AppServices.Current.FeatureRuntime;

        TitleText.Text = s.SettingsPageTitles[Core.Settings.SettingsPage.Features];
        IntroText.Text = hub.Intro;
        InstallAllButton.Content = hub.InstallAll;
        UninstallAllButton.Content = hub.UninstallAll;
        RestartText.Text = hub.RestartBannerText;
        RestartButton.Content = hub.RestartBannerButton;
        PresetsHeader.Text = hub.PresetsTitle;
        PresetsCaption.Text = hub.PresetsCaption;
        FooterText.Text = hub.FooterNote;

        PresetList.ItemsSource = FeatureHubViewModel.Presets(hub);
        GroupList.ItemsSource = FeatureHubViewModel.Groups(runtime, hub, s);
        RefreshHeader();
    }

    private void RefreshHeader()
    {
        var hub = FeatureHubStrings.For(L10n.Shared.Language);
        var runtime = AppServices.Current.FeatureRuntime;
        CountText.Text = string.Format(hub.InstalledCountFormat, runtime.AvailableCount, runtime.InstallableCount);
        InstallAllButton.IsEnabled = runtime.AvailableCount < runtime.InstallableCount;
        UninstallAllButton.IsEnabled = runtime.AvailableCount > 0;
        RestartBanner.Visibility = runtime.NeedsRestartToUnload ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnToggleFeature(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.Button { Tag: FeatureRowViewModel row })
        {
            row.Toggle();
            RefreshHeader();
            RefreshMonitorNote();
        }
    }

    private void OnInstallAll(object sender, RoutedEventArgs e)
    {
        AppServices.Current.FeatureRuntime.SetAllAvailable(true);
        Populate();
    }

    private void OnUninstallAll(object sender, RoutedEventArgs e)
    {
        AppServices.Current.FeatureRuntime.SetAllAvailable(false);
        Populate();
    }

    private void OnApplyPreset(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.Button { Tag: FeaturePreset preset })
        {
            return;
        }
        var hub = FeatureHubStrings.For(L10n.Shared.Language);
        var confirmed = Dialogs.Confirm(
            hub.PresetNames[preset],
            string.Format(hub.PresetConfirmFormat, hub.PresetNames[preset]),
            hub.PresetConfirmApply,
            hub.PresetConfirmCancel);
        if (!confirmed)
        {
            return;
        }
        AppServices.Current.FeatureRuntime.Apply(preset);
        Populate();
    }

    /// <summary>The Monitor group's note appears only while every metric is uninstalled.</summary>
    private void RefreshMonitorNote()
    {
        if (GroupList.ItemsSource is IReadOnlyList<FeatureGroupViewModel> groups
            && groups.Any(group => group.Rows.Any(row => row.Feature.Group() == FeatureGroup.Monitor)))
        {
            var hub = FeatureHubStrings.For(L10n.Shared.Language);
            GroupList.ItemsSource = FeatureHubViewModel.Groups(AppServices.Current.FeatureRuntime, hub, L10n.Shared.S);
        }
    }

    private void OnRestart(object sender, RoutedEventArgs e) => AppRelauncher.Relaunch();
}
