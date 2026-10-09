using Faqra.Core.Agents;
using Faqra.Core.Agents.Orb;

namespace Faqra.Core.Tests.Agents;

public class AgentBoardTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private static AgentEvent E(string name, string session = "s1", string? tool = null, string? input = null,
        string? prompt = null, string? message = null, string? last = null)
    {
        var obj = new System.Text.Json.Nodes.JsonObject { ["hook_event_name"] = name, ["session_id"] = session, ["cwd"] = @"C:\code\faqra" };
        if (tool is not null) obj["tool_name"] = tool;
        if (input is not null) obj["tool_input"] = System.Text.Json.Nodes.JsonNode.Parse(input);
        if (prompt is not null) obj["prompt"] = prompt;
        if (message is not null) obj["message"] = message;
        if (last is not null) obj["last_assistant_message"] = last;
        return AgentEvent.TryParse(obj.ToJsonString())!;
    }

    private static AgentSession Only(AgentBoard board) => Assert.Single(board.Sessions.Values);

    [Fact]
    public void AnyEventStartsASession()
    {
        var board = AgentBoard.Empty.Apply(E("PreToolUse", tool: "Bash", input: "{\"command\":\"npm test\"}"), T0);
        var session = Only(board);
        Assert.Equal("faqra", session.Project);
        Assert.Equal(AgentState.Working, session.State);
        Assert.Equal(new AgentStep(T0, AgentStepKind.Run, "npm test"), session.Steps[^1]);
    }

    [Fact]
    public void APromptStartsThinking()
    {
        var session = Only(AgentBoard.Empty.Apply(E("UserPromptSubmit", prompt: "fix the tray crash\nplease"), T0));
        Assert.Equal(AgentState.Thinking, session.State);
        Assert.Equal(new AgentStep(T0, AgentStepKind.Prompt, "fix the tray crash"), session.Steps[^1]);
    }

    [Theory]
    [InlineData("Read", "{\"file_path\":\"C:\\\\code\\\\a\\\\Tray.cs\"}", AgentState.Working, AgentStepKind.Read, "Tray.cs")]
    [InlineData("Edit", "{\"file_path\":\"src/b.cs\"}", AgentState.Working, AgentStepKind.Edit, "b.cs")]
    [InlineData("Write", "{\"file_path\":\"notes.md\"}", AgentState.Working, AgentStepKind.Edit, "notes.md")]
    [InlineData("Grep", "{\"pattern\":\"TrayIcon\"}", AgentState.Searching, AgentStepKind.Search, "TrayIcon")]
    [InlineData("Glob", "{\"pattern\":\"**/*.cs\"}", AgentState.Searching, AgentStepKind.Search, "**/*.cs")]
    [InlineData("WebSearch", "{\"query\":\"velopack uninstall\"}", AgentState.Searching, AgentStepKind.WebSearch, "velopack uninstall")]
    [InlineData("WebFetch", "{\"url\":\"https://docs.velopack.io/a/b\"}", AgentState.Searching, AgentStepKind.WebFetch, "docs.velopack.io")]
    [InlineData("Task", "{\"description\":\"Review the diff\"}", AgentState.Working, AgentStepKind.Subagent, "Review the diff")]
    [InlineData("TodoWrite", "{}", AgentState.Working, AgentStepKind.Plan, "")]
    [InlineData("mcp__x__y", "{}", AgentState.Working, AgentStepKind.Tool, "mcp__x__y")]
    public void ToolsBecomeSteps(string tool, string input, AgentState state, AgentStepKind kind, string detail)
    {
        var session = Only(AgentBoard.Empty.Apply(E("PreToolUse", tool: tool, input: input), T0));
        Assert.Equal(state, session.State);
        Assert.Equal(kind, session.Steps[^1].Kind);
        Assert.Equal(detail, session.Steps[^1].Detail);
    }

    [Fact]
    public void ALongCommandIsCutToOneShortLine()
    {
        var session = Only(AgentBoard.Empty.Apply(E("PreToolUse", tool: "Bash", input: "{\"command\":\"dotnet test Faqra.sln -c Release --filter Requires!=AudioDevice\\nsecond\"}"), T0));
        Assert.Equal("dotnet test Faqra.sln -c Release…", session.Steps[^1].Detail);
    }

    [Fact]
    public void PermissionAndQuestionWaitForTheOwner()
    {
        var board = AgentBoard.Empty.Apply(E("PermissionRequest", tool: "Bash", input: "{\"command\":\"rm x\"}"), T0);
        Assert.Equal(AgentState.Approval, Only(board).State);
        board = board.Apply(E("PostToolUse", tool: "Bash"), T0.AddSeconds(5));
        Assert.Equal(AgentState.Working, Only(board).State);
        board = board.Apply(E("PermissionRequest", tool: "AskUserQuestion", input: "{\"questions\":[]}"), T0.AddSeconds(6));
        Assert.Equal(AgentState.Question, Only(board).State);
    }

    [Theory]
    [InlineData("Claude hit the rate limit", AgentState.RateLimited)]
    [InlineData("Shall I also update the docs?", AgentState.Question)]
    [InlineData("Claude is waiting for your input", AgentState.Thinking)]
    public void NotificationsReadLikeCoucouReadsThem(string message, AgentState state)
    {
        var board = AgentBoard.Empty.Apply(E("UserPromptSubmit", prompt: "go"), T0);
        Assert.Equal(state, Only(board.Apply(E("Notification", message: message), T0.AddSeconds(1))).State);
    }

    [Fact]
    public void AFinishedTurnKeepsClaudesWordsAndRestsAfterFiveSeconds()
    {
        var board = AgentBoard.Empty.Apply(E("Stop", last: "Fixed it. Tests pass."), T0);
        Assert.Equal(AgentState.Finished, Only(board).State);
        Assert.Equal("Fixed it. Tests pass.", Only(board).LastMessage);
        Assert.Same(board, board.Tick(T0.AddSeconds(5)));
        Assert.Equal(AgentState.Idle, Only(board.Tick(T0.AddSeconds(5.2))).State);
    }

    [Fact]
    public void FailuresAndErrorsShow()
    {
        var board = AgentBoard.Empty.Apply(E("PostToolUseFailure", tool: "Bash"), T0);
        Assert.Equal(new AgentStep(T0, AgentStepKind.Failed, "Bash"), Only(board).Steps[^1]);
        Assert.Equal(AgentState.Error, Only(board.Apply(E("StopFailure", message: "API error"), T0)).State);
    }

    [Fact]
    public void SubagentsKeepAnIdleSessionInTheBackground()
    {
        var board = AgentBoard.Empty.Apply(E("Stop", last: "Started two reviewers."), T0)
            .Apply(E("SubagentStart"), T0.AddSeconds(1));
        board = board.Tick(T0.AddSeconds(7));
        Assert.Equal(AgentState.Background, Only(board).State);
        Assert.Equal(1, Only(board).Subagents);
        board = board.Apply(E("SubagentStop"), T0.AddSeconds(8));
        Assert.Equal(0, Only(board).Subagents);
        Assert.Equal(AgentState.Idle, Only(board).State);
    }

    [Fact]
    public void ASessionFirstSeenWithASubagentIsBackground()
    {
        var board = AgentBoard.Empty.Apply(E("SubagentStart"), T0);
        Assert.Equal(AgentState.Background, Only(board).State);
        Assert.Equal(1, Only(board).Subagents);
        Assert.True(board.IsActive);
    }

    [Fact]
    public void CompactingWhileSubagentsRunRestsToBackground()
    {
        var board = AgentBoard.Empty.Apply(E("PreCompact"), T0)
            .Apply(E("SubagentStart"), T0.AddSeconds(1))
            .Apply(E("PostCompact"), T0.AddSeconds(2));
        Assert.Equal(AgentState.Background, Only(board).State);
    }

    [Fact]
    public void ASubagentSpawnShowsOnceInTheTicker()
    {
        var board = AgentBoard.Empty
            .Apply(E("PreToolUse", tool: "Task", input: "{\"description\":\"Review the diff\"}"), T0)
            .Apply(E("SubagentStart"), T0.AddSeconds(1));
        Assert.Single(Only(board).Steps, step => step.Kind == AgentStepKind.Subagent);
    }

    [Fact]
    public void CompactingShowsThenRests()
    {
        var board = AgentBoard.Empty.Apply(E("PreCompact"), T0);
        Assert.Equal(AgentState.Compacting, Only(board).State);
        Assert.Equal(AgentState.Idle, Only(board.Apply(E("PostCompact"), T0)).State);
    }

    [Fact]
    public void SessionEndRemovesTheSessionAndUnknownEventsChangeNothing()
    {
        var board = AgentBoard.Empty.Apply(E("SessionStart"), T0);
        Assert.Same(board, board.Apply(E("SomethingNew"), T0));
        Assert.Empty(board.Apply(E("SessionEnd"), T0).Sessions);
    }

    [Fact]
    public void KeepsTheLastTwentySteps()
    {
        var board = AgentBoard.Empty;
        for (var i = 0; i < 25; i++)
        {
            board = board.Apply(E("PreToolUse", tool: "Bash", input: $"{{\"command\":\"step {i}\"}}"), T0.AddSeconds(i));
        }
        var steps = Only(board).Steps;
        Assert.Equal(AgentBoard.MaxSteps, steps.Count);
        Assert.Equal("step 5", steps[0].Detail);
        Assert.Equal("step 24", steps[^1].Detail);
    }

    [Fact]
    public void ForgetsASessionIdleForTwelveHours()
    {
        var board = AgentBoard.Empty.Apply(E("SessionStart"), T0);
        Assert.Single(board.Tick(T0 + AgentBoard.IdleExpiry - TimeSpan.FromSeconds(1)).Sessions);
        Assert.Empty(board.Tick(T0 + AgentBoard.IdleExpiry).Sessions);
    }

    [Fact]
    public void OrdersByUrgencyThenRecency()
    {
        var board = AgentBoard.Empty
            .Apply(E("SessionStart", "idle"), T0)
            .Apply(E("PreToolUse", "working", tool: "Bash", input: "{\"command\":\"x\"}"), T0.AddSeconds(1))
            .Apply(E("PermissionRequest", "ok", tool: "Bash", input: "{\"command\":\"y\"}"), T0.AddSeconds(2))
            .Apply(E("PermissionRequest", "question", tool: "AskUserQuestion", input: "{}"), T0.AddSeconds(3))
            .Apply(E("Stop", "done", last: "Done."), T0.AddSeconds(4));
        Assert.Equal(["ok", "question", "working", "done", "idle"], board.Ordered.Select(s => s.Id));
        Assert.Equal("ok", board.MostUrgent!.Id);
        Assert.True(board.IsActive);
        Assert.False(AgentBoard.Empty.Apply(E("SessionStart"), T0).IsActive);
    }

    [Theory]
    [InlineData(AgentState.Approval, OrbLook.Waiting, AgentTone.Caution)]
    [InlineData(AgentState.Question, OrbLook.Waiting, AgentTone.Accent)]
    [InlineData(AgentState.Error, OrbLook.RetryingSurge, AgentTone.Critical)]
    [InlineData(AgentState.RateLimited, OrbLook.Retrying, AgentTone.Caution)]
    [InlineData(AgentState.Working, OrbLook.Working, AgentTone.Neutral)]
    [InlineData(AgentState.Thinking, OrbLook.Reasoning, AgentTone.Neutral)]
    [InlineData(AgentState.Searching, OrbLook.Searching, AgentTone.Neutral)]
    [InlineData(AgentState.Compacting, OrbLook.Compacting, AgentTone.Neutral)]
    [InlineData(AgentState.Background, OrbLook.BackgroundSpiral, AgentTone.Secondary)]
    [InlineData(AgentState.Finished, OrbLook.Base, AgentTone.Success)]
    [InlineData(AgentState.Idle, OrbLook.Base, AgentTone.Secondary)]
    public void EachStateHasTheSpecsOrb(AgentState state, OrbLook look, AgentTone tone)
    {
        var style = AgentOrbStyles.For(state);
        Assert.Equal(look, style.Look);
        Assert.Equal(tone, style.Tone);
        Assert.Equal(state == AgentState.Idle ? 0.5 : 1, style.Speed);
    }
}
