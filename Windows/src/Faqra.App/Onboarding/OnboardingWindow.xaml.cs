// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/UI/Onboarding/OnboardingView.swift. The permissions step is dropped:
// Windows has no consent grants for anything Faqra does.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Localization;

namespace Faqra.App.Onboarding;

public partial class OnboardingWindow
{
    private static OnboardingWindow? s_instance;

    private readonly List<OnboardingStep> _steps = [OnboardingStep.Welcome, OnboardingStep.Purpose, OnboardingStep.Done];
    private readonly HashSet<AppFeature> _selected;
    private FeaturePreset? _selectedPreset;
    private int _index;

    public OnboardingWindow()
    {
        InitializeComponent();

        var store = AppServices.Current.Store;
        var runtime = AppServices.Current.FeatureRuntime;
        // A resumed or re-opened run starts from what is installed; a clean first run starts from Essentials.
        var selectionApplied = store.Bool(DefaultsKey.HasOnboarded) || store.Int(DefaultsKey.OnboardingStep) >= 2;
        _selected = selectionApplied
            ? AppFeatures.All.Where(runtime.IsAvailable).ToHashSet()
            : FeaturePresets.FirstRunFeatures.ToHashSet();
        // The first-run set is Essentials plus the island, so no preset card is pre-selected as an
        // exact match; choosing one still replaces the selection outright.
        _selectedPreset = null;
        _index = Math.Clamp(store.Int(DefaultsKey.OnboardingStep), 0, _steps.Count - 1);

        Render();
        Closed += (_, _) => s_instance = null;
    }

    public static void ShowSingleton()
    {
        if (s_instance is null)
        {
            s_instance = new OnboardingWindow();
            s_instance.Show();
        }
        else
        {
            s_instance.Activate();
        }
    }

    private OnboardingStep Current => _steps[_index];

    private void Render()
    {
        var s = L10n.Shared.S;
        Title = s.OnboardingWelcomeTitle;
        WindowTitleBar.Title = Title;

        StepHost.Content = Current switch
        {
            OnboardingStep.Welcome => new WelcomeStep(),
            OnboardingStep.Purpose => new PurposeStep(_selected, _selectedPreset, OnPresetChosen, OnFeatureToggled),
            _ => new DoneStep(),
        };
        StepScroll.ScrollToTop();

        BackButton.Content = s.OnboardingBack;
        BackButton.IsEnabled = _index > 0;
        NextButton.Content = _index == _steps.Count - 1 ? s.OnboardingStart : s.OnboardingContinue;
        RenderDots();
    }

    private void RenderDots()
    {
        ProgressDots.Children.Clear();
        for (var i = 0; i < _steps.Count; i++)
        {
            var current = i == _index;
            ProgressDots.Children.Add(new Border
            {
                Width = current ? 18 : 7,
                Height = 7,
                Margin = new Thickness(3, 0, 3, 0),
                CornerRadius = new CornerRadius(3.5),
                Background = current
                    ? (Brush)FindResource("AccentFillColorDefaultBrush")
                    : (Brush)FindResource("ControlFillColorSecondaryBrush"),
            });
        }
    }

    private void OnPresetChosen(FeaturePreset preset)
    {
        _selectedPreset = preset;
        _selected.Clear();
        foreach (var feature in preset.Features())
        {
            _selected.Add(feature);
        }
        Render();
    }

    private void OnFeatureToggled(AppFeature feature)
    {
        // Hand-picking a feature means the run is no longer a preset.
        _selectedPreset = null;
        if (!_selected.Remove(feature))
        {
            _selected.Add(feature);
        }
        Render();
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        _index = Math.Max(0, _index - 1);
        AppServices.Current.Store.Set(DefaultsKey.OnboardingStep, _index);
        Render();
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        if (Current == OnboardingStep.Purpose)
        {
            // The selection is applied on leaving the purpose step, exactly like upstream.
            AppServices.Current.FeatureRuntime.ReplaceAvailable(_selected, _selectedPreset?.EnableKeys());
        }
        if (_index == _steps.Count - 1)
        {
            AppServices.Current.MarkOnboardingComplete();
            Close();
            return;
        }
        _index++;
        AppServices.Current.Store.Set(DefaultsKey.OnboardingStep, _index);
        Render();
    }
}

public enum OnboardingStep
{
    Welcome, Purpose, Done,
}
