using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Faqra.App.Onboarding;
using Faqra.App.Settings;
using Faqra.App.Settings.Pages;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Localization;

namespace Faqra.App.Tests;

/// <summary>
/// Builds each window and page against an in-memory store, asserts the content that must be there,
/// and writes a PNG of every surface so the layout can be reviewed without running the app.
/// </summary>
public class UiRenderTests
{
    private static readonly string OutputDirectory =
        Environment.GetEnvironmentVariable("FAQRA_UI_SHOTS")
        ?? Path.Combine(Path.GetTempPath(), "faqra-ui");

    [Fact]
    public void SettingsWindow_ShowsEveryImplementedPageAndRenders()
    {
        StaThread.Run(() =>
        {
            using var app = StartApp();
            var window = new SettingsWindow { Width = 900, Height = 720 };
            try
            {
                Assert.Equal(Strings.EnUS.SettingsTitle, window.Title);
                // A group header must never be the selection, or the detail pane comes up empty.
                var selected = Assert.IsType<SidebarRow>(window.PageList.SelectedItem);
                Assert.False(selected.IsHeader);
                Assert.Equal(Core.Settings.SettingsPage.General, selected.Page);
                Assert.IsType<GeneralPage>(window.DetailHost.Content);
                Save(window, "settings-window");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void GeneralPage_BindsItsControls()
    {
        StaThread.Run(() =>
        {
            using var app = StartApp();
            var page = new GeneralPage();
            var s = Strings.EnUS;
            Assert.Equal(s.LaunchAtLogin, page.LaunchAtLoginLabel.Text);
            Assert.Equal(s.LanguageLabel, page.LanguageLabel.Text);
            Assert.Equal(3, page.AppearanceBox.Items.Count);
            Assert.Equal(AppLanguages.All.Count, page.LanguageBox.Items.Count);
            Save(page, "settings-general", 840, 560);
        });
    }

    [Fact]
    public void FeatureHubPage_ListsEveryFeatureInSevenGroups()
    {
        StaThread.Run(() =>
        {
            using var app = StartApp();
            var page = new FeatureHubPage();
            var groups = Assert.IsAssignableFrom<IReadOnlyList<FeatureGroupViewModel>>(page.GroupList.ItemsSource);

            Assert.Equal(7, groups.Count);
            Assert.Equal(67, groups.Sum(group => group.Rows.Count));
            // A clean install is Essentials plus the island and Agents: 11 installed of the 13 Windows-ready features.
            Assert.Equal("11 of 13 features installed", page.CountText.Text);
            var mixer = groups.SelectMany(g => g.Rows).Single(row => row.Feature == AppFeature.Mixer);
            var dockClick = groups.SelectMany(g => g.Rows).Single(row => row.Feature == AppFeature.DockClick);
            Assert.True(mixer.IsInstalled);
            Assert.Null(mixer.BlockedReason);
            Assert.False(dockClick.IsInstalled);
            Assert.Equal(Strings.EnUS.FeatureNotAvailableOnWindows, dockClick.BlockedReason);
            Assert.False(dockClick.CanFlip);
            Save(page, "settings-features", 840, 1400);
        });
    }

    [Fact]
    public void FeatureHubPage_InstallAndUninstallFlipAvailability()
    {
        StaThread.Run(() =>
        {
            using var app = StartApp();
            var page = new FeatureHubPage();
            var groups = (IReadOnlyList<FeatureGroupViewModel>)page.GroupList.ItemsSource!;
            var commandBar = groups.SelectMany(g => g.Rows).Single(row => row.Feature == AppFeature.CommandBar);

            Assert.False(commandBar.IsInstalled);
            commandBar.Toggle();
            Assert.True(commandBar.IsInstalled);
            Assert.True(AppServices.Current.Store.Bool(AppFeature.CommandBar.AvailabilityKey()));

            commandBar.Toggle();
            Assert.False(commandBar.IsInstalled);
            Assert.True(AppServices.Current.FeatureRuntime.NeedsRestartToUnload);
        });
    }

    [Fact]
    public void AdvancedAndAboutPagesRender()
    {
        StaThread.Run(() =>
        {
            using var app = StartApp();
            var advanced = new AdvancedPage();
            Assert.Equal(Strings.EnUS.BackupTitle, advanced.BackupHeader.Text);
            Save(advanced, "settings-advanced", 840, 420);

            var about = new AboutPage();
            Save(about, "settings-about", 840, 520);
        });
    }

    [Fact]
    public void OnboardingWindow_StartsOnWelcomeAndAppliesTheEssentialPreset()
    {
        StaThread.Run(() =>
        {
            using var app = StartApp();
            var window = new OnboardingWindow { Width = 560, Height = 640 };
            try
            {
                Save(window, "onboarding-welcome");
            }
            finally
            {
                window.Close();
            }

            var purpose = new PurposeStep(
                FeaturePreset.Essential.Features().ToHashSet(), FeaturePreset.Essential, _ => { }, _ => { });
            var groups = Assert.IsAssignableFrom<IReadOnlyList<PurposeGroupViewModel>>(purpose.GroupList.ItemsSource);
            // Only Windows-ready features are offered; everything offered is selectable.
            Assert.Equal(13, groups.Sum(group => group.Rows.Count));
            Assert.All(groups.SelectMany(g => g.Rows), row => Assert.True(row.CanSelect));
            Save(purpose, "onboarding-purpose", 496, 900);

            Save(new DoneStep(), "onboarding-done", 496, 520);
        });
    }

    private static AppServices StartApp() => AppServices.StartWith(DefaultsStore.InMemory());

    /// <summary>
    /// Renders a surface offscreen, composited on the theme background so the PNG shows what the
    /// user would see. A window is rendered through its content: showing it would put it on screen.
    /// </summary>
    private static void Save(FrameworkElement element, string name, double width = 0, double height = 0)
    {
        var w = width > 0 ? width : element.Width;
        var h = height > 0 ? height : element.Height;

        var content = element is Window window ? (FrameworkElement)window.Content : element;
        if (content.Parent is System.Windows.Controls.Decorator previous)
        {
            previous.Child = null;
        }
        else if (element is Window owner)
        {
            owner.Content = null;
        }

        var host = new System.Windows.Controls.Border
        {
            Background = (Brush)Application.Current.Resources["ApplicationBackgroundBrush"],
            Width = w,
            Child = content,
        };
        if (element is Window)
        {
            host.Height = h;
        }

        host.Measure(new Size(w, element is Window ? h : double.PositiveInfinity));
        host.Arrange(new Rect(0, 0, w, element is Window ? h : host.DesiredSize.Height));
        host.UpdateLayout();
        h = host.ActualHeight;

        var bitmap = new RenderTargetBitmap((int)w, (int)Math.Max(h, 1), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);

        Directory.CreateDirectory(OutputDirectory);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(OutputDirectory, $"{name}.png"));
        encoder.Save(stream);
    }
}
