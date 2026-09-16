using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Faqra.App.Island.Modules;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Island;
using Faqra.Services.Island;
using Faqra.Services.Media;

namespace Faqra.App.Tests;

/// <summary>
/// The island's window and modules, built offscreen. The window's live placement and hover timing
/// are exercised against the running app; these tests guard the content and the shape.
/// </summary>
public class IslandRenderTests
{
    private static readonly string OutputDirectory =
        Environment.GetEnvironmentVariable("FAQRA_UI_SHOTS")
        ?? Path.Combine(Path.GetTempPath(), "faqra-ui");

    [Fact]
    public void IslandIsInstalledOnAFreshRunSoItActuallyAppears()
    {
        StaThread.Run(() =>
        {
            using var services = AppServices.StartWith(DefaultsStore.InMemory());
            Assert.True(services.FeatureRuntime.IsAvailable(AppFeature.Notch));
            Assert.True(services.Store.Bool(DefaultsKey.NotchEnabled));
            // Hover opening and hover expanding both default on, so a hover reaches a module.
            Assert.True(services.Store.Bool(DefaultsKey.NotchOpenOnHover));
            Assert.True(services.Store.Bool(DefaultsKey.NotchHoverExpands));
        });
    }

    [Fact]
    public void MusicModuleShowsTheIdleStateWithoutASession()
    {
        StaThread.Run(() =>
        {
            using var services = AppServices.StartWith(DefaultsStore.InMemory());
            using var nowPlaying = new NowPlayingService();
            var module = new MusicModule(nowPlaying);
            Save(module, "island-music", 532, 250);
        });
    }

    [Fact]
    public void TimerModuleCountsDownAndResets()
    {
        StaThread.Run(() =>
        {
            using var services = AppServices.StartWith(DefaultsStore.InMemory());
            var module = new TimerModule();
            Save(module, "island-timer-idle", 532, 236);

            services.Timer.Start(TimeSpan.FromMinutes(25));
            Assert.True(services.Timer.HasSession);
            Assert.True(services.Timer.IsRunning);
            Assert.InRange(services.Timer.Remaining.TotalMinutes, 24.5, 25);
            Save(module, "island-timer-running", 532, 236);

            services.Timer.TogglePause();
            Assert.False(services.Timer.IsRunning);
            services.Timer.Reset();
            Assert.False(services.Timer.HasSession);
            Assert.Equal(TimeSpan.Zero, services.Timer.Remaining);
        });
    }

    [Fact]
    public void SectionPickerOffersEveryVisibleModule()
    {
        StaThread.Run(() =>
        {
            using var services = AppServices.StartWith(DefaultsStore.InMemory());
            var modules = IslandModules.Visible(null, null, services.FeatureRuntime.IsAvailable);
            // A fresh run installs the mixer, the metrics and the island timer.
            Assert.Contains(IslandModule.Music, modules);
            Assert.Contains(IslandModule.Timer, modules);
            Assert.Contains(IslandModule.System, modules);
            Assert.DoesNotContain(IslandModule.Clipboard, modules);

            IslandModule? chosen = null;
            var picker = new SectionPicker(modules, module => chosen = module);
            Save(picker, "island-sections", 532, 220);
            Assert.Null(chosen);
        });
    }

    [Theory]
    [InlineData(5)]
    [InlineData(25)]
    public void TimerFormatsLikeAClock(int minutes)
    {
        var text = IslandTimerService.Format(TimeSpan.FromMinutes(minutes));
        Assert.Equal($"{minutes}:00", text);
        Assert.Equal("1:05:00", IslandTimerService.Format(TimeSpan.FromMinutes(65)));
        Assert.Equal("0:09", IslandTimerService.Format(TimeSpan.FromSeconds(9)));
    }

    /// <summary>Renders on the island's own black surface, which is what the user sees.</summary>
    private static void Save(FrameworkElement element, string name, double width, double height)
    {
        // The same element may be rendered twice (before and after a state change), so release it
        // from the previous host first.
        if (element.Parent is System.Windows.Controls.Decorator previous)
        {
            previous.Child = null;
        }
        var host = new System.Windows.Controls.Border
        {
            Background = IslandPalette.Surface,
            Width = width,
            Height = height,
            Child = element,
        };
        host.Measure(new Size(width, height));
        host.Arrange(new Rect(0, 0, width, height));
        host.UpdateLayout();

        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);

        Directory.CreateDirectory(OutputDirectory);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(OutputDirectory, $"{name}.png"));
        encoder.Save(stream);
    }
}
