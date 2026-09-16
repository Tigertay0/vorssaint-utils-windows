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
    /// <summary>
    /// Follows the pointer: the island lives on whichever display the pointer is on. Upstream's
    /// automatic means "the screen with the camera cutout", which has no Windows equivalent, so on
    /// Windows the useful automatic behaviour is to follow you between monitors.
    /// </summary>
    Automatic,
    /// <summary>Upstream's built-in screen; sanitized to the primary display on Windows.</summary>
    BuiltIn,
    /// <summary>Stays on the primary display, the one holding the taskbar.</summary>
    Main,
}

/// <summary>
/// Which screen edge the island is attached to. Faqra's own setting: a Mac's notch is always at the
/// top, but a Windows display has no cutout, so the pill can live on any edge.
/// </summary>
public enum IslandEdge
{
    Top, Left, Right,
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
        // Upstream's builtIn means the MacBook screen; on Windows that is just the primary display,
        // which is exactly what main already means, so both pin the island there.
        "main" or "builtIn" => IslandDisplay.Main,
        _ => IslandDisplay.Automatic,
    };

    public static string RawValue(this IslandDisplay display) => display switch
    {
        IslandDisplay.Main => "main",
        IslandDisplay.BuiltIn => "builtIn",
        _ => "automatic",
    };

    public static IslandEdge EdgeFromRawValue(string? raw) => raw switch
    {
        "left" => IslandEdge.Left,
        "right" => IslandEdge.Right,
        _ => IslandEdge.Top,
    };

    public static string RawValue(this IslandEdge edge) => edge.ToString().ToLowerInvariant();

    /// <summary>True when the pill lies along a vertical edge, so its resting shape is rotated.</summary>
    public static bool IsVertical(this IslandEdge edge) => edge is IslandEdge.Left or IslandEdge.Right;

    public static IslandIdleContent IdleContentFromRawValue(string? raw) => raw switch
    {
        "none" => IslandIdleContent.None,
        "battery" => IslandIdleContent.Battery,
        _ => IslandIdleContent.Music,
    };

    public static string RawValue(this IslandIdleContent content) => content.ToString().ToLowerInvariant();
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

/// <summary>
/// How long the silhouette takes to change size, and on which curve. The values come from
/// transitions.dev's own tuning rather than upstream's SwiftUI springs, because a spring tuned for
/// macOS reads as slow here: its card-resize is 300ms, its panel-reveal 400ms open and 350ms close,
/// its menu-dropdown 250ms open and 150ms close, all on one shared ease.
/// </summary>
public static class IslandMotion
{
    /// <summary>transitions.dev's shared ease, cubic-bezier(0.22, 1, 0.36, 1).</summary>
    public static readonly (double X1, double Y1, double X2, double Y2) Ease = (0.22, 1, 0.36, 1);

    /// <summary>Its toggle ease, cubic-bezier(0.34, 1.35, 0.64, 1), which overshoots slightly.</summary>
    public static readonly (double X1, double Y1, double X2, double Y2) OvershootEase = (0.34, 1.35, 0.64, 1);

    /// <summary>card-resize: 300ms. The island growing is a card resizing.</summary>
    public static readonly TimeSpan Grow = TimeSpan.FromMilliseconds(300);

    /// <summary>Closing runs at the dropdown's 60% ratio, so leaving feels immediate.</summary>
    public static readonly TimeSpan Shrink = TimeSpan.FromMilliseconds(180);

    /// <summary>panel-reveal: the expanded content fades and unblurs as the shape opens.</summary>
    public static readonly TimeSpan ContentReveal = TimeSpan.FromMilliseconds(400);

    /// <summary>panel-reveal's close duration, used when one module replaces another.</summary>
    public static readonly TimeSpan ContentFade = TimeSpan.FromMilliseconds(350);

    /// <summary>panel-reveal's cross-blur, in device-independent pixels.</summary>
    public const double RevealBlur = 2;

    /// <summary>panel-reveal's travel, scaled down because the island's content is short.</summary>
    public const double RevealTranslate = 16;

    /// <summary>menu-dropdown: 250ms open, 150ms close, from a 0.97 pre-scale.</summary>
    public static readonly TimeSpan DropdownOpen = TimeSpan.FromMilliseconds(250);

    public static readonly TimeSpan DropdownClose = TimeSpan.FromMilliseconds(150);

    public const double DropdownPreScale = 0.97;

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
        double customHeight = IslandSizes.DefaultHeight,
        IslandEdge edge = IslandEdge.Top)
    {
        ScreenWidth = screenWidth;
        ScreenHeight = screenHeight;
        Layout = layout;
        Edge = edge;
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
    public IslandEdge Edge { get; }
    public double CustomWidth { get; }
    public double CustomHeight { get; }

    /// <summary>Width of the resting pill.</summary>
    public double CameraWidth { get; }

    /// <summary>Height of the resting pill.</summary>
    public double CameraHeight { get; }

    public double BarHeight { get; }

    /// <summary>Where expanded content may start, clear of the resting silhouette.</summary>
    public double SafeContentTop => CameraHeight + 10;

    /// <summary>
    /// The inset the expanded panel leaves for the resting pill. Only the top edge needs it: there
    /// the pill sits above the header, while on a side edge the panel replaces the pill outright.
    /// </summary>
    public double ContentInset => Edge == IslandEdge.Top ? SafeContentTop : 0;

    /// <summary>The resting size while idle content (album art, battery) is showing.</summary>
    public IslandSizeValue Collapsed => Oriented(Math.Min(LongEdge - 24, CameraWidth), BarHeight);

    public IslandSizeValue RestingSize(bool showsContent) =>
        showsContent ? Collapsed : Oriented(CameraWidth, CameraHeight);

    /// <summary>The small state a hover reaches when full expansion is switched off.</summary>
    public IslandSizeValue Peek =>
        Oriented(Math.Min(LongEdge - 24, Math.Max(CameraWidth + 110, 340)), SafeContentTop + 52);

    /// <summary>The screen dimension the pill lies along: width on the top edge, height on a side.</summary>
    private double LongEdge => Edge.IsVertical() ? ScreenHeight : ScreenWidth;

    /// <summary>
    /// Turns a size expressed along the pill's own axis into a screen size. On a side edge the pill
    /// is the same shape rotated a quarter turn.
    /// </summary>
    private IslandSizeValue Oriented(double along, double across) =>
        Edge.IsVertical() ? new IslandSizeValue(across, along) : new IslandSizeValue(along, across);

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
                Math.Max(IslandSizes.MinWidth, ScreenWidth - 24 - IslandLayout.QuickAccessGutter * 2));
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
        var preferredHeight = ContentInset + contentHeight + spaciousBonus;
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
    /// Top-left corner for a size: centred along the pill's edge and flush against it. Coordinates
    /// are relative to the display's own origin.
    /// </summary>
    public (double X, double Y) OriginFor(IslandSizeValue size) => Edge switch
    {
        IslandEdge.Left => (0, (ScreenHeight - size.Height) / 2),
        IslandEdge.Right => (ScreenWidth - size.Width, (ScreenHeight - size.Height) / 2),
        _ => ((ScreenWidth - size.Width) / 2, 0),
    };

    /// <summary>
    /// Which corners are rounded: the ones facing the screen, so the silhouette reads as growing
    /// out of the edge it is attached to. Returns radii in the order top-left, top-right,
    /// bottom-right, bottom-left.
    /// </summary>
    public (double TopLeft, double TopRight, double BottomRight, double BottomLeft) CornersFor(IslandSizeValue size)
    {
        var radius = Math.Min(IslandLayout.Shoulder, Math.Min(size.Width, size.Height) / 2);
        return Edge switch
        {
            IslandEdge.Left => (0, radius, radius, 0),
            IslandEdge.Right => (radius, 0, 0, radius),
            _ => (0, 0, radius, radius),
        };
    }
}
