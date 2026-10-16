// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Faqra.Core.Agents;
using Faqra.Core.Localization;
using Faqra.Services.Agents;

namespace Faqra.App.Island.Modules;

/// <summary>
/// A box under the focused session's details for prompting it from the island, whether or not Claude is waiting. The
/// words go to the session the box shows; switching sessions drops the draft, so they can never reach another one.
/// </summary>
internal sealed class MessageComposer : ContentControl
{
    private readonly AgentsStrings _s;
    private readonly Func<string, string, Task<AgentMessageOutcome>> _send;
    private bool _sending;

    public MessageComposer(AgentsStrings s, Func<string, string, Task<AgentMessageOutcome>> send, Action wantKeyboard)
    {
        _s = s;
        _send = send;
        Box = new Wpf.Ui.Controls.TextBox
        {
            PlaceholderText = s.MessagePlaceholder,
            FontSize = 12,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxLength = AgentMessage.MaxLength,
            MaxHeight = AgentCardParts.DetailMaxHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        AutomationProperties.SetName(Box, s.MessagePlaceholder);
        // The island never takes the keyboard by itself; clicking into the box is the owner asking for it.
        Box.AddHandler(PreviewMouseDownEvent, new MouseButtonEventHandler((_, _) => wantKeyboard()), handledEventsToo: true);
        Box.TextChanged += (_, _) => UpdateSend();
        Box.PreviewKeyDown += (_, e) =>
        {
            if (AnsweredCard.SendsOnEnter(e.Key, Keyboard.Modifiers))
            {
                e.Handled = true;
                _ = SendAsync();
            }
        };
        SendButton = AgentCardParts.Action(s.Send, primary: true, (_, _) => _ = SendAsync());
        Status = AgentCardParts.Caption(string.Empty);
        Status.Foreground = IslandPalette.Tertiary;
        Status.Margin = new Thickness(0, 6, 0, 0);

        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        stack.Children.Add(Box);
        stack.Children.Add(Status);
        stack.Children.Add(AgentCardParts.Footer(AgentCardParts.Actions(SendButton), quiet: null));
        Content = stack;
        UpdateSend();
    }

    internal Wpf.Ui.Controls.TextBox Box { get; }

    internal Button SendButton { get; }

    internal TextBlock Status { get; }

    /// <summary>The session the words go to.</summary>
    internal string? SessionId { get; private set; }

    /// <summary>Points the box at a session; a draft written for another session is dropped.</summary>
    public void ShowFor(string sessionId)
    {
        if (SessionId == sessionId)
        {
            return;
        }
        SessionId = sessionId;
        Box.Text = string.Empty;
        Status.Text = string.Empty;
    }

    /// <summary>Sends the box's words to its session. Never throws; the caption says how it went.</summary>
    internal async Task SendAsync()
    {
        if (_sending || SessionId is not { } session || !AgentMessage.Fits(Box.Text))
        {
            return;
        }
        var words = Box.Text.Trim();
        _sending = true;
        UpdateSend();
        AgentMessageOutcome outcome;
        try
        {
            outcome = await _send(session, words);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Trace.TraceWarning($"Faqra agents message: {ex.GetType().Name}");
            outcome = AgentMessageOutcome.Unreachable;
        }
        finally
        {
            _sending = false;
        }
        // The owner may have moved to another session meanwhile: that session's box is not touched.
        if (SessionId == session)
        {
            if (outcome == AgentMessageOutcome.Sent)
            {
                Box.Text = string.Empty;
            }
            Status.Text = outcome == AgentMessageOutcome.Sent ? _s.MessageSent : _s.MessageFailed;
        }
        UpdateSend();
    }

    private void UpdateSend() => SendButton.IsEnabled = !_sending && AgentMessage.Fits(Box.Text);
}
