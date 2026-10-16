using Faqra.Core.Agents;
using Faqra.Core.Defaults;
using Faqra.Core.Island;

namespace Faqra.Core.Tests;

public class HomeItemsTests
{
    private static bool Everything(HomeItem _) => true;

    [Fact]
    public void RawNamesAreStableAndRoundTrip()
    {
        Assert.Equal(["nowPlaying", "agents", "timer", "system", "battery"], HomeItems.All.Select(item => item.RawValue()));
        foreach (var item in HomeItems.All)
        {
            Assert.Equal(item, HomeItems.FromRawValue(item.RawValue()));
        }
        Assert.Null(HomeItems.FromRawValue("nextEvent"));
    }

    [Fact]
    public void HomeStartsWithMusicAndTheAgents()
    {
        Assert.Equal(HomeItems.DefaultOrder, DefaultsStore.InMemory().String(DefaultsKey.FaqraHomeItems));
        Assert.Equal([HomeItem.NowPlaying, HomeItem.Agents], HomeItems.Parse(HomeItems.DefaultOrder, Everything));
    }

    [Fact]
    public void ReadsWhatItKnowsAndKeepsTheRest()
    {
        var items = HomeItems.Parse(" battery , agents,agents,shelf,,timer ", item => item != HomeItem.Battery);
        Assert.Equal([HomeItem.Agents, HomeItem.Timer], items);
        Assert.Empty(HomeItems.Parse(null, Everything));

        // Names this version does not know survive a save, so a later Faqra still finds them.
        Assert.Equal("timer,agents,nextEvent,shelf", HomeItems.Saved([HomeItem.Timer, HomeItem.Agents], "agents,nextEvent,timer,shelf,timer", _ => true));
        Assert.Equal("agents", HomeItems.Saved([HomeItem.Agents], null, _ => true));
    }

    [Fact]
    public void AnEditKeepsRowsWhoseFeatureIsOff()
    {
        var saved = HomeItems.Saved([HomeItem.NowPlaying], "nowPlaying,nextEvent,agents", item => item != HomeItem.Agents);
        Assert.Equal("nowPlaying,nextEvent,agents", saved);
        // An available row the owner removed stays removed.
        Assert.Equal("nextEvent", HomeItems.Saved([], "nowPlaying,nextEvent", _ => true));
    }

    [Fact]
    public void EditsReturnNewListsAndStayInBounds()
    {
        IReadOnlyList<HomeItem> items = [HomeItem.NowPlaying, HomeItem.Agents, HomeItem.Timer];
        Assert.Equal([HomeItem.Agents, HomeItem.NowPlaying, HomeItem.Timer], HomeItems.Move(items, HomeItem.Agents, -1));
        Assert.Equal([HomeItem.NowPlaying, HomeItem.Timer, HomeItem.Agents], HomeItems.Move(items, HomeItem.Agents, 1));
        Assert.Same(items, HomeItems.Move(items, HomeItem.NowPlaying, -1));
        Assert.Same(items, HomeItems.Move(items, HomeItem.Timer, 1));
        Assert.Same(items, HomeItems.Move(items, HomeItem.Battery, 1));
        Assert.Equal([HomeItem.NowPlaying, HomeItem.Timer], HomeItems.Remove(items, HomeItem.Agents));
        Assert.Equal([HomeItem.NowPlaying, HomeItem.Agents, HomeItem.Timer, HomeItem.System], HomeItems.Add(items, HomeItem.System));
        Assert.Same(items, HomeItems.Add(items, HomeItem.Timer));
        Assert.Equal([HomeItem.NowPlaying, HomeItem.Agents, HomeItem.Timer], items);
    }

    [Fact]
    public void OnlyAvailableRowsNotOnHomeCanBeAdded() =>
        Assert.Equal([HomeItem.System], HomeItems.Addable([HomeItem.NowPlaying, HomeItem.Agents, HomeItem.Timer], item => item != HomeItem.Battery));

    [Fact]
    public void OnlyAWaitingAgentWearsTheAccent()
    {
        IReadOnlyList<HomeItem> items = [HomeItem.NowPlaying, HomeItem.Agents];
        Assert.Equal(HomeItem.Agents, HomeItems.Accent(items, agentWaiting: true));
        Assert.Null(HomeItems.Accent(items, agentWaiting: false));
        Assert.Null(HomeItems.Accent([HomeItem.NowPlaying], agentWaiting: true));
        Assert.True(HomeItems.Waits(AgentState.Approval));
        Assert.True(HomeItems.Waits(AgentState.Question));
        Assert.True(HomeItems.Waits(AgentState.AwaitingReply));
        Assert.False(HomeItems.Waits(AgentState.Working));
        Assert.False(HomeItems.Waits(AgentState.Finished));
    }
}
