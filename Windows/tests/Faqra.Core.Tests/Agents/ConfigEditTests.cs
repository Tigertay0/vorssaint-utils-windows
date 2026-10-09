using System.Text;
using Faqra.Core.Agents.Install;

namespace Faqra.Core.Tests.Agents;

public class ConfigEditTests
{
    private const string Path = @"C:\Users\me\.claude\settings.json";
    private static readonly DateTime Now = new(2026, 10, 9, 17, 30, 5);

    [Fact]
    public void BackupUsesTheTimestampWhenFree() =>
        Assert.Equal(Path + ".bak-20261009-173005", ConfigEdit.BackupPath(Path, Now, _ => false));

    [Fact]
    public void BackupAddsASuffixWhileTheNameIsTaken()
    {
        var taken = new HashSet<string> { Path + ".bak-20261009-173005" };
        Assert.Equal(Path + ".bak-20261009-173005-1", ConfigEdit.BackupPath(Path, Now, taken.Contains));
        taken.Add(Path + ".bak-20261009-173005-1");
        Assert.Equal(Path + ".bak-20261009-173005-2", ConfigEdit.BackupPath(Path, Now, taken.Contains));
    }

    [Fact]
    public void FingerprintIsStableAndSensitive()
    {
        var a = Encoding.UTF8.GetBytes("{\"a\":1}");
        var b = Encoding.UTF8.GetBytes("{\"a\":2}");
        Assert.Equal(ConfigEdit.Fingerprint(a), ConfigEdit.Fingerprint((byte[])a.Clone()));
        Assert.NotEqual(ConfigEdit.Fingerprint(a), ConfigEdit.Fingerprint(b));
    }

    [Fact]
    public void DecodeStripsAByteOrderMark()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, (byte)'{', (byte)'}'];
        Assert.Equal("{}", ConfigEdit.Decode(bytes));
    }

    [Fact]
    public void DecodeRefusesInvalidUtf8() =>
        Assert.Throws<ConfigFormatException>(() => ConfigEdit.Decode([0xC3, 0x28]));
}
