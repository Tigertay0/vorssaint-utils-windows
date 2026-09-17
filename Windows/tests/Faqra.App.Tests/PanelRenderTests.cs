using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Faqra.App.Panel;
using Faqra.App.Panel.Sections;
using Faqra.Core.Defaults;
using Faqra.Core.Localization;
using Faqra.Core.Metrics;
using Faqra.Core.Panel;

namespace Faqra.App.Tests;

/// <summary>
/// The popover panel and its four sections against a sample snapshot. Placement and dismissal are
/// verified on the running app; these tests guard what each section shows and write a PNG of it.
/// </summary>
public class PanelRenderTests
{
    private static readonly string OutputDirectory =
        Environment.GetEnvironmentVariable("FAQRA_UI_SHOTS")
        ?? Path.Combine(Path.GetTempPath(), "faqra-ui");

    private static readonly MonitorStrings S = MonitorStrings.EnUS;

    /// <summary>Sample readings shaped like the development PC's (34 GB of RAM, a 90% full system drive).</summary>
    private static SystemSnapshot Sample(PowerReading? power = null)
    {
        IReadOnlyList<double> Wave(double baseline, double swing, double phase) =>
            Enumerable.Range(0, 60).Select(i => Math.Max(0, baseline + swing * Math.Sin(i / 6.0 + phase))).ToList();

        return SystemSnapshot.Empty with
        {
            CpuUsage = 0.23,
            GpuUsage = 0.64,
            MemoryUsed = 24_800_000_000,
            MemoryAppUsed = 24_800_000_000,
            MemoryTotal = 34_150_000_000,
            MemoryCompressed = 2_450_000_000,
            MemoryCached = 9_600_000_000,
            MemorySwapUsed = 270_000_000,
            MemoryPressure = MemoryPressure.Normal,
            NetDownBytesPerSec = 1_250_000,
            NetUpBytesPerSec = 36_000,
            NetTotalDown = 3_400_000_000,
            NetTotalUp = 210_000_000,
            UptimeSeconds = 151_200,
            Disks =
            [
                new DiskDeviceReading { Id = @"C:\", Name = "C:", FileSystem = "NTFS", TotalBytes = 999_000_000_000, FreeBytes = 99_000_000_000, IsInternal = true, IsSystem = true, ReadBytesPerSec = 2_100_000, WriteBytesPerSec = 4_500_000, TotalReadBytes = 1_200_000_000, TotalWrittenBytes = 3_900_000_000 },
                new DiskDeviceReading { Id = @"G:\", Name = "Google Drive (G:)", FileSystem = "FAT32", TotalBytes = 999_000_000_000, FreeBytes = 850_000_000_000, IsInternal = true },
            ],
            Power = power ?? new PowerReading { HasBattery = false, ExternalConnected = true },
            CpuHistory = Wave(0.25, 0.12, 0),
            GpuHistory = Wave(0.55, 0.2, 1),
            MemoryHistory = Wave(0.72, 0.02, 2),
            NetDownHistory = Wave(900_000, 700_000, 0),
            NetUpHistory = Wave(40_000, 30_000, 1),
            DiskReadHistory = Wave(2_000_000, 1_500_000, 2),
            DiskWriteHistory = Wave(4_000_000, 2_500_000, 3),
            BatteryHistory = Wave(0.8, 0.05, 0),
            SystemPowerHistory = Wave(14, 6, 1),
        };
    }

    [Theory]
    [InlineData(PanelSectionId.System, "Hardware usage", "23%")]
    [InlineData(PanelSectionId.Network, "Download", "1.2 MB/s")]
    [InlineData(PanelSectionId.Disk, "90% used", "99 GB available")]
    [InlineData(PanelSectionId.Power, "Power metrics unavailable on this PC", null)]
    public void SectionShowsItsReadings(PanelSectionId id, string expectedText, string? expectedValue)
    {
        StaThread.Run(() =>
        {
            using var services = AppServices.StartWith(DefaultsStore.InMemory());
            var view = MenuPanelController.CreateSection(id, new SectionContext(services.Store, services.FeatureRuntime.IsAvailable), S);
            view.Update(Sample());

            var window = PanelWindow(id, view.Root);
            try
            {
                var texts = VisibleTexts(window.Content as DependencyObject);
                Assert.Contains(texts, t => t.Contains(expectedText, StringComparison.Ordinal));
                if (expectedValue is not null)
                {
                    Assert.Contains(texts, t => t.Contains(expectedValue, StringComparison.Ordinal));
                }
                Save(window, $"panel-{id.RawValue()}");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PowerShowsBatteryReadingsOnALaptop()
    {
        StaThread.Run(() =>
        {
            using var services = AppServices.StartWith(DefaultsStore.InMemory());
            var view = MenuPanelController.CreateSection(PanelSectionId.Power, new SectionContext(services.Store, services.FeatureRuntime.IsAvailable), S);
            view.Update(Sample(new PowerReading
            {
                HasBattery = true,
                ChargePercent = 82,
                BatteryWatts = -11.4,
                SystemWatts = 11.4,
                TimeRemainingSeconds = 13_320,
            }));

            var window = PanelWindow(PanelSectionId.Power, view.Root);
            try
            {
                var texts = VisibleTexts(window.Content as DependencyObject);
                Assert.Contains("82%", texts);
                Assert.Contains("3h 42m", texts);
                Assert.Contains(S.OnBattery, texts);
                Assert.DoesNotContain(S.PowerUnavailable, texts);
                Save(window, "panel-power-battery");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void HiddenBlocksAndGraphsStayOut()
    {
        StaThread.Run(() =>
        {
            using var services = AppServices.StartWith(DefaultsStore.InMemory());
            services.Store.Set(DefaultsKey.MonitorSysGPU, false);
            services.Store.Set(DefaultsKey.MonitorSysUptime, false);
            services.Store.Set(DefaultsKey.PanelSystemOrder, "memory,usage");
            var view = new SystemSectionView(new SectionContext(services.Store, services.FeatureRuntime.IsAvailable), S);
            view.Update(Sample());

            var texts = VisibleTexts(view.Root);
            Assert.DoesNotContain(S.Gpu, texts);
            Assert.DoesNotContain(texts, t => t.StartsWith("Up for", StringComparison.Ordinal));
            // Memory was moved above hardware usage.
            Assert.True(texts.IndexOf(S.Memory) < texts.IndexOf(S.HardwareUsage));
        });
    }

    [Fact]
    public void TabsFollowTheSavedOrderAndInsertMissingSectionsCanonically()
    {
        var tabs = PanelLayout.Visible(
            PanelLayout.Order("power,keepAwake,network"),
            _ => true,
            _ => true,
            brightnessControlEnabled: true,
            MenuPanelController.IsBuilt);

        Assert.Equal([PanelSectionId.Power, PanelSectionId.KeepAwake, PanelSectionId.Network, PanelSectionId.Disk, PanelSectionId.Mixer, PanelSectionId.System], tabs);
    }

    [Fact]
    public void IslandSystemModuleShowsItsCards()
    {
        StaThread.Run(() =>
        {
            using var services = AppServices.StartWith(DefaultsStore.InMemory());
            var cards = Core.Island.IslandSystemCards.Available(services.FeatureRuntime.IsAvailable, hasBattery: false);
            var module = new Faqra.App.Island.Modules.SystemModule(services.Monitor, cards, columns: 3);
            module.Update(Sample());

            var texts = VisibleTexts(module);
            Assert.Contains("23%", texts);
            Assert.Contains("64%", texts);
            Assert.Contains("73%", texts);
            Assert.Contains(S.Available, texts);
            Assert.Contains("↓ 1.2 MB/s", texts);
            Assert.DoesNotContain(S.Battery, texts);

            var host = new Border { Background = Faqra.App.Island.Modules.IslandPalette.Surface, Padding = new Thickness(14), Child = module };
            SaveElement(host, "island-system", 560);
        });
    }

    [Fact]
    public void MonitorPageOffersTrayMetricsAndReordersSections()
    {
        StaThread.Run(() =>
        {
            using var services = AppServices.StartWith(DefaultsStore.InMemory());
            var page = new Faqra.App.Settings.Pages.MonitorPage();
            var texts = VisibleTexts(page);
            var cardTitles = FindAll<Wpf.Ui.Controls.CardControl>(page)
                .Select(card => (card.Header as TextBlock)?.Text)
                .ToList();

            Assert.Contains(S.TraySection, texts);
            Assert.Contains(S.LiveActivity, cardTitles);
            Assert.Contains(S.UpdateEvery, cardTitles);
            Assert.Contains(S.PanelOrder, texts);
            Assert.Contains(S.Graphs, texts);
            var graphToggle = FindAll<Wpf.Ui.Controls.CardControl>(page)
                .Last(card => (card.Header as TextBlock)?.Text == S.Cpu).Content as Wpf.Ui.Controls.ToggleSwitch;
            Assert.True(graphToggle?.IsChecked, "the CPU graph is on by default");

            // The first "Move down" belongs to Keep awake, the first section on a clean install.
            var moveDown = FindAll<Wpf.Ui.Controls.Button>(page)
                .First(b => System.Windows.Automation.AutomationProperties.GetName(b).StartsWith(S.MoveDown, StringComparison.Ordinal));
            moveDown.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            var tabs = PanelLayout.Visible(
                PanelLayout.Order(services.Store.String(DefaultsKey.PanelSectionOrder)),
                id => services.Store.Bool(id.VisibilityKey()),
                services.FeatureRuntime.IsAvailable,
                false,
                MenuPanelController.IsBuilt);
            Assert.Equal([PanelSectionId.Mixer, PanelSectionId.KeepAwake, PanelSectionId.System, PanelSectionId.Network, PanelSectionId.Disk, PanelSectionId.Power], tabs);

            SaveElement(page, "settings-monitor", 840);
        });
    }

    private static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
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
        var host = new Border
        {
            Background = (Brush)Application.Current.Resources["ApplicationBackgroundBrush"],
            Width = width,
            Padding = new Thickness(24),
            Child = element,
        };
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
    }

    private static MenuPanelWindow PanelWindow(PanelSectionId active, UIElement section)
    {
        var window = new MenuPanelWindow();
        window.SetTabs([PanelSectionId.System, PanelSectionId.Network, PanelSectionId.Disk, PanelSectionId.Power], active, S);
        window.SetFooter(S);
        window.SetSection(section);
        return window;
    }

    private static List<string> VisibleTexts(DependencyObject? root)
    {
        var texts = new List<string>();
        void Walk(DependencyObject node)
        {
            if (node is UIElement { Visibility: not Visibility.Visible })
            {
                return;
            }
            if (node is TextBlock { Text.Length: > 0 } block)
            {
                texts.Add(block.Text);
            }
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            {
                Walk(child);
            }
        }
        if (root is not null)
        {
            Walk(root);
        }
        return texts;
    }

    /// <summary>Renders the window's content offscreen at the panel's width, on the theme background.</summary>
    private static void Save(Window window, string name)
    {
        var content = (FrameworkElement)window.Content;
        window.Content = null;
        var host = new Border
        {
            Background = (Brush)Application.Current.Resources["ApplicationBackgroundBrush"],
            Width = window.Width,
            Child = content,
        };
        host.Measure(new Size(window.Width, double.PositiveInfinity));
        host.Arrange(new Rect(0, 0, window.Width, host.DesiredSize.Height));
        host.UpdateLayout();

        var bitmap = new RenderTargetBitmap((int)window.Width, (int)Math.Max(host.ActualHeight, 1), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        Directory.CreateDirectory(OutputDirectory);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(OutputDirectory, $"{name}.png"));
        encoder.Save(stream);
    }
}
