using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using Faqra.Hook;

namespace Faqra.Services.Tests.Agents;

public class RelayTests
{
    private static readonly Func<string, string?> NoEnv = _ => null;

    private static string PipeName() => "faqra-test-" + Guid.NewGuid().ToString("N");

    private static Stream Stdin(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    private static NamedPipeServerStream Server(string name, int inBuffer = 0) =>
        new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, inBuffer, 0);

    [Fact]
    public async Task SendsOneTrimmedLine()
    {
        var name = PipeName();
        using var server = Server(name);
        var received = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            return await reader.ReadLineAsync();
        });

        var exit = Relay.Run(["PreToolUse"], Stdin("{\"session_id\":\"s1\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}"), NoEnv, @"C:\work", name);

        Assert.Equal(0, exit);
        var line = JsonNode.Parse((await received.WaitAsync(TimeSpan.FromSeconds(5)))!)!;
        Assert.Equal("PreToolUse", line["hook_event_name"]!.GetValue<string>());
        Assert.Equal("claude", line["faqra_agent"]!.GetValue<string>());
        Assert.Equal("npm test", line["tool_input"]!["command"]!.GetValue<string>());
    }

    [Fact]
    public void ExitsAtOnceWhenFaqraIsClosed()
    {
        Relay.Run(["Stop"], Stdin("{}"), NoEnv, @"C:\work", PipeName()); // warm up the JIT
        var clock = Stopwatch.StartNew();
        Assert.Equal(0, Relay.Run(["Stop"], Stdin("{\"session_id\":\"s1\"}"), NoEnv, @"C:\work", PipeName()));
        // No pipe means no Faqra: waiting the 300 ms connect budget here would slow every tool call.
        Assert.InRange(clock.ElapsedMilliseconds, 0, 150);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1]")]
    public async Task SendsNothingForGarbage(string stdin)
    {
        var name = PipeName();
        using var server = Server(name);
        var connected = server.WaitForConnectionAsync();
        Assert.Equal(0, Relay.Run(["Stop"], Stdin(stdin), NoEnv, @"C:\work", name));
        var winner = await Task.WhenAny(connected, Task.Delay(500));
        Assert.NotSame(connected, winner);
    }

    [Fact]
    public async Task GivesUpOnAServerThatNeverReads()
    {
        var name = PipeName();
        using var server = Server(name, inBuffer: 4096);
        var connected = server.WaitForConnectionAsync();
        // Every field stays under the 2,000-character cut, so the line stays far larger than the pipe's buffer.
        var many = "{\"session_id\":\"s1\"," + string.Join(",", Enumerable.Range(0, 2000).Select(i => $"\"f{i}\":\"{new string('x', 1500)}\"")) + "}";
        var clock = Stopwatch.StartNew();
        Assert.Equal(0, Relay.Run(["UserPromptSubmit"], Stdin(many), NoEnv, @"C:\work", name));
        Assert.InRange(clock.ElapsedMilliseconds, Relay.RunBudgetMs - 100, Relay.RunBudgetMs + 1500);
        await connected;
    }
}
