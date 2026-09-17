using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Faqra.App.Settings;
using Faqra.App.Settings.Pages;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;
using Faqra.Core.Shortcuts;

namespace Faqra.App.Tests;

/// <summary>The shortcut recorder on the Shortcuts and Energy pages, rendered offscreen to PNG.</summary>
public class ShortcutRenderTests
{
    private static readonly string OutputDirectory =
        Environment.GetEnvironmentVariable("FAQRA_UI_SHOTS")
        ?? Path.Combine(Path.GetTempPath(), "faqra-ui");

    [Fact]
    public void ShortcutsPageListsInstalledRoles()
    {
        StaThread.Run(() =>
        {
            using var services = AppServices.StartWith(DefaultsStore.InMemory());
            var page = new ShortcutsPage();
            var texts = Texts(page);
            Assert.Contains(ShortcutStrings.EnUS.PageCaption, texts);
            var recorders = FindAll<ShortcutRecorder>(page).ToList();
            Assert.NotEmpty(recorders);
            var keyCaps = FindAll<Wpf.Ui.Controls.Button>(page).Select(b => b.Content as string).ToList();
            Assert.Contains(GlobalShortcut.KeepAwakeDefault.DisplayText, keyCaps);
            Assert.Contains(ShortcutStrings.EnUS.Reset, keyCaps);
            SaveElement(page, "settings-shortcuts", 840);
        });
    }

    [Fact]
    public void EnergyPageRecorderShowsTheStoredShortcut()
    {
        StaThread.Run(() =>
        {
            var store = DefaultsStore.InMemory();
            store.Set(DefaultsKey.KeepAwakeShortcut, "control+shift:74");
            using var services = AppServices.StartWith(store);
            var page = new EnergyPage();
            Texts(page);
            var keyCaps = FindAll<Wpf.Ui.Controls.Button>(page.Recorder).ToList();
            Assert.Equal("Ctrl+Shift+J", keyCaps[0].Content);
            Assert.True(keyCaps[1].IsEnabled); // Reset, since this is not the default
            SaveElement(page.Recorder, "settings-shortcut-recorder", 360);
        });
    }

    private static List<string> Texts(object root)
    {
        var element = (FrameworkElement)root;
        element.Measure(new Size(560, double.PositiveInfinity));
        element.Arrange(new Rect(element.DesiredSize));
        element.UpdateLayout();
        return FindAll<TextBlock>(element).Select(t => t.Text).ToList();
    }

    private static IEnumerable<T> FindAll<T>(object root) where T : DependencyObject
    {
        if (root is not DependencyObject node)
        {
            yield break;
        }
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<object>())
        {
            if (child is T match)
            {
                yield return match;
            }
            foreach (var nested in FindAll<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static void SaveElement(FrameworkElement element, string name, double width)
    {
        if (element.Parent is Decorator decorator)
        {
            decorator.Child = null;
        }
        else if (element.Parent is System.Windows.Controls.Panel panel)
        {
            panel.Children.Remove(element);
        }
        else if (element.Parent is ContentControl content)
        {
            content.Content = null;
        }
        var host = new Border { Width = width, Padding = new Thickness(12), Child = element };
        host.Background = (Brush)Application.Current.Resources["ApplicationBackgroundBrush"];
        host.Measure(new Size(width, double.PositiveInfinity));
        host.Arrange(new Rect(0, 0, width, host.DesiredSize.Height));
        host.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)width, (int)Math.Max(host.ActualHeight, 1), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        Directory.CreateDirectory(OutputDirectory);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(OutputDirectory, $"{name}.png"));
        encoder.Save(stream);
        host.Child = null;
    }
}
