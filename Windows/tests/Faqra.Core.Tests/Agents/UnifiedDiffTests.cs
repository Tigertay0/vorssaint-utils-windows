using Faqra.Core.Agents.Install;
using static Faqra.Core.Agents.Install.UnifiedDiff;

namespace Faqra.Core.Tests.Agents;

public class UnifiedDiffTests
{
    private static string Text(params string[] lines) => string.Join("\n", lines) + "\n";

    [Fact]
    public void SameTextHasNoDiff() => Assert.Empty(Lines(Text("a", "b"), Text("a", "b")));

    [Fact]
    public void AChangeShowsWithThreeLinesAround()
    {
        var before = Text("l1", "l2", "l3", "l4", "l5", "l6", "l7", "l8", "l9", "l10");
        var after = Text("l1", "l2", "l3", "l4", "L5", "l6", "l7", "l8", "l9", "l10");
        Assert.Equal(
        [
            new(LineKind.Hunk, "@@ -2,7 +2,7 @@"),
            new(LineKind.Context, "l2"), new(LineKind.Context, "l3"), new(LineKind.Context, "l4"),
            new(LineKind.Removed, "l5"), new(LineKind.Added, "L5"),
            new(LineKind.Context, "l6"), new(LineKind.Context, "l7"), new(LineKind.Context, "l8"),
        ], Lines(before, after));
    }

    [Fact]
    public void ANewFileIsAllAdded()
    {
        Assert.Equal([new(LineKind.Hunk, "@@ -0,0 +1,2 @@"), new(LineKind.Added, "{"), new(LineKind.Added, "}")], Lines(string.Empty, Text("{", "}")));
    }

    [Fact]
    public void FarApartChangesMakeTwoHunks()
    {
        var before = Text(Enumerable.Range(1, 20).Select(i => $"l{i}").ToArray());
        var after = Text(Enumerable.Range(1, 20).Select(i => i is 2 or 19 ? $"L{i}" : $"l{i}").ToArray());
        Assert.Equal(2, Lines(before, after).Count(line => line.Kind == LineKind.Hunk));
    }

    [Fact]
    public void LineEndingsDoNotCountAsChanges() => Assert.Empty(Lines("a\r\nb\r\n", "a\nb\n"));
}
