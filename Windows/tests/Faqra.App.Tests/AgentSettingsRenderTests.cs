using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using Faqra.App.Settings.Pages;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;

namespace Faqra.App.Tests;

public class AgentSettingsRenderTests
{
    private static readonly AgentsStrings S = AgentsStrings.EnUS;

    private static List<Wpf.Ui.Controls.ToggleSwitch> Toggles(DependencyObject root)
    {
        var found = new List<Wpf.Ui.Controls.ToggleSwitch>();
        void Walk(DependencyObject node)
        {
            if (node is Wpf.Ui.Controls.ToggleSwitch toggle)
            {
                found.Add(toggle);
            }
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            {
                Walk(child);
            }
        }
        Walk(root);
        return found;
    }

    [Fact]
    public void TheAgentsPageHasTheAlertSwitches() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        var dir = Directory.CreateTempSubdirectory("faqra-page-").FullName;
        var page = new AgentsPage(Path.Combine(dir, "settings.json"), Path.Combine(dir, "faqra-hook.exe"));
        AgentsRenderTests.RenderInk(page, "settings-agents-alerts", 840, 900);

        var texts = AgentsRenderTests.AllText(page);
        Assert.Contains(S.AlertsSection, texts);
        var toggles = Toggles(page);
        Assert.Equal(4, toggles.Count);
        Assert.All(toggles, toggle => Assert.True(toggle.IsChecked));

        var sound = toggles.Single(toggle => AutomationProperties.GetName(toggle) == S.NeedsYouSound);
        sound.IsChecked = false;
        sound.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.False(services.Store.Bool(DefaultsKey.FaqraAgentsNeedsYouSound));
        Directory.Delete(dir, recursive: true);
    });

    [Fact]
    public void CoucousHooksMeanFaqraOnlyWatches() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        var dir = Directory.CreateTempSubdirectory("faqra-page-").FullName;
        var settings = Path.Combine(dir, "settings.json");
        File.WriteAllText(settings, "{\n  \"hooks\": {\n    \"Stop\": [\n      {\n        \"hooks\": [\n          {\n            \"type\": \"command\",\n            \"command\": \"\\\"C:/x/coucou-hook.exe\\\" Stop\"\n          }\n        ]\n      }\n    ]\n  }\n}\n");
        var page = new AgentsPage(settings, Path.Combine(dir, "faqra-hook.exe"));
        Assert.Contains(S.WatchOnly, AgentsRenderTests.AllText(page));
        Directory.Delete(dir, recursive: true);
    });

    [Fact]
    public void TheShortcutsPageListsTheFourAgentShortcuts() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        var page = new ShortcutsPage();
        var texts = AgentsRenderTests.AllText(page);
        foreach (var title in new[] { S.ShortcutJump, S.ShortcutWindow, S.ShortcutNext, S.ShortcutPrevious })
        {
            Assert.Contains(title, texts);
        }
    });

    [Fact]
    public void TheReplyWindowIsTheOwnersChoice() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(DefaultsStore.InMemory());
        var dir = Directory.CreateTempSubdirectory("faqra-page-").FullName;
        try
        {
            var page = new AgentsPage(Path.Combine(dir, "settings.json"), Path.Combine(dir, "faqra-hook.exe"));
            Assert.Contains(S.RepliesSection, AgentsRenderTests.AllText(page));
            var box = FindAll<System.Windows.Controls.ComboBox>(page).Single(b => AutomationProperties.GetName(b) == S.ReplyWindow);
            Assert.Equal(["1 minute", "2 minutes", "5 minutes", "8 minutes"], box.Items.Cast<string>());
            Assert.Equal("5 minutes", box.SelectedItem);

            box.SelectedIndex = 0;
            Assert.Equal(60, services.Store.Int(Faqra.Core.Defaults.DefaultsKey.FaqraAgentsReplyWindowSeconds));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    });

    private static List<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        var found = new List<T>();
        void Walk(DependencyObject node)
        {
            if (node is T match)
            {
                found.Add(match);
            }
            // A CardControl's Content is a logical child; its Header is not until the template applies.
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            {
                Walk(child);
            }
        }
        Walk(root);
        return found;
    }
}
