using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using Faqra.Core.Agents;
using Faqra.Services.Agents;
using static Faqra.Services.Tests.Agents.HubTestKit;

namespace Faqra.Services.Tests.Agents;

public class AgentHubMessageTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(5);
    private const string Token = "0123456789abcdef0123456789abcdef";

    private static string NewInboxName() => @"LOCAL\cc-msg-" + Guid.NewGuid().ToString("N");

    private static AgentInbox InboxFor(string pipeName) => AgentInbox.TryCreate(@"\\.\pipe\" + pipeName, Token)!;

    private static AgentHub Hub(SerialContext context, string pipe, string? log = null)
    {
        var hub = new AgentHub(pipe, context, () => T0, log);
        hub.SetRunning(true);
        return hub;
    }

    /// <summary>Plays a session's inbox: accepts one connection and returns everything written to it.</summary>
    private static Task<string> InboxReceives(string pipeName) => Task.Run(async () =>
    {
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await server.WaitForConnectionAsync();
        using var reader = new StreamReader(server, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    });

    /// <summary>Reads a log the hub may be appending to at that moment.</summary>
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task RemembersTheInboxAnEventCarries()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = Hub(context, pipe);
        var start = new JsonObject
        {
            ["hook_event_name"] = "SessionStart",
            ["session_id"] = "s1",
            ["messaging_socket"] = @"\\.\pipe\" + NewInboxName(),
            ["messaging_token"] = Token,
        }.ToJsonString();
        using (await FakeRelay.SendAsync(pipe, start))
        {
        }
        Assert.True(await Eventually(() => hub.CanMessage("s1")));
        Assert.False(hub.CanMessage("s2"));

        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"SessionEnd\",\"session_id\":\"s1\"}"))
        {
        }
        Assert.True(await Eventually(() => !hub.CanMessage("s1")));
    }

    [Fact]
    public async Task SendsTheTwoLinesToTheInbox()
    {
        const string words = "Oui, \"bleu\" 🙂\nRun the tests again";
        var name = NewInboxName();
        using var context = new SerialContext();
        using var hub = Hub(context, NewPipe());
        hub.RememberInboxForTests("s1", InboxFor(name));
        var received = InboxReceives(name);

        Assert.Equal(AgentMessageOutcome.Sent, await hub.MessageAsync("s1", "  " + words + "  ").WaitAsync(Within));

        var lines = (await received.WaitAsync(Within)).TrimEnd('\n').Split('\n');
        Assert.Equal("{\"type\":\"auth\",\"token\":\"" + Token + "\"}", lines[0]);
        var content = JsonNode.Parse(lines[1])!["message"]!["content"]!.GetValue<string>();
        Assert.Equal(AgentMessage.Preamble + "\n\n" + words, content);
    }

    [Fact]
    public async Task OnlyAKnownSessionAndRealWordsAreSent()
    {
        using var context = new SerialContext();
        using var hub = Hub(context, NewPipe());
        Assert.Equal(AgentMessageOutcome.Unavailable, await hub.MessageAsync("nobody", "hello"));
        hub.RememberInboxForTests("s1", InboxFor(NewInboxName()));
        Assert.Equal(AgentMessageOutcome.Invalid, await hub.MessageAsync("s1", "   "));
        Assert.Equal(AgentMessageOutcome.Invalid, await hub.MessageAsync("s1", new string('x', AgentMessage.MaxLength + 1)));
    }

    [Fact]
    public async Task AClosedInboxIsUnreachableQuickly()
    {
        using var context = new SerialContext();
        using var hub = Hub(context, NewPipe());
        hub.RememberInboxForTests("s1", InboxFor(NewInboxName()));
        var clock = Stopwatch.StartNew();
        Assert.Equal(AgentMessageOutcome.Unreachable, await hub.MessageAsync("s1", "hello").WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.InRange(clock.ElapsedMilliseconds, 0, 4500);
    }

    [Fact]
    public async Task TheLogNeverHoldsTheWordsOrTheToken()
    {
        var log = Path.Combine(Path.GetTempPath(), $"faqra-agents-{Guid.NewGuid():N}.log");
        var name = NewInboxName();
        using var context = new SerialContext();
        try
        {
            using (var hub = Hub(context, NewPipe(), log))
            {
                hub.RememberInboxForTests("s1", InboxFor(name));
                var received = InboxReceives(name);
                await hub.MessageAsync("s1", "secret plans");
                await received.WaitAsync(Within);
                Assert.True(await Eventually(() => File.Exists(log) && ReadShared(log).Contains("Message s1 sent")));
            }
            var text = ReadShared(log);
            Assert.DoesNotContain("secret plans", text);
            Assert.DoesNotContain(Token, text);
        }
        finally
        {
            File.Delete(log);
        }
    }

    [Fact]
    public async Task StoppingForgetsEveryInbox()
    {
        using var context = new SerialContext();
        using var hub = Hub(context, NewPipe());
        hub.RememberInboxForTests("s1", InboxFor(NewInboxName()));
        await OnContext(context, () => { hub.SetRunning(false); return 0; });
        Assert.False(hub.CanMessage("s1"));
    }
}
