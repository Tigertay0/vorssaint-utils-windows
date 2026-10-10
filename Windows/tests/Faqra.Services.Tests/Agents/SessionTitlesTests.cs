using Faqra.Services.Agents;

namespace Faqra.Services.Tests.Agents;

public class SessionTitlesTests
{
    private static string Root() => Directory.CreateTempSubdirectory("faqra-projects-").FullName;

    private static string Transcript(string root, params string[] lines)
    {
        var folder = Path.Combine(root, "C--code-faqra");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".jsonl");
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void TheOwnersNameBeatsClaudesTitle()
    {
        var root = Root();
        var path = Transcript(root,
            "{\"type\":\"custom-title\",\"customTitle\":\"Instagram transcripts\",\"sessionId\":\"s\"}",
            "{\"type\":\"user\",\"message\":{\"content\":\"hi\"}}",
            "{\"type\":\"ai-title\",\"aiTitle\":\"Extracting a video transcript\",\"sessionId\":\"s\"}");
        Assert.Equal("Instagram transcripts", SessionTitles.Read(path, root));
    }

    [Fact]
    public void TheNewestTitleWins()
    {
        var root = Root();
        var path = Transcript(root,
            "{\"type\":\"ai-title\",\"aiTitle\":\"Faqra milestone 5\",\"sessionId\":\"s\"}",
            "{\"type\":\"ai-title\",\"aiTitle\":\"Faqra milestones 5 and 6\",\"sessionId\":\"s\"}");
        Assert.Equal("Faqra milestones 5 and 6", SessionTitles.Read(path, root));
    }

    [Fact]
    public void ATitleFarBeforeTheTailIsStillFound()
    {
        var root = Root();
        var filler = Enumerable.Range(0, 3000).Select(_ => $"{{\"type\":\"assistant\",\"text\":\"{new string('x', 200)}\"}}");
        var path = Transcript(root, ["{\"type\":\"ai-title\",\"aiTitle\":\"Early title\",\"sessionId\":\"s\"}", .. filler]);
        Assert.Equal("Early title", SessionTitles.Read(path, root));
    }

    [Fact]
    public void ANameIsOneShortLine()
    {
        var root = Root();
        var path = Transcript(root, $"{{\"type\":\"ai-title\",\"aiTitle\":\"Two\\nlines {new string('w', 300)}\"}}");
        var name = SessionTitles.Read(path, root)!;
        Assert.DoesNotContain('\n', name);
        Assert.StartsWith("Two lines", name);
        Assert.True(name.Length <= SessionTitles.MaxTitleLength);
        Assert.EndsWith("…", name);
    }

    [Fact]
    public void NoTitleYetMeansNone()
    {
        var root = Root();
        Assert.Null(SessionTitles.Read(Transcript(root, "{\"type\":\"user\",\"message\":{\"content\":\"hi\"}}"), root));
        Assert.Null(SessionTitles.Read(Transcript(root, "{\"type\":\"ai-title\",\"aiTitle\":\"   \"}"), root));
    }

    [Fact]
    public void OnlyTranscriptsUnderTheProjectsFolderAreRead()
    {
        var root = Root();
        var outside = Path.Combine(Path.GetTempPath(), $"faqra-outside-{Guid.NewGuid():N}.jsonl");
        File.WriteAllText(outside, "{\"type\":\"ai-title\",\"aiTitle\":\"x\"}\n");
        var notJsonl = Path.Combine(root, "a.txt");
        File.WriteAllText(notJsonl, "{\"type\":\"ai-title\",\"aiTitle\":\"x\"}\n");

        Assert.Null(SessionTitles.Read(outside, root));
        Assert.Null(SessionTitles.Read(Path.Combine(root, "..", Path.GetFileName(outside)), root));
        Assert.Null(SessionTitles.Read(notJsonl, root));
        Assert.Null(SessionTitles.Read(null, root));
        Assert.Null(SessionTitles.Read(Path.Combine(root, "missing.jsonl"), root));
        File.Delete(outside);
    }

    [Fact]
    public void ATranscriptClaudeIsWritingCanBeRead()
    {
        var root = Root();
        var path = Transcript(root, "{\"type\":\"ai-title\",\"aiTitle\":\"Busy\"}");
        // Claude Code keeps its transcript open while it appends.
        using var writer = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        Assert.Equal("Busy", SessionTitles.Read(path, root));
    }
}
