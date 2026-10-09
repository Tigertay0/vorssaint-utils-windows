// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Ported from orb-core.ts in Thinking Orbs, https://github.com/yogesharc/thinking-orbs (MIT License,
// Copyright (c) 2026 Yogesh). The math is the original's; only the SVG output is replaced by OrbDot.

namespace Faqra.Core.Agents.Orb;

/// <summary>One look per thing an agent does, with Thinking Orbs' variants.</summary>
public enum OrbLook
{
    Base, Working, WorkingGyro, Reasoning, ReasoningTwins, Searching, SearchingLighthouse,
    Background, BackgroundSpiral, Retrying, RetryingSurge, Compacting, CompactingSqueeze, CompactingFuse, Waiting,
}

/// <summary>A dot where it lands in px, its radius in px and its opacity from 0 to 1.</summary>
public readonly record struct OrbDot(double X, double Y, double Radius, double Opacity);

public static class OrbLooks
{
    /// <summary>Thinking Orbs' state and variant names. Unknown states fall back to base, unknown variants to the state's default.</summary>
    public static OrbLook FromNames(string state, string? variant) => (state, variant) switch
    {
        ("working", "gyro") => OrbLook.WorkingGyro,
        ("working", _) => OrbLook.Working,
        ("reasoning", "twins") => OrbLook.ReasoningTwins,
        ("reasoning", _) => OrbLook.Reasoning,
        ("searching", "lighthouse") => OrbLook.SearchingLighthouse,
        ("searching", _) => OrbLook.Searching,
        ("background", "spiral") => OrbLook.BackgroundSpiral,
        ("background", _) => OrbLook.Background,
        ("retrying", "surge") => OrbLook.RetryingSurge,
        ("retrying", _) => OrbLook.Retrying,
        ("compacting", "squeeze") => OrbLook.CompactingSqueeze,
        ("compacting", "fuse") => OrbLook.CompactingFuse,
        ("compacting", _) => OrbLook.Compacting,
        ("waiting", _) => OrbLook.Waiting,
        _ => OrbLook.Base,
    };
}

/// <summary>
/// The dots of one orb. <see cref="Frame"/> places every dot at time t, in ms on the look's own clock.
/// Reasoning remembers its walk between frames, so frames must come in increasing t, as a display does.
/// </summary>
public sealed class OrbModel
{
    /// <summary>No dot is drawn smaller than this, or it renders as a grey haze.</summary>
    public const double MinRadius = 0.45;

    /// <summary>Dots fainter than this round to nothing in the original and are skipped.</summary>
    public const double HiddenOpacity = 0.005;

    private const double Tau = Math.PI * 2;
    private const double Twist = 1.4;
    private const double Trail = Math.PI / 2;
    private const double LensMs = 1800, Move = 0.4, LensRadius = 0.6;
    private const int Hop = 220, Tail = 5, Reach = 24, WalkLength = 16;
    private static readonly double Golden = Math.PI * (3 - Math.Sqrt(5));
    private static readonly double Lean = 30 * Math.PI / 180;
    private static readonly double[] RingAxis = MakeRingAxis();

    private readonly double[][] _pts;
    private readonly int[][] _near;
    private readonly List<int>[] _walks;
    private readonly OrbDot[] _dots;
    private readonly bool _reasoning;
    private readonly bool _compacting;
    private readonly double _c, _radius, _rs, _tilt;
    private int _hops;

    public OrbModel(OrbLook look, double size, double density = 1, double dotSize = 1, double tilt = 20)
    {
        Look = look;
        Size = size;
        _tilt = tilt;
        // Background's orb has a quarter of the dots, so each comes out twice the size.
        var dens = look == OrbLook.Background ? 1 : 4;
        var count = Math.Max(8, (int)Math.Floor(size * dens * density + 0.5));
        _c = size / 2;
        _radius = _c * 0.8;
        _rs = Math.Pow(size / 64, 0.6) * (0.72 * Math.Sqrt(4.0 / dens)) * dotSize;
        _pts = look == OrbLook.BackgroundSpiral ? Arms(count) : Sphere(count);
        _dots = new OrbDot[_pts.Length];
        _reasoning = (look is OrbLook.Reasoning or OrbLook.ReasoningTwins) && _pts.Length > 1;
        _compacting = look is OrbLook.Compacting or OrbLook.CompactingFuse;
        _near = _reasoning ? Enumerable.Range(0, _pts.Length).Select(i => Nearest(_pts, i, Reach)).ToArray() : [];
        _walks = Enumerable.Range(0, look == OrbLook.ReasoningTwins ? 2 : 1).Select(_ => new List<int>()).ToArray();
    }

    public OrbLook Look { get; }

    public double Size { get; }

    public int Count => _pts.Length;

    public IReadOnlyList<OrbDot> Frame(double t)
    {
        var period = Period(Look);
        var yaw = YawOf(Look, t);
        double? gyro = Look == OrbLook.WorkingGyro ? t / 5000 * Tau : null;
        var pitch = (_tilt + (gyro is { } g0 ? 10 * Math.Cos(g0) : 0)) * Math.PI / 180;
        var roll = gyro is { } g1 ? 12 * Math.Sin(g1) * Math.PI / 180 : 0;
        double sr = Math.Sin(roll), cr = Math.Cos(roll);
        double sy = Math.Sin(yaw), cy = Math.Cos(yaw), st = Math.Sin(pitch), ct = Math.Cos(pitch);

        double Facing(int k) => _pts[k][1] * st + (-_pts[k][0] * sy + _pts[k][2] * cy) * ct;

        var lit = _reasoning ? Wander(t, Facing) : null;

        // Waiting: a comet's head laps every 2s on screen, spiralling from 70 degrees north to 70 south every 6s.
        var head = t / 2000 * Tau + yaw;
        var ahead = Tau / 2000 + Tau / period;
        static double HeadLat(double tt) => 70 * Math.PI / 180 * (1 - 2 * (tt % 6000 / 6000));
        var glow = Math.Min(1, 3 * Math.Sin(Math.PI * (t % 6000 / 6000)));
        var lens = Look == OrbLook.Searching ? LensAt(t) : null;
        var tw = Look == OrbLook.WorkingGyro ? 0.5 * Math.Sin(t / 2600 * Tau)
            : Look == OrbLook.CompactingSqueeze ? Math.Sin(t / 2600 * Tau) : 0;

        // Compacting: a line crosses over 2s packing what it passed, then all lets go over 0.8s.
        double sweepAt = 0, sweepHold = 0;
        if (_compacting)
        {
            var u = t % 2800 / 2800;
            var s = (u - 0.7) / 0.3;
            var release = Look == OrbLook.CompactingFuse
                ? Math.Pow(1 - s, 3)
                : s < 0.45 ? 1 - 1.25 * Ease(s / 0.45)
                : s < 0.7 ? -0.25 * Math.Pow(1 - (s - 0.45) / 0.25, 2)
                : 0;
            sweepAt = -1.15 + 2.3 * Math.Min(1, u / 0.7);
            sweepHold = u < 0.7 ? 1 : release;
        }

        for (var i = 0; i < _pts.Length; i++)
        {
            double x = _pts[i][0], y = _pts[i][1], z = _pts[i][2];
            // Twist turns each dot by an angle that grows with its height: top and bottom opposite ways.
            var turn = yaw + Twist * tw * y;
            double ly = tw != 0 ? Math.Sin(turn) : sy, lc = tw != 0 ? Math.Cos(turn) : cy;
            var z1 = -x * ly + z * lc;
            double vx = x * lc + z * ly, vy = y * ct - z1 * st;
            var vz = y * st + z1 * ct;
            var d = (vz + 1) / 2;
            var r = (0.5 + 1.4 * d) * _rs;
            // Opacity ramps from 0 just behind the rim to 1 at the front, so the back fades instead of hazing.
            var a = Math.Max(0, (d - 0.3) / 0.7);

            if (lens is not null)
            {
                var ang = Math.Acos(Math.Min(1, (vx * lens[0] + vy * lens[1] + vz * lens[2]) / Math.Sqrt(vx * vx + vy * vy + vz * vz)));
                var w = ang < LensRadius ? Math.Pow(1 - Math.Pow(ang / LensRadius, 2), 2) : 0;
                a *= 1 - 0.55 * (1 - w);
                if (Size <= 24)
                {
                    r *= 1 + 0.5 * w;
                }
                if (w != 0)
                {
                    vx *= 1 + 0.12 * w;
                    vy *= 1 + 0.12 * w;
                    vx += (vx - lens[0]) * 0.35 * w;
                    vy += (vy - lens[1]) * 0.35 * w;
                    r *= 1 + 0.9 * w;
                    a += (1 - a) * w;
                }
            }
            if (_compacting)
            {
                var q = vx;
                var w = Math.Min(1, Math.Max(0, (sweepAt - q) / 0.2)) * sweepHold;
                var k = Look == OrbLook.Compacting ? 1.5 : 1;
                vx *= 1 - 0.2 * k * w;
                vy *= 1 - 0.2 * k * w;
                r *= 1 - 0.3 * k * w;
                if (Look == OrbLook.CompactingFuse)
                {
                    a *= 1 - 0.5 * w;
                    var g = Math.Exp(-Math.Pow((q - sweepAt) / 0.08, 2)) * Math.Max(0, sweepHold) * Math.Min(1, d / 0.5);
                    r *= 1 + 0.8 * g;
                    a += (1 - a) * g;
                }
            }
            if (Look == OrbLook.Working)
            {
                // Every 1.7s a ring of light runs down the sphere over 1.2s; what it passed pulls in, then lets go.
                var u = t % 1700;
                var at = 1.3 - 2.6 * Ease(Math.Min(1, u / 1200));
                var q = vx * RingAxis[0] + vy * RingAxis[1] + vz * RingAxis[2];
                var g = u < 1200 ? Math.Exp(-Math.Pow((q - at) / 0.2, 2)) : 0;
                r *= 1 + 0.6 * g;
                a += (1 - a) * g;
                var w = Math.Min(1, Math.Max(0, (q - at) / 0.2)) * (u < 1200 ? 1 : 1 - Math.Min(1, 1.6 * ((u - 1200) / 800)));
                vx *= 1 - 0.08 * w;
                vy *= 1 - 0.08 * w;
                r *= 1 - 0.15 * w;
            }
            if (Look == OrbLook.SearchingLighthouse)
            {
                // A beam leaning 30 degrees right laps every 2.5s with a trail dying out 90 degrees behind it.
                a *= 0.5;
                double lr = Math.Sin(Lean), lcos = Math.Cos(Lean);
                var across = vx * lcos - vy * lr;
                var toward = -st * (vx * lr + vy * lcos) + ct * vz;
                var off = Math.Atan2(across, toward) - (t / 2500 % 1 * Tau - Math.PI);
                var dphi = ((off + Math.PI) % Tau + Tau) % Tau - Math.PI;
                var beam = dphi < 0 ? Math.Max(0, 1 + dphi / Trail) : Math.Exp(-Math.Pow(dphi / 0.45, 2));
                var g = beam * (0.25 + 0.75 * Math.Min(1, Math.Max(0, (d - 0.4) / 0.3)));
                r *= 1 + 0.6 * g;
                a += (1 - a) * g;
            }
            if (lit is not null)
            {
                // The sphere rests at 50%, so only the walk reaches full.
                a *= 0.5;
                if (lit.TryGetValue(i, out var spark) && spark != 0)
                {
                    r *= 1 + 0.8 * spark;
                    a += (1 - a) * spark;
                }
            }
            if (Look == OrbLook.Waiting)
            {
                var hyp = Math.Sqrt(x * x + y * y + z * z);
                var lat = Math.Asin(y / (hyp == 0 ? 1 : hyp));
                var lon = Math.Atan2(z, x);
                var off = Math.Atan2(Math.Sin(lon - head), Math.Cos(lon - head));
                var along = off * Math.Cos(lat);
                var g = Math.Exp(-Math.Pow((lat - HeadLat(t + off / ahead)) / 0.28, 2))
                    * Math.Exp(-Math.Pow(along / (off * ahead > 0 ? 0.12 : 1), 2));
                var w = glow * Math.Pow(g, 0.6);
                a = a * 0.5 + (1 - a * 0.5) * w;
                r *= 1 + 1.1 * w;
            }
            if (roll != 0)
            {
                (vx, vy) = (vx * cr - vy * sr, vx * sr + vy * cr);
            }
            _dots[i] = new OrbDot(_c + vx * _radius, _c - vy * _radius, r, a);
        }
        return _dots;
    }

    /// <summary>Reasoning's walk: a hop every 220 ms to a nearby dot facing you, the last five lit and fading.</summary>
    private Dictionary<int, double> Wander(double t, Func<int, double> facing)
    {
        var s = t / Hop;
        var n = (int)Math.Floor(s);
        var f = s - n;
        if (_walks[0].Count == 0)
        {
            var first = 0;
            for (var k = 1; k < _pts.Length; k++)
            {
                if (facing(k) > facing(first))
                {
                    first = k;
                }
            }
            double Apart(int k) => facing(k) + Dist2(_pts[k], _pts[first]);
            for (var w = 0; w < _walks.Length; w++)
            {
                if (w == 0)
                {
                    _walks[w].Add(first);
                    continue;
                }
                var best = 0;
                for (var k = 1; k < _pts.Length; k++)
                {
                    if (Apart(k) > Apart(best))
                    {
                        best = k;
                    }
                }
                _walks[w].Add(best);
            }
        }
        _hops = Math.Max(_hops, n - WalkLength);
        for (; _hops < n; _hops++)
        {
            for (var w = 0; w < _walks.Length; w++)
            {
                var walk = _walks[w];
                var recent = walk.Skip(Math.Max(0, walk.Count - 8)).ToList();
                var from = walk[^1];
                int? other = Look == OrbLook.ReasoningTwins ? _walks[1 - w][^1] : null;
                var best = -1;
                var score = double.NegativeInfinity;
                var near = _near[from];
                for (var j = 0; j < near.Length; j++)
                {
                    var k = near[j];
                    var apart = other is { } o ? 1.2 * Math.Min(Math.Sqrt(Dist2(_pts[k], _pts[o])), 0.8) : 0;
                    var sc = facing(k) + 0.35 * Hash(_hops * 31 + j + w * 977) + apart;
                    if (!recent.Contains(k) && sc > score)
                    {
                        score = sc;
                        best = k;
                    }
                }
                walk.Add(best < 0 ? near[0] : best);
                if (walk.Count > WalkLength)
                {
                    walk.RemoveAt(0);
                }
            }
        }
        var lit = new Dictionary<int, double>();
        foreach (var walk in _walks)
        {
            for (var j = Tail - 1; j >= 0; j--)
            {
                var index = walk.Count - 1 - j;
                if (index < 0)
                {
                    continue;
                }
                var k = walk[index];
                var v = j == 0 ? Ease(Math.Min(1, f * 2)) : 1 - (j - 1 + f) / Tail;
                lit[k] = Math.Max(lit.TryGetValue(k, out var had) ? had : 0, v);
            }
        }
        return lit;
    }

    private static double Ease(double x) => (1 - Math.Cos(Math.PI * x)) / 2;

    private static double Hash(double n)
    {
        var s = Math.Sin(n * 127.1) * 43758.5453;
        return s - Math.Floor(s);
    }

    private static double Dist2(double[] a, double[] b) =>
        (a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]) + (a[2] - b[2]) * (a[2] - b[2]);

    /// <summary>The k points nearest pts[i], nearest first, keeping a short sorted list as the original does.</summary>
    internal static int[] Nearest(double[][] pts, int i, int k)
    {
        var idx = new int[k];
        var d = new double[k];
        var n = 0;
        for (var j = 0; j < pts.Length; j++)
        {
            if (j == i)
            {
                continue;
            }
            var e = Dist2(pts[i], pts[j]);
            var at = n;
            if (at == k)
            {
                if (e >= d[k - 1])
                {
                    continue;
                }
                at--;
            }
            else
            {
                n++;
            }
            while (at > 0 && d[at - 1] > e)
            {
                idx[at] = idx[at - 1];
                d[at] = d[at - 1];
                at--;
            }
            idx[at] = j;
            d[at] = e;
        }
        return idx[..n];
    }

    private static double Period(OrbLook look) => look switch
    {
        OrbLook.Base or OrbLook.Reasoning or OrbLook.ReasoningTwins => 6500,
        OrbLook.Working or OrbLook.WorkingGyro => 3000,
        OrbLook.Compacting or OrbLook.CompactingSqueeze or OrbLook.CompactingFuse => 10000,
        _ => 13000,
    };

    /// <summary>A spring settling from 0 to 1: shoots about 22% past, dips about 5% under, lands at x = 1.</summary>
    private static double Spring(double x) => 1 - Math.Exp(-4.5 * x) * Math.Cos(3 * Math.PI * x) - x * Math.Exp(-4.5);

    /// <summary>Retrying's spin: turns 2s, brakes 0.2s, springs back 60% over 0.8s, speeds up 0.2s.</summary>
    private static double Rewind(double t, double w)
    {
        const double p = 3200;
        var k = Math.Floor(t / p);
        var u = t - k * p;
        var back = 0.6 * 2100 * w;
        var fwd = (2000 + 100 + 100) * w;
        double a;
        if (u < 2000)
        {
            a = u * w;
        }
        else if (u < 2200)
        {
            var x = (u - 2000) / 200;
            a = (2000 + 200 * (x - x * x / 2)) * w;
        }
        else if (u < 3000)
        {
            a = 2100 * w - back * Spring((u - 2200) / 800);
        }
        else
        {
            var x = (u - 3000) / 200;
            a = 2100 * w - back + 100 * x * x * w;
        }
        return k * (fwd - back) + a;
    }

    private static double YawOf(OrbLook look, double t)
    {
        if (look == OrbLook.Retrying)
        {
            return Rewind(2 * t, Tau / 9000);
        }
        if (look == OrbLook.RetryingSurge)
        {
            var turns = t / 3250;
            var u = turns - Math.Floor(turns);
            return (Math.Floor(turns) + (1 - Math.Pow(1 - u, 3))) * Tau;
        }
        return t / Period(look) * Tau;
    }

    private static double[] MakeRingAxis()
    {
        double tip = 30 * Math.PI / 180, roll = 10 * Math.PI / 180;
        return [-Math.Sin(roll) * Math.Cos(tip), Math.Cos(roll) * Math.Cos(tip), Math.Sin(tip)];
    }

    /// <summary>The k-th lens spot in view space, kept 15 to 45 degrees off centre.</summary>
    private static double[] Spot(double k)
    {
        var phi = k * 2.45 + Hash(k) * 1.5;
        var theta = (15 + 30 * Hash(k + 0.5)) * Math.PI / 180;
        return [Math.Sin(theta) * Math.Cos(phi), Math.Sin(theta) * Math.Sin(phi), Math.Cos(theta)];
    }

    private static double[] LensAt(double t)
    {
        var k = Math.Floor(t / LensMs);
        var u = t / LensMs - k;
        var e = u < Move ? (1 - Math.Cos(u / Move * Math.PI)) / 2 : 1;
        double[] a = Spot(k), b = Spot(k + 1);
        var v = new[] { a[0] + (b[0] - a[0]) * e, a[1] + (b[1] - a[1]) * e, a[2] + (b[2] - a[2]) * e };
        var n = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
        return [v[0] / n, v[1] / n, v[2] / n];
    }

    private static double[][] Sphere(int count) => Enumerable.Range(0, count).Select(i =>
    {
        var y = 1 - 2 * (i + 0.5) / count;
        var r = Math.Sqrt(1 - y * y);
        var th = i * Golden;
        return new[] { r * Math.Cos(th), y, r * Math.Sin(th) };
    }).ToArray();

    /// <summary>Background spiral's eight arms, wound pole to pole; arms thin out toward the poles.</summary>
    private static double[][] Arms(int count)
    {
        static double[] At(double lat, double lon) => [Math.Cos(lat) * Math.Cos(lon), Math.Sin(lat), Math.Cos(lat) * Math.Sin(lon)];
        var g = Math.Sqrt(4 * Math.PI / count);
        const int n = 8;
        var along = 0.6 * g;
        var output = new List<double[]>();
        for (var m = 0; m < n; m++)
        {
            var room = m != 0 ? m & -m : n;
            var lim = Math.Min(85 * Math.PI / 180, Math.Acos(Math.Min(1, g * n / (Tau * room))));
            for (var lat = -lim + m * 0.618 % 1 * along; lat <= lim; lat += along / Math.Sqrt(1 + Math.Pow(Math.Cos(lat), 2)))
            {
                output.Add(At(lat, (double)m / n * Tau - lat));
            }
        }
        return output.ToArray();
    }
}
