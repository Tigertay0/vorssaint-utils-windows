// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/UI/MenuPanel/MixerSection.swift: the output picker and system level
// (124-198), one row per app with icon, playing dot, slider, editable percent, reset and mute (723-846),
// "Hide from the list" (830-838), and the Options disclosure (85-122) with hide inactive apps, the
// headphone-disconnect protection and "Apps in the list" (536-606). The system-sounds and microphone
// pickers, boost, per-app output routing, the precise volume roller and the output switcher have no
// Windows counterpart in this build and are left out.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;
using Faqra.Core.Metrics;
using Faqra.Core.Mixer;
using Faqra.Services.Audio;

namespace Faqra.App.Panel.Sections;

internal sealed class MixerSectionView : IPanelSectionView, IDisposable
{
    private const string VolumeGlyph = "";
    private const string MuteGlyph = "";
    private const string ResetGlyph = "";
    private const string ChevronRight = "";
    private const string ChevronDown = "";
    private const string SpeakerGlyph = "";
    private const double AudibleThreshold = 0.001;
    private const int DisconnectStep = 5;

    private readonly AppVolumeMixer _mixer;
    private readonly ISettingsStore _store;
    private readonly SectionPalette _palette;
    private readonly MixerStrings _s = MixerStrings.For(L10n.Shared.Language);

    private readonly ComboBox _outputPicker = new() { MinWidth = 120, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(8, 0, 0, 0) };
    private readonly StackPanel _levelRow = new() { Orientation = Orientation.Horizontal };
    private readonly Slider _outputSlider = VolumeSlider(1);
    private readonly GlyphButton _outputMute;
    private readonly PercentField _outputPercent;
    private readonly TextBlock _noOutputs;
    private readonly TextBlock _switchError;
    private readonly StackPanel _rows = new();
    private readonly TextBlock _empty;
    private readonly Dictionary<string, AppRow> _rowsById = [];
    private List<string> _shownIds = [];
    private readonly StackPanel _options = new() { Margin = new Thickness(20, 8, 0, 0), Visibility = Visibility.Collapsed };
    private readonly TextBlock _optionsChevron;
    private readonly StackPanel _listChoices = new() { Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private readonly TextBlock _listSummary;
    private readonly TextBlock _listChevron;
    private readonly FrameworkElement _disconnectStepper;
    private readonly TextBlock _disconnectValue;

    private IReadOnlyList<MixerOutputDevice>? _shownDevices;
    private MixerSnapshot _snapshot = MixerSnapshot.Empty;
    private bool _updating;
    private bool _disposed;

    public MixerSectionView(SectionContext context, AppVolumeMixer mixer, SectionPalette palette)
    {
        _mixer = mixer;
        _store = context.Store;
        _palette = palette;

        _outputMute = new GlyphButton(VolumeGlyph, _s.Mute, palette);
        _outputMute.Click += (_, _) => _mixer.ToggleOutputMute();
        _outputPercent = new PercentField(44, 100, palette, _s.SystemOutputTitle, v => _mixer.SetOutputVolume(v));
        _noOutputs = palette.Label(_s.SystemOutputNoDevices, 12);
        _switchError = palette.Label(string.Empty, 12, PaletteRole.Critical);
        _switchError.TextWrapping = TextWrapping.Wrap;
        _empty = palette.Label(_s.Empty, 12);
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.Margin = new Thickness(0, 4, 0, 4);
        _optionsChevron = palette.Glyph(ChevronRight, 10);
        _listSummary = palette.Label(string.Empty, 12);
        _listChevron = palette.Glyph(ChevronRight, 9);
        _disconnectValue = palette.Value(string.Empty, 12, PaletteRole.Secondary);
        _disconnectStepper = DisconnectStepper();

        var stack = new StackPanel();
        stack.Children.Add(OutputSection());
        stack.Children.Add(palette.Divider());
        stack.Children.Add(_rows);
        stack.Children.Add(palette.Divider());
        stack.Children.Add(OptionsDisclosure());
        Root = palette.IsIsland ? stack : new PanelCard(stack);

        _mixer.Changed += OnMixerChanged;
        _store.Changed += OnSettingChanged;
        _mixer.SetVisible(true);
        Apply(_mixer.Snapshot);
    }

    public UIElement Root { get; }

    public void Update(SystemSnapshot snapshot)
    {
        // The mixer has its own source; system samples do not concern it.
    }

    // MARK: output

    private UIElement OutputSection()
    {
        var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var icon = _palette.Glyph(SpeakerGlyph, 12);
        icon.Margin = new Thickness(0, 0, 6, 0);
        title.Children.Add(icon);
        title.Children.Add(_palette.Label(_s.SystemOutputTitle, 12, PaletteRole.Secondary, FontWeights.SemiBold));
        _outputPicker.ToolTip = _s.SystemOutputTooltip;
        System.Windows.Automation.AutomationProperties.SetName(_outputPicker, _s.SystemOutputTooltip);
        _outputPicker.SelectionChanged += (_, _) =>
        {
            if (!_updating && _outputPicker.SelectedIndex >= 0 && _outputPicker.SelectedIndex < (_shownDevices?.Count ?? 0))
            {
                _mixer.SetDefaultOutput(_shownDevices![_outputPicker.SelectedIndex].Id);
            }
        };

        _outputSlider.Width = 188;
        _outputSlider.Margin = new Thickness(8, 0, 8, 0);
        System.Windows.Automation.AutomationProperties.SetName(_outputSlider, _s.SystemOutputTitle);
        _outputSlider.ValueChanged += (_, e) =>
        {
            if (!_updating)
            {
                _mixer.SetOutputVolume(e.NewValue);
                _outputPercent.Set(e.NewValue);
            }
        };
        _levelRow.Margin = new Thickness(0, 8, 0, 0);
        _levelRow.Children.Add(_outputMute);
        _levelRow.Children.Add(_outputSlider);
        _levelRow.Children.Add(_outputPercent);

        var stack = new StackPanel();
        stack.Children.Add(SectionKit.Row(title, _outputPicker));
        stack.Children.Add(_levelRow);
        _noOutputs.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(_noOutputs);
        _switchError.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(_switchError);
        return stack;
    }

    // MARK: options

    private UIElement OptionsDisclosure()
    {
        var header = DisclosureButton(_optionsChevron, _palette.Label(_s.Options, 12, PaletteRole.Secondary, FontWeights.SemiBold), () =>
        {
            var open = _options.Visibility != Visibility.Visible;
            _options.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            _optionsChevron.Text = open ? ChevronDown : ChevronRight;
        });

        _options.Children.Add(SwitchRow(_s.HideInactiveApps, DefaultsKey.MixerHideInactiveApps));

        var lower = CheckRow(_s.LowerOnHeadphonesDisconnect, DefaultsKey.MixerLowerVolumeOnHeadphonesDisconnect);
        lower.Margin = new Thickness(0, 10, 0, 0);
        _options.Children.Add(lower);
        var caption = _palette.Label(_s.LowerOnHeadphonesDisconnectCaption, 11, PaletteRole.Tertiary);
        caption.TextWrapping = TextWrapping.Wrap;
        caption.Margin = new Thickness(28, 0, 0, 0);
        _options.Children.Add(caption);
        _options.Children.Add(_disconnectStepper);

        var listHeader = DisclosureButton(_listChevron, _palette.Label(_s.VisibleApps, 12), () =>
        {
            var open = _listChoices.Visibility != Visibility.Visible;
            _listChoices.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            _listChevron.Text = open ? ChevronDown : ChevronRight;
            RebuildListChoices();
        }, trailing: _listSummary);
        listHeader.Margin = new Thickness(0, 10, 0, 0);
        _options.Children.Add(listHeader);
        _options.Children.Add(_listChoices);
        SyncDisconnectStepper();

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(_options);
        return stack;
    }

    private FrameworkElement DisconnectStepper()
    {
        var minus = new GlyphButton("", $"{_s.HeadphonesDisconnectVolume} -{DisconnectStep}%", _palette, 10);
        var plus = new GlyphButton("", $"{_s.HeadphonesDisconnectVolume} +{DisconnectStep}%", _palette, 10);
        minus.Click += (_, _) => StepDisconnect(-DisconnectStep);
        plus.Click += (_, _) => StepDisconnect(DisconnectStep);
        _disconnectValue.Width = 40;
        _disconnectValue.TextAlignment = TextAlignment.Center;
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        controls.Children.Add(minus);
        controls.Children.Add(_disconnectValue);
        controls.Children.Add(plus);
        var row = SectionKit.Row(_palette.Label(_s.HeadphonesDisconnectVolume, 12), controls);
        row.Margin = new Thickness(28, 6, 0, 0);
        return row;
    }

    private void StepDisconnect(int delta)
    {
        var current = DefaultsSanitizers.MixerHeadphonesDisconnectVolumePercent(_store.Int(DefaultsKey.MixerHeadphonesDisconnectVolumePercent));
        _store.Set(DefaultsKey.MixerHeadphonesDisconnectVolumePercent, DefaultsSanitizers.MixerHeadphonesDisconnectVolumePercent(current + delta));
    }

    private void SyncDisconnectStepper()
    {
        _disconnectStepper.Visibility = _store.Bool(DefaultsKey.MixerLowerVolumeOnHeadphonesDisconnect) ? Visibility.Visible : Visibility.Collapsed;
        var percent = DefaultsSanitizers.MixerHeadphonesDisconnectVolumePercent(_store.Int(DefaultsKey.MixerHeadphonesDisconnectVolumePercent));
        _disconnectValue.Text = $"{percent}%";
    }

    private void RebuildListChoices()
    {
        var hidden = _snapshot.HiddenApps.Count;
        _listSummary.Text = hidden == 0 ? _s.AllShown : $"{_s.HiddenCountLabel}: {hidden}";
        if (_listChoices.Visibility != Visibility.Visible)
        {
            return;
        }
        _listChoices.Children.Clear();
        var choices = _snapshot.HiddenApps.Select(h => (h.Id, h.Name, Shown: false))
            .Concat(_snapshot.Apps.Select(a => (a.Id, a.Name, Shown: true)))
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(c => c.Id, StringComparer.Ordinal);
        foreach (var (id, name, shown) in choices)
        {
            var box = new CheckBox { Content = _palette.Label(name, 12, PaletteRole.Primary), IsChecked = shown, Margin = new Thickness(0, 2, 0, 0) };
            box.Click += (_, _) =>
            {
                if (box.IsChecked == true)
                {
                    _mixer.ShowInList(id);
                }
                else
                {
                    _mixer.HideFromList(id, name);
                }
            };
            _listChoices.Children.Add(box);
        }
    }

    private UIElement SwitchRow(string title, string key)
    {
        var toggle = new Wpf.Ui.Controls.ToggleSwitch { IsChecked = _store.Bool(key), HorizontalAlignment = HorizontalAlignment.Right };
        System.Windows.Automation.AutomationProperties.SetName(toggle, title);
        toggle.Click += (_, _) => _store.Set(key, toggle.IsChecked == true);
        return SectionKit.Row(_palette.Label(title, 12), toggle);
    }

    private FrameworkElement CheckRow(string title, string key)
    {
        var box = new CheckBox { Content = _palette.Label(title, 12, PaletteRole.Primary), IsChecked = _store.Bool(key) };
        box.Click += (_, _) => _store.Set(key, box.IsChecked == true);
        return box;
    }

    private FrameworkElement DisclosureButton(TextBlock chevron, TextBlock label, Action toggle, TextBlock? trailing = null)
    {
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.Children.Add(chevron);
        Grid.SetColumn(label, 1);
        content.Children.Add(label);
        if (trailing is not null)
        {
            Grid.SetColumn(trailing, 2);
            content.Children.Add(trailing);
        }
        var button = new GlyphButton(string.Empty, label.Text, _palette)
        {
            Content = content,
            Width = double.NaN,
            Height = 26,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        button.Click += (_, _) => toggle();
        return button;
    }

    // MARK: updates

    private void OnMixerChanged(MixerSnapshot snapshot) =>
        Root.Dispatcher.BeginInvoke(() =>
        {
            if (!_disposed)
            {
                Apply(snapshot);
            }
        });

    private void OnSettingChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.Key.StartsWith("mixer", StringComparison.Ordinal))
        {
            Root.Dispatcher.BeginInvoke(() =>
            {
                if (!_disposed)
                {
                    SyncDisconnectStepper();
                    Apply(_snapshot);
                }
            });
        }
    }

    private void Apply(MixerSnapshot snapshot)
    {
        _snapshot = snapshot;
        _updating = true;
        try
        {
            ApplyOutput(snapshot);
            ApplyRows(snapshot);
            RebuildListChoices();
        }
        finally
        {
            _updating = false;
        }
    }

    private void ApplyOutput(MixerSnapshot snapshot)
    {
        if (_shownDevices is null || !snapshot.Devices.SequenceEqual(_shownDevices))
        {
            _shownDevices = snapshot.Devices;
            _outputPicker.Items.Clear();
            foreach (var device in snapshot.Devices)
            {
                _outputPicker.Items.Add(device.IsDefault ? $"{device.Name} ({_s.OutputCurrent})" : device.Name);
            }
        }
        var index = snapshot.Devices.ToList().FindIndex(d => d.Id == snapshot.CurrentOutputId);
        if (_outputPicker.SelectedIndex != index)
        {
            _outputPicker.SelectedIndex = index;
        }
        _outputPicker.IsEnabled = snapshot.Devices.Count > 0;
        _noOutputs.Visibility = snapshot.Devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _switchError.Visibility = snapshot.OutputSwitchError is null ? Visibility.Collapsed : Visibility.Visible;
        _switchError.Text = string.Format(_s.SwitchErrorFormat, snapshot.OutputSwitchError);

        _levelRow.Visibility = snapshot.OutputVolume is null ? Visibility.Collapsed : Visibility.Visible;
        if (snapshot.OutputVolume is { } volume)
        {
            var muted = snapshot.OutputMuted == true;
            var shown = muted ? 0 : volume;
            if (!_outputSlider.IsMouseCaptureWithin)
            {
                _outputSlider.Value = shown;
            }
            if (!_outputPercent.IsEditing)
            {
                _outputPercent.Set(shown);
            }
            var silent = muted || volume <= AudibleThreshold;
            _outputMute.Set(silent ? MuteGlyph : VolumeGlyph, silent ? PaletteRole.Critical : PaletteRole.Secondary, _palette, silent ? _s.Unmute : _s.Mute);
        }
    }

    private void ApplyRows(MixerSnapshot snapshot)
    {
        var hideInactive = _store.Bool(DefaultsKey.MixerHideInactiveApps);
        var visible = snapshot.Apps.Where(a => MixerRoutingSupport.ShouldShowApp(a.IsPlaying, a.Volume, hideInactive)).ToList();
        var ids = visible.Select(a => a.Id).ToList();
        // Rebuild only when the rows themselves change: re-adding a row mid-drag would end the drag.
        if (!ids.SequenceEqual(_shownIds))
        {
            _shownIds = ids;
            foreach (var stale in _rowsById.Keys.Except(ids).ToList())
            {
                _rowsById.Remove(stale);
            }
            _rows.Children.Clear();
            foreach (var app in visible)
            {
                if (!_rowsById.TryGetValue(app.Id, out var row))
                {
                    row = new AppRow(app, this);
                    _rowsById[app.Id] = row;
                }
                row.Root.Margin = new Thickness(0, _rows.Children.Count == 0 ? 0 : 8, 0, 0);
                _rows.Children.Add(row.Root);
            }
            if (visible.Count == 0)
            {
                _rows.Children.Add(_empty);
            }
        }
        foreach (var app in visible)
        {
            _rowsById[app.Id].Set(app);
        }
    }

    private static Slider VolumeSlider(double maximum) => new()
    {
        Minimum = 0,
        Maximum = maximum,
        SmallChange = 0.01,
        LargeChange = 0.05,
        VerticalAlignment = VerticalAlignment.Center,
        IsMoveToPointEnabled = true,
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _mixer.Changed -= OnMixerChanged;
        _store.Changed -= OnSettingChanged;
        _mixer.SetVisible(false);
    }

    /// <summary>One app: 32 px icon with a playing dot, name, slider, percent, reset and mute.</summary>
    private sealed class AppRow
    {
        private readonly MixerSectionView _owner;
        private readonly string _id;
        private readonly TextBlock _name;
        private readonly System.Windows.Shapes.Ellipse _playing;
        private readonly Slider _slider = VolumeSlider(1);
        private readonly PercentField _percent;
        private readonly GlyphButton _reset;
        private readonly GlyphButton _mute;
        private bool _setting;

        public AppRow(MixerApp app, MixerSectionView owner)
        {
            _owner = owner;
            _id = app.Id;
            var palette = owner._palette;
            var s = owner._s;

            var icon = new Image { Width = 32, Height = 32, Source = AppIcons.For(app.ExePath) };
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
            _playing = new System.Windows.Shapes.Ellipse
            {
                Width = 9,
                Height = 9,
                StrokeThickness = 1.5,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -1, -1),
            };
            palette.Apply(_playing, System.Windows.Shapes.Shape.FillProperty, PaletteRole.Success);
            if (palette.IsIsland)
            {
                _playing.Stroke = Island.Modules.IslandPalette.Surface;
            }
            else
            {
                _playing.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "SolidBackgroundFillColorBaseBrush");
            }
            var iconHost = new Grid { Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            iconHost.Children.Add(icon);
            iconHost.Children.Add(_playing);

            _name = palette.Label(app.Name, 12, PaletteRole.Primary, FontWeights.SemiBold);
            _name.TextTrimming = TextTrimming.CharacterEllipsis;
            System.Windows.Automation.AutomationProperties.SetName(_slider, app.Name);
            _slider.Width = 142;
            _slider.Margin = new Thickness(0, 0, 8, 0);
            _slider.ValueChanged += (_, e) =>
            {
                if (!_setting && !_owner._updating)
                {
                    _owner._mixer.SetVolume(_id, e.NewValue);
                    _percent!.Set(e.NewValue);
                }
            };
            _percent = new PercentField(44, 100, palette, app.Name, v => _owner._mixer.SetVolume(_id, v));
            _reset = new GlyphButton(ResetGlyph, s.ResetTooltip, palette, 11) { ToolTip = s.ResetTooltip };
            _reset.Click += (_, _) => _owner._mixer.SetVolume(_id, 1);
            _mute = new GlyphButton(VolumeGlyph, s.Mute, palette);
            _mute.Click += (_, _) => _owner._mixer.ToggleMute(_id);

            var controls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(-4, 2, 0, 0) };
            controls.Children.Add(_slider);
            controls.Children.Add(_percent);
            controls.Children.Add(_reset);
            controls.Children.Add(_mute);

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(_name);
            text.Children.Add(controls);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(iconHost);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            grid.Background = Brushes.Transparent;

            var hide = new MenuItem { Header = s.HideFromList };
            hide.Click += (_, _) => _owner._mixer.HideFromList(_id, _name.Text);
            grid.ContextMenu = new ContextMenu { Items = { hide } };
            Root = grid;
        }

        public FrameworkElement Root { get; }

        public void Set(MixerApp app)
        {
            _name.Text = app.Name;
            _playing.Visibility = app.IsPlaying ? Visibility.Visible : Visibility.Hidden;
            if (!_slider.IsMouseCaptureWithin)
            {
                _setting = true;
                _slider.Value = app.Volume;
                _setting = false;
            }
            if (!_percent.IsEditing)
            {
                _percent.Set(app.Volume);
            }
            var atUnity = (int)Math.Round(app.Volume * 100) == 100;
            _reset.Opacity = atUnity ? 0 : 1;
            _reset.IsEnabled = !atUnity;
            var silent = app.Volume <= AudibleThreshold;
            var s = _owner._s;
            _mute.Set(silent ? MuteGlyph : VolumeGlyph, silent ? PaletteRole.Critical : PaletteRole.Secondary, _owner._palette, silent ? s.Unmute : s.Mute);
        }
    }
}
