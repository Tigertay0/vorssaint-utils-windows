using System.IO;
using Faqra.App.Agents;
using Faqra.Core.Agents.Install;

namespace Faqra.App.Tests;

public class ClaudeHooksTests
{
    [Fact]
    public void ReadsWhatIsInstalledAndShrugsAtWhatCannotBeRead()
    {
        var dir = Directory.CreateTempSubdirectory("faqra-hooks-").FullName;
        var settings = Path.Combine(dir, "settings.json");

        Assert.Equal(new HookStatus(0, 0), ClaudeHooks.Read(settings));

        File.WriteAllText(settings, ClaudeHookConfig.Install(null, @"C:\x\faqra-hook.exe", removeCoucou: false));
        Assert.True(ClaudeHooks.Read(settings).Installed);

        File.WriteAllText(settings, "{\"hooks\":{\"Stop\":[{\"hooks\":[{\"type\":\"command\",\"command\":\"C:/x/coucou-hook.exe Stop\"}]}]}}");
        Assert.Equal(1, ClaudeHooks.Read(settings).CoucouEvents);

        File.WriteAllBytes(settings, [0x7B, 0xFF, 0xFE, 0x7D]);
        Assert.Equal(new HookStatus(0, 0), ClaudeHooks.Read(settings));
        Directory.Delete(dir, recursive: true);
    }
}
