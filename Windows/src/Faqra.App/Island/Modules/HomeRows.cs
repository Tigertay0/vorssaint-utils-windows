// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Faqra.App.Agents;
using Faqra.Core.Agents;
using Faqra.Core.Island;
using Faqra.Core.Localization;
using Faqra.Core.Metrics;
using Faqra.Services.Agents;
using Faqra.Services.Island;
using Faqra.Services.Media;
using Faqra.Services.Monitor;
using Faqra.Win32.Power;

namespace Faqra.App.Island.Modules;

/// <summary>One row on Home: built once, then refreshed in place so its buttons and orbs keep their state.</summary>
internal abstract class HomeRow
{
    protected HomeRow(HomeItem item) => Item = item;

    public HomeItem Item { get; }

    public abstract FrameworkElement View { get; }

    public abstract void Update();

    protected static string Format(string format, object value) => string.Format(CultureInfo.CurrentCulture, format, value);

    protected static TextBlock Label(string text) => new()
    {
        Text = text,
        FontFamily = AgentCardParts.TextFont,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = IslandPalette.Tertiary,
    };

    protected static TextBlock Line(double size, Brush ink) => new()
    {
        FontFamily = AgentCardParts.TextFont,
        FontSize = size,
        Foreground = ink,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    /// <summary>A whole-row button: the row's panel look, hover and press from the island's round template.</summary>
    protected static Button RowButton(UIElement content, string name, Action click)
    {
        var button = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 10),
            Background = IslandPalette.Fill,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Template = MusicModule.RoundButtonTemplate(12),
        };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>A round icon button for actions inside a row.</summary>
    protected static Button IconButton(string glyph, string name, Action click)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = IslandPalette.Primary,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Width = 32,
            Height = 32,
            Margin = new Thickness(6, 0, 0, 0),
            Background = IslandPalette.FillStrong,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Template = MusicModule.RoundButtonTemplate(16),
        };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>Runs a media command without letting a failure reach the dispatcher.</summary>
    protected static async void Fire(Func<Task> command)
    {
        try
        {
            await command();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Trace.TraceWarning($"Faqra home: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

/// <summary>What is playing: artwork, title and artist, play or pause, next.</summary>
internal sealed class NowPlayingRow : HomeRow
{
    private readonly NowPlayingService _service;
    private readonly HomeStrings _s;
    private readonly Border _art = new() { Width = 44, Height = 44, CornerRadius = new CornerRadius(8) };
    private readonly TextBlock _title = Line(13, IslandPalette.Primary);
    private readonly TextBlock _artist = Line(12, IslandPalette.Secondary);
    private readonly TextBlock _playGlyph;
    private readonly Button _play;
    private readonly Button _next;

    public NowPlayingRow(NowPlayingService service, HomeStrings s) : base(HomeItem.NowPlaying)
    {
        _service = service;
        _s = s;
        _title.FontWeight = FontWeights.SemiBold;
        _play = IconButton("\uE768", s.PlayPause, () => Fire(_service.TogglePlayPauseAsync));
        _playGlyph = (TextBlock)_play.Content;
        _next = IconButton("\uE893", s.NextTrack, () => Fire(_service.NextAsync));

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var words = new StackPanel { Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(_title);
        words.Children.Add(_artist);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        buttons.Children.Add(_play);
        buttons.Children.Add(_next);
        Grid.SetColumn(words, 1);
        Grid.SetColumn(buttons, 2);
        grid.Children.Add(_art);
        grid.Children.Add(words);
        grid.Children.Add(buttons);
        View = AgentCardParts.Panel(grid);
        Update();
    }

    public override FrameworkElement View { get; }

    public override void Update()
    {
        var track = _service.Current;
        _title.Text = track.HasTrack ? track.Title : _s.NothingPlaying;
        _artist.Text = track.HasTrack ? track.Artist : string.Empty;
        _art.Background = IdleMusicView.Artwork(track.Artwork) is { } art
            ? new ImageBrush(art) { Stretch = Stretch.UniformToFill }
            : IslandPalette.FillStrong;
        _playGlyph.Text = track.IsPlaying ? "\uE769" : "\uE768";
        _play.IsEnabled = track.HasTrack;
        _next.IsEnabled = track.HasTrack;
    }
}

/// <summary>Up to three sessions, most urgent first; a waiting one turns the row's title to the accent.</summary>
internal sealed class AgentsRow : HomeRow
{
    private const int Slots = 3;
    private readonly AgentHub _hub;
    private readonly HomeStrings _s;
    private readonly AgentsStrings _words;
    private readonly TextBlock _label;
    private readonly TextBlock _empty;
    private readonly Button _more;
    private readonly Slot[] _slots;

    public AgentsRow(AgentHub hub, HomeStrings s, AgentsStrings words, Action<string> openSession, Action openSection) : base(HomeItem.Agents)
    {
        _hub = hub;
        _s = s;
        _words = words;
        _label = Label(s.ItemAgents);
        _empty = Line(12, IslandPalette.Secondary);
        _empty.Text = s.NoAgents;
        _empty.Margin = new Thickness(0, 6, 0, 0);
        _slots = Enumerable.Range(0, Slots).Select(_ => new Slot(s, openSession)).ToArray();
        _more = AgentCardParts.Quiet(string.Empty, (_, _) => openSection());

        var stack = new StackPanel();
        stack.Children.Add(_label);
        stack.Children.Add(_empty);
        foreach (var slot in _slots)
        {
            stack.Children.Add(slot.Button);
        }
        stack.Children.Add(_more);
        View = AgentCardParts.Panel(stack);
        Update();
    }

    public override FrameworkElement View { get; }

    /// <summary>True while the most urgent session waits on the owner.</summary>
    public bool Waiting => _hub.Board.Ordered.FirstOrDefault() is { } first && HomeItems.Waits(first.State);

    public override void Update()
    {
        var sessions = _hub.Board.Ordered;
        _label.Foreground = Waiting ? IslandPalette.Accent : IslandPalette.Tertiary;
        _empty.Visibility = sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        for (var i = 0; i < Slots; i++)
        {
            _slots[i].Show(i < sessions.Count ? sessions[i] : null, _words, canOpen: i == 0);
        }
        _more.Content = sessions.Count > Slots ? Format(_s.MoreAgentsFormat, sessions.Count - Slots) : null;
        _more.Visibility = sessions.Count > Slots ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>One session line: orb, name, status, and an Open pill when it waits. Reused as sessions come and go.</summary>
    private sealed class Slot
    {
        private readonly OrbView _orb = new() { Diameter = 16, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _name = Line(13, IslandPalette.Primary);
        private readonly TextBlock _status = Line(12, IslandPalette.Secondary);
        private readonly Border _open;
        private string? _sessionId;

        public Slot(HomeStrings s, Action<string> openSession)
        {
            _name.FontWeight = FontWeights.SemiBold;
            _open = new Border
            {
                Background = IslandPalette.Accent,
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(10, 3, 10, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Child = new TextBlock { Text = s.Open, FontFamily = AgentCardParts.TextFont, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = IslandPalette.Surface },
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var words = new StackPanel { Margin = new Thickness(10, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            words.Children.Add(_name);
            words.Children.Add(_status);
            Grid.SetColumn(words, 1);
            Grid.SetColumn(_open, 2);
            grid.Children.Add(_orb);
            grid.Children.Add(words);
            grid.Children.Add(_open);
            Button = new Button
            {
                Content = grid,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 6, 0, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Template = MusicModule.RoundButtonTemplate(8),
            };
            Button.Click += (_, _) =>
            {
                if (_sessionId is { } id)
                {
                    openSession(id);
                }
            };
        }

        public Button Button { get; }

        /// <summary>Only the first slot (the most urgent session) may wear the Open pill, so Home keeps one accented outlier.</summary>
        public void Show(AgentSession? session, AgentsStrings words, bool canOpen)
        {
            _sessionId = session?.Id;
            Button.Visibility = session is null ? Visibility.Collapsed : Visibility.Visible;
            if (session is null)
            {
                return;
            }
            var style = AgentOrbStyles.For(session.State);
            _orb.Apply(style, AgentInk.For(style.Tone));
            _name.Text = session.Name;
            _status.Text = AgentsText.Status(session, words);
            _open.Visibility = canOpen && HomeItems.Waits(session.State) ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetName(Button, $"{_name.Text}, {_status.Text}");
        }
    }
}

/// <summary>The running timer, or that none runs; the row opens the Timer section.</summary>
internal sealed class TimerRow : HomeRow
{
    private readonly IslandTimerService _timer;
    private readonly HomeStrings _s;
    private readonly TextBlock _value = Line(13, IslandPalette.Primary);

    public TimerRow(IslandTimerService timer, HomeStrings s, Action open) : base(HomeItem.Timer)
    {
        _timer = timer;
        _s = s;
        var stack = new StackPanel();
        stack.Children.Add(Label(s.ItemTimer));
        _value.Margin = new Thickness(0, 4, 0, 0);
        stack.Children.Add(_value);
        View = RowButton(stack, s.ItemTimer, open);
        Update();
    }

    public override FrameworkElement View { get; }

    public override void Update() => _value.Text = !_timer.HasSession
        ? _s.NoTimer
        : _timer.IsRunning
            ? Format(_s.TimerLeftFormat, IslandTimerService.Format(_timer.Remaining))
            : $"{Format(_s.TimerLeftFormat, IslandTimerService.Format(_timer.Remaining))}, {_s.TimerPaused.ToLower(CultureInfo.CurrentCulture)}";
}

/// <summary>Processor and memory use in one line; the row opens the System section.</summary>
internal sealed class SystemRow : HomeRow
{
    private readonly SystemMonitor _monitor;
    private readonly HomeStrings _s;
    private readonly TextBlock _cpu = Line(13, IslandPalette.Primary);
    private readonly TextBlock _memory = Line(13, IslandPalette.Primary);

    public SystemRow(SystemMonitor monitor, HomeStrings s, Action open) : base(HomeItem.System)
    {
        _monitor = monitor;
        _s = s;
        var values = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        _memory.Margin = new Thickness(20, 0, 0, 0);
        values.Children.Add(_cpu);
        values.Children.Add(_memory);
        var stack = new StackPanel();
        stack.Children.Add(Label(s.ItemSystem));
        stack.Children.Add(values);
        View = RowButton(stack, s.ItemSystem, open);
        Update();
    }

    public override FrameworkElement View { get; }

    public override void Update()
    {
        var snapshot = _monitor.Snapshot;
        _cpu.Text = Format(_s.CpuFormat, snapshot.CpuUsage is { } cpu && double.IsFinite(cpu) ? MetricFormat.Percent(cpu) : "…");
        _memory.Text = Format(_s.MemoryFormat,
            snapshot.MemoryUsed is { } used && snapshot.MemoryTotal is { } total && total > 0 ? MetricFormat.Percent((double)used / total) : "…");
    }
}

/// <summary>Charge and whether it charges.</summary>
internal sealed class BatteryRow : HomeRow
{
    private readonly HomeStrings _s;
    private readonly TextBlock _value = Line(13, IslandPalette.Primary);

    public BatteryRow(HomeStrings s) : base(HomeItem.Battery)
    {
        _s = s;
        var stack = new StackPanel();
        stack.Children.Add(Label(s.ItemBattery));
        _value.Margin = new Thickness(0, 4, 0, 0);
        stack.Children.Add(_value);
        View = AgentCardParts.Panel(stack);
        Update();
    }

    public override FrameworkElement View { get; }

    public override void Update()
    {
        var status = Power.BatteryStatus();
        var percent = status.Percent >= 0 ? $"{status.Percent}%" : "…";
        _value.Text = $"{percent}, {(status.Charging ? _s.BatteryCharging : _s.BatteryOnBattery).ToLower(CultureInfo.CurrentCulture)}";
    }
}
