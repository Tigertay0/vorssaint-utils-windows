using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Faqra.App.Island.Modules;
using Faqra.App.Panel.Sections;
using Faqra.App.Tray;
using Faqra.Core.Defaults;
using Faqra.Core.KeepAwake;
using Faqra.Core.Localization;
using Faqra.Services.Audio;

namespace Faqra.App.Tests;

/// <summary>
/// The keep-awake and mixer sections, the island mixer, the Energy page and the tray glyph variants,
/// rendered offscreen to PNG. Audio comes from a fake backend so the renders never depend on what this PC plays.
/// </summary>
public class MixerKeepAwakeRenderTests
{
    private static readonly string OutputDirectory =
        Environment.GetEnvironmentVariable("FAQRA_UI_SHOTS")
        ?? Path.Combine(Path.GetTempPath(), "faqra-ui");

    private static readonly MixerStrings M = MixerStrings.EnUS;
    private static readonly KeepAwakeStrings K = KeepAwakeStrings.EnUS;

    private sealed class InlineDispatcher : IAudioDispatcher
    {
        public double Now => 0;

        public void Post(Action action) => action();

        public void PostDelayed(Action action, TimeSpan delay)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class SampleAudio : IAudioBackend
    {
        public IReadOnlyList<AudioDeviceInfo> Devices() =>
            [new("speakers", "Speakers (High Definition Audio Device)", false), new("buds", "Galaxy Buds2 Pro", true)];

        public string? DefaultDeviceId() => "speakers";

        public IReadOnlyList<AudioSessionInfo> Sessions() =>
        [
            new("1", "speakers", 10, "spotify.exe", null, "Spotify", true, 0.62, false),
            new("2", "speakers", 11, "chrome.exe", null, "Google Chrome", false, 1, false),
            new("3", "speakers", 12, "discord.exe", null, "Discord", false, 0.4, true),
        ];

        public void SetAppVolume(string exeId, double volume)
        {
        }

        public OutputLevel? OutputLevel(string deviceId) => new OutputLevel(0.16, false);

        public void SetOutputVolume(string deviceId, double volume)
        {
        }

        public void SetOutputMuted(string deviceId, bool muted)
        {
        }

        public int SetDefaultOutput(string deviceId) => 0;

        public event Action? DevicesChanged { add { } remove { } }

        public event Action? SessionCreated { add { } remove { } }

        public event Action? OutputLevelChanged { add { } remove { } }

        public void Dispose()
        {
        }
    }

    private static AppVolumeMixer SampleMixer(ISettingsStore store)
    {
        var mixer = new AppVolumeMixer(store, () => true, () => new SampleAudio(), new InlineDispatcher(), ownProcessId: 1);
        mixer.SyncWithPreferences();
        return mixer;
    }

    [Fact]
    public void MixerSection_ListsAppsAndTheOutput()
    {
        StaThread.Run(() =>
        {
            using var services = AppServices.StartWith(DefaultsStore.InMemory());
            using var mixer = SampleMixer(services.Store);
            using var view = new MixerSectionView(new SectionContext(services.Store, services.FeatureRuntime.IsAvailable), mixer, SectionPalette.Panel);

            var texts = Texts(view.Root);
            Assert.Contains("Discord", texts);
            Assert.Contains("Google Chrome", texts);
            Assert.Contains("62%", texts);
            Assert.Contains("0%", texts);
            Assert.Contains("16%", texts);
            Assert.Contains(M.SystemOutputTitle, texts);
            Assert.True(texts.IndexOf("Discord") < texts.IndexOf("Spotify"), "rows sort by name");
            SaveElement((FrameworkElement)view.Root, "panel-mixer", 332, panelBackground: true);
        });
    }

    [Fact]
    public void MixerSection_HideInactiveKeepsPlayingAndAdjustedApps()
    {
        StaThread.Run(() =>
        {
            using var services = AppServices.StartWith(DefaultsStore.InMemory());
            services.Store.Set(DefaultsKey.MixerHideInactiveApps, true);
            using var mixer = SampleMixer(services.Store);
            using var view = new MixerSectionView(new SectionContext(services.Store, services.FeatureRuntime.IsAvailable), mixer, SectionPalette.Panel);

            var texts = Texts(view.Root);
            Assert.Contains("Spotify", texts);
            Assert.Contains("Discord", texts);
            Assert.DoesNotContain("Google Chrome", texts);
        });
    }

    [Fact]
    public void IslandMixerRendersOnTheBlackSurface()
    {
        StaThread.Run(() =>
        {
            using var services = AppServices.StartWith(DefaultsStore.InMemory());
            using var mixer = SampleMixer(services.Store);
            using var view = new MixerSectionView(new SectionContext(services.Store, services.FeatureRuntime.IsAvailable), mixer, SectionPalette.Island);
            var host = new Border { Background = IslandPalette.Surface, Padding = new Thickness(14), Child = view.Root };
            Assert.Contains("Spotify", Texts(view.Root));
            SaveElement(host, "island-mixer", 560, panelBackground: false);
        });
    }

    [Fact]
    public void KeepAwakeSection_IdleThenTimed()
    {
        StaThread.Run(() =>
        {
            using var services = AppServices.StartWith(DefaultsStore.InMemory());
            services.KeepAwake.Session.MarkRecoveryCompleted();
            using var view = new KeepAwakeSectionView(new SectionContext(services.Store, services.FeatureRuntime.IsAvailable), services.KeepAwake, SectionPalette.Panel);

            var idle = Texts(view.Root);
            Assert.Contains(K.NormalRules, idle);
            Assert.Contains(K.Duration, idle);
            SaveElement((FrameworkElement)view.Root, "panel-keepawake-idle", 332, panelBackground: true);

            try
            {
                services.KeepAwake.Session.Activate(60);
                var timed = Texts(view.Root);
                Assert.Contains(timed, t => t.StartsWith(K.EndsIn, StringComparison.Ordinal));
                Assert.Contains("+15 min", FindAll<Wpf.Ui.Controls.Button>(view.Root).Select(b => b.Content as string));
                Assert.DoesNotContain(K.Duration, timed);
                SaveElement((FrameworkElement)view.Root, "panel-keepawake-timed", 332, panelBackground: true);
            }
            finally
            {
                services.KeepAwake.Session.Deactivate(KeepAwakeEndReason.Manual);
            }
        });
    }

    [Fact]
    public void EnergyPageRenders()
    {
        StaThread.Run(() =>
        {
            using var services = AppServices.StartWith(DefaultsStore.InMemory());
            var page = new Faqra.App.Settings.Pages.EnergyPage();
            var texts = Texts(page);
            Assert.Contains(K.SessionSection, texts);
            Assert.Contains(K.AutomationSection, texts);
            var cardTitles = FindAll<Wpf.Ui.Controls.CardControl>(page)
                .SelectMany(card => card.Header is null ? [] : FindAll<TextBlock>(card.Header).Prepend(card.Header as TextBlock))
                .Select(t => t?.Text)
                .ToList();
            Assert.Contains(K.PauseWhenLocked, cardTitles);
            Assert.Contains(K.DefaultDuration, cardTitles);
            Assert.Contains(K.GlobalHotkeySection, texts);
            SaveElement(page, "settings-energy", 840, panelBackground: true);
        });
    }

    [Theory]
    [InlineData(KeepAwakeActiveIcon.Coffee)]
    [InlineData(KeepAwakeActiveIcon.Eye)]
    [InlineData(KeepAwakeActiveIcon.Moon)]
    [InlineData(KeepAwakeActiveIcon.Light)]
    [InlineData(KeepAwakeActiveIcon.Brand)]
    public void TrayVariantsHaveInkAndStayInside(KeepAwakeActiveIcon icon)
    {
        StaThread.Run(() =>
        {
            foreach (var pixels in new[] { 16, 20, 24, 32 })
            {
                foreach (var tint in Enum.GetValues<KeepAwakeIconTint>())
                {
                    var buffer = TrayIconBitmap.RenderPixels(pixels, active: true, lightTaskbar: false, icon, tint);
                    Assert.True(TrayIconBitmap.HasInk(buffer), $"{icon}/{tint} has no ink at {pixels}px");
                    Assert.False(TrayIconBitmap.InkTouchesEdge(buffer, pixels), $"{icon}/{tint} touches the edge at {pixels}px");
                }
            }
            var strip = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Black };
            foreach (var tint in Enum.GetValues<KeepAwakeIconTint>())
            {
                strip.Children.Add(new Image { Source = TrayIconBitmap.Render(32, true, false, icon, tint), Width = 32, Height = 32, Margin = new Thickness(4) });
            }
            SaveElement(strip, $"tray-{icon.RawValue()}", 260, panelBackground: false);
        });
    }

    private static List<string> Texts(object root)
    {
        var element = (FrameworkElement)root;
        element.Measure(new Size(560, double.PositiveInfinity));
        element.Arrange(new Rect(element.DesiredSize));
        element.UpdateLayout();
        return FindAll<TextBlock>(element).Where(t => t.IsVisible || t.Visibility == Visibility.Visible).Select(t => t.Text).ToList();
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

    private static void SaveElement(FrameworkElement element, string name, double width, bool panelBackground)
    {
        if (element.Parent is Decorator decorator)
        {
            decorator.Child = null;
        }
        else if (element.Parent is System.Windows.Controls.Panel panel)
        {
            panel.Children.Remove(element);
        }
        var host = new Border { Width = width, Padding = new Thickness(panelBackground ? 12 : 0), Child = element };
        host.Background = panelBackground ? (Brush)Application.Current.Resources["ApplicationBackgroundBrush"] : Brushes.Black;
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
