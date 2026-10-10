// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// The session list plays the role of Coucou's session ticker (windows/src/views), https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé), with a thinking orb in place of its mascot.

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Faqra.App.Agents;
using Faqra.Core.Agents;
using Faqra.Core.Localization;
using Faqra.Services.Agents;

namespace Faqra.App.Island.Modules;

/// <summary>
/// Every agent session, most urgent first. Above the list sits the focused session's card (an approval, a question, or
/// its latest answer); below it, the session's folder, the owner's last prompt, Claude's last words and its activity.
/// Rows and cards are kept and updated in place, so orbs keep moving and the owner's picks and typing survive events.
/// </summary>
public sealed class AgentsModule : UserControl
{
    private const int ActivityLines = 6;
    private const string AnsweredKey = "answered:";
    private static readonly FontFamily TextFont = new("Segoe UI Variable Text, Segoe UI");
    private static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas");

    private readonly AgentHub _hub;
    private readonly Func<bool> _hooksInstalled;
    private readonly AgentsStrings _s = AgentsStrings.For(L10n.Shared.Language);
    private readonly Dictionary<string, SessionRow> _rows = new(StringComparer.Ordinal);
    private readonly ContentControl _card = new();
    private readonly StackPanel _list = new();
    private readonly StackPanel _detail = new() { Margin = new Thickness(0, 12, 0, 0) };
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private string? _focused;
    private string? _cardKey;

    public AgentsModule(AgentHub hub, Func<bool> hooksInstalled)
    {
        _hub = hub;
        _hooksInstalled = hooksInstalled;
        var body = new StackPanel();
        body.Children.Add(_card);
        body.Children.Add(_list);
        body.Children.Add(_detail);
        Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _clock.Tick += (_, _) => RefreshTimes();
        Render();
        _hub.Changed += Render;
        Loaded += (_, _) => _clock.Start();
        Unloaded += (_, _) =>
        {
            _clock.Stop();
            _hub.Changed -= Render;
        };
    }

    /// <summary>The owner clicked into a box that needs typing: the island should take the keyboard.</summary>
    public event Action? KeyboardWanted;

    /// <summary>The owner asked to see a session's window.</summary>
    public event Action<AgentSession>? GoToWindowRequested;

    /// <summary>The session the card and the details are about.</summary>
    public AgentSession? FocusedSession =>
        _focused is not null && _hub.Board.Sessions.TryGetValue(_focused, out var session) ? session : null;

    /// <summary>Shows this session's card and details.</summary>
    public void FocusSession(string sessionId)
    {
        if (_hub.Board.Sessions.ContainsKey(sessionId))
        {
            _focused = sessionId;
            Render();
        }
    }

    /// <summary>Moves to this session, unless the owner is in the middle of another session's request.</summary>
    public void OfferFocus(string sessionId)
    {
        if (_focused is null || _focused == sessionId || _hub.Requests.All(request => request.SessionId != _focused))
        {
            FocusSession(sessionId);
        }
    }

    /// <summary>Moves the focus to the next (+1) or previous (-1) session in the list, wrapping around.</summary>
    public void MoveFocus(int delta)
    {
        var sessions = _hub.Board.Ordered;
        if (sessions.Count == 0)
        {
            return;
        }
        var at = sessions.Select(session => session.Id).ToList().IndexOf(_focused ?? string.Empty);
        var next = at < 0 ? 0 : ((at + delta) % sessions.Count + sessions.Count) % sessions.Count;
        _focused = sessions[next].Id;
        Render();
    }

    private void Render()
    {
        var sessions = _hub.Board.Ordered;
        if (sessions.Count == 0)
        {
            _rows.Clear();
            _list.Children.Clear();
            _detail.Children.Clear();
            ShowCard(null, null);
            _list.Children.Add(Empty());
            return;
        }
        if (_list.Children.Count > 0 && _list.Children[0] is not SessionRow)
        {
            _list.Children.Clear();
        }
        foreach (var gone in _rows.Keys.Except(sessions.Select(s => s.Id)).ToList())
        {
            _list.Children.Remove(_rows[gone]);
            _rows.Remove(gone);
        }
        if (_focused is null || sessions.All(s => s.Id != _focused))
        {
            // The board orders sessions waiting on the owner first.
            _focused = sessions[0].Id;
        }
        for (var i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            if (!_rows.TryGetValue(session.Id, out var row))
            {
                row = new SessionRow(id => FocusSession(id));
                _rows[session.Id] = row;
            }
            row.Update(session, _s, DateTimeOffset.Now, session.Id == _focused);
            var at = _list.Children.IndexOf(row);
            if (at != i)
            {
                if (at >= 0)
                {
                    _list.Children.RemoveAt(at);
                }
                _list.Children.Insert(i, row);
            }
        }
        var focused = sessions.First(s => s.Id == _focused);
        RenderCard(focused);
        RenderDetail(focused);
    }

    /// <summary>The focused session's card, rebuilt only when what it shows changes, so picks and typing survive.</summary>
    private void RenderCard(AgentSession session)
    {
        if (_hub.Requests.FirstOrDefault(r => r.SessionId == session.Id) is { } request)
        {
            ShowCard(request.Id, () => request.Kind == AgentRequestKind.Question
                ? new QuestionCard(request, session.Name, _s, decision => _hub.Answer(request.Id, decision), () => _hub.Release(request.Id), () => KeyboardWanted?.Invoke())
                : new ApprovalCard(request, session.Name, _s, decision => _hub.Answer(request.Id, decision), () => _hub.Release(request.Id)));
            return;
        }
        if (session.Unread && session.LastMessage is { Length: > 0 })
        {
            ShowCard($"{AnsweredKey}{session.Id}:{session.FinishedAt:O}", () => new AnsweredCard(session, _s, () => _hub.MarkRead(session.Id)));
            return;
        }
        ShowCard(null, null);
    }

    private void ShowCard(string? key, Func<UIElement>? build)
    {
        if (key == _cardKey)
        {
            return;
        }
        _cardKey = key;
        _card.Content = build?.Invoke();
    }

    private void RefreshTimes()
    {
        foreach (var session in _hub.Board.Ordered)
        {
            if (_rows.TryGetValue(session.Id, out var row))
            {
                row.Update(session, _s, DateTimeOffset.Now, session.Id == _focused);
            }
        }
    }

    private void RenderDetail(AgentSession session)
    {
        _detail.Children.Clear();
        var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 10) };
        if (_hub.WindowOwner(session.Id) is not null)
        {
            var go = AgentCardParts.Action(_s.GoToWindow, primary: false, (_, _) => GoToWindowRequested?.Invoke(session));
            DockPanel.SetDock(go, Dock.Right);
            header.Children.Add(go);
        }
        header.Children.Add(new TextBlock
        {
            Text = string.Format(CultureInfo.CurrentCulture, _s.InFolderFormat, session.Project),
            FontFamily = TextFont,
            FontSize = 12,
            Foreground = IslandPalette.Tertiary,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        _detail.Children.Add(header);
        if (session.LastPrompt is { Length: > 0 } prompt)
        {
            _detail.Children.Add(Label(_s.PromptedHeader));
            _detail.Children.Add(Paragraph(prompt));
        }
        // An answered card already shows Claude's words.
        var answeredOnCard = _cardKey?.StartsWith(AnsweredKey, StringComparison.Ordinal) == true;
        if (!answeredOnCard && session.LastMessage is { Length: > 0 } message)
        {
            _detail.Children.Add(Label(_s.LastMessageHeader));
            _detail.Children.Add(Paragraph(message));
        }
        if (session.Steps.Count == 0)
        {
            return;
        }
        _detail.Children.Add(Label(_s.ActivityHeader));
        foreach (var step in session.Steps.Reverse().Take(ActivityLines))
        {
            _detail.Children.Add(new TextBlock
            {
                Text = AgentsText.Step(step, _s),
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontFamily = TextFont,
                FontSize = 12,
                Foreground = IslandPalette.Secondary,
                Margin = new Thickness(0, 2, 0, 0),
            });
        }
    }

    private static TextBlock Paragraph(string text) => new()
    {
        Text = text,
        MaxHeight = 54,
        TextWrapping = TextWrapping.Wrap,
        TextTrimming = TextTrimming.WordEllipsis,
        FontFamily = TextFont,
        FontSize = 12,
        Foreground = IslandPalette.Primary,
        Margin = new Thickness(0, 2, 0, 10),
    };

    private UIElement Empty()
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0) };
        var orb = new OrbView { Diameter = 48, HorizontalAlignment = HorizontalAlignment.Center };
        orb.Apply(AgentOrbStyles.For(AgentState.Idle), IslandPalette.Secondary);
        stack.Children.Add(orb);
        stack.Children.Add(new TextBlock
        {
            Text = _s.EmptyTitle,
            Margin = new Thickness(0, 12, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Center,
            FontFamily = TextFont,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = IslandPalette.Primary,
        });
        var installed = _hooksInstalled();
        stack.Children.Add(new TextBlock
        {
            Text = installed ? _s.EmptyHintInstalled : _s.EmptyHintNotInstalled,
            MaxWidth = 340,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontFamily = TextFont,
            FontSize = 12,
            Foreground = IslandPalette.Secondary,
        });
        if (!installed)
        {
            var open = new Button
            {
                Content = _s.OpenSettings,
                FontFamily = TextFont,
                FontSize = 12,
                MinWidth = 120,
                Height = 32,
                Margin = new Thickness(0, 14, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = IslandPalette.Primary,
                Background = IslandPalette.Fill,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Template = MusicModule.RoundButtonTemplate(16),
            };
            open.Click += (_, _) => App.ShowSettings(Core.Settings.SettingsPage.Agents);
            stack.Children.Add(open);
        }
        return stack;
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        FontFamily = TextFont,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = IslandPalette.Tertiary,
    };

    /// <summary>One session: orb, conversation name, status, and how long since its last event.</summary>
    private sealed class SessionRow : Button
    {
        private readonly OrbView _orb = new() { Diameter = 20, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _name = new() { FontFamily = TextFont, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = IslandPalette.Primary, TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly TextBlock _status = new() { FontFamily = TextFont, FontSize = 12, Foreground = IslandPalette.Secondary, TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly TextBlock _elapsed = new() { FontFamily = MonoFont, FontSize = 11, Foreground = IslandPalette.Tertiary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        private string _id = string.Empty;

        public SessionRow(Action<string> focus)
        {
            var grid = new Grid { Margin = new Thickness(10, 8, 10, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(_name);
            text.Children.Add(_status);
            Grid.SetColumn(text, 1);
            Grid.SetColumn(_elapsed, 2);
            grid.Children.Add(_orb);
            grid.Children.Add(text);
            grid.Children.Add(_elapsed);
            Content = grid;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            Margin = new Thickness(0, 0, 0, 4);
            BorderThickness = new Thickness(0);
            Cursor = System.Windows.Input.Cursors.Hand;
            Template = MusicModule.RoundButtonTemplate(10);
            Click += (_, _) => focus(_id);
        }

        public void Update(AgentSession session, AgentsStrings s, DateTimeOffset now, bool focused)
        {
            _id = session.Id;
            var style = AgentOrbStyles.For(session.State);
            _orb.Apply(style, AgentInk.For(style.Tone));
            _name.Text = session.Name;
            _status.Text = AgentsText.Status(session, s);
            _elapsed.Text = AgentsText.Elapsed(now - session.UpdatedAt, s);
            Background = focused ? IslandPalette.FillStrong : IslandPalette.Fill;
            System.Windows.Automation.AutomationProperties.SetName(this, $"{_name.Text}, {_status.Text}");
        }
    }
}
