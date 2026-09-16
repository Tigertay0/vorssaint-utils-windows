// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the music module of Sources/Vorssaint/UI/Notch: artwork, track, transport. Upstream reads
// MediaRemote through a signed-binary workaround; Windows exposes the same session publicly.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Faqra.Services.Media;

namespace Faqra.App.Island.Modules;

/// <summary>Now playing with transport controls, driven by the system media session.</summary>
public sealed class MusicModule : UserControl
{
    private readonly NowPlayingService _service;
    private readonly Border _art = new()
    {
        Width = 88,
        Height = 88,
        CornerRadius = new CornerRadius(10),
        Background = IslandPalette.Fill,
        HorizontalAlignment = HorizontalAlignment.Center,
    };
    private readonly TextBlock _title = Text(14, FontWeights.SemiBold, IslandPalette.Primary);
    private readonly TextBlock _artist = Text(12, FontWeights.Normal, IslandPalette.Secondary);
    private readonly Button _playPause;

    public MusicModule(NowPlayingService service)
    {
        _service = service;
        _playPause = TransportButton("", 40, OnPlayPause);

        var transport = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0),
        };
        transport.Children.Add(TransportButton("", 32, async (_, _) => await _service.PreviousAsync()));
        transport.Children.Add(_playPause);
        transport.Children.Add(TransportButton("", 32, async (_, _) => await _service.NextAsync()));

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(_art);
        _title.Margin = new Thickness(0, 12, 0, 0);
        _title.TextAlignment = TextAlignment.Center;
        _artist.TextAlignment = TextAlignment.Center;
        _artist.Margin = new Thickness(0, 2, 0, 0);
        stack.Children.Add(_title);
        stack.Children.Add(_artist);
        stack.Children.Add(transport);
        Content = stack;

        Render();
        _service.Changed += OnChanged;
        Unloaded += (_, _) => _service.Changed -= OnChanged;
    }

    private void OnChanged() => Dispatcher.BeginInvoke(Render);

    private void Render()
    {
        var track = _service.Current;
        _title.Text = track.HasTrack ? track.Title : "Nothing playing";
        _artist.Text = track.Artist;
        _artist.Visibility = string.IsNullOrWhiteSpace(track.Artist) ? Visibility.Collapsed : Visibility.Visible;
        _playPause.Content = track.IsPlaying ? "" : "";
        if (IdleMusicView.Artwork(track.Artwork) is { } art)
        {
            _art.Background = new ImageBrush(art) { Stretch = Stretch.UniformToFill };
        }
        else
        {
            _art.Background = IslandPalette.Fill;
        }
    }

    private async void OnPlayPause(object? sender, RoutedEventArgs e) => await _service.TogglePlayPauseAsync();

    private static TextBlock Text(double size, FontWeight weight, Brush color) => new()
    {
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
        FontSize = size,
        FontWeight = weight,
        Foreground = color,
        TextTrimming = TextTrimming.CharacterEllipsis,
        MaxWidth = 460,
    };

    private static Button TransportButton(string glyph, double size, RoutedEventHandler onClick)
    {
        var button = new Button
        {
            Content = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = size / 2.4,
            Width = size,
            Height = size,
            Margin = new Thickness(6, 0, 6, 0),
            Foreground = IslandPalette.Primary,
            Background = IslandPalette.Fill,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Template = RoundButtonTemplate(size / 2),
        };
        button.Click += onClick;
        return button;
    }

    /// <summary>A circular button that presses in, per the motion rules for pressables.</summary>
    internal static ControlTemplate RoundButtonTemplate(double radius)
    {
        var border = new FrameworkElementFactory(typeof(Border), "Chrome");
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        border.SetValue(Border.SnapsToDevicePixelsProperty, true);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, IslandPalette.FillStrong, "Chrome"));
        template.Triggers.Add(hover);
        var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(RenderTransformProperty, new ScaleTransform(0.97, 0.97), "Chrome"));
        pressed.Setters.Add(new Setter(RenderTransformOriginProperty, new Point(0.5, 0.5), "Chrome"));
        template.Triggers.Add(pressed);
        template.Seal();
        return template;
    }
}
