using System.IO.Pipes;
using System.Text;
using Faqra.Core.Agents;
using Faqra.Services.Agents;

namespace Faqra.Services.Tests.Agents;

public class AgentHubTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private static async Task SendAsync(string pipe, string line)
    {
        using var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(2000);
        await client.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"));
        await client.FlushAsync();
    }

    private static bool CanConnect(string pipe)
    {
        using var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
        try
        {
            client.Connect(100);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static async Task<AgentBoard> WaitFor(AgentHub hub, Func<AgentBoard, bool> done)
    {
        for (var i = 0; i < 100 && !done(hub.Board); i++)
        {
            await Task.Delay(50);
        }
        return hub.Board;
    }

    [Fact]
    public async Task FoldsEventsIntoTheBoard()
    {
        var pipe = "faqra-test-" + Guid.NewGuid().ToString("N");
        using var context = new SerialContext();
        using var hub = new AgentHub(pipe, context, () => T0);
        var changes = 0;
        hub.Changed += () => Interlocked.Increment(ref changes);
        hub.SetRunning(true);

        await SendAsync(pipe, "{\"hook_event_name\":\"UserPromptSubmit\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\faqra\",\"prompt\":\"go\"}");

        var board = await WaitFor(hub, b => b.Sessions.Count == 1);
        Assert.Equal(AgentState.Thinking, board.Sessions["s1"].State);
        Assert.True(changes >= 1);
    }

    [Fact]
    public async Task AcceptsManyConnectionsAtOnce()
    {
        var pipe = "faqra-test-" + Guid.NewGuid().ToString("N");
        using var context = new SerialContext();
        using var hub = new AgentHub(pipe, context, () => T0);
        hub.SetRunning(true);

        await Task.WhenAll(Enumerable.Range(0, 40).Select(i =>
            SendAsync(pipe, $"{{\"hook_event_name\":\"SessionStart\",\"session_id\":\"s{i}\"}}")));

        Assert.Equal(40, (await WaitFor(hub, b => b.Sessions.Count == 40)).Sessions.Count);
    }

    [Fact]
    public async Task IgnoresGarbageAndStopsCleanly()
    {
        var pipe = "faqra-test-" + Guid.NewGuid().ToString("N");
        using var context = new SerialContext();
        using var hub = new AgentHub(pipe, context, () => T0);
        hub.SetRunning(true);
        await SendAsync(pipe, "not json");
        await SendAsync(pipe, "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"ok\"}");
        Assert.Single((await WaitFor(hub, b => b.Sessions.Count == 1)).Sessions);

        hub.SetRunning(false);
        Assert.False(hub.IsRunning);
        Assert.Empty((await WaitFor(hub, b => b.Sessions.Count == 0)).Sessions);
        // Stop cancels the accept loop at once, but the listening instance is disposed on a thread-pool continuation a moment later.
        Assert.True(await HubTestKit.Eventually(() => !CanConnect(pipe)));
    }

    [Fact]
    public async Task WritesALogWithoutTheOwnersText()
    {
        var pipe = "faqra-test-" + Guid.NewGuid().ToString("N");
        var log = Path.Combine(Path.GetTempPath(), $"faqra-agents-{Guid.NewGuid():N}.log");
        using var context = new SerialContext();
        using (var hub = new AgentHub(pipe, context, () => T0, log))
        {
            hub.SetRunning(true);
            await SendAsync(pipe, "{\"hook_event_name\":\"UserPromptSubmit\",\"session_id\":\"abcdef123456\",\"prompt\":\"secret plans\"}");
            await WaitFor(hub, b => b.Sessions.Count == 1);
            await Task.Delay(200);
        }
        var text = File.ReadAllText(log);
        Assert.Contains("UserPromptSubmit", text);
        Assert.Contains("abcdef12", text);
        Assert.DoesNotContain("secret plans", text);
        File.Delete(log);
    }

    [Fact]
    public async Task AnEventReadBeforeStopNeverReturnsToTheBoard()
    {
        var pipe = "faqra-test-" + Guid.NewGuid().ToString("N");
        using var context = new SerialContext();
        using var hub = new AgentHub(pipe, context, () => T0);
        using var reached = new ManualResetEventSlim();
        using var gate = new ManualResetEventSlim();
        hub.BeforePost = () =>
        {
            reached.Set();
            gate.Wait(5000);
        };
        hub.SetRunning(true);
        await SendAsync(pipe, "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"late\"}");
        Assert.True(reached.Wait(5000));

        hub.SetRunning(false);
        await Drain(context);
        gate.Set();
        await Drain(context);
        await Task.Delay(200);
        await Drain(context);

        Assert.False(hub.IsRunning);
        Assert.Empty(hub.Board.Sessions);
    }

    private static Task Drain(SynchronizationContext context)
    {
        var done = new TaskCompletionSource();
        context.Post(_ => done.SetResult(), null);
        return done.Task;
    }
}
