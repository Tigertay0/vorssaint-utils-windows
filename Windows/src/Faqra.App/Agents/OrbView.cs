// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Draws Thinking Orbs (https://github.com/yogesharc/thinking-orbs, MIT, Copyright (c) 2026 Yogesh) in WPF.
// The original draws SVG circles each animation frame; this draws the same dots in OnRender.

using System.Windows;
using System.Windows.Media;
using Faqra.Core.Agents;
using Faqra.Core.Agents.Orb;

namespace Faqra.App.Agents;

/// <summary>
/// A thinking orb that keeps moving while it is on screen. With Windows' animations off it holds one
/// frame, and the status text beside it carries the change.
/// </summary>
public sealed class OrbView : FrameworkElement
{
    /// <summary>The frame shown when animations are off: far enough in that every look shows its motif.</summary>
    public const double StillTime = 1500;

    /// <summary>A frame's gap is capped, so an orb coming back on screen carries on instead of jumping.</summary>
    private const double MaxStepMs = 100;

    public static readonly DependencyProperty LookProperty = DependencyProperty.Register(
        nameof(Look), typeof(OrbLook), typeof(OrbView), new FrameworkPropertyMetadata(OrbLook.Base, OnShapeChanged));

    public static readonly DependencyProperty DiameterProperty = DependencyProperty.Register(
        nameof(Diameter), typeof(double), typeof(OrbView),
        new FrameworkPropertyMetadata(20.0, FrameworkPropertyMetadataOptions.AffectsMeasure, OnShapeChanged));

    public static readonly DependencyProperty InkProperty = DependencyProperty.Register(
        nameof(Ink), typeof(Brush), typeof(OrbView), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SpeedProperty = DependencyProperty.Register(
        nameof(Speed), typeof(double), typeof(OrbView), new FrameworkPropertyMetadata(1.0));

    private OrbModel _model = new(OrbLook.Base, 20);
    private double _t = AnimationsEnabled ? 0 : StillTime;
    private TimeSpan? _last;
    private bool _ticking;

    public OrbView()
    {
        Loaded += (_, _) => UpdateTicking();
        Unloaded += (_, _) => StopTicking();
        IsVisibleChanged += (_, _) => UpdateTicking();
    }

    public OrbLook Look
    {
        get => (OrbLook)GetValue(LookProperty);
        set => SetValue(LookProperty, value);
    }

    public double Diameter
    {
        get => (double)GetValue(DiameterProperty);
        set => SetValue(DiameterProperty, value);
    }

    public Brush Ink
    {
        get => (Brush)GetValue(InkProperty);
        set => SetValue(InkProperty, value);
    }

    public double Speed
    {
        get => (double)GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    /// <summary>Windows reports reduced motion by turning client-area animations off.</summary>
    private static bool AnimationsEnabled => SystemParameters.ClientAreaAnimation;

    /// <summary>Look, speed and ink in one go. Setting the same look again keeps the orb's motion going.</summary>
    public void Apply(AgentOrbStyle style, Brush ink)
    {
        Look = style.Look;
        Speed = style.Speed;
        Ink = ink;
    }

    protected override Size MeasureOverride(Size availableSize) => new(Diameter, Diameter);

    protected override void OnRender(DrawingContext drawingContext)
    {
        var ink = Ink;
        foreach (var dot in _model.Frame(_t))
        {
            if (dot.Opacity < OrbModel.HiddenOpacity)
            {
                continue;
            }
            var radius = Math.Max(OrbModel.MinRadius, dot.Radius);
            drawingContext.PushOpacity(Math.Min(1, dot.Opacity));
            drawingContext.DrawEllipse(ink, null, new Point(dot.X, dot.Y), radius, radius);
            drawingContext.Pop();
        }
    }

    private static void OnShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (OrbView)d;
        view._model = new OrbModel(view.Look, view.Diameter);
        view._t = AnimationsEnabled ? 0 : StillTime;
        view._last = null;
        view.InvalidateVisual();
    }

    private void UpdateTicking()
    {
        if (IsLoaded && IsVisible && AnimationsEnabled)
        {
            StartTicking();
        }
        else
        {
            StopTicking();
        }
    }

    private void StartTicking()
    {
        if (_ticking)
        {
            return;
        }
        _ticking = true;
        _last = null;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopTicking()
    {
        if (!_ticking)
        {
            return;
        }
        _ticking = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (_last is { } last && now > last)
        {
            _t += Math.Min((now - last).TotalMilliseconds, MaxStepMs) * Speed;
        }
        _last = now;
        InvalidateVisual();
    }
}
