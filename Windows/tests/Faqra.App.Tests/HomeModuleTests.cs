using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Faqra.App.Island.Modules;
using Faqra.Core.Defaults;
using Faqra.Core.Island;
using Faqra.Core.Localization;

namespace Faqra.App.Tests;

public class HomeModuleTests
{
    private static readonly HomeStrings S = HomeStrings.EnUS;

    private static IEnumerable<T> All<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            foreach (var nested in All<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static Button Named(DependencyObject root, string name) =>
        All<Button>(root).Single(button => AutomationProperties.GetName(button) == name);

    private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static HomeModule Home(AppServices services, Faqra.Services.Agents.AgentHub? hub = null) =>
        new(services.Store, services.NowPlaying, hub ?? services.Agents, services.Timer, services.Monitor, _ => true);

    [Fact]
    public void TheSectionListNamesHome() => StaThread.Run(() =>
    {
        var picker = new SectionPicker([IslandModule.FaqraHome, IslandModule.Music], _ => { });
        var text = AgentsRenderTests.AllText(picker);
        Assert.Contains("Home", text);
        Assert.DoesNotContain("faqraHome", text);
    });

    [Fact]
    public void HomeShowsItsDefaultRowsInOrder() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        var home = Home(services);
        AgentsRenderTests.RenderInk(new Border { Background = IslandPalette.Surface, Child = home, Width = 412, Height = 400 }, "island-home", 412, 400);

        Assert.Equal([HomeItem.NowPlaying, HomeItem.Agents], home.Items);
        var text = AgentsRenderTests.AllText(home);
        Assert.Contains(S.NothingPlaying, text);
        Assert.Contains(S.NoAgents, text);
        Assert.True(text.IndexOf(S.NothingPlaying, StringComparison.Ordinal) < text.IndexOf(S.NoAgents, StringComparison.Ordinal));
    });

    [Fact]
    public void EditingReordersRemovesAddsAndRemembers() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        var home = Home(services);
        Click(Named(home, S.Edit));
        Assert.True(home.Editing);
        AgentsRenderTests.RenderInk(new Border { Background = IslandPalette.Surface, Child = home, Width = 412, Height = 400 }, "island-home-edit", 412, 400);

        Click(Named(home, string.Format(S.MoveUpFormat, S.ItemAgents)));
        Assert.Equal("agents,nowPlaying,nextEvent", services.Store.String(DefaultsKey.FaqraHomeItems));
        Click(Named(home, string.Format(S.RemoveFormat, S.ItemNowPlaying)));
        Click(Named(home, string.Format(S.AddFormat, S.ItemTimer)));
        Assert.Equal([HomeItem.Agents, HomeItem.Timer], home.Items);
        Assert.Equal("agents,timer,nextEvent", services.Store.String(DefaultsKey.FaqraHomeItems));

        Click(Named(home, S.Done));
        Assert.False(home.Editing);
        Assert.Contains(S.NoTimer, AgentsRenderTests.AllText(home));
    });

    [Fact]
    public void ItemsChangedFiresOnceWhenARowIsAddedInEdit() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        var home = Home(services);
        var raised = 0;
        home.ItemsChanged += () => raised++;
        Click(Named(home, S.Edit));
        Assert.Equal(0, raised);
        Click(Named(home, string.Format(S.AddFormat, S.ItemTimer)));
        Assert.Equal(1, raised);
        Assert.Contains(HomeItem.Timer, home.Items);
    });

    [Fact]
    public void AnEmptyHomeSaysHowToFillIt() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        services.Store.Set(DefaultsKey.FaqraHomeItems, "nextEvent");
        var home = Home(services);
        Assert.Empty(home.Items);
        Assert.Contains(S.Empty, AgentsRenderTests.AllText(home));
        Assert.NotNull(Named(home, S.Edit));
    });

    [Fact]
    public void OnlyAWaitingAgentWearsTheAccent() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Read\"}");
        var home = Home(services, hub);
        Assert.DoesNotContain(All<TextBlock>(home), block => ReferenceEquals(block.Foreground, IslandPalette.Accent));

        hub.ReplaceBoardForTests(hub.Board.Apply(Faqra.Core.Agents.AgentEvent.TryParse("{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}")!, DateTimeOffset.Now));
        var accented = All<TextBlock>(home).Where(block => ReferenceEquals(block.Foreground, IslandPalette.Accent)).ToList();
        Assert.Single(accented);
        Assert.Equal(S.ItemAgents, AgentsRenderTests.Words(accented[0]));
        AgentsRenderTests.RenderInk(new Border { Background = IslandPalette.Surface, Child = home, Width = 412, Height = 400 }, "island-home-waiting", 412, 400);
    });

    [Fact]
    public void RowsOpenTheirSectionOrSession() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        services.Store.Set(DefaultsKey.FaqraHomeItems, "agents,timer,system,battery");
        using var hub = AgentsRenderTests.HubWith("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Read\"}");
        var home = Home(services, hub);
        var sections = new List<IslandModule>();
        var sessions = new List<string>();
        home.SectionRequested += sections.Add;
        home.SessionRequested += sessions.Add;

        Click(Named(home, S.ItemTimer));
        Click(Named(home, S.ItemSystem));
        Click(All<Button>(home).First(button => AutomationProperties.GetName(button).StartsWith("faqra", StringComparison.Ordinal)));

        Assert.Equal([IslandModule.Timer, IslandModule.System], sections);
        Assert.Equal(["s1"], sessions);
    });

    [Fact]
    public void RowsAreUpdatedInPlace() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Read\"}");
        var home = Home(services, hub);
        var play = Named(home, S.PlayPause);
        var session = All<Button>(home).First(button => AutomationProperties.GetName(button).StartsWith("faqra", StringComparison.Ordinal));

        hub.ReplaceBoardForTests(hub.Board.Apply(Faqra.Core.Agents.AgentEvent.TryParse("{\"hook_event_name\":\"PostToolUse\",\"session_id\":\"s1\",\"tool_name\":\"Read\"}")!, DateTimeOffset.Now));

        Assert.Same(play, Named(home, S.PlayPause));
        Assert.Same(session, All<Button>(home).First(button => AutomationProperties.GetName(button).StartsWith("faqra", StringComparison.Ordinal)));
    });

    [Fact]
    public void HomeStillUpdatesAfterBeingUnloadedAndLoadedAgain() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Read\"}");
        var home = Home(services, hub);

        home.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        home.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        hub.ReplaceBoardForTests(hub.Board.Apply(Faqra.Core.Agents.AgentEvent.TryParse("{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}")!, DateTimeOffset.Now));

        Assert.Single(All<TextBlock>(home), block => ReferenceEquals(block.Foreground, IslandPalette.Accent));
    });

    [Fact]
    public void OnlyOneOpenPillShowsWhenSeveralAgentsWait() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        using var hub = AgentsRenderTests.HubWith(
            "{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}",
            "{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s2\",\"cwd\":\"C:\\\\code\\\\other\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}");
        var home = Home(services, hub);

        var pills = All<Border>(home).Where(border => ReferenceEquals(border.Background, IslandPalette.Accent) && border.Visibility == Visibility.Visible);
        Assert.Single(pills);
    });
}
