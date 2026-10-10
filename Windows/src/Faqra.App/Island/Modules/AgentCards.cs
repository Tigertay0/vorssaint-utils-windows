// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// The cards play the role of Coucou's approval and question cards (windows/src/island/hooks.ts),
// https://github.com/Louis-CFM/coucou (MIT License, Copyright (c) 2026 Louis Raillé).

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Faqra.Core.Agents;
using Faqra.Core.Localization;

namespace Faqra.App.Island.Modules;

/// <summary>The cards' shared look: a filled rounded panel, a title, pill buttons with the primary one in the accent.</summary>
internal static class AgentCardParts
{
    internal static readonly FontFamily TextFont = new("Segoe UI Variable Text, Segoe UI");
    internal static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas");

    internal static string Format(string format, params object[] values) => string.Format(CultureInfo.CurrentCulture, format, values);

    internal const double DetailMaxHeight = 120;
    internal const double MessageMaxHeight = 160;

    /// <summary>Long text in a bounded, vertically scrolling box, so the card stays small and nothing is cut off.</summary>
    internal static ScrollViewer Scrolling(TextBlock text, double maxHeight) => new()
    {
        Content = text,
        MaxHeight = maxHeight,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Margin = new Thickness(0, 6, 0, 0),
    };

    internal static Border Panel(UIElement content) => new()
    {
        Background = IslandPalette.Fill,
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(12),
        Margin = new Thickness(0, 0, 0, 10),
        Child = content,
    };

    internal static TextBlock Title(string text) => new()
    {
        Text = text,
        FontFamily = TextFont,
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Foreground = IslandPalette.Primary,
        TextWrapping = TextWrapping.Wrap,
    };

    internal static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontFamily = TextFont,
        FontSize = 12,
        Foreground = IslandPalette.Secondary,
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>A pill button. The primary one is filled with the accent and carries dark text, so it reads first.</summary>
    internal static Button Action(string text, bool primary, RoutedEventHandler click)
    {
        var button = new Button
        {
            Content = text,
            FontFamily = TextFont,
            FontSize = 12,
            FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
            MinWidth = 72,
            Height = 30,
            Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(8, 0, 0, 0),
            Foreground = primary ? IslandPalette.Surface : IslandPalette.Primary,
            Background = primary ? IslandPalette.Accent : IslandPalette.FillStrong,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Template = ActionTemplate,
        };
        button.Click += click;
        return button;
    }

    /// <summary>A low-key text button for the way out ("Answer in Claude Code").</summary>
    internal static Button Quiet(string text, RoutedEventHandler click)
    {
        var button = Action(text, primary: false, click);
        button.Background = Brushes.Transparent;
        button.Foreground = IslandPalette.Secondary;
        button.Padding = new Thickness(0);
        button.Margin = new Thickness(0, 6, 0, 0);
        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.MinWidth = 0;
        return button;
    }

    internal static StackPanel Actions(params UIElement[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var button in buttons)
        {
            row.Children.Add(button);
        }
        return row;
    }

    /// <summary>The actions on the right, the way out under them on the left: both fit the island's width.</summary>
    internal static StackPanel Footer(StackPanel actions, Button? quiet)
    {
        var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        footer.Children.Add(actions);
        if (quiet is not null)
        {
            footer.Children.Add(quiet);
        }
        return footer;
    }

    /// <summary>Rounded chrome that keeps its own colour: lighter on hover, pressed in on click, dimmed when disabled.</summary>
    private static readonly ControlTemplate ActionTemplate = BuildActionTemplate();

    private static ControlTemplate BuildActionTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border), "Chrome");
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(15));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.88, "Chrome"));
        template.Triggers.Add(hover);
        var focused = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focused.Setters.Add(new Setter(Border.BorderBrushProperty, IslandPalette.Primary, "Chrome"));
        focused.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "Chrome"));
        template.Triggers.Add(focused);
        var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(UIElement.RenderTransformProperty, new ScaleTransform(0.97, 0.97), "Chrome"));
        pressed.Setters.Add(new Setter(UIElement.RenderTransformOriginProperty, new Point(0.5, 0.5), "Chrome"));
        template.Triggers.Add(pressed);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4, "Chrome"));
        template.Triggers.Add(disabled);
        template.Seal();
        return template;
    }
}

/// <summary>Claude wants to use a tool: what it would do, then Deny, Always allow and Allow, with Allow the primary.</summary>
internal sealed class ApprovalCard : ContentControl
{
    public ApprovalCard(AgentRequest request, string name, AgentsStrings s, Action<AgentDecision> decide, Action release)
    {
        var stack = new StackPanel();
        stack.Children.Add(AgentCardParts.Title(AgentCardParts.Format(s.CardTitleFormat, name, AgentsText.RequestTitle(request, s))));
        if (request.Detail.Length > 0)
        {
            // Scrolls instead of clipping: every character of the command can be read before Allow.
            stack.Children.Add(AgentCardParts.Scrolling(new TextBlock
            {
                Text = request.Detail,
                FontFamily = AgentCardParts.MonoFont,
                FontSize = 12,
                Foreground = IslandPalette.Secondary,
                TextWrapping = TextWrapping.Wrap,
            }, AgentCardParts.DetailMaxHeight));
        }
        if (request.InputTruncated)
        {
            var shortened = AgentCardParts.Caption(s.InputTruncated);
            shortened.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(shortened);
        }
        if (request.CanAlways && !request.InputTruncated)
        {
            var saves = request.AlwaysRules.ToList();
            if (request.AlwaysAcceptsEdits)
            {
                saves.Add(s.AlwaysAcceptEdits);
            }
            var note = AgentCardParts.Caption(AgentCardParts.Format(s.AlwaysSavesFormat, string.Join(", ", saves)));
            note.Foreground = IslandPalette.Tertiary;
            note.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(note);
        }
        var actions = AgentCardParts.Actions(AgentCardParts.Action(s.Deny, primary: false, (_, _) => decide(AgentDecision.Deny())));
        if (request.CanAlways && !request.InputTruncated)
        {
            actions.Children.Add(AgentCardParts.Action(s.AlwaysAllow, primary: false, (_, _) => decide(AgentDecision.Always)));
        }
        // Allow is never the default button: Enter must not run a command the owner has not read.
        if (!request.InputTruncated)
        {
            actions.Children.Add(AgentCardParts.Action(s.Allow, primary: true, (_, _) => decide(AgentDecision.Allow)));
        }
        stack.Children.Add(AgentCardParts.Footer(actions, AgentCardParts.Quiet(s.AnswerInClaude, (_, _) => release())));
        Content = AgentCardParts.Panel(stack);
    }
}

/// <summary>
/// Claude asks one or more questions: every option shown (radio buttons, or check boxes when several may be picked),
/// a box for the owner's own words, then Send once every question has an answer.
/// </summary>
internal sealed class QuestionCard : ContentControl
{
    private readonly AgentRequest _request;
    private readonly Action<AgentDecision> _decide;
    private readonly List<Part> _parts = [];
    private readonly Button? _send;

    /// <summary>One question's controls.</summary>
    private sealed record Part(AgentQuestion Question, List<ToggleButton> Choices, TextBox Own);

    public QuestionCard(AgentRequest request, string name, AgentsStrings s, Action<AgentDecision> decide, Action release, Action wantKeyboard)
    {
        _request = request;
        _decide = decide;
        var stack = new StackPanel();
        stack.Children.Add(AgentCardParts.Title(AgentCardParts.Format(s.CardTitleFormat, name, s.StateQuestion)));
        var elsewhere = AgentCardParts.Quiet(s.AnswerInClaude, (_, _) => release());
        if (request.Questions.Count == 0)
        {
            var unreadable = AgentCardParts.Caption(s.QuestionUnreadable);
            unreadable.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(unreadable);
            stack.Children.Add(AgentCardParts.Footer(AgentCardParts.Actions(), elsewhere));
            Content = AgentCardParts.Panel(stack);
            return;
        }
        for (var i = 0; i < request.Questions.Count; i++)
        {
            _parts.Add(AddQuestion(stack, request.Questions[i], $"{request.Id}-{i}", s, wantKeyboard));
        }
        _send = AgentCardParts.Action(s.Send, primary: true, (_, _) => Send());
        stack.Children.Add(AgentCardParts.Footer(AgentCardParts.Actions(_send), elsewhere));
        Content = AgentCardParts.Panel(stack);
        UpdateSend();
    }

    internal IReadOnlyList<ToggleButton> ChoicesFor(int question) => _parts[question].Choices;

    internal TextBox OwnAnswerFor(int question) => _parts[question].Own;

    internal Button? SendButton => _send;

    /// <summary>Every question's answer keyed by its text, or null until each has one.</summary>
    internal IReadOnlyDictionary<string, string>? Answers() => AgentQuestions.Answers(
        _request.Questions,
        _parts.Select(part => AgentQuestions.Compose(part.Question, Picked(part), part.Own.Text)).ToList());

    private Part AddQuestion(StackPanel stack, AgentQuestion question, string group, AgentsStrings s, Action wantKeyboard)
    {
        if (question.Header.Length > 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = question.Header,
                FontFamily = AgentCardParts.TextFont,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = IslandPalette.Tertiary,
                Margin = new Thickness(0, 12, 0, 0),
            });
        }
        var text = AgentCardParts.Caption(question.Text);
        text.Foreground = IslandPalette.Primary;
        text.FontSize = 13;
        text.Margin = new Thickness(0, question.Header.Length > 0 ? 2 : 12, 0, 0);
        stack.Children.Add(text);

        var own = new Wpf.Ui.Controls.TextBox { PlaceholderText = s.QuestionOwnAnswer, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };
        System.Windows.Automation.AutomationProperties.SetName(own, s.QuestionOwnAnswer);
        var choices = new List<ToggleButton>();
        foreach (var option in question.Options)
        {
            ToggleButton choice = question.MultiSelect ? new CheckBox() : new RadioButton { GroupName = group };
            choice.Content = OptionContent(option);
            choice.Tag = option.Label;
            choice.Margin = new Thickness(0, 6, 0, 0);
            System.Windows.Automation.AutomationProperties.SetName(choice, option.Label);
            choice.Checked += (_, _) =>
            {
                // One answer per single-choice question: a pick clears the owner's own words.
                if (!question.MultiSelect)
                {
                    own.Text = string.Empty;
                }
                UpdateSend();
            };
            choice.Unchecked += (_, _) => UpdateSend();
            choices.Add(choice);
            stack.Children.Add(choice);
        }
        // The island never takes the keyboard by itself; clicking into the box is the owner asking for it. Registered
        // with handledEventsToo so the text box's own mouse handling cannot swallow it.
        own.AddHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler((_, _) => wantKeyboard()), handledEventsToo: true);
        own.TextChanged += (_, _) =>
        {
            if (!question.MultiSelect && own.Text.Trim().Length > 0)
            {
                foreach (var choice in choices)
                {
                    choice.IsChecked = false;
                }
            }
            UpdateSend();
        };
        own.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _send?.IsEnabled == true)
            {
                e.Handled = true;
                Send();
            }
        };
        stack.Children.Add(own);
        return new Part(question, choices, own);
    }

    private static List<string> Picked(Part part) =>
        part.Choices.Where(choice => choice.IsChecked == true).Select(choice => choice.Tag as string).OfType<string>().ToList();

    private void UpdateSend()
    {
        if (_send is not null)
        {
            _send.IsEnabled = Answers() is not null;
        }
    }

    private void Send()
    {
        if (Answers() is not { } answers)
        {
            return;
        }
        var ownWords = _parts.Any(part => part.Own.Text.Trim().Length > 0);
        _decide(ownWords && !AgentQuestions.OwnTextInAnswers
            ? AgentDecision.Deny(AgentQuestions.AsDenialMessage(answers))
            : AgentDecision.Answer(answers));
    }

    private static UIElement OptionContent(AgentOption option)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = option.Label,
            FontFamily = AgentCardParts.TextFont,
            FontSize = 12,
            Foreground = IslandPalette.Primary,
            TextWrapping = TextWrapping.Wrap,
        });
        if (option.Description.Length > 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = option.Description,
                FontFamily = AgentCardParts.TextFont,
                FontSize = 11,
                Foreground = IslandPalette.Secondary,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        return stack;
    }
}

/// <summary>Claude finished a turn: what it said, and Dismiss. A3 adds the reply box here.</summary>
internal sealed class AnsweredCard : ContentControl
{
    public AnsweredCard(AgentSession session, AgentsStrings s, Action dismiss)
    {
        var stack = new StackPanel();
        stack.Children.Add(AgentCardParts.Title(AgentCardParts.Format(s.CardTitleFormat, session.Name, s.AnsweredTitle)));
        stack.Children.Add(AgentCardParts.Scrolling(new TextBlock
        {
            Text = session.LastMessage ?? string.Empty,
            FontFamily = AgentCardParts.TextFont,
            FontSize = 12,
            Foreground = IslandPalette.Primary,
            TextWrapping = TextWrapping.Wrap,
        }, AgentCardParts.MessageMaxHeight));
        stack.Children.Add(AgentCardParts.Footer(AgentCardParts.Actions(AgentCardParts.Action(s.Dismiss, primary: false, (_, _) => dismiss())), quiet: null));
        Content = AgentCardParts.Panel(stack);
    }
}
