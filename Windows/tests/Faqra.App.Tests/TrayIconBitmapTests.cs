using Faqra.App.Tray;

namespace Faqra.App.Tests;

public class TrayIconBitmapTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(32)]
    public void GlyphHasInkAndStaysInsideTheSlot(int pixels)
    {
        StaThread.Run(() =>
        {
            foreach (var active in new[] { false, true })
            {
                var buffer = TrayIconBitmap.RenderPixels(pixels, active, lightTaskbar: false);
                Assert.Equal(pixels * pixels * 4, buffer.Length);
                Assert.True(TrayIconBitmap.HasInk(buffer), $"no ink at {pixels}px active={active}");
                Assert.False(TrayIconBitmap.InkTouchesEdge(buffer, pixels), $"ink touches edge at {pixels}px active={active}");
            }
        });
    }

    [Fact]
    public void ProducesAValidNativeIcon()
    {
        StaThread.Run(() =>
        {
            using var icon = TrayIconBitmap.RenderIcon(16, active: false, lightTaskbar: true);
            Assert.False(icon.IsInvalid);
            Assert.NotEqual(IntPtr.Zero, icon.Handle);
        });
    }
}
