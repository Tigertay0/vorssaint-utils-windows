using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Faqra.App.Agents;
using Faqra.App.Island.Modules;
using Faqra.Core.Agents;
using Faqra.Services.Agents;

namespace Faqra.App.Tests;

public class AgentsRenderTests
{
    private static readonly string OutputDirectory =
        Environment.GetEnvironmentVariable("FAQRA_UI_SHOTS") ?? Path.Combine(Path.GetTempPath(), "faqra-ui");

    /// <summary>Renders on a transparent ground, saves a PNG, and returns how many pixels carry ink.</summary>
    internal static int RenderInk(FrameworkElement element, string name, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        Directory.CreateDirectory(OutputDirectory);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(OutputDirectory, $"{name}.png")))
        {
            encoder.Save(stream);
        }
        var pixels = new byte[(int)width * (int)height * 4];
        bitmap.CopyPixels(pixels, (int)width * 4, 0);
        var inked = 0;
        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0)
            {
                inked++;
            }
        }
        return inked;
    }

    [Fact]
    public void EveryStateDrawsItsOrb() => StaThread.Run(() =>
    {
        foreach (var state in Enum.GetValues<AgentState>())
        {
            var style = AgentOrbStyles.For(state);
            var orb = new OrbView { Diameter = 64 };
            orb.Apply(style, AgentInk.For(style.Tone));
            Assert.True(RenderInk(orb, $"agents-orb-{state}", 64, 64) > 200, $"{state} drew too little");
        }
    });

    [Fact]
    public void TheOrbTakesItsDiameter() => StaThread.Run(() =>
    {
        var orb = new OrbView { Diameter = 18 };
        orb.Measure(new Size(100, 100));
        Assert.Equal(new Size(18, 18), orb.DesiredSize);
    });

    private static AgentHub HubWith(params string[] lines)
    {
        var hub = new AgentHub("faqra-test-unused", SynchronizationContext.Current ?? new SynchronizationContext(),
            () => new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
        var board = AgentBoard.Empty;
        foreach (var line in lines)
        {
            board = board.Apply(AgentEvent.TryParse(line)!, new DateTimeOffset(2026, 10, 9, 11, 58, 0, TimeSpan.Zero));
        }
        hub.ReplaceBoardForTests(board);
        return hub;
    }

    [Fact]
    public void TheModuleListsSessionsMostUrgentFirst() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(Core.Defaults.DefaultsStore.InMemory());
        using var hub = HubWith(
            "{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"a\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"dotnet test\"}}",
            "{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"b\",\"cwd\":\"C:\\\\code\\\\site\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"rm -rf dist\"}}");
        var module = new AgentsModule(hub, () => true);
        var host = new System.Windows.Controls.Border { Background = Island.Modules.IslandPalette.Surface, Child = module, Width = 412, Height = 400 };
        Assert.True(RenderInk(host, "island-agents", 412, 400) > 0);
        var texts = AllText(module);
        Assert.True(texts.IndexOf("site") < texts.IndexOf("faqra"), "the waiting session comes first");
        Assert.Contains("Needs your OK", texts);
        Assert.Contains("Runs dotnet test", texts);
    });

    [Fact]
    public void TheEmptyModuleSaysHowToStart() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(Core.Defaults.DefaultsStore.InMemory());
        using var hub = HubWith();
        var module = new AgentsModule(hub, () => false);
        var host = new System.Windows.Controls.Border { Background = Island.Modules.IslandPalette.Surface, Child = module, Width = 412, Height = 300 };
        RenderInk(host, "island-agents-empty", 412, 300);
        Assert.Contains("Install the Claude Code hooks", AllText(module));
    });

    [Fact]
    public void ThePillShowsTheMostUrgentState() => StaThread.Run(() =>
    {
        var board = AgentBoard.Empty.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"b\",\"tool_name\":\"Bash\"}")!, DateTimeOffset.Now);
        var view = new IdleAgentsView(board.MostUrgent!);
        var host = new System.Windows.Controls.Border { Background = Island.Modules.IslandPalette.Surface, Child = view, Width = 135, Height = 24 };
        RenderInk(host, "island-agents-pill", 135, 24);
        Assert.Contains("Needs your OK", AllText(view));
    });

    [Fact]
    public void RowsAreReusedWhenASessionChangesState() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(Core.Defaults.DefaultsStore.InMemory());
        var first = "{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"a\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"dotnet test\"}}";
        var second = "{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"b\",\"cwd\":\"C:\\\\code\\\\site\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"ls\"}}";
        using var hub = HubWith(first, second);
        var module = new AgentsModule(hub, () => true);
        var before = Rows(module);
        Assert.Equal(2, before.Count);
        var site = before.Single(r => AllText(r).Contains("site"));
        var faqra = before.Single(r => AllText(r).Contains("faqra"));

        var board = hub.Board.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"b\",\"cwd\":\"C:\\\\code\\\\site\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"rm -rf dist\"}}")!, new DateTimeOffset(2026, 10, 9, 11, 59, 0, TimeSpan.Zero));
        hub.ReplaceBoardForTests(board);

        var after = Rows(module);
        Assert.Equal(2, after.Count);
        Assert.Same(site, after[0]);
        Assert.Same(faqra, after[1]);
        Assert.Contains("Needs your OK", AllText(after[0]));
    });

    private static List<System.Windows.Controls.Button> Rows(DependencyObject root)
    {
        var rows = new List<System.Windows.Controls.Button>();
        void Walk(DependencyObject node)
        {
            if (node is System.Windows.Controls.Button button)
            {
                rows.Add(button);
            }
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            {
                Walk(child);
            }
        }
        Walk(root);
        return rows;
    }

    private static string AllText(DependencyObject root)
    {
        var builder = new System.Text.StringBuilder();
        void Walk(DependencyObject node)
        {
            if (node is System.Windows.Controls.TextBlock block)
            {
                builder.Append(block.Text).Append('\n');
            }
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            {
                Walk(child);
            }
        }
        Walk(root);
        return builder.ToString();
    }
}
