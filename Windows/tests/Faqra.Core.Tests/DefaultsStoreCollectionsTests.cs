using Faqra.Core.Defaults;

namespace Faqra.Core.Tests;

public class DefaultsStoreCollectionsTests
{
    [Fact]
    public void ListsAndMapsRoundTripThroughTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"faqra-store-{Guid.NewGuid():N}", "settings.json");
        try
        {
            using (var store = DefaultsStore.Open(path))
            {
                store.Set("apps", new[] { "a.exe", "b.exe" });
                store.Set("names", new Dictionary<string, string> { ["x"] = "X app", ["y"] = "Y app" });
                store.Set("volumes", new Dictionary<string, double> { ["x"] = 0.5, ["y"] = 1.25 });
                store.Flush();
            }
            using var reloaded = DefaultsStore.Open(path);
            Assert.Equal(["a.exe", "b.exe"], reloaded.StringList("apps"));
            Assert.Equal("Y app", reloaded.StringMap("names")!["y"]);
            Assert.Equal(1.25, reloaded.DoubleMap("volumes")!["y"]);
            Assert.Null(reloaded.StringMap("volumes"));
            Assert.Null(reloaded.StringList("names"));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void EqualCollectionsDoNotRaiseChanged()
    {
        var store = DefaultsStore.InMemory();
        var raised = 0;
        store.Changed += (_, _) => raised++;
        store.Set("apps", new[] { "a" });
        store.Set("apps", new List<string> { "a" });
        store.Set("volumes", new Dictionary<string, double> { ["a"] = 1 });
        store.Set("volumes", new Dictionary<string, double> { ["a"] = 1 });
        Assert.Equal(2, raised);
    }

    [Fact]
    public void RegisteredEmptyListIsReturnedAsAList()
    {
        var store = DefaultsStore.InMemory();
        Assert.Empty(store.StringList(DefaultsKey.KeepAwakeRunningAppBundleIDs)!);
        Assert.Empty(store.Data(DefaultsKey.NotchQuickAccessLayout)!);
    }
}
