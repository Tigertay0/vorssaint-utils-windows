// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Faqra.Core.Defaults;
using Faqra.Core.Island;
using Faqra.Core.Localization;
using Faqra.Core.Metrics;
using Faqra.Services.Agents;
using Faqra.Services.Island;
using Faqra.Services.Media;
using Faqra.Services.Monitor;

namespace Faqra.App.Island.Modules;

/// <summary>
/// The island's Home: one row per item in the owner's order, each acting in place or opening its section. Edit turns
/// the rows into a list to reorder, remove and add, saved at once. Rows are built when the order changes and refreshed
/// in place from their sources the rest of the time.
/// </summary>
public sealed class HomeModule : UserControl
{
    private static readonly TimeSpan BatteryInterval = TimeSpan.FromSeconds(30);

    private readonly ISettingsStore _store;
    private readonly NowPlayingService _nowPlaying;
    private readonly AgentHub _agents;
    private readonly IslandTimerService _timer;
    private readonly SystemMonitor _monitor;
    private readonly Func<HomeItem, bool> _isAvailable;
    private readonly HomeStrings _s = HomeStrings.For(L10n.Shared.Language);
    private readonly AgentsStrings _agentWords = AgentsStrings.For(L10n.Shared.Language);
    private readonly StackPanel _body = new() { Margin = new Thickness(12, 0, 12, 12) };
    private readonly DispatcherTimer _battery = new() { Interval = BatteryInterval };
    private readonly List<HomeRow> _rows = [];

    public HomeModule(ISettingsStore store, NowPlayingService nowPlaying, AgentHub agents, IslandTimerService timer, SystemMonitor monitor, Func<HomeItem, bool> isAvailable)
    {
        _store = store;
        _nowPlaying = nowPlaying;
        _agents = agents;
        _timer = timer;
        _monitor = monitor;
        _isAvailable = isAvailable;
        Content = new ScrollViewer { Content = _body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Items = HomeItems.Parse(_store.String(DefaultsKey.FaqraHomeItems), _isAvailable);
        Build();

        Subscribe();
        _battery.Tick += (_, _) => Refresh(HomeItem.Battery);
        Loaded += (_, _) =>
        {
            Subscribe();
            _battery.Start();
        };
        Unloaded += (_, _) =>
        {
            _battery.Stop();
            Unsubscribe();
        };
        // Collapsing the island hides Home without unloading it; a hidden Home keeps nothing busy.
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                Subscribe();
                _battery.Start();
            }
            else
            {
                _battery.Stop();
                Unsubscribe();
            }
        };
    }

    private bool _subscribed;

    /// <summary>WPF can raise Loaded again after Unloaded; subscribe once regardless, and catch up on what was missed.</summary>
    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }
        _subscribed = true;
        _nowPlaying.Changed += OnNowPlaying;
        _agents.Changed += OnAgents;
        _timer.Changed += OnTimer;
        _monitor.SnapshotChanged += OnSnapshot;
        foreach (var row in _rows)
        {
            row.Update();
        }
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }
        _subscribed = false;
        _nowPlaying.Changed -= OnNowPlaying;
        _agents.Changed -= OnAgents;
        _timer.Changed -= OnTimer;
        _monitor.SnapshotChanged -= OnSnapshot;
    }
    /// <summary>A row asked for its section (Timer, System, Agents).</summary>
    public event Action<IslandModule>? SectionRequested;

    /// <summary>A session row asked for that session's card in the Agents section.</summary>
    public event Action<string>? SessionRequested;

    /// <summary>The rows changed in Edit (added, removed or moved), after Home was rebuilt.</summary>
    public event Action? ItemsChanged;

    public IReadOnlyList<HomeItem> Items { get; private set; }

    public bool Editing { get; private set; }

    /// <summary>Whether a row is on Home, so the island knows to sample the system.</summary>
    public bool Shows(HomeItem item) => Items.Contains(item);

    public void BeginEdit()
    {
        Editing = true;
        Build();
    }

    public void EndEdit()
    {
        Editing = false;
        Build();
    }

    private void Build()
    {
        _body.Children.Clear();
        _rows.Clear();
        if (Editing)
        {
            BuildEditor();
            return;
        }
        if (Items.Count == 0)
        {
            _body.Children.Add(AgentCardParts.Caption(_s.Empty));
        }
        foreach (var item in Items)
        {
            var row = CreateRow(item);
            _rows.Add(row);
            _body.Children.Add(row.View);
        }
        var edit = AgentCardParts.Quiet(_s.Edit, (_, _) => BeginEdit());
        AutomationProperties.SetName(edit, _s.Edit);
        _body.Children.Add(edit);
    }

    private HomeRow CreateRow(HomeItem item) => item switch
    {
        HomeItem.NowPlaying => new NowPlayingRow(_nowPlaying, _s),
        HomeItem.Agents => new AgentsRow(_agents, _s, _agentWords, id => SessionRequested?.Invoke(id), () => SectionRequested?.Invoke(IslandModule.FaqraAgents)),
        HomeItem.Timer => new TimerRow(_timer, _s, () => SectionRequested?.Invoke(IslandModule.Timer)),
        HomeItem.System => new SystemRow(_monitor, _s, () => SectionRequested?.Invoke(IslandModule.System)),
        HomeItem.Battery => new BatteryRow(_s),
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null),
    };

    /// <summary>The rows as a list with Move up, Move down and Remove, then the rows that can be added, then Done.</summary>
    private void BuildEditor()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var name = ItemName(item);
            var line = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(EditButton("\uE70E", Format(_s.MoveUpFormat, name), i > 0, () => Change(HomeItems.Move(Items, item, -1))));
            buttons.Children.Add(EditButton("\uE70D", Format(_s.MoveDownFormat, name), i < Items.Count - 1, () => Change(HomeItems.Move(Items, item, 1))));
            buttons.Children.Add(EditButton("\uE711", Format(_s.RemoveFormat, name), true, () => Change(HomeItems.Remove(Items, item))));
            DockPanel.SetDock(buttons, Dock.Right);
            line.Children.Add(buttons);
            var label = AgentCardParts.Title(name);
            label.VerticalAlignment = VerticalAlignment.Center;
            line.Children.Add(label);
            _body.Children.Add(AgentCardParts.Panel(line));
        }
        var addable = HomeItems.Addable(Items, _isAvailable);
        if (addable.Count > 0)
        {
            var header = AgentCardParts.Caption(_s.AddHeader);
            header.Margin = new Thickness(0, 6, 0, 0);
            _body.Children.Add(header);
            var adds = new WrapPanel { Margin = new Thickness(-8, 0, 0, 0) };
            foreach (var item in addable)
            {
                var add = AgentCardParts.Action(Format(_s.AddFormat, ItemName(item)), primary: false, (_, _) => Change(HomeItems.Add(Items, item)));
                add.Margin = new Thickness(8, 8, 0, 0);
                AutomationProperties.SetName(add, Format(_s.AddFormat, ItemName(item)));
                adds.Children.Add(add);
            }
            _body.Children.Add(adds);
        }
        var done = AgentCardParts.Action(_s.Done, primary: true, (_, _) => EndEdit());
        AutomationProperties.SetName(done, _s.Done);
        _body.Children.Add(AgentCardParts.Footer(AgentCardParts.Actions(done), quiet: null));
    }

    private Button EditButton(string glyph, string name, bool enabled, Action click)
    {
        var button = AgentCardParts.Action(glyph, primary: false, (_, _) => click());
        button.FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        button.MinWidth = 32;
        button.Width = 32;
        button.Padding = new Thickness(0);
        button.Margin = new Thickness(6, 0, 0, 0);
        button.IsEnabled = enabled;
        AutomationProperties.SetName(button, name);
        button.ToolTip = name;
        return button;
    }

    private void Change(IReadOnlyList<HomeItem> items)
    {
        _store.Set(DefaultsKey.FaqraHomeItems, HomeItems.Saved(items, _store.String(DefaultsKey.FaqraHomeItems), _isAvailable));
        Items = items;
        Build();
        ItemsChanged?.Invoke();
    }

    private string ItemName(HomeItem item) => item switch
    {
        HomeItem.NowPlaying => _s.ItemNowPlaying,
        HomeItem.Agents => _s.ItemAgents,
        HomeItem.Timer => _s.ItemTimer,
        HomeItem.System => _s.ItemSystem,
        HomeItem.Battery => _s.ItemBattery,
        _ => item.RawValue(),
    };

    private static string Format(string format, object value) => string.Format(CultureInfo.CurrentCulture, format, value);

    private void Refresh(HomeItem item)
    {
        foreach (var row in _rows.Where(row => row.Item == item))
        {
            row.Update();
        }
    }

    // Sources may raise their events off the UI thread; every refresh runs on this control's dispatcher.
    private void OnNowPlaying() => Dispatcher.BeginInvoke(() => Refresh(HomeItem.NowPlaying));

    private void OnAgents() => Refresh(HomeItem.Agents);

    private void OnTimer() => Dispatcher.BeginInvoke(() => Refresh(HomeItem.Timer));

    private void OnSnapshot(SystemSnapshot _) => Dispatcher.BeginInvoke(() => Refresh(HomeItem.System));
}
