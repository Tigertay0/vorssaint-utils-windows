using Faqra.Core.Defaults;

namespace Faqra.Core.Tests;

public class DefaultsStoreTests
{
    private static readonly IReadOnlyDictionary<string, object> Registered = new Dictionary<string, object>
    {
        ["flag"] = true,
        ["count"] = 3L,
        ["ratio"] = 0.5,
        ["name"] = "default",
    };

    [Fact]
    public void GettersFallThroughToRegisteredDefaultsThenZero()
    {
        var store = DefaultsStore.InMemory(Registered);

        Assert.True(store.Bool("flag"));
        Assert.Equal(3, store.Int("count"));
        Assert.Equal(0.5, store.Double("ratio"));
        Assert.Equal("default", store.String("name"));
        Assert.False(store.Bool("missing"));
        Assert.Equal(0, store.Int("missing"));
        Assert.Null(store.String("missing"));
        Assert.Null(store.Data("missing"));
        Assert.False(store.Contains("flag"));
    }

    [Fact]
    public void SetOverridesAndRemoveRestoresTheDefault()
    {
        var store = DefaultsStore.InMemory(Registered);

        store.Set("flag", false);
        store.Set("count", 9);
        store.Set("name", "custom");
        store.Set("blob", new byte[] { 1, 2, 3 });

        Assert.False(store.Bool("flag"));
        Assert.Equal(9, store.Int("count"));
        Assert.Equal("custom", store.String("name"));
        Assert.Equal(new byte[] { 1, 2, 3 }, store.Data("blob"));
        Assert.True(store.Contains("flag"));

        store.Remove("flag");
        Assert.True(store.Bool("flag"));
        Assert.False(store.Contains("flag"));
    }

    [Fact]
    public void ChangedFiresOncePerEffectiveChange()
    {
        var store = DefaultsStore.InMemory();
        var keys = new List<string>();
        store.Changed += (_, e) => keys.Add(e.Key);

        store.Set("a", 1);
        store.Set("a", 1); // no-op
        store.Set("a", 2);
        store.Set("b", "x");
        store.Remove("missing"); // no-op
        store.Remove("b");

        Assert.Equal(["a", "a", "b", "b"], keys);
    }

    [Fact]
    public void SettingNullRemovesTheKey()
    {
        var store = DefaultsStore.InMemory(Registered);
        store.Set("name", "custom");
        store.Set("name", (string?)null);
        Assert.Equal("default", store.String("name"));
    }

    [Fact]
    public void PersistsAndReloadsEveryValueType()
    {
        var path = Path.Combine(Path.GetTempPath(), $"faqra-store-{Guid.NewGuid():N}", "settings.json");
        try
        {
            using (var store = DefaultsStore.Open(path, Registered))
            {
                store.Set("flag", false);
                store.Set("count", 42);
                store.Set("ratio", 1.25);
                store.Set("name", "Faqra");
                store.Set("blob", new byte[] { 9, 8, 7 });
                store.Flush();
            }
            using var reloaded = DefaultsStore.Open(path, Registered);
            Assert.False(reloaded.Bool("flag"));
            Assert.Equal(42, reloaded.Int("count"));
            Assert.Equal(1.25, reloaded.Double("ratio"));
            Assert.Equal("Faqra", reloaded.String("name"));
            Assert.Equal(new byte[] { 9, 8, 7 }, reloaded.Data("blob"));
            Assert.Equal(["blob", "count", "flag", "name", "ratio"], reloaded.Keys.Order(StringComparer.Ordinal));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void SecondSaveKeepsABackupOfThePreviousFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"faqra-store-{Guid.NewGuid():N}", "settings.json");
        try
        {
            using var store = DefaultsStore.Open(path);
            store.Set("a", 1);
            store.Flush();
            store.Set("a", 2);
            store.Flush();
            Assert.True(File.Exists(path + ".bak"));
            Assert.False(File.Exists(path + ".tmp"));
            Assert.Contains("\"a\": 1", File.ReadAllText(path + ".bak"));
            Assert.Contains("\"a\": 2", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void CorruptFileIsSetAsideAndTheStoreStartsFresh()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"faqra-store-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, "{ not json");
        try
        {
            using var store = DefaultsStore.Open(path, Registered);
            Assert.True(store.Bool("flag"));
            Assert.Empty(store.Keys);
            Assert.False(File.Exists(path));
            Assert.Single(Directory.GetFiles(directory, "settings.corrupt-*.json"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
