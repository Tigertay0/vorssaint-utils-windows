using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Faqra.App.Agents;
using Faqra.Core.Agents;

namespace Faqra.App.Tests;

public class AgentsRenderTests
{
    private static readonly string OutputDirectory =
        Environment.GetEnvironmentVariable("FAQRA_UI_SHOTS") ?? Path.Combine(Path.GetTempPath(), "faqra-ui");

    /// <summary>Renders on a transparent ground, saves a PNG, and returns how many pixels carry ink.</summary>
    internal static int RenderInk(FrameworkElement element, string name, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        Directory.CreateDirectory(OutputDirectory);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(OutputDirectory, $"{name}.png")))
        {
            encoder.Save(stream);
        }
        var pixels = new byte[(int)width * (int)height * 4];
        bitmap.CopyPixels(pixels, (int)width * 4, 0);
        var inked = 0;
        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0)
            {
                inked++;
            }
        }
        return inked;
    }

    [Fact]
    public void EveryStateDrawsItsOrb() => StaThread.Run(() =>
    {
        foreach (var state in Enum.GetValues<AgentState>())
        {
            var style = AgentOrbStyles.For(state);
            var orb = new OrbView { Diameter = 64 };
            orb.Apply(style, AgentInk.For(style.Tone));
            Assert.True(RenderInk(orb, $"agents-orb-{state}", 64, 64) > 200, $"{state} drew too little");
        }
    });

    [Fact]
    public void TheOrbTakesItsDiameter() => StaThread.Run(() =>
    {
        var orb = new OrbView { Diameter = 18 };
        orb.Measure(new Size(100, 100));
        Assert.Equal(new Size(18, 18), orb.DesiredSize);
    });
}
