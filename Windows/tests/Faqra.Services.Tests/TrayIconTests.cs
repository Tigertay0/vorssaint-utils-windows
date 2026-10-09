using Faqra.Win32.Native;
using Faqra.Win32.Tray;

namespace Faqra.Services.Tests;

// The shell refuses tray calls for a few seconds after a display wake, during sign-in and while
// Explorer restarts. Faqra died twice in September when a metric icon's redraw hit that window.
public class TrayIconTests
{
    private static readonly IntPtr Owner = new(0x1234);
    private static readonly IntPtr Glyph = new(0x5678);
    private static readonly Guid IconGuid = new("6f1c3c0e-3b1a-4b62-9a7e-3f2a1f5d9c10");

    [Fact]
    public void ARefusedUpdateDoesNotThrow()
    {
        var shell = new FakeShell();
        var icon = new TrayIcon(Owner, 10, IconGuid, shell);
        icon.Update(Glyph, "CPU 12%");

        shell.RefuseAll = true;
        var error = Record.Exception(() => icon.Update(Glyph, "CPU 13%"));

        Assert.Null(error);
    }

    [Fact]
    public void ARefusedUpdateReportsThatTheIconIsNotShowing()
    {
        var shell = new FakeShell();
        var icon = new TrayIcon(Owner, 10, IconGuid, shell);
        Assert.True(icon.Update(Glyph, "CPU 12%"));

        shell.RefuseAll = true;

        Assert.False(icon.Update(Glyph, "CPU 13%"));
        Assert.False(icon.IsAdded);
    }

    [Fact]
    public void AfterARefusalTheNextUpdateReusesTheIconThatSurvived()
    {
        var shell = new FakeShell();
        var icon = new TrayIcon(Owner, 10, IconGuid, shell);
        icon.Update(Glyph, "CPU 12%");
        shell.RefuseAll = true;
        icon.Update(Glyph, "CPU 13%");

        shell.RefuseAll = false;

        Assert.True(icon.Update(Glyph, "CPU 14%"));
        Assert.True(icon.IsAdded);
        Assert.Equal([$"guid:{IconGuid}"], shell.Icons);
    }

    [Fact]
    public void NothingIsSentWhileTheTaskbarIsNotAnswering()
    {
        // A call to a hung taskbar can be queued and run later, so a fallback sent now could add a
        // second icon once Explorer catches up.
        var shell = new FakeShell { Responsive = false };
        var icon = new TrayIcon(Owner, 10, IconGuid, shell);

        Assert.False(icon.Update(Glyph, "CPU 12%"));
        Assert.Empty(shell.Calls);
    }

    [Fact]
    public void TheIconAppearsOnceTheTaskbarAnswersAgain()
    {
        var shell = new FakeShell { Responsive = false };
        var icon = new TrayIcon(Owner, 10, IconGuid, shell);
        icon.Update(Glyph, "CPU 12%");

        shell.Responsive = true;

        Assert.True(icon.Update(Glyph, "CPU 12%"));
        Assert.Equal([$"guid:{IconGuid}"], shell.Icons);
    }

    [Fact]
    public void FallsBackToTheWindowIdentityWhenTheShellBoundTheGuidToAnotherPath()
    {
        var shell = new FakeShell { GuidOwnedElsewhere = true };
        var icon = new TrayIcon(Owner, 10, IconGuid, shell);

        Assert.True(icon.Update(Glyph, "CPU 12%"));
        Assert.Equal([$"window:{Owner}:10"], shell.Icons);

        icon.Remove();
        Assert.Empty(shell.Icons);
    }

    [Fact]
    public void AfterExplorerRestartsTheIconIsAddedAgain()
    {
        var shell = new FakeShell();
        var icon = new TrayIcon(Owner, 10, IconGuid, shell);
        icon.Update(Glyph, "CPU 12%");

        shell.Icons.Clear();
        icon.MarkRemoved();

        Assert.True(icon.Update(Glyph, "CPU 12%"));
        Assert.Equal([$"guid:{IconGuid}"], shell.Icons);
    }

    /// <summary>
    /// Plays the notification area: icons keyed by GUID (or window+id without one), a taskbar that can
    /// stop answering, and GUIDs the shell has bound to another executable path.
    /// </summary>
    internal sealed class FakeShell : INotifyIconShell
    {
        public bool RefuseAll { get; set; }
        public bool Responsive { get; set; } = true;
        public bool GuidOwnedElsewhere { get; set; }
        public HashSet<string> Icons { get; } = [];
        public List<uint> Calls { get; } = [];

        public bool IsTaskbarResponsive() => Responsive;

        public bool NotifyIcon(uint message, ref NOTIFYICONDATAW data)
        {
            Calls.Add(message);
            var usesGuid = (data.uFlags & Shell32.NIF_GUID) != 0;
            if (RefuseAll || !Responsive || (usesGuid && GuidOwnedElsewhere))
            {
                return false;
            }
            var key = usesGuid ? $"guid:{data.guidItem}" : $"window:{data.hWnd}:{data.uID}";
            return message switch
            {
                Shell32.NIM_ADD => Icons.Add(key),
                Shell32.NIM_MODIFY or Shell32.NIM_SETVERSION => Icons.Contains(key),
                Shell32.NIM_DELETE => Icons.Remove(key),
                _ => false,
            };
        }
    }
}
