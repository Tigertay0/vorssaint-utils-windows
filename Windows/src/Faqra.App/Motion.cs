// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// The motion vocabulary, built from transitions.dev's published tokens. WPF has no cubic-bezier
// easing, so CubicBezierEase solves the CSS curve directly and every animation reads from here.

using System.Windows;
using System.Windows.Media.Animation;

namespace Faqra.App;

/// <summary>
/// A CSS <c>cubic-bezier(x1, y1, x2, y2)</c> curve as a WPF easing function. The X axis is solved
/// with Newton-Raphson, falling back to bisection, which is how browsers evaluate the same curve.
/// </summary>
public sealed class CubicBezierEase : EasingFunctionBase
{
    private const int NewtonIterations = 8;
    private const double NewtonEpsilon = 1e-6;

    public static readonly DependencyProperty X1Property =
        DependencyProperty.Register(nameof(X1), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(0.0));

    public static readonly DependencyProperty Y1Property =
        DependencyProperty.Register(nameof(Y1), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(0.0));

    public static readonly DependencyProperty X2Property =
        DependencyProperty.Register(nameof(X2), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(1.0));

    public static readonly DependencyProperty Y2Property =
        DependencyProperty.Register(nameof(Y2), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(1.0));

    public CubicBezierEase()
    {
    }

    public CubicBezierEase((double X1, double Y1, double X2, double Y2) curve)
    {
        X1 = curve.X1;
        Y1 = curve.Y1;
        X2 = curve.X2;
        Y2 = curve.Y2;
        // The curve already describes its whole shape. EaseIn is WPF's pass-through mode: EaseOut
        // would evaluate 1 - EaseInCore(1 - t) and apply the curve a second time, mirrored.
        EasingMode = EasingMode.EaseIn;
    }

    public double X1 { get => (double)GetValue(X1Property); set => SetValue(X1Property, value); }

    public double Y1 { get => (double)GetValue(Y1Property); set => SetValue(Y1Property, value); }

    public double X2 { get => (double)GetValue(X2Property); set => SetValue(X2Property, value); }

    public double Y2 { get => (double)GetValue(Y2Property); set => SetValue(Y2Property, value); }

    protected override double EaseInCore(double normalizedTime) => Solve(normalizedTime);

    protected override Freezable CreateInstanceCore() => new CubicBezierEase();

    private double Solve(double x)
    {
        if (x <= 0)
        {
            return 0;
        }
        if (x >= 1)
        {
            return 1;
        }
        var t = SolveForT(x);
        return Bezier(t, Y1, Y2);
    }

    private double SolveForT(double x)
    {
        var t = x;
        for (var i = 0; i < NewtonIterations; i++)
        {
            var error = Bezier(t, X1, X2) - x;
            if (Math.Abs(error) < NewtonEpsilon)
            {
                return t;
            }
            var slope = Slope(t, X1, X2);
            if (Math.Abs(slope) < NewtonEpsilon)
            {
                break;
            }
            t -= error / slope;
        }

        // Newton stalled on a flat stretch; bisection always converges.
        double low = 0, high = 1;
        t = x;
        while (high - low > NewtonEpsilon)
        {
            if (Bezier(t, X1, X2) < x)
            {
                low = t;
            }
            else
            {
                high = t;
            }
            t = (low + high) / 2;
        }
        return t;
    }

    /// <summary>The cubic Bezier with endpoints pinned at 0 and 1, in polynomial form.</summary>
    private static double Bezier(double t, double a, double b) =>
        (((1 - 3 * b + 3 * a) * t + (3 * b - 6 * a)) * t + 3 * a) * t;

    private static double Slope(double t, double a, double b) =>
        3 * (1 - 3 * b + 3 * a) * t * t + 2 * (3 * b - 6 * a) * t + 3 * a;
}

/// <summary>Ready-made animations on the project's motion tokens.</summary>
public static class Motion
{
    /// <summary>transitions.dev's shared ease, used by card-resize, panel-reveal and menu-dropdown.</summary>
    public static IEasingFunction Ease() => Frozen(new CubicBezierEase(Core.Island.IslandMotion.Ease));

    /// <summary>Its toggle ease, which overshoots slightly.</summary>
    public static IEasingFunction Overshoot() => Frozen(new CubicBezierEase(Core.Island.IslandMotion.OvershootEase));

    public static DoubleAnimation Double(double from, double to, TimeSpan duration, IEasingFunction? easing = null) =>
        new(from, to, new Duration(duration))
        {
            EasingFunction = easing ?? Ease(),
            FillBehavior = FillBehavior.HoldEnd,
        };

    private static IEasingFunction Frozen(CubicBezierEase ease)
    {
        ease.Freeze();
        return ease;
    }
}
