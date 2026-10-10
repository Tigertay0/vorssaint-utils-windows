using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Faqra.Core.Agents;
using Faqra.Services.Agents;
using static Faqra.Services.Tests.Agents.HubTestKit;

namespace Faqra.Services.Tests.Agents;

public class AgentHubSessionTests
{
    [Fact]
    public async Task RemembersWhichWindowASessionRunsIn()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        var asked = new ConcurrentQueue<int>();
        using var hub = new AgentHub(pipe, context, () => DateTimeOffset.Now, locateWindow: pid =>
        {
            asked.Enqueue(pid);
            return new WindowLookup(true, 4242);
        });
        hub.SetRunning(true);

        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"s1\"}"))
        {
            Assert.True(await Eventually(() => hub.WindowOwner("s1") == 4242));
        }
        Assert.Equal(Environment.ProcessId, Assert.Single(asked));

        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"UserPromptSubmit\",\"session_id\":\"s1\",\"prompt\":\"go\"}"))
        {
            Assert.True(await Eventually(() => hub.Board.Sessions.TryGetValue("s1", out var s) && s.State == AgentState.Thinking));
        }
        Assert.Single(asked); // looked for once per session

        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"SessionEnd\",\"session_id\":\"s1\"}"))
        {
            Assert.True(await Eventually(() => hub.WindowOwner("s1") is null && !hub.Board.Sessions.ContainsKey("s1")));
        }
    }

    [Fact]
    public async Task ARelayAlreadyGoneIsLookedForAgainNextTime()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        var lookups = 0;
        using var hub = new AgentHub(pipe, context, () => DateTimeOffset.Now,
            locateWindow: _ => Interlocked.Increment(ref lookups) == 1 ? new WindowLookup(false, null) : new WindowLookup(true, null));
        hub.SetRunning(true);

        foreach (var name in new[] { "SessionStart", "UserPromptSubmit", "PreToolUse" })
        {
            using (await FakeRelay.SendAsync(pipe, $"{{\"hook_event_name\":\"{name}\",\"session_id\":\"s1\",\"tool_name\":\"Read\"}}"))
            {
                await Task.Delay(150);
            }
        }

        Assert.True(await Eventually(() => Volatile.Read(ref lookups) == 2));
        await Task.Delay(200);
        Assert.Equal(2, Volatile.Read(ref lookups)); // settled on the second look, with no window
        Assert.Null(hub.WindowOwner("s1"));
    }

    [Fact]
    public async Task ImplausibleSessionIdsAreNeverLookedUp()
    {
        var pipe = NewPipe();
        using var context = new SerialContext();
        var lookups = 0;
        using var hub = new AgentHub(pipe, context, () => DateTimeOffset.Now, locateWindow: _ =>
        {
            Interlocked.Increment(ref lookups);
            return new WindowLookup(true, 1);
        });
        hub.SetRunning(true);
        using (await FakeRelay.SendAsync(pipe, "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"../x\"}"))
        {
            Assert.True(await Eventually(() => hub.Board.Sessions.ContainsKey("../x")));
        }
        Assert.Equal(0, lookups);
    }

    [Fact]
    public async Task NamesASessionAfterItsConversation()
    {
        var root = Directory.CreateTempSubdirectory("faqra-projects-").FullName;
        var transcript = Path.Combine(root, "C--code-faqra", "s1.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);
        File.WriteAllText(transcript, "{\"type\":\"ai-title\",\"aiTitle\":\"Faqra milestones 5 and 6\",\"sessionId\":\"s1\"}\n");
        var pipe = NewPipe();
        using var context = new SerialContext();
        using var hub = new AgentHub(pipe, context, () => DateTimeOffset.Now, projectsRoot: root);
        hub.SetRunning(true);

        var stop = new JsonObject { ["hook_event_name"] = "Stop", ["session_id"] = "s1", ["cwd"] = @"C:\code\Windows", ["transcript_path"] = transcript }.ToJsonString();
        using (await FakeRelay.SendAsync(pipe, stop))
        {
            Assert.True(await Eventually(() => hub.Board.Sessions.TryGetValue("s1", out var s) && s.Name == "Faqra milestones 5 and 6"));
        }
        Directory.Delete(root, recursive: true);
    }
}
