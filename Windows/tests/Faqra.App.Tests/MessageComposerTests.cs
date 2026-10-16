using System.Windows;
using Faqra.App.Island.Modules;
using Faqra.Core.Agents;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;
using Faqra.Services.Agents;

namespace Faqra.App.Tests;

public class MessageComposerTests
{
    private static readonly AgentsStrings S = AgentsStrings.EnUS;
    private const string WorkingLine = "{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s2\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Read\",\"tool_input\":{\"file_path\":\"C:\\\\code\\\\faqra\\\\a.cs\"}}";

    private static AgentInbox Inbox() =>
        AgentInbox.TryCreate(@"\\.\pipe\LOCAL\cc-msg-" + Guid.NewGuid().ToString("N"), "0123456789abcdef0123456789abcdef")!;

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

    private static MessageComposer Composer(Func<string, string, Task<AgentMessageOutcome>> send, Action? keyboard = null) =>
        new(S, send, keyboard ?? (() => { }));

    /// <summary>Waits for a send while letting this thread's dispatcher run, as the island's would: the composer resumes on it.</summary>
    private static void Drain(Task task)
    {
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        var frame = new System.Windows.Threading.DispatcherFrame();
        task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
        if (!task.IsCompleted)
        {
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }

    [Fact]
    public void TheBoxShowsForASessionFaqraCanReach() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(WorkingLine);
        var module = new AgentsModule(hub, () => true);
        Assert.All(All<MessageComposer>(module), c => Assert.Equal(Visibility.Collapsed, c.Visibility));

        hub.RememberInboxForTests("s2", Inbox());
        hub.ReplaceBoardForTests(hub.Board);
        var composer = Assert.Single(All<MessageComposer>(module));
        Assert.Equal(Visibility.Visible, composer.Visibility);
        Assert.Equal("s2", composer.SessionId);
        AgentsRenderTests.RenderInk(new System.Windows.Controls.Border { Background = IslandPalette.Surface, Child = module, Width = 412, Height = 520 }, "island-agents-message", 412, 520);
    });

    [Fact]
    public void TheBoxKeepsItsWordsWhileEventsArrive() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(WorkingLine);
        hub.RememberInboxForTests("s2", Inbox());
        var module = new AgentsModule(hub, () => true);
        var composer = All<MessageComposer>(module).Single(c => c.Visibility == Visibility.Visible);
        composer.Box.Text = "half typed";

        // A busy session sends many events; the box is never taken out of the window, so the caret stays in it.
        hub.ReplaceBoardForTests(hub.Board.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"PostToolUse\",\"session_id\":\"s2\",\"tool_name\":\"Read\"}")!, DateTimeOffset.Now));
        hub.ReplaceBoardForTests(hub.Board.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s2\",\"tool_name\":\"Grep\",\"tool_input\":{\"pattern\":\"x\"}}")!, DateTimeOffset.Now));

        var after = Assert.Single(All<MessageComposer>(module));
        Assert.Same(composer, after);
        Assert.Equal("half typed", after.Box.Text);
    });

    [Fact]
    public void TheBoxStepsAsideWhileACardWaits() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(WorkingLine);
        hub.RememberInboxForTests("s2", Inbox());
        hub.ReplaceRequestsForTests(AgentRequest.ForReply("r1", "s2", DateTimeOffset.Now, DateTimeOffset.Now.AddMinutes(5)));
        var module = new AgentsModule(hub, () => true);
        Assert.All(All<MessageComposer>(module), c => Assert.Equal(Visibility.Collapsed, c.Visibility));
    });

    [Fact]
    public void SendingGoesToTheSessionShownAndClearsTheBox() => StaThread.Run(() =>
    {
        (string Session, string Text)? sent = null;
        var composer = Composer((session, text) =>
        {
            sent = (session, text);
            return Task.FromResult(AgentMessageOutcome.Sent);
        });
        composer.ShowFor("s1");
        Assert.False(composer.SendButton.IsEnabled);
        composer.Box.Text = "  Run the tests again  ";
        Assert.True(composer.SendButton.IsEnabled);

        Drain(composer.SendAsync());

        Assert.Equal(("s1", "Run the tests again"), sent);
        Assert.Equal(string.Empty, composer.Box.Text);
        Assert.Equal(S.MessageSent, composer.Status.Text);
    });

    [Fact]
    public void AFailedSendKeepsTheWords() => StaThread.Run(() =>
    {
        var composer = Composer((_, _) => Task.FromResult(AgentMessageOutcome.Unreachable));
        composer.ShowFor("s1");
        composer.Box.Text = "Run the tests again";
        Drain(composer.SendAsync());
        Assert.Equal("Run the tests again", composer.Box.Text);
        Assert.Equal(S.MessageFailed, composer.Status.Text);
    });

    [Fact]
    public void ADraftNeverFollowsTheFocusToAnotherSession() => StaThread.Run(() =>
    {
        var composer = Composer((_, _) => Task.FromResult(AgentMessageOutcome.Sent));
        composer.ShowFor("s1");
        composer.Box.Text = "for s1 only";
        composer.ShowFor("s1");
        Assert.Equal("for s1 only", composer.Box.Text);
        composer.ShowFor("s2");
        Assert.Equal(string.Empty, composer.Box.Text);
        Assert.Equal("s2", composer.SessionId);
    });

    [Fact]
    public void ASendThatFinishesAfterASwitchLeavesTheNewSessionAlone() => StaThread.Run(() =>
    {
        var pending = new TaskCompletionSource<AgentMessageOutcome>();
        var composer = Composer((_, _) => pending.Task);
        composer.ShowFor("s1");
        composer.Box.Text = "for s1";
        var sending = composer.SendAsync();
        composer.ShowFor("s2");
        composer.Box.Text = "draft for s2";

        pending.SetResult(AgentMessageOutcome.Sent);
        Drain(sending);

        Assert.Equal("draft for s2", composer.Box.Text);
        Assert.Equal(string.Empty, composer.Status.Text);
    });

    [Fact]
    public void WordsTypedDuringASendAreKept() => StaThread.Run(() =>
    {
        var pending = new TaskCompletionSource<AgentMessageOutcome>();
        var composer = Composer((_, _) => pending.Task);
        composer.ShowFor("s1");
        composer.Box.Text = "first";
        var sending = composer.SendAsync();
        composer.Box.Text = "second";

        pending.SetResult(AgentMessageOutcome.Sent);
        Drain(sending);

        Assert.Equal("second", composer.Box.Text);
    });

    [Fact]
    public void ALateSendAfterLeavingAndComingBackLeavesTheNewDraft() => StaThread.Run(() =>
    {
        var pending = new TaskCompletionSource<AgentMessageOutcome>();
        var composer = Composer((_, _) => pending.Task);
        composer.ShowFor("s1");
        composer.Box.Text = "first";
        var sending = composer.SendAsync();
        composer.ShowFor("s2");
        composer.ShowFor("s1");
        composer.Box.Text = "new draft";

        pending.SetResult(AgentMessageOutcome.Sent);
        Drain(sending);

        Assert.Equal("new draft", composer.Box.Text);
        Assert.Equal(string.Empty, composer.Status.Text);
    });

    [Fact]
    public void AThrowingSendSaysItFailedAndAllowsAnother() => StaThread.Run(() =>
    {
        var composer = Composer((_, _) => throw new InvalidOperationException("boom"));
        composer.ShowFor("s1");
        Assert.Equal(Visibility.Collapsed, composer.Status.Visibility);
        composer.Box.Text = "Run the tests again";

        Drain(composer.SendAsync());

        Assert.Equal(S.MessageFailed, composer.Status.Text);
        Assert.Equal(Visibility.Visible, composer.Status.Visibility);
        Assert.Equal("Run the tests again", composer.Box.Text);
        Assert.True(composer.SendButton.IsEnabled);
    });

    [Fact]
    public void TheBoxStopsAtTheLimitAndAsksForTheKeyboard() => StaThread.Run(() =>
    {
        var asked = 0;
        var composer = Composer((_, _) => Task.FromResult(AgentMessageOutcome.Sent), () => asked++);
        Assert.Equal(AgentMessage.MaxLength, composer.Box.MaxLength);
        Assert.Equal(S.MessagePlaceholder, System.Windows.Automation.AutomationProperties.GetName(composer.Box));
        composer.Box.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
        {
            RoutedEvent = UIElement.PreviewMouseDownEvent,
        });
        Assert.Equal(1, asked);
    });
}
