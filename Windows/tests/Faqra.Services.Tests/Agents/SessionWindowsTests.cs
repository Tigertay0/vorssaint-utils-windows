using System.Diagnostics;
using Faqra.Services.Agents;
using Faqra.Win32.Agents;

namespace Faqra.Services.Tests.Agents;

public class SessionWindowsTests
{
    [Fact]
    public void TheSnapshotHoldsThisProcessAndItsParent()
    {
        var self = ProcessSnapshot.Take().Single(process => process.Id == Environment.ProcessId);
        Assert.Equal(Process.GetCurrentProcess().ProcessName + ".exe", self.Executable, ignoreCase: true);
        Assert.NotEqual(0, self.ParentId);
    }

    [Fact]
    public void LookingFromThisProcessSettles() => Assert.True(SessionWindows.Locate(Environment.ProcessId).Settled);

    [Fact]
    public void AProcessThatIsGoneIsNotSettled() => Assert.False(SessionWindows.Locate(int.MaxValue - 1).Settled);
}
