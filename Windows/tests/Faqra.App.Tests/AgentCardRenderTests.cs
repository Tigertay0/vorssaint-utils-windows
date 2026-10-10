using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Faqra.App.Island.Modules;
using Faqra.Core.Agents;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;

namespace Faqra.App.Tests;

public class AgentCardRenderTests
{
    private static readonly AgentsStrings S = AgentsStrings.EnUS;
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private const string WaitingLine = "{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\site\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}";
    private const string AskingLine = "{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\site\",\"tool_name\":\"AskUserQuestion\"}";
    private const string WorkingLine = "{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s2\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Read\",\"tool_input\":{\"file_path\":\"C:\\\\code\\\\faqra\\\\a.cs\"}}";

    private static AgentRequest Approval(bool always = true, string session = "s1") => new("r1", session, AgentRequestKind.Approval, "Bash",
        "npm test -- --watch=false", [], always ? ["Bash(npm test:*)"] : [], false, T0);

    private static AgentRequest TwoQuestions() => new("r2", "s1", AgentRequestKind.Question, "AskUserQuestion", "",
        [
            new AgentQuestion("Which color?", "Color", false, [new AgentOption("Red", "Warm"), new AgentOption("Blue", "Cool")]),
            new AgentQuestion("Which extras?", "Extras", true, [new AgentOption("Tests", ""), new AgentOption("Docs", ""), new AgentOption("Lint", "")]),
        ],
        [], false, T0);

    private static IEnumerable<T> All<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            foreach (var nested in All<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static Button ButtonNamed(DependencyObject root, string text) => All<Button>(root).Single(button => button.Content as string == text);

    private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static void Render(FrameworkElement element, string name, double height) =>
        AgentsRenderTests.RenderInk(new Border { Background = IslandPalette.Surface, Child = element, Width = 412, Height = height }, name, 412, height);

    [Fact]
    public void ATruncatedApprovalShowsACaptionAndCannotBeAllowed() => StaThread.Run(() =>
    {
        var request = Approval() with { InputTruncated = true };
        var card = new ApprovalCard(request, "Faqra milestones", S, _ => { }, () => { });
        Assert.Contains(S.InputTruncated, AgentsRenderTests.AllText(card));
        var labels = All<Button>(card).Select(button => button.Content as string).ToList();
        Assert.DoesNotContain("Allow", labels);
        Assert.DoesNotContain("Always allow", labels);
        Assert.Contains("Deny", labels);
        Assert.Contains("Answer in Claude Code", labels);
    });

    [Fact]
    public void ActionButtonsShowKeyboardFocusAndNoneIsTheDefault() => StaThread.Run(() =>
    {
        var card = new ApprovalCard(Approval(), "Faqra milestones", S, _ => { }, () => { });
        var buttons = All<Button>(card).ToList();
        Assert.NotEmpty(buttons);
        foreach (var button in buttons)
        {
            Assert.False(button.IsDefault);
            Assert.Contains(button.Template.Triggers.OfType<Trigger>(), t => t.Property == UIElement.IsKeyboardFocusedProperty);
        }
    });

    [Fact]
    public void AnApprovalShowsTheCommandAndAllThreeChoices() => StaThread.Run(() =>
    {
        AgentDecision? chosen = null;
        var card = new ApprovalCard(Approval(), "Faqra milestones", S, decision => chosen = decision, () => { });
        Render(card, "island-card-approval", 220);
        var text = AgentsRenderTests.AllText(card);
        Assert.Contains("Faqra milestones · Wants to run a command", text);
        Assert.Contains("npm test -- --watch=false", text);
        Assert.Contains("Always allow also saves: Bash(npm test:*)", text);

        Click(ButtonNamed(card, "Allow"));
        Assert.Equal(AgentDecisionKind.Allow, chosen!.Kind);
        Click(ButtonNamed(card, "Always allow"));
        Assert.Equal(AgentDecisionKind.Always, chosen!.Kind);
        Click(ButtonNamed(card, "Deny"));
        Assert.Equal(AgentDecisionKind.Deny, chosen!.Kind);
    });

    [Fact]
    public void WithoutSuggestionsThereIsNoAlwaysButton() => StaThread.Run(() =>
    {
        var card = new ApprovalCard(Approval(always: false), "x", S, _ => { }, () => { });
        Assert.DoesNotContain(All<Button>(card), button => button.Content as string == "Always allow");
    });

    [Fact]
    public void AnswerInClaudeCodeLetsTheRequestGo() => StaThread.Run(() =>
    {
        var released = false;
        var card = new ApprovalCard(Approval(), "x", S, _ => { }, () => released = true);
        Click(ButtonNamed(card, "Answer in Claude Code"));
        Assert.True(released);
    });

    [Fact]
    public void AQuestionShowsEveryOptionAndSendsTheAnswers() => StaThread.Run(() =>
    {
        AgentDecision? chosen = null;
        var card = new QuestionCard(TwoQuestions(), "Faqra", S, decision => chosen = decision, () => { }, () => { });
        Render(card, "island-card-question", 520);
        var text = AgentsRenderTests.AllText(card);
        foreach (var label in new[] { "Faqra · Has a question", "Which color?", "Red", "Warm", "Blue", "Cool", "Which extras?", "Tests", "Docs", "Lint" })
        {
            Assert.Contains(label, text);
        }

        Assert.False(card.SendButton!.IsEnabled);
        card.ChoicesFor(0)[1].IsChecked = true;   // Blue
        Assert.False(card.SendButton.IsEnabled);  // the second question has no answer yet
        card.ChoicesFor(1)[0].IsChecked = true;   // Tests
        card.ChoicesFor(1)[2].IsChecked = true;   // Lint
        card.OwnAnswerFor(1).Text = "a changelog";
        Assert.True(card.SendButton.IsEnabled);

        Click(card.SendButton);
        Assert.Equal(AgentDecisionKind.Answer, chosen!.Kind);
        Assert.Equal("Blue", chosen.Answers!["Which color?"]);
        Assert.Equal("Tests, Lint, a changelog", chosen.Answers["Which extras?"]);
    });

    [Fact]
    public void OwnWordsReplaceASingleChoiceAndBack() => StaThread.Run(() =>
    {
        var card = new QuestionCard(TwoQuestions(), "Faqra", S, _ => { }, () => { }, () => { });
        card.ChoicesFor(0)[0].IsChecked = true;
        card.OwnAnswerFor(0).Text = "Green";
        Assert.False(card.ChoicesFor(0)[0].IsChecked);
        card.ChoicesFor(1)[1].IsChecked = true;
        Assert.Equal("Green", card.Answers()!["Which color?"]);

        card.ChoicesFor(0)[1].IsChecked = true;
        Assert.Equal(string.Empty, card.OwnAnswerFor(0).Text);
        Assert.Equal("Blue", card.Answers()!["Which color?"]);
    });

    [Fact]
    public void ClickingIntoTheOwnAnswerAsksForTheKeyboard() => StaThread.Run(() =>
    {
        var asked = 0;
        var card = new QuestionCard(TwoQuestions(), "Faqra", S, _ => { }, () => { }, () => asked++);
        card.OwnAnswerFor(0).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent });
        Assert.Equal(1, asked);
    });

    [Fact]
    public void AnUnreadableQuestionPointsToClaudeCode() => StaThread.Run(() =>
    {
        var card = new QuestionCard(TwoQuestions() with { Questions = [] }, "Faqra", S, _ => { }, () => { }, () => { });
        Assert.Contains("Answer it in Claude Code", AgentsRenderTests.AllText(card));
        Assert.Null(card.SendButton);
    });

    [Fact]
    public void TheModuleShowsTheWaitingCardAboveTheList() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(WaitingLine, WorkingLine);
        hub.ReplaceRequestsForTests(Approval());
        var module = new AgentsModule(hub, () => true);
        Render(module, "island-agents-approval", 480);
        var text = AgentsRenderTests.AllText(module);
        Assert.Contains("site · Wants to run a command", text);
        Assert.True(text.IndexOf("Wants to run a command", StringComparison.Ordinal) < text.IndexOf("Reads a.cs", StringComparison.Ordinal));
    });

    [Fact]
    public void TheCardKeepsWhatTheOwnerTypedWhileEventsArrive() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(AskingLine, WorkingLine);
        hub.ReplaceRequestsForTests(TwoQuestions());
        var module = new AgentsModule(hub, () => true);
        var card = All<QuestionCard>(module).Single();
        card.ChoicesFor(0)[1].IsChecked = true;
        card.OwnAnswerFor(1).Text = "half typed";

        hub.ReplaceBoardForTests(hub.Board.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"PostToolUse\",\"session_id\":\"s2\",\"tool_name\":\"Read\"}")!, T0));
        hub.ReplaceBoardForTests(hub.Board.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s2\",\"tool_name\":\"Grep\",\"tool_input\":{\"pattern\":\"x\"}}")!, T0));

        var after = All<QuestionCard>(module).Single();
        Assert.Same(card, after);
        Assert.True(after.ChoicesFor(0)[1].IsChecked);
        Assert.Equal("half typed", after.OwnAnswerFor(1).Text);
    });

    [Fact]
    public void AnAnsweredTurnShowsWhatClaudeSaidUntilDismissed() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(
            "{\"hook_event_name\":\"UserPromptSubmit\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\Windows\",\"prompt\":\"Fix the tray crash and run the tests\"}",
            "{\"hook_event_name\":\"Stop\",\"session_id\":\"s1\",\"last_assistant_message\":\"Fixed the tray crash. All 1,022 tests pass.\"}");
        hub.ReplaceBoardForTests(hub.Board.Titled("s1", "Faqra milestones 5 and 6"));
        var module = new AgentsModule(hub, () => true);
        Render(module, "island-agents-answered", 420);

        var text = AgentsRenderTests.AllText(module);
        Assert.Contains("Faqra milestones 5 and 6 · Has answered", text);
        Assert.Contains("Fixed the tray crash. All 1,022 tests pass.", text);
        Assert.Contains("Prompted", text);
        Assert.Contains("Fix the tray crash and run the tests", text);
        Assert.Contains("In Windows", text);

        Click(ButtonNamed(module, "Dismiss"));
        var after = AgentsRenderTests.AllText(module);
        Assert.DoesNotContain("Has answered", after);
        Assert.Contains("Fixed the tray crash. All 1,022 tests pass.", after); // now under "Claude said"
    });

    [Fact]
    public void GoToWindowAppearsOnceTheWindowIsKnown() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(WorkingLine);
        var module = new AgentsModule(hub, () => true);
        Assert.DoesNotContain(All<Button>(module), button => button.Content as string == "Go to window");

        hub.RememberWindowForTests("s2", 4242);
        hub.ReplaceBoardForTests(hub.Board);
        AgentSession? asked = null;
        module.GoToWindowRequested += session => asked = session;
        Click(ButtonNamed(module, "Go to window"));
        Assert.Equal("s2", asked!.Id);
    });

    [Fact]
    public void TheShortcutsStepThroughSessions() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(
            "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"a\"}",
            "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"b\"}",
            "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"c\"}");
        var module = new AgentsModule(hub, () => true);
        var order = hub.Board.Ordered.Select(session => session.Id).ToList();
        Assert.Equal(order[0], module.FocusedSession!.Id);
        module.MoveFocus(1);
        Assert.Equal(order[1], module.FocusedSession!.Id);
        module.MoveFocus(-1);
        module.MoveFocus(-1);
        Assert.Equal(order[2], module.FocusedSession!.Id); // wraps around
    });

    [Fact]
    public void ANewRequestDoesNotPullTheFocusFromACardInUse() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(WaitingLine, "{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s3\",\"tool_name\":\"Bash\"}");
        hub.ReplaceRequestsForTests(Approval(session: "s1"));
        var module = new AgentsModule(hub, () => true);
        module.FocusSession("s1");

        module.OfferFocus("s3");
        Assert.Equal("s1", module.FocusedSession!.Id);

        hub.ReplaceRequestsForTests();
        module.OfferFocus("s3");
        Assert.Equal("s3", module.FocusedSession!.Id);
    });

    [Fact]
    public void ALongCommandScrollsInsteadOfBeingCut() => StaThread.Run(() =>
    {
        var lines = Enumerable.Range(1, 19).Select(i => $"echo line {i}").Append("rm -rf /tmp/unique-last-line").ToList();
        var command = string.Join('\n', lines);
        var request = Approval() with { Detail = command };
        var card = new ApprovalCard(request, "x", S, _ => { }, () => { });
        Render(card, "island-card-approval-long", 300);

        var box = All<TextBlock>(card).Single(block => block.Text == command);
        Assert.Contains("unique-last-line", box.Text);
        var scroller = Assert.IsType<ScrollViewer>(box.Parent);
        Assert.Equal(ScrollBarVisibility.Auto, scroller.VerticalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Disabled, scroller.HorizontalScrollBarVisibility);

        var brief = new ApprovalCard(Approval(), "x", S, _ => { }, () => { });
        card.Measure(new Size(412, double.PositiveInfinity));
        brief.Measure(new Size(412, double.PositiveInfinity));
        Assert.True(card.DesiredSize.Height < brief.DesiredSize.Height + 120, "the card grew with the command");
    });
}
