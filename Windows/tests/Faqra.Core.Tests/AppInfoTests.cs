using Faqra.Core;

namespace Faqra.Core.Tests;

public class AppInfoTests
{
    [Theory]
    [InlineData("1.0.0", false)]
    [InlineData("1.0.0-beta.1", true)]
    [InlineData("1.0.0-RC.2", true)]
    [InlineData("0.1.0-alpha.1", true)]
    public void IsPreRelease_DetectsSuffixes(string version, bool expected) =>
        Assert.Equal(expected, AppInfo.IsPreRelease(version));

    [Fact]
    public void NameIsFaqraNotTheUpstreamTrademark()
    {
        Assert.Equal("Faqra", AppInfo.Name);
        Assert.DoesNotContain("vorssaint", AppInfo.Name, StringComparison.OrdinalIgnoreCase);
    }
}
