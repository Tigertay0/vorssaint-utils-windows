// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.ComponentModel;
using System.Runtime.InteropServices;
using Faqra.Win32.Native;

namespace Faqra.Win32.Icons;

/// <summary>Turns a 32-bit premultiplied BGRA pixel buffer into an HICON with an alpha channel.</summary>
public static class IconFactory
{
    public static NativeIcon CreateFromPbgra32(int width, int height, ReadOnlySpan<byte> pixels)
    {
        if (pixels.Length != width * height * 4)
        {
            throw new ArgumentException("pixel buffer must be width * height * 4 bytes", nameof(pixels));
        }

        var header = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height, // negative: top-down rows, matching the WPF buffer
            biPlanes = 1,
            biBitCount = 32,
            biCompression = Gdi32.BI_RGB,
        };

        var color = Gdi32.CreateDIBSection(IntPtr.Zero, ref header, Gdi32.DIB_RGB_COLORS, out var bits, IntPtr.Zero, 0);
        if (color == IntPtr.Zero || bits == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateDIBSection failed");
        }
        var mask = IntPtr.Zero;
        try
        {
            unsafe
            {
                pixels.CopyTo(new Span<byte>((void*)bits, pixels.Length));
            }
            // A zero-filled AND mask: hosts that ignore the alpha channel then draw every pixel
            // from the color bitmap instead of reading uninitialized memory.
            var maskStride = ((width + 15) / 16) * 2;
            var maskBits = new byte[maskStride * height];
            unsafe
            {
                fixed (byte* maskPointer = maskBits)
                {
                    mask = Gdi32.CreateBitmap(width, height, 1, 1, (IntPtr)maskPointer);
                }
            }
            if (mask == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateBitmap failed");
            }
            var info = new ICONINFO { fIcon = true, hbmMask = mask, hbmColor = color };
            var icon = User32.CreateIconIndirect(ref info);
            if (icon == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateIconIndirect failed");
            }
            return new NativeIcon(icon);
        }
        finally
        {
            if (mask != IntPtr.Zero)
            {
                Gdi32.DeleteObject(mask);
            }
            Gdi32.DeleteObject(color);
        }
    }
}
