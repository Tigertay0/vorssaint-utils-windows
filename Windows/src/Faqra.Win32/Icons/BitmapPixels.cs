// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Runtime.InteropServices;

namespace Faqra.Win32.Icons;

/// <summary>Top-down premultiplied BGRA pixels, the layout WPF's Pbgra32 expects.</summary>
public sealed record BitmapPixels(int Width, int Height, byte[] Bgra)
{
    private const uint DIB_RGB_COLORS = 0;

    /// <summary>
    /// Copies an HBITMAP's pixels and deletes the bitmap. Imaging.CreateBitmapSourceFromHBitmap drops
    /// the alpha of the shell's premultiplied icons, which turns their transparent corners black.
    /// </summary>
    public static BitmapPixels? ReadAndDelete(IntPtr hBitmap)
    {
        if (hBitmap == IntPtr.Zero)
        {
            return null;
        }
        try
        {
            if (GetObjectW(hBitmap, Marshal.SizeOf<BITMAP>(), out var info) == 0 || info.bmWidth <= 0 || info.bmHeight == 0)
            {
                return null;
            }
            var width = info.bmWidth;
            var height = Math.Abs(info.bmHeight);
            var header = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // negative: top-down rows
                biPlanes = 1,
                biBitCount = 32,
            };
            var pixels = new byte[width * height * 4];
            var dc = GetDC(IntPtr.Zero);
            try
            {
                return GetDIBits(dc, hBitmap, 0, (uint)height, pixels, ref header, DIB_RGB_COLORS) == height
                    ? new BitmapPixels(width, height, pixels)
                    : null;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, dc);
            }
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
        // Room for the color table GetDIBits may write for 32 bpp with BI_BITFIELDS.
        public uint mask0;
        public uint mask1;
        public uint mask2;
    }

    [DllImport("gdi32.dll")]
    private static extern int GetObjectW(IntPtr h, int c, out BITMAP pv);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER bmi, uint usage);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr ho);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
}
