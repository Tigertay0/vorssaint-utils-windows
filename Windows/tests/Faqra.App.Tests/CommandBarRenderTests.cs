using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Faqra.App.CommandBar;
using Faqra.App.Settings.Pages;
using Faqra.Core.CommandBar;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Localization;
using Faqra.Win32.Shell;
using Wpf.Ui.Controls;

namespace Faqra.App.Tests;

/// <summary>The command bar's list, answer, confirm and argument states and its settings page, rendered offscreen to PNG.</summary>
public class CommandBarRenderTests
{
    private static readonly string OutputDirectory =
        Environment.GetEnvironmentVariable("FAQRA_UI_SHOTS")
        ?? Path.Combine(Path.GetTempPath(), "faqra-ui");

    private static readonly CommandBarStrings Bar = CommandBarStrings.EnUS;

    private static readonly InstalledApp[] SampleApps =
    [
        new("Google Chrome", "Chrome"),
        new("Character Map", "{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\\charmap.exe"),
        new("Control Panel", "Microsoft.Windows.ControlPanel"),
        new("Calculator", "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"),
    ];

    [Fact]
    public void HomeListShowsSuggestionsAndGroups()
    {
        StaThread.Run(() =>
        {
            using var services = Start();
            var catalog = new CommandBarCatalog(services);
            var rows = catalog.Build();
            var byId = rows.ToDictionary(r => r.Entry.Id);
            var sections = CommandBarCatalogSupport.Home(rows.Select(r => r.Entry).ToList(), new Dictionary<string, CommandBarUse>(), CommandBarCatalog.CuratedSuggestionIds, Bar);
            Assert.Equal(Bar.SuggestionsLabel, sections[0].Title);
            Assert.Contains(sections[0].Entries, e => e.Id == "action.keepAwake");
            Assert.Contains(rows, r => r.Entry.Id == "toggle.notch");
            var items = sections.SelectMany(s => s.Entries.Select((e, i) => new CommandBarListItem(byId[e.Id], i == 0 ? s.Title : null))).Take(14).ToList();
            Render("commandbar-home", window => window.RenderList(items, 0, string.Empty, Bar, null));
        });
    }

    [Fact]
    public void ChrRanksChromeFirstAmongThisPcsApps()
    {
        StaThread.Run(() =>
        {
            using var services = Start();
            var rows = new CommandBarCatalog(services).Build().Concat(CommandBarCatalog.Apps(SampleApps)).ToList();
            var ranked = CommandBarRanking.Rank(rows.Select(r => r.Entry).ToList(), "chr");
            Assert.Equal("app.Chrome", ranked[0].Id);
            var byId = rows.ToDictionary(r => r.Entry.Id);
            var items = ranked.Select(e => new CommandBarListItem(byId[e.Id], null)).ToList();
            Render("commandbar-search-chr", window => window.RenderList(items, 0, "chr", Bar, null));
        });
    }

    [Fact]
    public void AnswerLeadsTheList()
    {
        StaThread.Run(() =>
        {
            using var services = Start();
            var answer = CommandBarCatalogSupport.Answer("10 km in mi", DateTimeOffset.Now, TimeZoneInfo.Local, CultureInfo.GetCultureInfo("en-US"), Bar)!;
            var row = new CommandBarRow(answer.Entry, new CommandBarIcon.Symbol(SymbolRegular.Calculator24), _ => { }, AnswerValue: answer.Value);
            Render("commandbar-answer", window => window.RenderList([new CommandBarListItem(row, null)], 0, "10 km in mi", Bar, null));
        });
    }

    [Fact]
    public void ConfirmAndArgumentCards()
    {
        StaThread.Run(() =>
        {
            using var services = Start();
            var rows = new CommandBarCatalog(services).Build();
            var restart = rows.Single(r => r.Entry.Id == "action.power.restart");
            Assert.Equal(Bar.PowerRestartConfirm, restart.ConfirmationPrompt);
            Render("commandbar-confirm", window => window.RenderCard(CommandBarWindow.ConfirmCard(restart, Bar, () => { }, () => { })));
            var volume = rows.Single(r => r.Entry.Id == "action.volume");
            Assert.Equal(new CommandBarArgument(0, 100, false), volume.Argument);
            Render("commandbar-argument", window =>
            {
                window.SetModeChip(volume.Entry.Title);
                window.SetPlaceholder(string.Format(Bar.ArgumentRangeFormat, 0, 100));
                window.RenderCard(CommandBarWindow.ArgumentCard(volume, Bar.ArgumentHint));
            });
        });
    }

    [Fact]
    public void EmptyStateOffersSuggestions()
    {
        StaThread.Run(() =>
        {
            using var services = Start();
            Render("commandbar-empty", window =>
            {
                window.Query = "zzzz";
                window.RenderList([], 0, "zzzz", Bar, () => { });
            });
        });
    }

    [Fact]
    public void SettingsPageRenders()
    {
        StaThread.Run(() =>
        {
            using var services = Start();
            var page = new CommandBarPage();
            page.Measure(new Size(560, double.PositiveInfinity));
            page.Arrange(new Rect(page.DesiredSize));
            Save((FrameworkElement)page.Content, "settings-commandbar", 840, detach: () => page.Content = null);
        });
    }

    private static AppServices Start()
    {
        var store = DefaultsStore.InMemory();
        var services = AppServices.StartWith(store);
        store.Set(AppFeature.CommandBar.AvailabilityKey(), true);
        store.Set(AppFeature.Notch.AvailabilityKey(), true);
        return services;
    }

    private static void Render(string name, Action<CommandBarWindow> draw)
    {
        var window = new CommandBarWindow();
        window.SetPlaceholder(Bar.SearchPlaceholder);
        window.SetFooter("Alt+Space", "Ctrl+P  Ctrl+N  ↑↓   Enter   Esc");
        draw(window);
        var content = (FrameworkElement)window.Content;
        Save(content, name, 560, detach: () => window.Content = null);
        window.Close();
    }

    private static void Save(FrameworkElement element, string name, double width, Action detach)
    {
        detach();
        var host = new Border { Width = width, Child = element };
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
