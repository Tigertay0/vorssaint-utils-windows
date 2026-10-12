using System.Diagnostics;
using Faqra.Core.Agents;
using Faqra.Services.Agents;
using static Faqra.Services.Tests.Agents.HubTestKit;

namespace Faqra.Services.Tests.Agents;

public class AgentHubReplyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(5);

    private const string Asked = "{\"hook_event_name\":\"Stop\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\faqra\",\"last_assistant_message\":\"Red or blue?\"}";

    private static AgentHub Hub(SerialContext context, string pipe, Func<AgentSession, TimeSpan?>? holdFor = null, string? log = null)
    {
        var hub = new AgentHub(pipe, context, () => T0, log) { CanAsk = () => true, HoldFor = holdFor ?? (_ => null) };
        hub.SetRunning(true);
        return hub;
    }

    [Fact]
    public async Task ATurnNobodyHoldsClosesAtOnce()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        using var relay = await FakeRelay.SendAsync(pipe, Asked);
        var clock = Stopwatch.StartNew();

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.InRange(clock.ElapsedMilliseconds, 0, 1000);
        Assert.Empty(hub.Requests);
        Assert.Equal(AgentState.Finished, (await OnContext(context, () => hub.Board)).Sessions["s1"].State);
    }

    [Fact]
    public async Task AHeldTurnSendsTheHoldLineThenTheReply()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe, _ => TimeSpan.FromMinutes(5));
        using var relay = await FakeRelay.SendAsync(pipe, Asked);

        Assert.Equal(StopReply.HoldLine, await relay.ReplyAsync(Within));
        var request = Assert.Single(hub.Requests);
        Assert.Equal((AgentRequestKind.Reply, "s1", (DateTimeOffset?)T0.AddMinutes(5)), (request.Kind, request.SessionId, request.ClosesAt));
        Assert.Equal(AgentState.AwaitingReply, (await OnContext(context, () => hub.Board)).Sessions["s1"].State);

        Assert.True(await OnContext(context, () => hub.Answer(request.Id, AgentDecision.Reply("Blue, please"))));
        var reply = AgentDecision.TryParse(await relay.ReplyAsync(Within))!;
        Assert.Equal((AgentDecisionKind.Reply, "Blue, please"), (reply.Kind, reply.Message));
        var session = (await OnContext(context, () => hub.Board)).Sessions["s1"];
        Assert.Equal((AgentState.Thinking, "Blue, please", 1), (session.State, session.LastPrompt, session.ConsecutiveReplies));
        Assert.Empty(hub.Requests);
    }

    [Fact]
    public async Task DoneLetsTheTurnEnd()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe, _ => TimeSpan.FromMinutes(5));
        using var relay = await FakeRelay.SendAsync(pipe, Asked);
        Assert.Equal(StopReply.HoldLine, await relay.ReplyAsync(Within));

        await OnContext(context, () => { hub.Release(hub.Requests[0].Id); return 0; });

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.Empty(hub.Requests);
        Assert.Equal(AgentState.Idle, (await OnContext(context, () => hub.Board)).Sessions["s1"].State);
    }

    [Fact]
    public async Task TheReplyWindowRunsOut()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe, _ => TimeSpan.FromMilliseconds(300));
        using var relay = await FakeRelay.SendAsync(pipe, Asked);
        Assert.Equal(StopReply.HoldLine, await relay.ReplyAsync(Within));

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.True(await Eventually(() => hub.Requests.Count == 0));
        Assert.Equal(AgentState.Idle, (await OnContext(context, () => hub.Board)).Sessions["s1"].State);
    }

    [Fact]
    public async Task ANewPromptLetsAHeldTurnGo()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe, _ => TimeSpan.FromMinutes(5));
        using var relay = await FakeRelay.SendAsync(pipe, Asked);
        Assert.Equal(StopReply.HoldLine, await relay.ReplyAsync(Within));

        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"UserPromptSubmit\",\"session_id\":\"s1\",\"prompt\":\"never mind\"}"))
        {
        }

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.True(await Eventually(() => hub.Requests.Count == 0));
    }

    [Fact]
    public async Task TheIslandDecidesBeforeItHearsOfTheAnswer()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        var order = new List<string>();
        using var hub = Hub(context, pipe, session =>
        {
            order.Add($"hold:{session.LastMessage}:{session.State}");
            return TimeSpan.FromMinutes(1);
        });
        hub.TurnFinished += session => order.Add($"finished:{session.State}");
        using var relay = await FakeRelay.SendAsync(pipe, Asked);
        Assert.Equal(StopReply.HoldLine, await relay.ReplyAsync(Within));

        Assert.Equal(["hold:Red or blue?:Finished", "finished:AwaitingReply"], await OnContext(context, () => order.ToList()));
    }

    [Fact]
    public async Task AThrowingHoldForLetsTheTurnEnd()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe, _ => throw new InvalidOperationException("island gone"));
        using var relay = await FakeRelay.SendAsync(pipe, Asked);

        Assert.Null(await relay.ReplyAsync(Within));
        Assert.Empty(hub.Requests);
        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"s2\"}"))
        {
        }
        Assert.True(await Eventually(() => hub.Board.Sessions.ContainsKey("s2")));
    }

    [Fact]
    public async Task RepliesCanBeSwitchedOnPerSession()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"s1\"}"))
        {
        }
        Assert.True(await Eventually(() => hub.Board.Sessions.ContainsKey("s1")));

        var on = await OnContext(context, () =>
        {
            hub.SetRepliesOn("s1", true);
            return hub.Board.Sessions["s1"].RepliesOn;
        });
        Assert.True(on);
    }

    [Fact]
    public async Task TheLogNamesTheReplyButNeverItsWords()
    {
        var pipe = NewPipe();
        var log = Path.Combine(Path.GetTempPath(), $"faqra-agents-{Guid.NewGuid():N}.log");
        using var context = new SerialContext();
        try
        {
            using (var hub = Hub(context, pipe, _ => TimeSpan.FromMinutes(5), log))
            using (var relay = await FakeRelay.SendAsync(pipe, Asked))
            {
                Assert.Equal(StopReply.HoldLine, await relay.ReplyAsync(Within));
                await OnContext(context, () => hub.Answer(hub.Requests[0].Id, AgentDecision.Reply("secret plans")));
                await relay.ReplyAsync(Within);
                Assert.True(await Eventually(() => File.Exists(log) && File.ReadAllText(log).Contains("replied")));
            }
            var text = File.ReadAllText(log);
            Assert.Contains("Decision s1 - replied", text);
            Assert.DoesNotContain("secret plans", text);
            Assert.DoesNotContain("Red or blue", text);
        }
        finally
        {
            File.Delete(log);
        }
    }
}
