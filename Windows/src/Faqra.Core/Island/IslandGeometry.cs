// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors NotchGeometry, NotchLayout, NotchSize and NotchMotion in
// Sources/Vorssaint/Services/Notch/NotchSupport.swift, with isNotched always false: no Windows
// machine has a camera cutout, so the island always draws its own silhouette.

namespace Faqra.Core.Island;

public enum IslandSize
{
    Compact, Spacious, Custom,
}

public enum IslandDisplay
{
    /// <summary>The primary display.</summary>
    Automatic,
    /// <summary>Upstream's built-in screen; sanitized to <see cref="Automatic"/> on Windows.</summary>
    BuiltIn,
    /// <summary>The display holding the taskbar.</summary>
    Main,
}

public enum IslandIdleContent
{
    None, Battery, Music,
}

/// <summary>A size in device-independent pixels.</summary>
public readonly record struct IslandSizeValue(double Width, double Height);

public static class IslandSizes
{
    public const double MinWidth = 360;
    public const double MaxWidth = 600;
    public const double MinHeight = 400;
    public const double MaxHeight = 640;
    public const double DefaultWidth = 440;
    public const double DefaultHeight = 480;

    public static double Clamped(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Min(max, Math.Max(min, value)) : fallback;

    public static IslandSize FromRawValue(string? raw) => raw switch
    {
        "compact" => IslandSize.Compact,
        "custom" => IslandSize.Custom,
        _ => IslandSize.Spacious,
    };

    public static string RawValue(this IslandSize size) => size.ToString().ToLowerInvariant();

    public static IslandDisplay DisplayFromRawValue(string? raw) => raw switch
    {
        // Upstream's builtIn means the MacBook screen; on Windows that is just the primary display.
        "main" => IslandDisplay.Main,
        _ => IslandDisplay.Automatic,
    };

    public static IslandIdleContent IdleContentFromRawValue(string? raw) => raw switch
    {
        "none" => IslandIdleContent.None,
        "battery" => IslandIdleContent.Battery,
        _ => IslandIdleContent.Music,
    };
}

/// <summary>Shared measurements so the window's size and its content layout stay in agreement.</summary>
public static class IslandLayout
{
    public const double Shoulder = 14;
    public const double HorizontalInset = 28;
    public const double HeaderHeight = 36;
    public const double NavigationHeight = 36;
    public const double Spacing = 18;
    public const double BottomInset = 22;
    public const double ControlHeight = 94;
    public const double MusicControlHeight = 112;
    public const double ActionHeight = 52;
    public const double ActionSpacing = 8;
    public const double SectionTileHeight = 88;
    public const double QuickAccessGutter = 72;

    public static double ChromeHeight => HeaderHeight + Spacing + BottomInset;
}

/// <summary>How long the silhouette takes to change size.</summary>
public static class IslandMotion
{
    public static readonly TimeSpan Grow = TimeSpan.FromSeconds(0.34);
    public static readonly TimeSpan Shrink = TimeSpan.FromSeconds(0.26);

    /// <summary>Content cross-fade when one module replaces another.</summary>
    public static readonly TimeSpan ContentFade = TimeSpan.FromSeconds(0.18);

    public static TimeSpan Duration(IslandSizeValue from, IslandSizeValue to)
    {
        var grows = to.Height > from.Height || (to.Height.Equals(from.Height) && to.Width > from.Width);
        return grows ? Grow : Shrink;
    }
}

/// <summary>
/// Every size the island can take on one display. All values are device-independent pixels; the
/// window scales them by the monitor's DPI.
/// </summary>
public sealed class IslandGeometry
{
    /// <summary>Upstream reads the macOS menu bar; Windows has none, so this is the resting height.</summary>
    public const double DefaultBarHeight = 24;

    public IslandGeometry(
        double screenWidth,
        double screenHeight,
        double barHeight = DefaultBarHeight,
        IslandSize layout = IslandSize.Spacious,
        double customWidth = IslandSizes.DefaultWidth,
        double customHeight = IslandSizes.DefaultHeight)
    {
        ScreenWidth = screenWidth;
        ScreenHeight = screenHeight;
        Layout = layout;
        CustomWidth = IslandSizes.Clamped(customWidth, IslandSizes.MinWidth, IslandSizes.MaxWidth, IslandSizes.DefaultWidth);
        CustomHeight = IslandSizes.Clamped(customHeight, IslandSizes.MinHeight, IslandSizes.MaxHeight, IslandSizes.DefaultHeight);

        var bar = double.IsFinite(barHeight) ? Math.Min(64, Math.Max(16, barHeight)) : DefaultBarHeight;
        // The drawn silhouette: upstream's formula for a screen without a cutout.
        CameraWidth = Math.Min(180 * bar / 32, screenWidth * 0.7);
        CameraHeight = bar;
        BarHeight = Math.Max(CameraHeight, bar);
    }

    public double ScreenWidth { get; }
    public double ScreenHeight { get; }
    public IslandSize Layout { get; }
    public double CustomWidth { get; }
    public double CustomHeight { get; }

    /// <summary>Width of the resting pill.</summary>
    public double CameraWidth { get; }

    /// <summary>Height of the resting pill.</summary>
    public double CameraHeight { get; }

    public double BarHeight { get; }

    /// <summary>Where expanded content may start, clear of the resting silhouette.</summary>
    public double SafeContentTop => CameraHeight + 10;

    /// <summary>The resting size while idle content (album art, battery) is showing.</summary>
    public IslandSizeValue Collapsed => new(Math.Min(ScreenWidth - 24, CameraWidth), BarHeight);

    public IslandSizeValue RestingSize(bool showsContent) =>
        showsContent ? Collapsed : new IslandSizeValue(CameraWidth, CameraHeight);

    /// <summary>The small state a hover reaches when full expansion is switched off.</summary>
    public IslandSizeValue Peek =>
        new(Math.Min(ScreenWidth - 24, Math.Max(CameraWidth + 110, 340)), SafeContentTop + 52);

    public double ExpandedWidth
    {
        get
        {
            var preferred = Layout switch
            {
                IslandSize.Compact => 480.0,
                IslandSize.Spacious => 560.0,
                _ => CustomWidth,
            };
            return Math.Min(
                Math.Max(preferred, CameraWidth + 36),
                ScreenWidth - 24 - IslandLayout.QuickAccessGutter * 2);
        }
    }

    public bool UsesCompactContent => ExpandedWidth < 480;

    public int SystemColumns => ExpandedWidth >= 440 ? 3 : 2;

    public bool HasSideBySideLevels => ExpandedWidth >= 440;

    public int SectionColumns => ExpandedWidth >= 440 ? 4 : 2;

    /// <summary>
    /// The expanded size for a module. Content heights are upstream's; a module this milestone has
    /// not built yet still reports its upstream height so the window never changes size later.
    /// </summary>
    public IslandSizeValue ExpandedSize(
        IslandModule module,
        int controlRows = 2,
        int sliderCount = 2,
        bool controlsHaveMusic = false,
        bool musicHasContent = true,
        int systemRows = 3,
        bool timerHasSession = false,
        bool timerShowsPomodoro = false)
    {
        var contentHeight = module switch
        {
            IslandModule.Controls => ControlsHeight(controlRows, sliderCount, controlsHaveMusic),
            IslandModule.Mixer => 400.0,
            IslandModule.Music => IslandLayout.ChromeHeight + (musicHasContent ? (UsesCompactContent ? 226 : 216) : 132),
            IslandModule.System => IslandLayout.ChromeHeight
                + (systemRows == 0 ? 160 : systemRows * 96 + (systemRows - 1) * 10),
            IslandModule.Files => 336.0,
            IslandModule.Clipboard => 340.0,
            IslandModule.Captures => 340.0,
            IslandModule.Timer => IslandLayout.ChromeHeight
                + (timerHasSession ? (timerShowsPomodoro ? 118 : 96) : (timerShowsPomodoro ? 370 : 202)),
            IslandModule.Camera => IslandLayout.ChromeHeight
                + (ExpandedWidth - IslandLayout.HorizontalInset * 2) * 0.75 + 50,
            _ => 400.0,
        };

        // A spacious layout gives the taller modules extra room, as upstream does.
        var spaciousBonus = Layout == IslandSize.Spacious
            && module is not (IslandModule.Controls or IslandModule.Music or IslandModule.Timer)
            ? 40
            : 0;
        var preferredHeight = SafeContentTop + contentHeight + spaciousBonus;
        if (Layout == IslandSize.Custom)
        {
            var fillsHeight = module is IslandModule.Mixer or IslandModule.Clipboard
                or IslandModule.Captures or IslandModule.Tools;
            preferredHeight = fillsHeight ? CustomHeight : Math.Min(preferredHeight, CustomHeight);
        }
        return new IslandSizeValue(ExpandedWidth, Math.Min(preferredHeight, ScreenHeight - 48));
    }

    private double ControlsHeight(int controlRows, int sliderCount, bool hasMusic)
    {
        var rows = Math.Max(0, controlRows);
        var sliders = Math.Min(2, Math.Max(0, sliderCount));
        var levelRows = HasSideBySideLevels ? Math.Min(1, sliders) : sliders;
        var groups = levelRows + (rows > 0 ? 1 : 0) + (hasMusic ? 1 : 0);
        var controls = (hasMusic ? IslandLayout.MusicControlHeight : 0)
            + levelRows * IslandLayout.ControlHeight
            + rows * IslandLayout.ActionHeight
            + Math.Max(0, rows - 1) * IslandLayout.ActionSpacing
            + Math.Max(0, groups - 1) * 18;
        return IslandLayout.ChromeHeight + (groups == 0 ? 160 : controls);
    }

    /// <summary>
    /// Top-left corner for a size, centered horizontally and flush with the top edge of the
    /// display. Coordinates are relative to the display's own origin.
    /// </summary>
    public (double X, double Y) TopCenterOrigin(IslandSizeValue size) => ((ScreenWidth - size.Width) / 2, 0);
}
