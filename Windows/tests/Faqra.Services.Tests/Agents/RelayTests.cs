using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using Faqra.Hook;

namespace Faqra.Services.Tests.Agents;

public class RelayTests
{
    private static readonly Func<string, string?> NoEnv = _ => null;

    private const string BashRequest = "{\"session_id\":\"s1\",\"hook_event_name\":\"PermissionRequest\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}";
    private const string AllowJson = "{\"hookSpecificOutput\":{\"hookEventName\":\"PermissionRequest\",\"decision\":{\"behavior\":\"allow\"}}}";

    private static string PipeName() => "faqra-test-" + Guid.NewGuid().ToString("N");

    private static Stream Stdin(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    private static NamedPipeServerStream Server(string name, int inBuffer = 0) =>
        new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, inBuffer, 0);

    /// <summary>Plays Faqra for one connection: reads the relay's line, waits, writes <paramref name="reply"/> (or nothing) and hangs up.</summary>
    private static Task<string?> FaqraAnswers(NamedPipeServerStream server, string? reply, int delayMs = 0) => Task.Run(async () =>
    {
        string? line = null;
        try
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
            line = await reader.ReadLineAsync();
            await Task.Delay(delayMs);
            if (reply is not null)
            {
                await server.WriteAsync(Encoding.UTF8.GetBytes(reply + "\n"));
                await server.FlushAsync();
            }
        }
        catch (IOException)
        {
            // The relay already left, which some tests expect.
        }
        server.Dispose();
        return line;
    });

    private static string Printed(MemoryStream stdout) => Encoding.UTF8.GetString(stdout.ToArray());

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

        var exit = Relay.Run(["PreToolUse"], Stdin("{\"session_id\":\"s1\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}"), Stream.Null, NoEnv, @"C:\work", name);

        Assert.Equal(0, exit);
        var line = JsonNode.Parse((await received.WaitAsync(TimeSpan.FromSeconds(5)))!)!;
        Assert.Equal("PreToolUse", line["hook_event_name"]!.GetValue<string>());
        Assert.Equal("claude", line["faqra_agent"]!.GetValue<string>());
        Assert.Equal("npm test", line["tool_input"]!["command"]!.GetValue<string>());
    }

    [Fact]
    public void ExitsAtOnceWhenFaqraIsClosed()
    {
        Relay.Run(["Stop"], Stdin("{}"), Stream.Null, NoEnv, @"C:\work", PipeName()); // warm up the JIT
        var clock = Stopwatch.StartNew();
        Assert.Equal(0, Relay.Run(["PermissionRequest"], Stdin(BashRequest), Stream.Null, NoEnv, @"C:\work", PipeName()));
        // No pipe means no Faqra: even a permission request never waits out the 300 ms connect budget.
        Assert.InRange(clock.ElapsedMilliseconds, 0, 250);
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
        Assert.Equal(0, Relay.Run(["PermissionRequest"], Stdin(stdin), Stream.Null, NoEnv, @"C:\work", name));
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
        Assert.Equal(0, Relay.Run(["UserPromptSubmit"], Stdin(many), Stream.Null, NoEnv, @"C:\work", name));
        Assert.InRange(clock.ElapsedMilliseconds, Relay.RunBudgetMs - 100, Relay.RunBudgetMs + 1500);
        await connected;
    }

    [Fact]
    public void GivesUpOnAStdinThatNeverCloses()
    {
        using var stdin = new BlockingStream();
        try
        {
            var clock = Stopwatch.StartNew();
            Assert.Equal(0, Relay.Run(["PermissionRequest"], stdin, Stream.Null, NoEnv, @"C:\work", PipeName()));
            Assert.InRange(clock.ElapsedMilliseconds, 0, Relay.RunBudgetMs + 1500);
        }
        finally
        {
            stdin.Release();
        }
    }

    [Fact]
    public async Task PrintsTheOwnersAllow()
    {
        var name = PipeName();
        var faqra = FaqraAnswers(Server(name), "{\"decision\":\"allow\"}");
        using var stdout = new MemoryStream();
        Assert.Equal(0, Relay.Run(["PermissionRequest"], Stdin(BashRequest), stdout, NoEnv, @"C:\work", name));
        Assert.Equal(AllowJson + "\n", Printed(stdout));
        Assert.Contains("\"tool_name\":\"Bash\"", await faqra);
    }

    [Fact]
    public async Task PrintsNothingWhenFaqraLetsGo()
    {
        var name = PipeName();
        var faqra = FaqraAnswers(Server(name), reply: null);
        using var stdout = new MemoryStream();
        var clock = Stopwatch.StartNew();
        Assert.Equal(0, Relay.Run(["PermissionRequest"], Stdin(BashRequest), stdout, NoEnv, @"C:\work", name));
        Assert.InRange(clock.ElapsedMilliseconds, 0, 1000);
        Assert.Empty(stdout.ToArray());
        await faqra;
    }

    [Fact]
    public async Task WaitsBeyondTheRunBudgetForTheOwner()
    {
        var name = PipeName();
        var faqra = FaqraAnswers(Server(name), "{\"decision\":\"deny\"}", delayMs: Relay.RunBudgetMs + 600);
        using var stdout = new MemoryStream();
        Relay.Run(["PermissionRequest"], Stdin(BashRequest), stdout, NoEnv, @"C:\work", name);
        Assert.Contains("\"behavior\":\"deny\"", Printed(stdout));
        await faqra;
    }

    [Fact]
    public async Task AnEventNobodyWaitsOnNeverPrints()
    {
        var name = PipeName();
        var faqra = FaqraAnswers(Server(name), "{\"decision\":\"allow\"}", delayMs: 200);
        using var stdout = new MemoryStream();
        var clock = Stopwatch.StartNew();
        Relay.Run(["PreToolUse"], Stdin("{\"session_id\":\"s1\",\"tool_name\":\"Bash\"}"), stdout, NoEnv, @"C:\work", name);
        Assert.InRange(clock.ElapsedMilliseconds, 0, 1000);
        Assert.Empty(stdout.ToArray());
        await faqra;
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"decision\":\"maybe\"}")]
    [InlineData("{\"permissionDecision\":\"allow\"}")]
    public async Task AnythingButADecisionPrintsNothing(string reply)
    {
        var name = PipeName();
        var faqra = FaqraAnswers(Server(name), reply);
        using var stdout = new MemoryStream();
        Relay.Run(["PermissionRequest"], Stdin(BashRequest), stdout, NoEnv, @"C:\work", name);
        Assert.Empty(stdout.ToArray());
        await faqra;
    }

    [Fact]
    public async Task AQuestionTakesNoPlainAllow()
    {
        var name = PipeName();
        var faqra = FaqraAnswers(Server(name), "{\"decision\":\"allow\"}");
        using var stdout = new MemoryStream();
        Relay.Run(["PermissionRequest"], Stdin("{\"session_id\":\"s1\",\"hook_event_name\":\"PermissionRequest\",\"tool_name\":\"AskUserQuestion\",\"tool_input\":{\"questions\":[{\"question\":\"Q?\"}]}}"), stdout, NoEnv, @"C:\work", name);
        Assert.Empty(stdout.ToArray());
        await faqra;
    }

    [Fact]
    public async Task TheEventNameMayComeFromTheCommandLine()
    {
        var name = PipeName();
        var faqra = FaqraAnswers(Server(name), "{\"decision\":\"allow\"}");
        using var stdout = new MemoryStream();
        Relay.Run(["PermissionRequest"], Stdin("{\"session_id\":\"s1\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"ls\"}}"), stdout, NoEnv, @"C:\work", name);
        Assert.Equal(AllowJson + "\n", Printed(stdout));
        await faqra;
    }

    [Fact]
    public async Task PrintsAnswersAsUtf8WithTheQuestionWhole()
    {
        var question = "Quelle option préférez-vous ? " + new string('é', 3000);
        var request = new JsonObject
        {
            ["session_id"] = "s1",
            ["hook_event_name"] = "PermissionRequest",
            ["tool_name"] = "AskUserQuestion",
            ["tool_input"] = new JsonObject
            {
                ["questions"] = new JsonArray(new JsonObject { ["question"] = question, ["options"] = new JsonArray(new JsonObject { ["label"] = "Oui" }) }),
            },
        }.ToJsonString();
        var reply = new JsonObject { ["decision"] = "answer", ["answers"] = new JsonObject { [question] = "Ni l'un ni l'autre 🙂" } }.ToJsonString();
        var name = PipeName();
        var faqra = FaqraAnswers(Server(name), reply);
        using var stdout = new MemoryStream();

        Relay.Run(["PermissionRequest"], Stdin(request), stdout, NoEnv, @"C:\work", name);

        var bytes = stdout.ToArray();
        Assert.Equal((byte)'{', bytes[0]); // no byte-order mark
        var input = JsonNode.Parse(Encoding.UTF8.GetString(bytes))!["hookSpecificOutput"]!["decision"]!["updatedInput"]!;
        Assert.Equal(question, input["questions"]![0]!["question"]!.GetValue<string>());
        Assert.Equal("Ni l'un ni l'autre 🙂", input["answers"]![question]!.GetValue<string>());
        await faqra;
    }

    private sealed class BlockingStream : Stream
    {
        private readonly ManualResetEventSlim _gate = new(false);

        public void Release() => _gate.Set();

        public override int Read(byte[] buffer, int offset, int count)
        {
            _gate.Wait();
            return 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
