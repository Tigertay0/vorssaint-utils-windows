// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the NSImage assembly in Sources/Vorssaint/App/StatusItemController.swift

using System.Windows.Media;
using System.Windows.Media.Imaging;
using Faqra.Core.Defaults;
using Faqra.Win32.Icons;

namespace Faqra.App.Tray;

/// <summary>Rasterizes the tray glyph at a pixel size and turns it into an HICON or a WPF bitmap.</summary>
public static class TrayIconBitmap
{
    public static BitmapSource Render(int pixels, bool active, bool lightTaskbar,
        KeepAwakeActiveIcon icon = KeepAwakeActiveIcon.Brand, KeepAwakeIconTint tint = KeepAwakeIconTint.Orange)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            GlyphPainter.Draw(dc, pixels, active, lightTaskbar, icon, tint);
        }
        var bitmap = new RenderTargetBitmap(pixels, pixels, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    public static byte[] RenderPixels(int pixels, bool active, bool lightTaskbar,
        KeepAwakeActiveIcon icon = KeepAwakeActiveIcon.Brand, KeepAwakeIconTint tint = KeepAwakeIconTint.Orange)
    {
        var bitmap = Render(pixels, active, lightTaskbar, icon, tint);
        var stride = pixels * 4;
        var buffer = new byte[stride * pixels];
        bitmap.CopyPixels(buffer, stride, 0);
        return buffer;
    }

    public static NativeIcon RenderIcon(int pixels, bool active, bool lightTaskbar,
        KeepAwakeActiveIcon icon = KeepAwakeActiveIcon.Brand, KeepAwakeIconTint tint = KeepAwakeIconTint.Orange) =>
        IconFactory.CreateFromPbgra32(pixels, pixels, RenderPixels(pixels, active, lightTaskbar, icon, tint));

    /// <summary>True when any pixel on the outermost ring has ink (the mark would be clipped by the slot).</summary>
    public static bool InkTouchesEdge(byte[] pbgra, int pixels)
    {
        for (var i = 0; i < pixels; i++)
        {
            if (Alpha(pbgra, pixels, i, 0) > 0 || Alpha(pbgra, pixels, i, pixels - 1) > 0
                || Alpha(pbgra, pixels, 0, i) > 0 || Alpha(pbgra, pixels, pixels - 1, i) > 0)
            {
                return true;
            }
        }
        return false;
    }

    public static bool HasInk(byte[] pbgra)
    {
        for (var i = 3; i < pbgra.Length; i += 4)
        {
            if (pbgra[i] > 0)
            {
                return true;
            }
        }
        return false;
    }

    private static byte Alpha(byte[] pbgra, int pixels, int x, int y) => pbgra[(y * pixels + x) * 4 + 3];
}
