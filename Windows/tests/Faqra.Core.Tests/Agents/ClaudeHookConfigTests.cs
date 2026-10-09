using System.Text.Json.Nodes;
using Faqra.Core.Agents.Install;

namespace Faqra.Core.Tests.Agents;

public class ClaudeHookConfigTests
{
    private const string Relay = @"C:\Users\me\AppData\Local\Faqra\bin\faqra-hook.exe";

    // Shaped like a real settings.json: the owner's own hooks, permissions, and Coucou's leftovers.
    private const string Owner = """
        {
          "permissions": {
            "allow": [
              "Bash(npm test:*)"
            ]
          },
          "hooks": {
            "PostToolUse": [
              {
                "matcher": "Write|Edit",
                "hooks": [
                  {
                    "type": "command",
                    "command": "node \"C:/Users/me/.claude/activity/hook.js\"",
                    "timeout": 10
                  }
                ]
              },
              {
                "hooks": [
                  {
                    "type": "command",
                    "command": "\"C:/Users/me/AppData/Local/Coucou/bin/coucou-hook.exe\" PostToolUse",
                    "timeout": 10
                  }
                ]
              }
            ],
            "Stop": [
              {
                "hooks": [
                  {
                    "type": "command",
                    "command": "\"C:/Users/me/AppData/Local/Coucou/bin/coucou-hook.exe\" Stop",
                    "timeout": 10
                  }
                ]
              }
            ]
          }
        }
        """;

    private static JsonObject Parse(string json) => (JsonObject)JsonNode.Parse(json)!;

    private static IEnumerable<JsonObject> Handlers(JsonObject root, string name) =>
        ((JsonArray)root["hooks"]![name]!).OfType<JsonObject>().SelectMany(group => ((JsonArray)group["hooks"]!).OfType<JsonObject>());

    [Fact]
    public void InstallsEveryEventInExecForm()
    {
        var root = Parse(ClaudeHookConfig.Install(null, Relay, removeCoucou: false));
        foreach (var (name, timeout) in ClaudeHookConfig.Events)
        {
            var handler = Assert.Single(Handlers(root, name));
            Assert.Equal("command", handler["type"]!.GetValue<string>());
            Assert.Equal(Relay, handler["command"]!.GetValue<string>());
            Assert.Equal(name, Assert.Single((JsonArray)handler["args"]!)!.GetValue<string>());
            Assert.Equal(timeout, handler["timeout"]!.GetValue<int>());
        }
        Assert.Equal(14, ClaudeHookConfig.Events.Count);
    }

    [Fact]
    public void LeavesTheOwnersHooksAlone()
    {
        var before = Parse(Owner);
        var after = Parse(ClaudeHookConfig.Install(Owner, Relay, removeCoucou: false));
        Assert.True(JsonNode.DeepEquals(before["permissions"], after["permissions"]));
        var postToolUse = (JsonArray)after["hooks"]!["PostToolUse"]!;
        Assert.True(JsonNode.DeepEquals(before["hooks"]!["PostToolUse"]![0], postToolUse[0]));
        Assert.True(JsonNode.DeepEquals(before["hooks"]!["PostToolUse"]![1], postToolUse[1]));
        Assert.Equal(3, postToolUse.Count);
    }

    [Fact]
    public void RemovesCoucouOnlyWhenAsked()
    {
        var after = Parse(ClaudeHookConfig.Install(Owner, Relay, removeCoucou: true));
        var all = ClaudeHookConfig.Events.SelectMany(e => Handlers(after, e.Event)).Select(h => h["command"]!.GetValue<string>()).ToList();
        Assert.DoesNotContain(all, command => command.Contains("coucou-hook"));
        Assert.Contains(all, command => command.Contains("activity/hook.js"));
        Assert.Equal(2, ((JsonArray)after["hooks"]!["PostToolUse"]!).Count);
    }

    [Fact]
    public void ReinstallingKeepsOneEntryPerEvent()
    {
        var twice = ClaudeHookConfig.Install(ClaudeHookConfig.Install(Owner, Relay, false), Relay, false);
        Assert.Single(Handlers(Parse(twice), "SessionStart"));
        Assert.Equal(new HookStatus(14, 2), ClaudeHookConfig.Inspect(twice));
        Assert.True(ClaudeHookConfig.Inspect(twice).Installed);
    }

    [Fact]
    public void UninstallingRestoresTheOwnersFile()
    {
        var installed = ClaudeHookConfig.Install(Owner, Relay, removeCoucou: false);
        Assert.True(JsonNode.DeepEquals(Parse(Owner), Parse(ClaudeHookConfig.Uninstall(installed, removeCoucou: false))));
        Assert.Equal(new HookStatus(0, 0), ClaudeHookConfig.Inspect(ClaudeHookConfig.Uninstall(installed, removeCoucou: true)));
    }

    [Theory]
    [InlineData("{ // a comment\n}")]
    [InlineData("{\"a\": 1,}")]
    [InlineData("[]")]
    [InlineData("{\"hooks\": []}")]
    [InlineData("{\"hooks\": {\"Stop\": {}}}")]
    public void RefusesAFileItCannotEditSafely(string json) =>
        Assert.Throws<ConfigFormatException>(() => ClaudeHookConfig.Install(json, Relay, false));

    [Fact]
    public void WritesInTheFilesOwnStyle()
    {
        var crlf = ClaudeHookConfig.Install("{\r\n  \"a\": \"é <b> 'x' &\"\r\n}\r\n", Relay, false);
        Assert.Contains("\"é <b> 'x' &\"", crlf);
        Assert.DoesNotContain("\n", crlf.Replace("\r\n", string.Empty));
        Assert.EndsWith("}\r\n", crlf);
        var lf = ClaudeHookConfig.Install("{\n  \"a\": 1\n}", Relay, false);
        Assert.DoesNotContain("\r", lf);
        Assert.EndsWith("}", lf);
    }

    [Fact]
    public void ALeaveAloneRoundTripChangesNothing()
    {
        Assert.Equal(Owner.ReplaceLineEndings("\n"), ClaudeHookConfig.Uninstall(Owner.ReplaceLineEndings("\n"), removeCoucou: false));
    }
}
