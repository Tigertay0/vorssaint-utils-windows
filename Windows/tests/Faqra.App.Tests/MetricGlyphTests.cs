using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Faqra.App.Tray;
using Faqra.Core.Tray;

namespace Faqra.App.Tests;

public class MetricGlyphTests
{
    private static readonly string OutputDirectory =
        Environment.GetEnvironmentVariable("FAQRA_UI_SHOTS")
        ?? Path.Combine(Path.GetTempPath(), "faqra-ui");

    private static readonly TrayMetricText[] Samples =
    [
        new("CPU", "23%", string.Empty),
        new("RAM", "100%", string.Empty),
        new("↓1.2M", "↑320K", string.Empty),
        new("BAT", "3h42m", string.Empty),
    ];

    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(32)]
    public void MetricGlyphsHaveInkInsideTheSlotAtEveryTraySize(int pixels)
    {
        StaThread.Run(() =>
        {
            var sheet = new DrawingVisual();
            using (var dc = sheet.RenderOpen())
            {
                for (var i = 0; i < Samples.Length; i++)
                {
                    foreach (var light in new[] { false, true })
                    {
                        var buffer = MetricGlyphPainter.RenderPixels(pixels, Samples[i], light);
                        Assert.Equal(pixels * pixels * 4, buffer.Length);
                        Assert.True(TrayIconBitmap.HasInk(buffer), $"{Samples[i]} has no ink at {pixels}px");
                        var bitmap = BitmapSource.Create(pixels, pixels, 96, 96, PixelFormats.Pbgra32, null, buffer, pixels * 4);
                        var scale = 6;
                        var x = i * (pixels + 4) * scale;
                        var y = light ? (pixels + 4) * scale : 0;
                        dc.DrawRectangle(light ? Brushes.WhiteSmoke : new SolidColorBrush(Color.FromRgb(32, 32, 32)), null, new System.Windows.Rect(x, y, pixels * scale, pixels * scale));
                        RenderOptions.SetBitmapScalingMode(bitmap, BitmapScalingMode.NearestNeighbor);
                        dc.DrawImage(bitmap, new System.Windows.Rect(x, y, pixels * scale, pixels * scale));
                    }
                }
            }
            SaveSheet(sheet, $"tray-metrics-{pixels}px", Samples.Length * (pixels + 4) * 6, 2 * (pixels + 4) * 6);
        });
    }

    [Theory]
    [InlineData("23%", "23")]
    [InlineData("100%", "100")]
    [InlineData("%", "%")]
    [InlineData("3h42m", "3:42")]
    [InlineData("0h1m", "0:01")]
    [InlineData("9W", "9W")]
    [InlineData("-", "-")]
    public void SmallSlotsDropWhatTheLabelAlreadySays(string value, string expected) =>
        Assert.Equal(expected, MetricGlyphPainter.Tighten(value));

    [Fact]
    public void MetricIconIsAValidNativeIcon()
    {
        StaThread.Run(() =>
        {
            using var icon = MetricGlyphPainter.RenderIcon(16, Samples[0], lightTaskbar: false);
            Assert.False(icon.IsInvalid);
        });
    }

    private static void SaveSheet(Visual visual, string name, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        Directory.CreateDirectory(OutputDirectory);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(OutputDirectory, $"{name}.png"));
        encoder.Save(stream);
    }
}
