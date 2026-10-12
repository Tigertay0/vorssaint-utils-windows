// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Faqra.App.Agents;
using Faqra.Core.Agents;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Island;
using Faqra.Services.Agents;
using Faqra.Win32.Agents;
using Faqra.Win32.Windows;

namespace Faqra.App.Island;

/// <summary>The island's side of the agents: when cards may show, opening on a session, the shortcuts, and which turns wait for a reply.</summary>
public sealed partial class IslandController
{
    // MARK: agents

    /// <summary>The island can show the Agents section right now: running, not stepped aside for a fullscreen app, and the section on.</summary>
    private bool CanShowAgents() =>
        _window is not null
        && !_suspendedForFullscreen
        && _runtime.IsAvailable(AppFeature.FaqraAgents)
        && VisibleModules().Contains(IslandModule.FaqraAgents);

    /// <summary>
    /// Whether a card may take a request (the hub asks for each one). Never while Coucou's hooks are installed: two apps
    /// answering one request would race, so Faqra only watches then.
    /// </summary>
    private bool CanShowAgentCards() => CanShowAgents() && ClaudeHooks.Read().CoucouEvents == 0;

    private bool IsShowingAgents() =>
        _presentation == IslandPresentation.Expanded && _module == IslandModule.FaqraAgents && !_sectionsOpen && _agentsModule is not null;

    /// <summary>
    /// How long the hub holds a finished turn for the owner's reply, or null to let Claude stop. The hub asks on the UI
    /// thread before the island hears of the answer, so "watching" means the owner was already on that session.
    /// </summary>
    private TimeSpan? HoldFor(AgentSession session) => ReplyRules.HoldFor(
        session,
        cardsCanShow: CanShowAgentCards(),
        watching: IsShowingAgents() && _agentsModule!.FocusedSession?.Id == session.Id,
        opensOnAnswer: _store.Bool(DefaultsKey.FaqraAgentsOpenOnAnswer),
        windowSeconds: _store.Int(DefaultsKey.FaqraAgentsReplyWindowSeconds));

    /// <summary>An agent waits on the owner: chime if asked to, then open on its card without taking the keyboard.</summary>
    private void OnAgentRequest(AgentRequest request)
    {
        if (_store.Bool(DefaultsKey.FaqraAgentsNeedsYouSound))
        {
            AlertSound.Play(AlertKind.NeedsYou);
        }
        OpenOnAgent(request.SessionId);
    }

    /// <summary>
    /// An agent finished a turn: chime and, if the owner wants it, open on its answer without taking the keyboard. A turn
    /// held for a reply always opens: it is held only because the owner switched replies on or was already looking.
    /// </summary>
    private void OnAgentFinished(AgentSession session)
    {
        if (!CanShowAgents())
        {
            return;
        }
        if (_store.Bool(DefaultsKey.FaqraAgentsAnsweredSound))
        {
            AlertSound.Play(AlertKind.Answered);
        }
        if (session.State == AgentState.AwaitingReply || _store.Bool(DefaultsKey.FaqraAgentsOpenOnAnswer))
        {
            OpenOnAgent(session.Id);
        }
    }

    /// <summary>Opens on a session without activating; an Agents section already open only offers it the focus.</summary>
    private void OpenOnAgent(string sessionId)
    {
        if (_window is null)
        {
            return;
        }
        if (IsShowingAgents())
        {
            _agentsModule!.OfferFocus(sessionId);
            return;
        }
        _agentsFocus = sessionId;
        _sectionsOpen = false;
        Expand(takeFocus: false, IslandModule.FaqraAgents);
    }

    /// <summary>The owner is about to type in the island (a question's own-answer box): take the keyboard now.</summary>
    internal void TakeKeyboard()
    {
        if (_window is null || _presentation == IslandPresentation.Collapsed || WindowStyles.ForegroundWindow() == _window.Handle)
        {
            return;
        }
        _previousForeground = WindowStyles.ForegroundWindow();
        WindowStyles.SetNonActivating(_window.Handle, nonActivating: false);
        WindowStyles.Focus(_window.Handle);
        _window.Activate();
    }

    private void GoToWindow(AgentSession session)
    {
        if (_agents.WindowOwner(session.Id) is not { } owner)
        {
            return;
        }
        _agents.MarkRead(session.Id);
        // The keyboard goes to the window being brought forward, not back to whatever had it before.
        _previousForeground = IntPtr.Zero;
        Collapse();
        SessionWindows.BringForward(owner, session.Project);
    }

    /// <summary>Shortcut: open on the oldest waiting card (else the most urgent session) and take the keyboard.</summary>
    public void JumpToWaitingAgent()
    {
        if (!CanShowAgents())
        {
            return;
        }
        _agentsFocus = _agents.Requests.FirstOrDefault()?.SessionId ?? _agents.Board.MostUrgent?.Id;
        _sectionsOpen = false;
        Expand(takeFocus: true, IslandModule.FaqraAgents);
    }

    /// <summary>Shortcut: bring the focused (else the most urgent) session's window forward.</summary>
    public void GoToAgentWindow()
    {
        if ((_agentsModule?.FocusedSession ?? _agents.Board.MostUrgent) is { } session)
        {
            GoToWindow(session);
        }
    }

    /// <summary>Shortcut: the next (+1) or previous (-1) session, opening the island on Agents first.</summary>
    public void CycleAgentSession(int delta)
    {
        if (!CanShowAgents())
        {
            return;
        }
        if (IsShowingAgents())
        {
            _agentsModule!.MoveFocus(delta);
            return;
        }
        _agentsFocus = _agents.Board.MostUrgent?.Id;
        _sectionsOpen = false;
        Expand(takeFocus: false, IslandModule.FaqraAgents);
    }
}
