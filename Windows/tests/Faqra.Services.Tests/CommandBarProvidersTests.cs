using Faqra.Win32.Shell;
using Faqra.Win32.Windows;

namespace Faqra.Services.Tests;

// Windows-only smoke tests: the shell namespace and window list the command bar reads.
public class CommandBarProvidersTests
{
    [Fact]
    public void AppsFolderListsLaunchableApps()
    {
        var apps = AppsFolder.Enumerate();
        Assert.NotEmpty(apps);
        Assert.All(apps, app =>
        {
            Assert.False(string.IsNullOrWhiteSpace(app.Name));
            Assert.False(string.IsNullOrWhiteSpace(app.ParsingName));
        });
    }

    [Fact]
    public void OpenWindowsExcludesThisProcess()
    {
        var windows = OpenWindows.Enumerate();
        Assert.DoesNotContain(windows, w => w.ProcessId == Environment.ProcessId);
        Assert.All(windows, w => Assert.False(string.IsNullOrWhiteSpace(w.Title)));
    }
}
