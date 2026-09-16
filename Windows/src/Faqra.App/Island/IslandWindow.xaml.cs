// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors NotchWindowHost in Sources/Vorssaint/Services/Notch/NotchWindowHost.swift: the shaped,
// non-activating, always-on-top surface. The state machine that drives it is IslandController.

using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Faqra.Core.Island;
using Faqra.Win32.Windows;

namespace Faqra.App.Island;

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
    /// Sets the window rect to the motion envelope and animates the silhouette inside it, so the
    /// shape can grow before the window does and shrink before the window follows.
    /// </summary>
    internal void ApplyShape(IslandSizeValue from, IslandSizeValue to, double scale, double originX, double screenTop, bool animate)
    {
        _shapeGeneration++;
        var generation = _shapeGeneration;
        var radius = Math.Min(IslandLayout.Shoulder, to.Height / 2);
        Shape.CornerRadius = new CornerRadius(0, 0, radius, radius);

        void SetWindow(IslandSizeValue size) => WindowStyles.SetBounds(
            Handle,
            // The shape is centered in the window, so the window is centered on the same point and
            // the silhouette never shifts sideways as it resizes.
            (int)Math.Round(originX - (size.Width - to.Width) / 2 * scale),
            (int)Math.Round(screenTop),
            (int)Math.Round(size.Width * scale),
            (int)Math.Round(size.Height * scale));

        if (!animate || !SystemParameters.ClientAreaAnimation)
        {
            Shape.BeginAnimation(WidthProperty, null);
            Shape.BeginAnimation(HeightProperty, null);
            Shape.Width = to.Width;
            Shape.Height = to.Height;
            SetWindow(to);
            return;
        }

        // Backing space for both ends: the shape grows before the window has to, and shrinks
        // before the window follows it down.
        SetWindow(new IslandSizeValue(Math.Max(from.Width, to.Width), Math.Max(from.Height, to.Height)));

        var duration = IslandMotion.Duration(from, to);
        var grows = duration == IslandMotion.Grow;
        // Entering and exiting both ease out; a spring-like overshoot only on the way open.
        IEasingFunction easing = grows
            ? new BackEase { Amplitude = 0.18, EasingMode = EasingMode.EaseOut }
            : new CubicEase { EasingMode = EasingMode.EaseOut };

        var width = Animate(from.Width, to.Width, duration, easing);
        width.Completed += (_, _) =>
        {
            // A newer transition may have started; only the latest one owns the window rect.
            if (generation == _shapeGeneration)
            {
                SetWindow(to);
            }
        };
        Shape.BeginAnimation(WidthProperty, width);
        Shape.BeginAnimation(HeightProperty, Animate(from.Height, to.Height, duration, easing));
    }

    private static DoubleAnimation Animate(double from, double to, TimeSpan duration, IEasingFunction easing) =>
        new(from, to, new Duration(duration)) { EasingFunction = easing, FillBehavior = FillBehavior.HoldEnd };

    internal void ShowExpanded(bool expanded, double safeContentTop)
    {
        SafeTopRow.Height = new GridLength(safeContentTop);
        ExpandedRoot.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        IdleHost.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        if (expanded)
        {
            // A cross-fade rather than a pop: the shape is already moving underneath.
            ExpandedRoot.BeginAnimation(OpacityProperty,
                Animate(0, 1, IslandMotion.ContentFade, new CubicEase { EasingMode = EasingMode.EaseOut }));
        }
    }

    internal void SetModule(string title, UIElement? content)
    {
        ModuleTitle.Text = title;
        ModuleHost.Content = content;
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
        App.ShowSettings(Core.Settings.SettingsPage.Features);

    private void OnCollapseClicked(object sender, RoutedEventArgs e) => _controller?.Collapse();
}
