// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the row and group model of Sources/Vorssaint/UI/Settings/FeatureHubSettings.swift

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Faqra.Core.Features;
using Faqra.Core.Localization;
using Faqra.Services;

namespace Faqra.App.Settings.Pages;

/// <summary>One feature row: what the hub shows and what the Install button does.</summary>
public sealed class FeatureRowViewModel(AppFeature feature, FeatureRuntime runtime, FeatureHubStrings hub, Strings s)
    : INotifyPropertyChanged
{
    public AppFeature Feature { get; } = feature;

    public string Title => hub.FeatureTitles[Feature];

    public string Description => hub.FeatureDescriptions[Feature];

    public string EnergyLabel => hub.EnergyLabels[Feature.EnergyProfile(AppServices.Current.Store)];

    public bool IsBeta => Feature.IsBeta();

    public Visibility BetaVisibility => IsBeta ? Visibility.Visible : Visibility.Collapsed;

    public bool IsInstalled => runtime.IsAvailable(Feature);

    /// <summary>Set when Windows cannot run the feature; the row greys out and Install is refused.</summary>
    public string? BlockedReason => IsInstalled ? null : FeatureWindowsSupport.UnsupportedReason(Feature, s);

    public bool CanFlip => IsInstalled || BlockedReason is null;

    public double RowOpacity => BlockedReason is null ? 1.0 : 0.4;

    public string ButtonText => IsInstalled ? hub.Uninstall : hub.Install;

    public Visibility BlockedVisibility => BlockedReason is null ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Set when Windows can run the feature but this build does not have it yet.</summary>
    public string? PendingNote => FeatureWindowsSupport.PendingNote(Feature, hub);

    public Visibility PendingVisibility => PendingNote is null ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Flips availability. The runtime refuses installs Windows cannot honor.</summary>
    public void Toggle()
    {
        runtime.SetAvailable(Feature, !IsInstalled);
        Refresh();
    }

    public void Refresh()
    {
        Raise(nameof(IsInstalled));
        Raise(nameof(ButtonText));
        Raise(nameof(BlockedReason));
        Raise(nameof(BlockedVisibility));
        Raise(nameof(CanFlip));
        Raise(nameof(RowOpacity));
        Raise(nameof(EnergyLabel));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>A hub section: one FeatureGroup and its rows, in catalog order.</summary>
public sealed class FeatureGroupViewModel
{
    public required string Title { get; init; }
    public required IReadOnlyList<FeatureRowViewModel> Rows { get; init; }
    public string? Note { get; init; }
    public Visibility NoteVisibility => Note is null ? Visibility.Collapsed : Visibility.Visible;
}

/// <summary>A preset card.</summary>
public sealed class PresetViewModel
{
    public required FeaturePreset Preset { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string ApplyText { get; init; }
    public required bool IsAvailableOnWindows { get; init; }
}

public static class FeatureHubViewModel
{
    /// <summary>Builds every group with its rows, plus the Monitor note when no metric is installed.</summary>
    public static IReadOnlyList<FeatureGroupViewModel> Groups(FeatureRuntime runtime, FeatureHubStrings hub, Strings s)
    {
        return FeatureGroups.All.Select(group =>
        {
            var rows = AppFeatures.FeaturesIn(group)
                .Select(feature => new FeatureRowViewModel(feature, runtime, hub, s))
                .ToList();
            var note = group == FeatureGroup.Monitor && !rows.Any(row => row.IsInstalled) ? hub.MonitorAllOffNote : null;
            return new FeatureGroupViewModel { Title = hub.GroupTitles[group], Rows = rows, Note = note };
        }).ToList();
    }

    /// <summary>The three preset cards. A preset with nothing ported yet is offered but disabled.</summary>
    public static IReadOnlyList<PresetViewModel> Presets(FeatureHubStrings hub) =>
        FeaturePresets.All.Select(preset => new PresetViewModel
        {
            Preset = preset,
            Name = hub.PresetNames[preset],
            Description = hub.PresetDescriptions[preset],
            ApplyText = hub.PresetApplyButton,
            IsAvailableOnWindows = preset.Features().Any(feature => feature.IsSupported()),
        }).ToList();
}
