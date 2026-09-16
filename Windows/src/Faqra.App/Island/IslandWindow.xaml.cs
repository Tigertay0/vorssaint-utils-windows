// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors NotchWindowHost in Sources/Vorssaint/Services/Notch/NotchWindowHost.swift: the shaped,
// non-activating, always-on-top surface. The state machine that drives it is IslandController.
// Motion comes from transitions.dev's card-resize and panel-reveal tokens, not upstream's springs.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Faqra.Core.Island;
using Faqra.Win32.Windows;

namespace Faqra.App.Island;

/// <summary>A window rectangle in physical screen pixels.</summary>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height);

public partial class IslandWindow
{
    private IslandController? _controller;

    /// <summary>Bumped per transition so a finished animation cannot resize a window that moved on.</summary>
    private int _shapeGeneration;

    public IslandWindow()
    {
        InitializeComponent();
    }

    /// <summary>The window handle, valid once the window has been shown.</summary>
    public IntPtr Handle => new WindowInteropHelper(this).Handle;

    internal void Attach(IslandController controller)
    {
        _controller = controller;
        Shape.MouseEnter += (_, _) => controller.PointerEnteredShape();
        Shape.MouseLeave += (_, _) => controller.PointerLeftShape();
        Shape.MouseLeftButtonUp += OnShapeClicked;
        PreviewKeyDown += OnKeyDown;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Applied before the first paint so the island never steals focus, not even once.
        WindowStyles.MakeNonActivatingOverlay(Handle);
    }

    /// <summary>
    /// Moves and resizes the silhouette. The window takes the envelope of both sizes up front so
    /// the shape can grow before the window does and shrink before the window follows, then the
    /// window snaps to the final rect once the transition ends.
    /// </summary>
    internal void ApplyShape(
        IslandSizeValue from,
        IslandSizeValue to,
        ScreenRect envelope,
        ScreenRect final,
        IslandEdge edge,
        (double TopLeft, double TopRight, double BottomRight, double BottomLeft) corners,
        bool animate)
    {
        _shapeGeneration++;
        var generation = _shapeGeneration;

        AlignToEdge(edge);
        Shape.CornerRadius = new CornerRadius(corners.TopLeft, corners.TopRight, corners.BottomRight, corners.BottomLeft);
        // Laying the content out at its final size once keeps the resize to a re-clip per frame.
        ShapeContent.Width = to.Width;
        ShapeContent.Height = to.Height;

        if (!animate || !SystemParameters.ClientAreaAnimation)
        {
            Shape.BeginAnimation(WidthProperty, null);
            Shape.BeginAnimation(HeightProperty, null);
            Shape.Width = to.Width;
            Shape.Height = to.Height;
            SetBounds(final);
            return;
        }

        SetBounds(envelope);

        var duration = IslandMotion.Duration(from, to);
        var width = Motion.Double(from.Width, to.Width, duration);
        width.Completed += (_, _) =>
        {
            // A newer transition may have started; only the latest one owns the window rect.
            if (generation == _shapeGeneration)
            {
                SetBounds(final);
            }
        };
        Shape.BeginAnimation(WidthProperty, width);
        Shape.BeginAnimation(HeightProperty, Motion.Double(from.Height, to.Height, duration));
    }

    private void SetBounds(ScreenRect rect) =>
        WindowStyles.SetBounds(Handle, rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>
    /// The shape sits against its edge inside the window, and the content against the same edge
    /// inside the shape, so a growing silhouette reveals content from the edge it is attached to.
    /// </summary>
    private void AlignToEdge(IslandEdge edge)
    {
        var (horizontal, vertical) = edge switch
        {
            IslandEdge.Left => (HorizontalAlignment.Left, VerticalAlignment.Center),
            IslandEdge.Right => (HorizontalAlignment.Right, VerticalAlignment.Center),
            _ => (HorizontalAlignment.Center, VerticalAlignment.Top),
        };
        Shape.HorizontalAlignment = horizontal;
        Shape.VerticalAlignment = vertical;
        ShapeContent.HorizontalAlignment = horizontal;
        ShapeContent.VerticalAlignment = vertical;
    }

    /// <summary>
    /// Switches between the resting and expanded content. Opening runs transitions.dev's
    /// panel-reveal: the panel travels in, fades up, on the shared ease. Its 2px cross-blur is left
    /// out because this is a layered window, which WPF composites in software, so a per-frame blur
    /// would cost more than it adds.
    /// </summary>
    internal void ShowExpanded(bool expanded, double contentInset, bool animate)
    {
        SafeTopRow.Height = new GridLength(contentInset);
        ExpandedRoot.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        IdleHost.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        if (!expanded)
        {
            return;
        }
        if (!animate || !SystemParameters.ClientAreaAnimation)
        {
            ExpandedRoot.Opacity = 1;
            RevealTransform.Y = 0;
            return;
        }
        ExpandedRoot.BeginAnimation(OpacityProperty, Motion.Double(0, 1, IslandMotion.ContentReveal));
        RevealTransform.BeginAnimation(
            TranslateTransform.YProperty,
            Motion.Double(-IslandMotion.RevealTranslate, 0, IslandMotion.ContentReveal));
    }

    /// <summary>Swaps the module, cross-fading on panel-reveal's close duration.</summary>
    internal void SetModule(string title, UIElement? content, bool animate)
    {
        ModuleTitle.Text = title;
        ModuleHost.Content = content;
        if (animate && SystemParameters.ClientAreaAnimation)
        {
            ModuleHost.BeginAnimation(OpacityProperty, Motion.Double(0, 1, IslandMotion.ContentFade));
        }
    }

    internal void SetIdleContent(UIElement? content) => IdleHost.Content = content;

    internal void SetPinned(bool pinned)
    {
        // Unpin uses the filled pin glyph so the button reads as "currently pinned".
        PinButton.Content = pinned ? "" : "";
    }

    /// <summary>True while the pointer is over the silhouette, checked against the live cursor.</summary>
    internal bool PointerIsOverShape()
    {
        if (!IsLoaded || Shape.ActualWidth <= 0)
        {
            return false;
        }
        var position = Shape.PointFromScreen(CursorPosition());
        return position.X >= 0 && position.Y >= 0 && position.X <= Shape.ActualWidth && position.Y <= Shape.ActualHeight;
    }

    private static Point CursorPosition()
    {
        Faqra.Win32.Native.User32.GetCursorPos(out var point);
        return new Point(point.X, point.Y);
    }

    private void OnShapeClicked(object sender, MouseButtonEventArgs e) => _controller?.ShapeClicked();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_controller is null)
        {
            return;
        }
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        switch (e.Key)
        {
            case Key.Escape:
                _controller.EscapePressed();
                e.Handled = true;
                break;
            case Key.K when ctrl:
                _controller.ToggleSections();
                e.Handled = true;
                break;
            case Key.Tab when ctrl:
                _controller.CycleModule(shift ? -1 : 1);
                e.Handled = true;
                break;
        }
    }

    private void OnSectionsClicked(object sender, RoutedEventArgs e) => _controller?.ToggleSections();

    private void OnPinClicked(object sender, RoutedEventArgs e) => _controller?.TogglePinned();

    private void OnSettingsClicked(object sender, RoutedEventArgs e) =>
        App.ShowSettings(Core.Settings.SettingsPage.Notch);

    private void OnCollapseClicked(object sender, RoutedEventArgs e) => _controller?.Collapse();
}
