using System.Text;
using Faqra.Core.Agents.Install;
using Faqra.Services.Agents;

namespace Faqra.Services.Tests.Agents;

public sealed class ConfigFileTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 9, 17, 30, 5);
    private readonly string _dir = Directory.CreateTempSubdirectory("faqra-config-").FullName;

    private string SettingsPath => Path.Combine(_dir, "settings.json");

    private static string AddKey(string? json) => (json is null ? "{\n" : json.TrimEnd().TrimEnd('}')) + "  ,\"faqra\": 1\n}\n";

    [Fact]
    public void PreviewShowsTheEditAndWritesNothing()
    {
        File.WriteAllText(SettingsPath, "{\n  \"a\": 1\n}\n");
        var preview = ConfigFile.Preview(SettingsPath, AddKey, Now);
        Assert.True(preview.Changes);
        Assert.NotEmpty(preview.Diff);
        Assert.Equal(SettingsPath + ".bak-20261009-173005", preview.BackupPath);
        Assert.Equal("{\n  \"a\": 1\n}\n", File.ReadAllText(SettingsPath));
        Assert.False(File.Exists(preview.BackupPath));
    }

    [Fact]
    public void ApplyKeepsAByteExactBackup()
    {
        var original = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("{\r\n  \"a\": 1\r\n}\r\n")).ToArray();
        File.WriteAllBytes(SettingsPath, original);
        var preview = ConfigFile.Preview(SettingsPath, AddKey, Now);
        ConfigFile.Apply(preview);
        Assert.Equal(original, File.ReadAllBytes(preview.BackupPath!));
        Assert.Equal(preview.After, File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void ApplyRefusesAFileThatChangedSinceThePreview()
    {
        File.WriteAllText(SettingsPath, "{\n  \"a\": 1\n}\n");
        var preview = ConfigFile.Preview(SettingsPath, AddKey, Now);
        File.WriteAllText(SettingsPath, "{\n  \"a\": 2\n}\n");
        Assert.Throws<ConfigChangedException>(() => ConfigFile.Apply(preview));
        Assert.Equal("{\n  \"a\": 2\n}\n", File.ReadAllText(SettingsPath));
        Assert.False(File.Exists(preview.BackupPath));
    }

    [Fact]
    public void ApplyCreatesAMissingFileWithoutABackup()
    {
        var path = Path.Combine(_dir, "new", "settings.json");
        var preview = ConfigFile.Preview(path, AddKey, Now);
        Assert.Null(preview.BackupPath);
        ConfigFile.Apply(preview);
        Assert.Equal(preview.After, File.ReadAllText(path));
    }

    [Fact]
    public void PreviewPassesAFormatProblemThrough()
    {
        File.WriteAllText(SettingsPath, "{ // comment\n}");
        Assert.Throws<ConfigFormatException>(() => ConfigFile.Preview(SettingsPath, json => ClaudeHookConfig.Install(json, "x", false), Now));
    }

    [Fact]
    public void ApplyNeverOverwritesAnExistingBackup()
    {
        File.WriteAllText(SettingsPath, "{\n  \"a\": 1\n}\n");
        var preview = ConfigFile.Preview(SettingsPath, AddKey, Now);
        File.WriteAllText(preview.BackupPath!, "earlier backup");

        var used = ConfigFile.Apply(preview);

        Assert.NotNull(used);
        Assert.NotEqual(preview.BackupPath, used);
        Assert.Equal("earlier backup", File.ReadAllText(preview.BackupPath!));
        Assert.Equal("{\n  \"a\": 1\n}\n", File.ReadAllText(used!));
        Assert.Equal(preview.After, File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void AFailedApplyLeavesNoTempFileBehind()
    {
        File.WriteAllText(SettingsPath, "{\n  \"a\": 1\n}\n");
        var preview = ConfigFile.Preview(SettingsPath, AddKey, Now);
        File.SetAttributes(SettingsPath, FileAttributes.ReadOnly);
        try
        {
            Assert.ThrowsAny<Exception>(() => ConfigFile.Apply(preview));
        }
        finally
        {
            File.SetAttributes(SettingsPath, FileAttributes.Normal);
        }
        Assert.False(File.Exists(SettingsPath + ".faqra-tmp"));
        Assert.Equal("{\n  \"a\": 1\n}\n", File.ReadAllText(SettingsPath));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
