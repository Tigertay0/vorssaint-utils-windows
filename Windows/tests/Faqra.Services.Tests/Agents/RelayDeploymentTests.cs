using Faqra.Services.Agents;

namespace Faqra.Services.Tests.Agents;

public sealed class RelayDeploymentTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("faqra-relay-").FullName;

    private string Source => Path.Combine(_dir, "app");

    private string Target => Path.Combine(_dir, "bin", RelayDeployment.FileName);

    private void Ship(string content)
    {
        Directory.CreateDirectory(Source);
        File.WriteAllText(Path.Combine(Source, RelayDeployment.FileName), content);
    }

    [Fact]
    public void CopiesTheRelayBesideTheApp()
    {
        Ship("v1");
        Assert.True(RelayDeployment.Ensure(Source, Target));
        Assert.Equal("v1", File.ReadAllText(Target));
    }

    [Fact]
    public void ReplacesAnOlderRelayAndKeepsAnIdenticalOne()
    {
        Ship("v1");
        RelayDeployment.Ensure(Source, Target);
        var stamp = File.GetLastWriteTimeUtc(Target);
        Assert.True(RelayDeployment.Ensure(Source, Target));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(Target));
        Ship("v2");
        Assert.True(RelayDeployment.Ensure(Source, Target));
        Assert.Equal("v2", File.ReadAllText(Target));
    }

    [Fact]
    public void WithoutASourceItReportsWhetherOneIsInPlace()
    {
        Assert.False(RelayDeployment.Ensure(Source, Target));
        Directory.CreateDirectory(Path.GetDirectoryName(Target)!);
        File.WriteAllText(Target, "old");
        Assert.True(RelayDeployment.Ensure(Source, Target));
    }

    [Fact]
    public void AFailedPlacementPutsTheOldRelayBack()
    {
        Ship("v1");
        RelayDeployment.Ensure(Source, Target);
        Ship("v2");

        var ok = RelayDeployment.Ensure(Source, Target, (_, _) => throw new IOException("blocked"));

        Assert.True(ok);
        Assert.Equal("v1", File.ReadAllText(Target));
    }

    [Fact]
    public void RemovesRelaysSetAsideByEarlierUpdates()
    {
        Ship("v1");
        RelayDeployment.Ensure(Source, Target);
        var stale = Target + ".old-20200101000000000";
        File.WriteAllText(stale, "ancient");
        Ship("v2");

        Assert.True(RelayDeployment.Ensure(Source, Target));

        Assert.Equal("v2", File.ReadAllText(Target));
        Assert.False(File.Exists(stale));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
