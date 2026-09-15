// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the glyph checks in Sources/Vorssaint/Support/SelfTest.swift

using Faqra.App.Tray;
using Faqra.Services.Diagnostics;

namespace Faqra.App;

/// <summary>Self-test checks that need WPF rendering and therefore live in the app project.</summary>
public static class AppSelfTestChecks
{
    private static readonly int[] GlyphSizes = [16, 20, 24, 32];

    public static IEnumerable<SelfTestCheck> All => [TrayGlyph];

    private static void TrayGlyph(SelfTestContext context)
    {
        foreach (var pixels in GlyphSizes)
        {
            foreach (var active in new[] { false, true })
            {
                var buffer = TrayIconBitmap.RenderPixels(pixels, active, lightTaskbar: false);
                if (!TrayIconBitmap.HasInk(buffer))
                {
                    context.Fail($"tray glyph empty at {pixels}px");
                }
                if (TrayIconBitmap.InkTouchesEdge(buffer, pixels))
                {
                    context.Fail($"tray glyph touches edge at {pixels}px");
                }
            }
        }
        using var icon = TrayIconBitmap.RenderIcon(16, active: false, lightTaskbar: false);
        if (icon.IsInvalid)
        {
            context.Fail("tray icon handle");
        }
    }
}
