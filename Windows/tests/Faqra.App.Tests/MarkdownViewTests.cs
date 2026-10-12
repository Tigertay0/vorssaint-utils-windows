using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Faqra.App.Island.Modules;

namespace Faqra.App.Tests;

public class MarkdownViewTests
{
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

    [Fact]
    public void ClaudesMarkdownBecomesStyledText() => StaThread.Run(() =>
    {
        var view = MarkdownView.Build("# Fixed\n\nRun **all** tests with `dotnet test`.\n\n- one\n  - two\n\n```\nx = 1\n```");
        AgentsRenderTests.RenderInk(new Border { Background = IslandPalette.Surface, Child = view, Width = 380 }, "island-markdown", 380, 220);

        var blocks = All<TextBlock>(view).ToList();
        Assert.Equal(["Fixed", "Run all tests with dotnet test.", "•", "one", "•", "two", "x = 1"], blocks.Select(AgentsRenderTests.Words));
        Assert.Equal(FontWeights.SemiBold, blocks[0].FontWeight);
        var runs = All<Run>(view).ToList();
        Assert.Equal(FontWeights.SemiBold, runs.Single(run => run.Text == "all").FontWeight);
        Assert.Equal(AgentCardParts.MonoFont, runs.Single(run => run.Text == "dotnet test").FontFamily);
        Assert.Equal(AgentCardParts.MonoFont, blocks[^1].FontFamily);
        var items = All<Grid>(view).ToList();
        Assert.True(items[1].Margin.Left > items[0].Margin.Left, "the nested item is indented");
    });

    [Fact]
    public void OnlyWebLinksCanBeClicked() => StaThread.Run(() =>
    {
        var view = MarkdownView.Build("See [the docs](https://example.com/a) or [this](file:///C:/Windows/notepad.exe).");
        var link = Assert.Single(All<Hyperlink>(view));
        Assert.Equal(new Uri("https://example.com/a"), link.NavigateUri);
        Assert.Equal("See the docs or this.", AgentsRenderTests.Words(All<TextBlock>(view).Single()));
        Assert.Null(MarkdownView.WebLink("javascript:alert(1)"));
        Assert.Null(MarkdownView.WebLink("C:\\Windows\\notepad.exe"));
        Assert.NotNull(MarkdownView.WebLink("http://localhost:3000/"));
    });

    [Fact]
    public void NothingIsAnEmptyView() => StaThread.Run(() =>
    {
        Assert.Empty(MarkdownView.Build(null).Children);
        Assert.Empty(MarkdownView.Build("  \n").Children);
    });
}
