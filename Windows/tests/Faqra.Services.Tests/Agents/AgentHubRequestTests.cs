using System.Diagnostics;
using Faqra.Core.Agents;
using Faqra.Services.Agents;
using static Faqra.Services.Tests.Agents.HubTestKit;

namespace Faqra.Services.Tests.Agents;

public class AgentHubRequestTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(5);

    private const string BashRequest = "{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}";
    private const string Question = "{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"tool_name\":\"AskUserQuestion\",\"tool_input\":{\"questions\":[{\"question\":\"Which one?\",\"options\":[{\"label\":\"A\"},{\"label\":\"B\"}]}]}}";

    private static AgentHub Hub(SerialContext context, string pipe, bool canAsk = true, string? log = null)
    {
        var hub = new AgentHub(pipe, context, () => T0, log) { CanAsk = () => canAsk };
        hub.SetRunning(true);
        return hub;
    }

    [Fact]
    public async Task HoldsARequestUntilTheOwnerAnswers()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        AgentRequest? arrived = null;
        hub.RequestArrived += request => arrived = request;
        using var relay = await FakeRelay.SendAsync(pipe, BashRequest);

        Assert.True(await Eventually(() => hub.Requests.Count == 1));
        var request = hub.Requests[0];
        Assert.Equal("npm test", request.Detail);
        Assert.Same(request, await OnContext(context, () => arrived));
        Assert.Equal(AgentState.Approval, (await OnContext(context, () => hub.Board)).Sessions["s1"].State);

        Assert.True(await OnContext(context, () => hub.Answer(request.Id, AgentDecision.Allow)));
        Assert.Equal("{\"decision\":\"allow\"}", await relay.ReplyAsync(Within));
        Assert.Empty(hub.Requests);
        Assert.Equal(AgentState.Working, (await OnContext(context, () => hub.Board)).Sessions["s1"].State);
        Assert.False(await OnContext(context, () => hub.Answer(request.Id, AgentDecision.Deny())));
    }

    [Fact]
    public async Task LetsGoAtOnceWhenNoCardCanShow()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe, canAsk: false);
        using var relay = await FakeRelay.SendAsync(pipe, BashRequest);
        var clock = Stopwatch.StartNew();

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.InRange(clock.ElapsedMilliseconds, 0, 1000);
        Assert.Empty(hub.Requests);
        // Watching still works: the session shows it needs the owner's OK in Claude Code.
        Assert.Equal(AgentState.Approval, (await OnContext(context, () => hub.Board)).Sessions["s1"].State);
    }

    [Fact]
    public async Task ReleasingSendsNothing()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        using var relay = await FakeRelay.SendAsync(pipe, BashRequest);
        Assert.True(await Eventually(() => hub.Requests.Count == 1));

        await OnContext(context, () => { hub.Release(hub.Requests[0].Id); return 0; });

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.Empty(hub.Requests);
        Assert.Equal(AgentState.Approval, (await OnContext(context, () => hub.Board)).Sessions["s1"].State);
    }

    [Fact]
    public async Task ACardGoesWhenTheRelayGoesAway()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        var relay = await FakeRelay.SendAsync(pipe, BashRequest);
        Assert.True(await Eventually(() => hub.Requests.Count == 1));

        relay.Dispose();

        Assert.True(await Eventually(() => hub.Requests.Count == 0));
    }

    [Fact]
    public async Task ACardGoesWhenTheTurnMovesOn()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        using var relay = await FakeRelay.SendAsync(pipe, BashRequest);
        Assert.True(await Eventually(() => hub.Requests.Count == 1));

        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"Stop\",\"session_id\":\"s1\"}"))
        {
        }

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.Empty(hub.Requests);
    }

    [Fact]
    public async Task AnotherSessionsTurnLeavesTheCardAlone()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        using var relay = await FakeRelay.SendAsync(pipe, BashRequest);
        Assert.True(await Eventually(() => hub.Requests.Count == 1));

        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"Stop\",\"session_id\":\"s2\"}"))
        {
        }

        Assert.True(await Eventually(() => (hub.Board.Sessions.ContainsKey("s2"))));
        Assert.Single(hub.Requests);
    }

    [Fact]
    public async Task ACardTimesOut()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        hub.DecisionWait = TimeSpan.FromMilliseconds(300);
        using var relay = await FakeRelay.SendAsync(pipe, BashRequest);

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.True(await Eventually(() => hub.Requests.Count == 0));
    }

    [Fact]
    public async Task AQuestionArrivesWithItsOptionsAndTakesAnAnswer()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        using var relay = await FakeRelay.SendAsync(pipe, Question);
        Assert.True(await Eventually(() => hub.Requests.Count == 1));
        var request = hub.Requests[0];
        Assert.Equal(AgentRequestKind.Question, request.Kind);
        Assert.Equal(["A", "B"], request.Questions[0].Options.Select(o => o.Label));

        await OnContext(context, () => hub.Answer(request.Id, AgentDecision.Answer(new Dictionary<string, string> { ["Which one?"] = "B" })));

        var reply = AgentDecision.TryParse(await relay.ReplyAsync(Within))!;
        Assert.Equal(AgentDecisionKind.Answer, reply.Kind);
        Assert.Equal("B", reply.Answers!["Which one?"]);
    }

    [Fact]
    public async Task ARepeatedKeyInAQuestionNeverMakesACard()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        using var relay = await FakeRelay.SendAsync(pipe,
            "{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"tool_name\":\"AskUserQuestion\",\"tool_input\":{\"questions\":[{\"question\":\"A?\",\"question\":\"B?\"}]}}");

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.Empty(hub.Requests);
        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"s2\"}"))
        {
        }
        Assert.True(await Eventually(() => hub.Board.Sessions.ContainsKey("s2")));
    }

    [Theory]
    [InlineData("canAsk")]
    [InlineData("arrived")]
    public async Task AThrowingHandlerLetsTheRequestGoAndTheHubKeepsServing(string thrower)
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        if (thrower == "canAsk")
        {
            hub.CanAsk = () => throw new InvalidOperationException("boom");
        }
        else
        {
            hub.RequestArrived += _ => throw new InvalidOperationException("boom");
        }
        using var relay = await FakeRelay.SendAsync(pipe, BashRequest);

        Assert.Null(await relay.ReplyAsync(TimeSpan.FromSeconds(1)));
        Assert.True(await Eventually(() => hub.Requests.Count == 0));
        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"s2\"}"))
        {
        }
        Assert.True(await Eventually(() => hub.Board.Sessions.ContainsKey("s2")));
    }

    [Fact]
    public async Task StoppingLetsEveryRequestGo()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        using var relay = await FakeRelay.SendAsync(pipe, BashRequest);
        Assert.True(await Eventually(() => hub.Requests.Count == 1));

        hub.SetRunning(false);

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.True(await Eventually(() => hub.Requests.Count == 0));
    }

    [Fact]
    public async Task AFinishedTurnWithWordsIsAnnouncedOnce()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        var finished = new List<AgentSession>();
        hub.TurnFinished += finished.Add;

        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"Stop\",\"session_id\":\"s1\",\"last_assistant_message\":\"All done.\"}"))
        {
        }
        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"Stop\",\"session_id\":\"s2\"}"))
        {
        }

        Assert.True(await Eventually(() => hub.Board.Sessions.Count == 2));
        var announced = await OnContext(context, () => finished.ToList());
        Assert.Equal("s1", Assert.Single(announced).Id);
        Assert.True(announced[0].Unread);

        await OnContext(context, () => { hub.MarkRead("s1"); return 0; });
        Assert.False(hub.Board.Sessions["s1"].Unread);
    }

    [Fact]
    public async Task TheLogNamesTheOutcomeButNeverTheAnswer()
    {
        var pipe = NewPipe();
        var log = Path.Combine(Path.GetTempPath(), $"faqra-agents-{Guid.NewGuid():N}.log");
        using var context = new SerialContext();
        using (var hub = Hub(context, pipe, log: log))
        using (var relay = await FakeRelay.SendAsync(pipe, Question))
        {
            Assert.True(await Eventually(() => hub.Requests.Count == 1));
            await OnContext(context, () => hub.Answer(hub.Requests[0].Id, AgentDecision.Answer(new Dictionary<string, string> { ["Which one?"] = "secret answer" })));
            await relay.ReplyAsync(Within);
            await Task.Delay(200);
        }
        var text = File.ReadAllText(log);
        Assert.Contains("Decision s1 AskUserQuestion answered", text);
        Assert.DoesNotContain("secret answer", text);
        Assert.DoesNotContain("Which one?", text);
        File.Delete(log);
    }
}
