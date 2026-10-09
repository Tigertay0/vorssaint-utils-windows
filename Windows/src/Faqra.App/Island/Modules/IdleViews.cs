// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the idle compact content of Sources/Vorssaint/UI/Notch/NotchView.swift (lines 36-85):
// album art with a level meter, or a battery reading, inside the resting pill.

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Faqra.App.Agents;
using Faqra.Core.Agents;
using Faqra.Core.Localization;
using Faqra.Services.Media;
using Faqra.Win32.Power;

namespace Faqra.App.Island.Modules;

/// <summary>The resting pill while music plays: artwork on the left, three animated bars on the right.</summary>
public sealed class IdleMusicView : UserControl
{
    public IdleMusicView(NowPlaying track)
    {
        Margin = new Thickness(6, 0, 8, 0);
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        if (Artwork(track.Artwork) is { } art)
        {
            row.Children.Add(new Border
            {
                Width = 16,
                Height = 16,
                CornerRadius = new CornerRadius(4),
                Background = new ImageBrush(art) { Stretch = Stretch.UniformToFill },
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        var bars = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Height = 14,
        };
        for (var i = 0; i < 3; i++)
        {
            bars.Children.Add(Bar(i, track.IsPlaying));
        }
        row.Children.Add(bars);
        Content = row;
        ToolTip = track.HasTrack ? $"{track.Title}\n{track.Artist}" : null;
    }

    private static FrameworkElement Bar(int index, bool playing)
    {
        var bar = new Border
        {
            Width = 2.5,
            Height = playing ? 5 : 3,
            Margin = new Thickness(index == 0 ? 0 : 2, 0, 0, 0),
            CornerRadius = new CornerRadius(1.5),
            Background = IslandPalette.Primary,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        if (!playing || !SystemParameters.ClientAreaAnimation)
        {
            return bar;
        }
        // A level meter is continuous motion, so it runs linear and loops.
        var animation = new DoubleAnimation(4, 13, new Duration(TimeSpan.FromMilliseconds(420 + index * 110)))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        bar.BeginAnimation(HeightProperty, animation);
        return bar;
    }

    internal static BitmapImage? Artwork(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException)
        {
            return null;
        }
    }
}

/// <summary>The resting pill showing the battery, for a laptop or a desktop with a UPS.</summary>
public sealed class IdleBatteryView : UserControl
{
    public IdleBatteryView()
    {
        Margin = new Thickness(8, 0, 8, 0);
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var status = Power.BatteryStatus();
        row.Children.Add(new TextBlock
        {
            Text = status.Charging ? "" : "",
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 12,
            Foreground = IslandPalette.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
        });
        row.Children.Add(new TextBlock
        {
            Text = status.Percent >= 0 ? $"{status.Percent}%" : "--",
            Margin = new Thickness(5, 0, 0, 0),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 11,
            Foreground = IslandPalette.Primary,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Content = row;
    }
}

/// <summary>A module whose feature has not been ported yet. It names what will live here.</summary>
public sealed class ModulePlaceholder : UserControl
{
    public ModulePlaceholder(string title)
    {
        Content = new TextBlock
        {
            Text = Core.Localization.MonitorStrings.For(Core.Localization.L10n.Shared.Language).ComingLater,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 12,
            Foreground = IslandPalette.Tertiary,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };
    }
}

/// <summary>The resting pill while an agent is busy or waiting: its orb and its state, most urgent session first.</summary>
public sealed class IdleAgentsView : UserControl
{
    public IdleAgentsView(AgentSession session)
    {
        Margin = new Thickness(6, 0, 8, 0);
        var style = AgentOrbStyles.For(session.State);
        var orb = new OrbView { Diameter = 18, VerticalAlignment = VerticalAlignment.Center };
        orb.Apply(style, AgentInk.For(style.Tone));
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(orb);
        row.Children.Add(new TextBlock
        {
            Text = AgentsText.State(session, AgentsStrings.For(L10n.Shared.Language)),
            Margin = new Thickness(6, 0, 0, 0),
            MaxWidth = 100,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 11,
            Foreground = IslandPalette.Primary,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Content = row;
    }
}
