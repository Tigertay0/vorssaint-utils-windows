// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the timer module of Sources/Vorssaint/UI/Notch (NotchActivityStrings, NotchTimerService).

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Faqra.Services.Island;

namespace Faqra.App.Island.Modules;

/// <summary>Countdown presets, the remaining time, and pause and reset.</summary>
public sealed class TimerModule : UserControl
{
    private readonly IslandTimerService _timer;
    private readonly TextBlock _remaining = new()
    {
        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
        FontSize = 44,
        Foreground = IslandPalette.Primary,
        HorizontalAlignment = HorizontalAlignment.Center,
    };
    private readonly Border _track = new()
    {
        Height = 4,
        CornerRadius = new CornerRadius(2),
        Background = IslandPalette.Fill,
        Margin = new Thickness(0, 12, 0, 0),
    };
    private readonly Border _fill = new()
    {
        Height = 4,
        CornerRadius = new CornerRadius(2),
        Background = IslandPalette.Accent,
        HorizontalAlignment = HorizontalAlignment.Left,
    };
    private readonly StackPanel _presets = new()
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 16, 0, 0),
    };
    private readonly StackPanel _controls = new()
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 16, 0, 0),
    };
    private readonly Button _pause;
    private readonly Button _reset;

    public TimerModule()
    {
        _timer = AppServices.Current.Timer;
        _track.Child = _fill;

        foreach (var minutes in IslandTimerService.Presets)
        {
            _presets.Children.Add(PresetButton(minutes));
        }
        _pause = PillButton("Pause", (_, _) => { _timer.TogglePause(); Render(); });
        _reset = PillButton("Reset", (_, _) => { _timer.Reset(); Render(); });
        _controls.Children.Add(_pause);
        _controls.Children.Add(_reset);

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(_remaining);
        stack.Children.Add(_track);
        stack.Children.Add(_presets);
        stack.Children.Add(_controls);
        Content = stack;

        Render();
        _timer.Changed += OnChanged;
        _track.SizeChanged += (_, _) => UpdateProgress();
        Unloaded += (_, _) => _timer.Changed -= OnChanged;
    }

    private void OnChanged() => Dispatcher.BeginInvoke(Render);

    private void Render()
    {
        _remaining.Text = _timer.HasSession ? IslandTimerService.Format(_timer.Remaining) : "0:00";
        _remaining.Foreground = _timer.HasSession ? IslandPalette.Primary : IslandPalette.Tertiary;
        _pause.Content = _timer.IsRunning ? "Pause" : "Resume";
        _pause.IsEnabled = _timer.HasSession;
        _reset.IsEnabled = _timer.HasSession;
        _presets.Visibility = _timer.HasSession ? Visibility.Collapsed : Visibility.Visible;
        _controls.Visibility = _timer.HasSession ? Visibility.Visible : Visibility.Collapsed;
        _track.Visibility = _timer.HasSession ? Visibility.Visible : Visibility.Hidden;
        UpdateProgress();
    }

    private void UpdateProgress() => _fill.Width = Math.Max(0, _track.ActualWidth * _timer.Progress);

    private Button PresetButton(int minutes)
    {
        var button = PillButton($"{minutes} min", (_, _) =>
        {
            _timer.Start(TimeSpan.FromMinutes(minutes));
            Render();
        });
        button.Margin = new Thickness(4, 0, 4, 0);
        return button;
    }

    private static Button PillButton(string text, RoutedEventHandler onClick)
    {
        var button = new Button
        {
            Content = text,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 12,
            MinWidth = 76,
            Height = 32,
            Margin = new Thickness(5, 0, 5, 0),
            Foreground = IslandPalette.Primary,
            Background = IslandPalette.Fill,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Template = MusicModule.RoundButtonTemplate(16),
        };
        button.Click += onClick;
        return button;
    }
}
