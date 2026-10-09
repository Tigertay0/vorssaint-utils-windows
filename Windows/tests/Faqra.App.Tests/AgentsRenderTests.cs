using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Faqra.App.Agents;
using Faqra.App.Island.Modules;
using Faqra.App.Settings;
using Faqra.App.Settings.Pages;
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

    [Fact]
    public void ThePageOffersAReviewedInstall() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(Core.Defaults.DefaultsStore.InMemory());
        var dir = Directory.CreateTempSubdirectory("faqra-page-").FullName;
        var settings = Path.Combine(dir, "settings.json");
        File.WriteAllText(settings, "{\n  \"hooks\": {\n    \"Stop\": [\n      {\n        \"hooks\": [\n          {\n            \"type\": \"command\",\n            \"command\": \"\\\"C:/x/coucou-hook.exe\\\" Stop\"\n          }\n        ]\n      }\n    ]\n  }\n}\n");
        var relay = Path.Combine(dir, "faqra-hook.exe");
        File.WriteAllText(relay, "stub");
        var page = new AgentsPage(settings, relay);
        RenderInk(page, "settings-agents", 840, 560);
        var texts = AllText(page);
        Assert.Contains("Hooks not installed", texts);
        Assert.Contains("Coucou's hooks still run on 1 of", texts);
        Directory.Delete(dir, recursive: true);
    });

    [Fact]
    public void TheReviewShowsTheDiffBeforeWriting() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(Core.Defaults.DefaultsStore.InMemory());
        var dir = Directory.CreateTempSubdirectory("faqra-review-").FullName;
        var settings = Path.Combine(dir, "settings.json");
        File.WriteAllText(settings, "{\n  \"a\": 1\n}\n");
        var review = new ConfigReviewWindow(ConfigReviewWindow.Mode.Install, settings, @"C:\x\faqra-hook.exe", coucouEvents: 0);
        RenderInk((FrameworkElement)review.Content, "settings-agents-review", 760, 560);
        var texts = AllText((DependencyObject)review.Content);
        Assert.Contains(texts.Split('\n'), line => line.StartsWith('+') && line.Contains("\"SessionStart\": ["));
        Assert.Contains("settings.json.bak-", texts);
        Assert.Equal("{\n  \"a\": 1\n}\n", File.ReadAllText(settings));
        review.Close();
        Directory.Delete(dir, recursive: true);
    });

    private const string CoucouOnly = "{\n  \"hooks\": {\n    \"Stop\": [\n      {\n        \"hooks\": [\n          {\n            \"type\": \"command\",\n            \"command\": \"\\\"C:/x/coucou-hook.exe\\\" Stop\"\n          }\n        ]\n      }\n    ]\n  }\n}\n";
    private const string Relay = @"C:\x\faqra-hook.exe";

    private static string TempDir() => Directory.CreateTempSubdirectory("faqra-fix-").FullName;

    private static ConfigReviewWindow Install(string settings, int coucouEvents = 0) =>
        new(ConfigReviewWindow.Mode.Install, settings, Relay, coucouEvents);

    private static string[] Lines(ConfigReviewWindow review) => AllText((DependencyObject)review.Content).Split('\n');

    [Fact]
    public void TryApplyWritesTheEditAndKeepsAnExactBackup() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(Core.Defaults.DefaultsStore.InMemory());
        var dir = TempDir();
        var settings = Path.Combine(dir, "settings.json");
        const string original = "{\n  \"a\": 1\n}\n";
        File.WriteAllText(settings, original);
        var review = Install(settings);
        Assert.True(review.TryApply());
        Assert.Contains("SessionStart", File.ReadAllText(settings));
        var backup = Assert.Single(Directory.GetFiles(dir, "settings.json.bak-*"));
        Assert.Equal(original, File.ReadAllText(backup));
        review.Close();
        Directory.Delete(dir, recursive: true);
    });

    [Fact]
    public void AFileChangedAfterThePreviewIsReshownNotWritten() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(Core.Defaults.DefaultsStore.InMemory());
        var dir = TempDir();
        var settings = Path.Combine(dir, "settings.json");
        File.WriteAllText(settings, "{\n  \"a\": 1\n}\n");
        var review = Install(settings);
        const string changed = "{\n  \"b\": 2\n}\n";
        File.WriteAllText(settings, changed);
        Assert.False(review.TryApply());
        Assert.Equal(changed, File.ReadAllText(settings));
        Assert.Empty(Directory.GetFiles(dir, "settings.json.bak-*"));
        var lines = Lines(review);
        Assert.Contains("Claude's settings changed since this preview. Here is the new version.", lines);
        Assert.Contains(lines, line => line.StartsWith('+') && line.Contains("\"b\": 2"));
        review.Close();
        Directory.Delete(dir, recursive: true);
    });

    [Fact]
    public void UncheckingTheCoucouBoxKeepsItsHooks() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(Core.Defaults.DefaultsStore.InMemory());
        var dir = TempDir();
        var settings = Path.Combine(dir, "settings.json");
        File.WriteAllText(settings, CoucouOnly);
        var review = Install(settings, coucouEvents: 1);
        Assert.Contains(Lines(review), line => line.StartsWith('-') && line.Contains("coucou-hook"));
        review.SetRemoveCoucou(false);
        Assert.DoesNotContain(Lines(review), line => line.StartsWith('-') && line.Contains("coucou-hook"));
        review.Close();
        Directory.Delete(dir, recursive: true);
    });

    [Fact]
    public void AFileThatIsNotUtf8DisablesConfirmAndWritesNothing() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(Core.Defaults.DefaultsStore.InMemory());
        var dir = TempDir();
        var settings = Path.Combine(dir, "settings.json");
        byte[] bytes = [0x7B, 0x22, 0x61, 0x22, 0x3A, 0x22, 0xFF, 0xFE, 0x22, 0x7D];
        File.WriteAllBytes(settings, bytes);
        var review = Install(settings);
        Assert.False(review.CanConfirm);
        Assert.Contains("aren't plain JSON", AllText((DependencyObject)review.Content));
        Assert.False(review.TryApply());
        Assert.Equal(bytes, File.ReadAllBytes(settings));
        Assert.Empty(Directory.GetFiles(dir, "settings.json.bak-*"));
        var page = new AgentsPage(settings, Path.Combine(dir, "faqra-hook.exe"));
        Assert.Contains("aren't plain JSON", AllText(page));
        Assert.False(ActionButton(page).IsEnabled);
        review.Close();
        Directory.Delete(dir, recursive: true);
    });

    [Fact]
    public void ThePageReflectsInstalledPartialAndMissingRelay() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(Core.Defaults.DefaultsStore.InMemory());
        var dir = TempDir();
        var settings = Path.Combine(dir, "settings.json");
        var relay = Path.Combine(dir, "faqra-hook.exe");
        File.WriteAllText(relay, "stub");

        File.WriteAllText(settings, Core.Agents.Install.ClaudeHookConfig.Install(null, relay, removeCoucou: false));
        var installed = new AgentsPage(settings, relay);
        Assert.Contains("Hooks installed", AllText(installed));
        Assert.Equal("Remove hooks", ActionButton(installed).Content);

        File.WriteAllText(settings, "{\n  \"hooks\": {\n    \"Stop\": [\n      {\n        \"hooks\": [\n          {\n            \"type\": \"command\",\n            \"command\": \"\\\"C:/x/faqra-hook.exe\\\" Stop\"\n          }\n        ]\n      }\n    ]\n  }\n}\n");
        var partial = new AgentsPage(settings, relay);
        Assert.Contains("Hooks need reinstalling", AllText(partial));

        File.Delete(settings);
        File.Delete(relay);
        var missing = new AgentsPage(settings, relay);
        Assert.Equal("Review and install", ActionButton(missing).Content);
        Assert.False(ActionButton(missing).IsEnabled);
        Directory.Delete(dir, recursive: true);
    });

    private static Wpf.Ui.Controls.Button ActionButton(DependencyObject root)
    {
        Wpf.Ui.Controls.Button? found = null;
        void Walk(DependencyObject node)
        {
            if (node is Wpf.Ui.Controls.Button button)
            {
                found = button;
            }
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            {
                Walk(child);
            }
        }
        Walk(root);
        return found ?? throw new InvalidOperationException("no button");
    }

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
            // A CardControl's Header is not a logical child until the template is applied.
            if (node is Wpf.Ui.Controls.CardControl { Header: DependencyObject header })
            {
                Walk(header);
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
