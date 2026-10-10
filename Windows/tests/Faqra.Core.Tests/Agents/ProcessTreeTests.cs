using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

// Ported from the tests in Coucou's windows/src-tauri/src/session_window.rs.
public class ProcessTreeTests
{
    private static Dictionary<int, ProcessEntry> Tree(params (int Id, int Parent, string Exe)[] entries) =>
        entries.ToDictionary(entry => entry.Id, entry => new ProcessEntry(entry.Parent, entry.Exe));

    [Fact]
    public void WindowsTerminalIsFoundAboveTheShell()
    {
        var procs = Tree((4, 0, "System"), (100, 4, "explorer.exe"), (200, 100, "WindowsTerminal.exe"), (300, 200, "pwsh.exe"), (400, 300, "claude.exe"), (600, 400, "faqra-hook.exe"));
        var ancestors = ProcessTree.Ancestors(procs, 600);
        Assert.Equal([400, 300, 200], ancestors);
        Assert.Equal(200, ProcessTree.FirstOwner(ancestors, pid => pid is 200 or 100));
    }

    [Fact]
    public void AntigravityIsFoundThroughItsExtensionHost()
    {
        var procs = Tree((100, 1, "explorer.exe"), (210, 100, "Antigravity.exe"), (220, 210, "Antigravity.exe"), (400, 220, "claude.exe"), (600, 400, "faqra-hook.exe"));
        Assert.Equal(210, ProcessTree.FirstOwner(ProcessTree.Ancestors(procs, 600), pid => pid == 210));
    }

    [Fact]
    public void AClassicConsoleFindsNothingRatherThanTheDesktop()
    {
        // cmd.exe's window belongs to conhost, which is no ancestor: the walk stops at explorer instead of the taskbar.
        var procs = Tree((100, 1, "explorer.exe"), (300, 100, "cmd.exe"), (400, 300, "claude.exe"), (600, 400, "faqra-hook.exe"));
        Assert.Null(ProcessTree.FirstOwner(ProcessTree.Ancestors(procs, 600), pid => pid == 100));
    }

    [Fact]
    public void AGoneRelayALoopOrADeepChainEndsTheWalk()
    {
        Assert.Empty(ProcessTree.Ancestors(Tree(), 600));
        Assert.Equal([1, 2], ProcessTree.Ancestors(Tree((1, 2, "a.exe"), (2, 1, "b.exe"), (3, 1, "faqra-hook.exe")), 3));
        var deep = Enumerable.Range(1, 99).Select(i => (i, i + 1, "x.exe")).ToArray();
        Assert.Equal(ProcessTree.MaxDepth, ProcessTree.Ancestors(Tree(deep), 1).Count);
    }

    [Fact]
    public void TreeTopsMatchWhateverTheCase() =>
        Assert.Empty(ProcessTree.Ancestors(Tree((100, 1, "EXPLORER.EXE"), (600, 100, "faqra-hook.exe")), 600));

    [Fact]
    public void TheWindowNamedAfterTheProjectWins()
    {
        string[] titles = ["notes - Visual Studio Code", "app.ts - Faqra - Antigravity"];
        Assert.Equal(1, ProcessTree.PickWindow(titles, "faqra"));
        Assert.Equal(0, ProcessTree.PickWindow(titles, "other"));
        Assert.Equal(0, ProcessTree.PickWindow(titles, ""));
        Assert.Equal(-1, ProcessTree.PickWindow([], "x"));
    }
}
