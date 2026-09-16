using Faqra.Core.Startup;

namespace Faqra.Core.Tests;

public class LaunchAtLoginSupportTests
{
    [Theory]
    [InlineData(true, LaunchRegistration.Enabled, false, StartupAction.None)]
    [InlineData(false, LaunchRegistration.Enabled, true, StartupAction.AdoptEnabled)]
    [InlineData(true, LaunchRegistration.NeedsApproval, false, StartupAction.None)]
    [InlineData(false, LaunchRegistration.NeedsApproval, false, StartupAction.None)]
    [InlineData(true, LaunchRegistration.Off, false, StartupAction.Register)]
    [InlineData(true, LaunchRegistration.Off, true, StartupAction.None)]
    [InlineData(false, LaunchRegistration.Off, false, StartupAction.None)]
    public void Decide_MatchesUpstreamTable(bool wanted, LaunchRegistration registration, bool unstable, StartupAction expected) =>
        Assert.Equal(expected, LaunchAtLoginSupport.Decide(wanted, registration, unstable));
}
