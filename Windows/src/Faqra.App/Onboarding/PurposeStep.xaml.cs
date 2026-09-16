// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors PurposeStep in Sources/Vorssaint/UI/Onboarding/OnboardingView.swift (lines 213-408):
// preset cards and a grouped feature checklist, where picking a feature clears the preset.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Faqra.Core.Features;
using Faqra.Core.Localization;
using Wpf.Ui.Controls;

namespace Faqra.App.Onboarding;

/// <summary>A preset card on the first-run picker.</summary>
public sealed class PurposePresetViewModel
{
    public required FeaturePreset Preset { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string ChooseText { get; init; }
    public required bool IsSelected { get; init; }
    public required Brush CardBackground { get; init; }
    public required Brush CardBorder { get; init; }
    public ControlAppearance ButtonAppearance => IsSelected ? ControlAppearance.Primary : ControlAppearance.Secondary;
}

/// <summary>A feature checkbox on the first-run picker.</summary>
public sealed class PurposeFeatureViewModel
{
    public required AppFeature Feature { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required bool IsSelected { get; init; }
    public required bool CanSelect { get; init; }
    public double RowOpacity => CanSelect ? 1.0 : 0.4;
}

public sealed class PurposeGroupViewModel
{
    public required string Title { get; init; }
    public required IReadOnlyList<PurposeFeatureViewModel> Rows { get; init; }
}

public partial class PurposeStep : UserControl
{
    private readonly Action<FeaturePreset> _onPreset;
    private readonly Action<AppFeature> _onFeature;

    public PurposeStep(
        IReadOnlySet<AppFeature> selected,
        FeaturePreset? selectedPreset,
        Action<FeaturePreset> onPreset,
        Action<AppFeature> onFeature)
    {
        InitializeComponent();
        _onPreset = onPreset;
        _onFeature = onFeature;

        var s = L10n.Shared.S;
        var hub = FeatureHubStrings.For(L10n.Shared.Language);
        TitleText.Text = s.OnboardingPurposeTitle;
        SubtitleText.Text = s.OnboardingPurposeBody;
        SkipText.Text = s.OnboardingPurposeSkip;
        FeaturesHeader.Text = hub.TabFeatures.ToUpperInvariant();

        PresetList.ItemsSource = FeaturePresets.All
            .Where(preset => preset.Features().Any(feature => feature.IsSupported()))
            .Select(preset => new PurposePresetViewModel
            {
                Preset = preset,
                Name = hub.PresetNames[preset],
                Description = hub.PresetDescriptions[preset],
                ChooseText = hub.PresetApplyButton,
                IsSelected = preset == selectedPreset,
                CardBackground = (Brush)FindResource(preset == selectedPreset
                    ? "AccentFillColorSelectedTextBackgroundBrush"
                    : "CardBackgroundFillColorDefaultBrush"),
                CardBorder = (Brush)FindResource(preset == selectedPreset
                    ? "AccentFillColorDefaultBrush"
                    : "CardStrokeColorDefaultBrush"),
            })
            .ToList();

        // Only features Windows can run are offered; the rest would install into nothing.
        GroupList.ItemsSource = FeatureGroups.All
            .Select(group => new PurposeGroupViewModel
            {
                Title = hub.GroupTitles[group],
                Rows = AppFeatures.FeaturesIn(group)
                    .Where(feature => feature.IsSupported())
                    .Select(feature => new PurposeFeatureViewModel
                    {
                        Feature = feature,
                        Title = hub.FeatureTitles[feature],
                        Description = hub.FeatureDescriptions[feature],
                        IsSelected = selected.Contains(feature),
                        CanSelect = true,
                    })
                    .ToList(),
            })
            .Where(group => group.Rows.Count > 0)
            .ToList();
    }

    private void OnChoosePreset(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.Button { Tag: FeaturePreset preset })
        {
            _onPreset(preset);
        }
    }

    private void OnToggleFeature(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: AppFeature feature })
        {
            _onFeature(feature);
        }
    }
}
