# Faqra Agents A1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Watch Claude Code sessions live in Faqra's island, each shown with a native port of the Thinking Orbs, fed by a hook relay that Faqra installs into `~/.claude/settings.json` only after the owner reviews the exact edit.

**Architecture:** Claude Code runs `faqra-hook.exe <Event>` (exec form, no shell) for each hook event. The relay trims the payload and sends one JSON line over a per-user named pipe to `AgentHub` in Faqra, which folds events into an immutable `AgentBoard` (sessions, states, ticker). The island's new Agents module and the collapsed pill draw the board with `OrbView`, a WPF element that renders `OrbModel`, a line-for-line port of Thinking Orbs' math. Nothing in A1 answers Claude Code: the relay never prints, so sessions behave exactly as without Faqra.

**Tech Stack:** C# / .NET 8, WPF + WPF UI 4.3, System.Text.Json (Nodes), System.IO.Pipes, System.Collections.Immutable, xunit. No new NuGet packages.

**Spec:** `docs/superpowers/specs/2026-10-09-faqra-agents-design.md` (A1 row of the milestone table, plus the "How it works" and "What the owner sees" sections).

## Global Constraints

- Solution lives in `Windows/`; run every `dotnet` command from `C:\Users\Tigre\vorssaint-utils-windows\Windows`.
- Every new source file starts with `// SPDX-License-Identifier: GPL-3.0-or-later` and `// Copyright (C) 2026 Faqra contributors`. Ported files add a third line naming the source and its MIT notice.
- Nullable warnings are errors (`Directory.Build.props`). Package versions live only in `Directory.Packages.props`; this plan adds none.
- Pipe name: `faqra-agents-<user SID>` (full path `\\.\pipe\faqra-agents-<SID>`), both ends opened with `PipeOptions.CurrentUserOnly`. Tests override it with the `FAQRA_AGENTS_PIPE` environment variable.
- Relay: when Faqra's pipe does not exist it exits at once (no waiting); it waits up to 300 ms only while the pipe exists but every instance is busy; whole run budget 2 s; always exits 0 and prints nothing to stdout in A1.
- Hook marker: an entry is Faqra's when its `command` contains `faqra-hook`; Coucou's when it contains `coucou-hook` (case-insensitive).
- Faqra-only identifiers: feature `AppFeature.FaqraAgents` (raw `faqraAgents`, key `featureAvailable.faqraAgents`), island module `IslandModule.FaqraAgents` (raw `faqraAgents`, shortcut key `g`), migration marker `faqraAgentsInstalled`.
- `~/.claude/settings.json` is written only from the review dialog's confirm button, after a fingerprint check, with a byte-exact backup `settings.json.bak-yyyyMMdd-HHmmss` (then `-1`, `-2`, ... on collision).
- User-visible copy: sentence case, no em or en dashes, no exclamation marks, never the words Coucou or Mochi except where the text names Coucou's leftover hooks.
- Commits: conventional prefixes (`feat(windows):`, `test(windows):`, `docs(windows):`), and no `Co-Authored-By` or any Claude attribution line.
- WPF tests run inside `StaThread.Run(...)` (tests/Faqra.App.Tests/StaThread.cs).

## Review Focus

1. Faqra closed, crashed or hung while Claude Code runs: with no pipe the relay must return at once (two hook events per tool call would otherwise add 0.6 s to every call), with a hung Faqra within the 2 s budget, always exit 0 with empty stdout. Pinned in Task 5 (`ExitsAtOnceWhenFaqraIsClosed`, `GivesUpOnAServerThatNeverReads`).
2. Odd stdin: empty, not JSON, a JSON array, a BOM, or a multi-megabyte `Write`: no crash, nothing sent for garbage, huge edit bodies cut with `faqra_diff_truncated`. Pinned in Task 2 and Task 5.
3. `settings.json` that changed between preview and confirm, has comments or trailing commas, or has a non-object `hooks`: refuse, never write, and leave the owner's own hooks byte-for-byte. Pinned in Task 4 and Task 6.
4. A burst of concurrent events (parallel tool calls across several sessions): the hub must accept every connection without dropping events or blocking the UI thread. Pinned in Task 6 (`AcceptsManyConnectionsAtOnce`).
5. A session first seen mid-way (Faqra started after Claude) or never ended (terminal killed): created on any event, expired after 12 hours idle. Pinned in Task 3.

---

## File structure

| File | Responsibility |
|---|---|
| `src/Faqra.Core/Agents/Orb/OrbModel.cs` | Thinking Orbs math: looks, dot placement per frame |
| `src/Faqra.Core/Agents/AgentJson.cs` | Shared System.Text.Json options |
| `src/Faqra.Core/Agents/HookPayload.cs` | The relay's normalisation and trimming of a hook payload |
| `src/Faqra.Core/Agents/AgentEvent.cs` | One parsed pipe line |
| `src/Faqra.Core/Agents/AgentBoard.cs` | Sessions, states, steps; `Apply` and `Tick` |
| `src/Faqra.Core/Agents/AgentOrbStyles.cs` | State to orb look, tone and speed; urgency order |
| `src/Faqra.Core/Agents/AgentsText.cs` | State labels and step lines from `AgentsStrings` |
| `src/Faqra.Core/Agents/AgentPipe.cs` | Pipe name rules |
| `src/Faqra.Core/Agents/Install/ClaudeHookConfig.cs` | Add, remove and inspect hook entries in settings JSON |
| `src/Faqra.Core/Agents/Install/UnifiedDiff.cs` | Line diff for the review dialog |
| `src/Faqra.Core/Agents/Install/ConfigEdit.cs` | Fingerprint and backup naming |
| `src/Faqra.Core/Localization/AgentsStrings.cs`, `AgentsStrings.EnUS.cs` | Every Agents string |
| `src/Faqra.Hook/` (new console project, assembly `faqra-hook`) | The relay |
| `src/Faqra.Services/Agents/AgentHub.cs` | Pipe server, board owner, change event, event log |
| `src/Faqra.Services/Agents/ConfigFile.cs` | Preview and apply an edit to a config file on disk |
| `src/Faqra.Services/Agents/RelayDeployment.cs` | Copies the relay to `%LOCALAPPDATA%\Faqra\bin` |
| `src/Faqra.App/Agents/OrbView.cs` | WPF element drawing an `OrbModel` |
| `src/Faqra.App/Agents/AgentInk.cs` | Tone to brush on the island's dark surface |
| `src/Faqra.App/Island/Modules/AgentsModule.cs` | The expanded island module |
| `src/Faqra.App/Island/Modules/IdleViews.cs` (modify) | `IdleAgentsView` for the collapsed pill |
| `src/Faqra.App/Settings/Pages/AgentsPage.cs` | Settings page: status, install, remove |
| `src/Faqra.App/Settings/ConfigReviewWindow.cs` | The diff review dialog |
| `tests/Faqra.Core.Tests/Agents/*` | Orb, payload, board, config, diff tests |
| `tests/Faqra.Services.Tests/Agents/*` | Relay, hub, config file tests |
| `tests/Faqra.App.Tests/AgentsRenderTests.cs` | Orb, module, page renders |
| `tools/orb-reference/gen.mjs` | Regenerates the orb reference data from Thinking Orbs |
| `THIRD-PARTY-NOTICES.md` (repo `Windows/`) | MIT notices for Thinking Orbs and Coucou |

---

### Task 1: Thinking Orbs math (`OrbModel`)

**Files:**
- Create: `src/Faqra.Core/Agents/Orb/OrbModel.cs`
- Create: `tests/Faqra.Core.Tests/Agents/OrbModelTests.cs`
- Already present: `tests/Faqra.Core.Tests/Agents/Data/orb-reference.json` (frames recorded from the original `orb-core.ts` by `tools/orb-reference/gen.mjs`: 18 cases, frames every 50 ms, samples at 0, 400, 1250, 3000 and 6100 ms; each dot `[cx, cy, r, opacity]` rounded to 2 decimals as the original's SVG writes them)
- Modify: `tests/Faqra.Core.Tests/Faqra.Core.Tests.csproj` (copy the data file to output)

**Interfaces:**
- Produces: `enum OrbLook { Base, Working, WorkingGyro, Reasoning, ReasoningTwins, Searching, SearchingLighthouse, Background, BackgroundSpiral, Retrying, RetryingSurge, Compacting, CompactingSqueeze, CompactingFuse, Waiting }`; `readonly record struct OrbDot(double X, double Y, double Radius, double Opacity)`; `static class OrbLooks { OrbLook FromNames(string state, string? variant) }`; `sealed class OrbModel(OrbLook look, double size, double density = 1, double dotSize = 1, double tilt = 20)` with `OrbLook Look`, `double Size`, `int Count`, `IReadOnlyList<OrbDot> Frame(double t)` (t in ms on the look's clock; frames must come in increasing t), `const double MinRadius = 0.45`, `const double HiddenOpacity = 0.005`.

- [ ] **Step 1: Copy the reference data to the test output**

Add inside the existing `<Project>` of `tests/Faqra.Core.Tests/Faqra.Core.Tests.csproj`:

```xml
  <ItemGroup>
    <None Include="Agents/Data/*.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing test**

`tests/Faqra.Core.Tests/Agents/OrbModelTests.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using Faqra.Core.Agents.Orb;

namespace Faqra.Core.Tests.Agents;

// The reference frames come from Thinking Orbs' own orb-core.ts (MIT, Copyright (c) 2026 Yogesh),
// recorded by tools/orb-reference/gen.mjs. The port must land every dot where the original does.
public class OrbModelTests
{
    // The original writes 2 decimals, so a value can differ from the exact one by half a hundredth.
    private const double Tolerance = 0.0051;

    private static readonly JsonElement Reference = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Agents", "Data", "orb-reference.json"))).RootElement;

    public static IEnumerable<object[]> Cases() => Reference.GetProperty("cases").EnumerateArray()
        .Select((c, i) => new object[] { i, c.GetProperty("state").GetString()!, c.GetProperty("variant").GetString()!, c.GetProperty("size").GetInt32() });

    [Theory]
    [MemberData(nameof(Cases))]
    public void MatchesTheOriginalFrameForFrame(int index, string state, string variant, int size)
    {
        var expected = Reference.GetProperty("cases")[index];
        var model = new OrbModel(OrbLooks.FromNames(state, variant), size);
        Assert.Equal(expected.GetProperty("dots").GetInt32(), model.Count);

        var step = Reference.GetProperty("step").GetInt32();
        var samples = Reference.GetProperty("samples").EnumerateArray().Select(s => s.GetInt32()).ToHashSet();
        for (var t = 0; t <= samples.Max(); t += step)
        {
            var dots = model.Frame(t);
            if (!samples.Contains(t))
            {
                continue;
            }
            var frame = expected.GetProperty("frames").GetProperty(t.ToString(CultureInfo.InvariantCulture));
            for (var i = 0; i < model.Count; i++)
            {
                var dot = frame[i];
                var opacity = dot[3].GetDouble();
                if (opacity == 0)
                {
                    // A hidden dot keeps the position of the frame it vanished on, so only its opacity counts.
                    Assert.True(dots[i].Opacity < OrbModel.HiddenOpacity + 1e-9, $"{state}-{variant} t={t} dot {i} should be hidden");
                    continue;
                }
                Assert.InRange(dots[i].X, dot[0].GetDouble() - Tolerance, dot[0].GetDouble() + Tolerance);
                Assert.InRange(dots[i].Y, dot[1].GetDouble() - Tolerance, dot[1].GetDouble() + Tolerance);
                Assert.InRange(Math.Max(OrbModel.MinRadius, dots[i].Radius), dot[2].GetDouble() - Tolerance, dot[2].GetDouble() + Tolerance);
                Assert.InRange(dots[i].Opacity, opacity - Tolerance, opacity + Tolerance);
            }
        }
    }

    [Fact]
    public void BackgroundUsesAQuarterOfTheDots()
    {
        Assert.Equal(80, new OrbModel(OrbLook.Base, 20).Count);
        Assert.Equal(20, new OrbModel(OrbLook.Background, 20).Count);
        Assert.Equal(8, new OrbModel(OrbLook.Background, 4).Count);
    }

    [Theory]
    [InlineData("working", "gyro", OrbLook.WorkingGyro)]
    [InlineData("compacting", "fuse", OrbLook.CompactingFuse)]
    [InlineData("waiting", null, OrbLook.Waiting)]
    [InlineData("nonsense", "x", OrbLook.Base)]
    [InlineData("searching", "nonsense", OrbLook.Searching)]
    public void NamesFallBackLikeTheOriginal(string state, string? variant, OrbLook look) =>
        Assert.Equal(look, OrbLooks.FromNames(state, variant));
}
```

- [ ] **Step 3: Run it to see it fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~OrbModelTests"`
Expected: build error, `The type or namespace name 'Orb' does not exist in the namespace 'Faqra.Core.Agents'`.

- [ ] **Step 4: Write the port**

`src/Faqra.Core/Agents/Orb/OrbModel.cs`:

```csharp
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
```

- [ ] **Step 5: Run the tests to see them pass**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~OrbModelTests"`
Expected: PASS, 24 tests (18 reference cases, 1 count test, 5 name cases). If a reference case fails, compare that look's branch against `orb-core.ts` line by line; do not widen the tolerance.

- [ ] **Step 6: Commit**

```bash
git add src/Faqra.Core/Agents/Orb tests/Faqra.Core.Tests/Agents tests/Faqra.Core.Tests/Faqra.Core.Tests.csproj tools/orb-reference
git commit -m "feat(windows): port the Thinking Orbs math to Faqra.Core"
```

---

### Task 2: Hook payloads and pipe events

**Files:**
- Create: `src/Faqra.Core/Agents/AgentJson.cs`, `src/Faqra.Core/Agents/HookPayload.cs`, `src/Faqra.Core/Agents/AgentEvent.cs`, `src/Faqra.Core/Agents/AgentPipe.cs`
- Create: `tests/Faqra.Core.Tests/Agents/HookPayloadTests.cs`, `tests/Faqra.Core.Tests/Agents/AgentEventTests.cs`

**Interfaces:**
- Produces: `static class AgentJson { JsonSerializerOptions Compact; JsonSerializerOptions Indented; }`; `static class HookPayload { const int MaxString = 2000; const int MaxEditString = 262144; const int MaxEditTotal = 524288; string? ToLine(string stdin, string eventArg, string agent, Func<string,string?> env, string processCwd) }`; `sealed record AgentEvent(string Event, string SessionId, string Agent, string Cwd, string? ToolName, JsonObject? ToolInput, string? Prompt, string? Message, string? LastAssistantMessage, string? NotificationType, string? TermProgram, string? Entrypoint)` with `static AgentEvent? TryParse(string line)`; `static class AgentPipe { const string Prefix = "faqra-agents-"; const string OverrideVariable = "FAQRA_AGENTS_PIPE"; string Name(string userSid, Func<string,string?> env) }`.

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.Core.Tests/Agents/HookPayloadTests.cs`:

```csharp
using System.Text.Json.Nodes;
using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class HookPayloadTests
{
    private static readonly Func<string, string?> NoEnv = _ => null;

    private static JsonObject Line(string stdin, string eventArg = "PreToolUse", Func<string, string?>? env = null) =>
        (JsonObject)JsonNode.Parse(HookPayload.ToLine(stdin, eventArg, "claude", env ?? NoEnv, @"C:\work")!)!;

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("42")]
    public void IgnoresAnythingButAnObject(string stdin) =>
        Assert.Null(HookPayload.ToLine(stdin, "Stop", "claude", NoEnv, @"C:\work"));

    [Fact]
    public void ReadsThroughAByteOrderMark()
    {
        var line = Line("\uFEFF{\"session_id\":\"s1\",\"hook_event_name\":\"Stop\"}");
        Assert.Equal("Stop", line["hook_event_name"]!.GetValue<string>());
    }

    [Fact]
    public void NamesTheEventFromTheArgumentWhenThePayloadDoesNot()
    {
        var line = Line("{\"session_id\":\"s1\"}", "SessionStart");
        Assert.Equal("SessionStart", line["hook_event_name"]!.GetValue<string>());
    }

    [Fact]
    public void TagsTheAgentTheTerminalAndAMissingFolder()
    {
        var env = new Dictionary<string, string?> { ["TERM_PROGRAM"] = "vscode", ["WT_SESSION"] = "abc", ["CLAUDE_CODE_ENTRYPOINT"] = "claude-vscode" };
        var line = Line("{\"session_id\":\"s1\"}", env: name => env.GetValueOrDefault(name));
        Assert.Equal("claude", line["faqra_agent"]!.GetValue<string>());
        Assert.Equal("vscode", line["term_program"]!.GetValue<string>());
        Assert.Equal("abc", line["wt_session"]!.GetValue<string>());
        Assert.Equal("claude-vscode", line["claude_entrypoint"]!.GetValue<string>());
        Assert.Equal(@"C:\work", line["cwd"]!.GetValue<string>());
    }

    [Fact]
    public void CutsLongStringsWithoutSplittingACharacter()
    {
        var command = new string('a', HookPayload.MaxString - 1) + "\U0001F600" + "tail";
        var line = Line($"{{\"session_id\":\"s1\",\"tool_name\":\"Bash\",\"tool_input\":{{\"command\":\"{command}\"}}}}");
        var cut = line["tool_input"]!["command"]!.GetValue<string>();
        Assert.Equal(new string('a', HookPayload.MaxString - 1) + "…", cut);
    }

    [Fact]
    public void KeepsEditBodiesUpToTheirOwnLimitAndFlagsACut()
    {
        var small = new string('x', 5000);
        var huge = new string('y', HookPayload.MaxEditString + 10);
        var line = Line($"{{\"hook_event_name\":\"PostToolUse\",\"session_id\":\"s1\",\"tool_name\":\"Edit\",\"tool_input\":{{\"file_path\":\"a.cs\",\"old_string\":\"{small}\",\"new_string\":\"{huge}\"}}}}");
        Assert.Equal(small, line["tool_input"]!["old_string"]!.GetValue<string>());
        Assert.Equal(HookPayload.MaxEditString + 1, line["tool_input"]!["new_string"]!.GetValue<string>().Length);
        Assert.True(line["faqra_diff_truncated"]!.GetValue<bool>());
    }

    [Fact]
    public void KeepsAQuestionWhole()
    {
        var question = new string('q', HookPayload.MaxString + 500);
        var line = Line($"{{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"s1\",\"tool_name\":\"AskUserQuestion\",\"tool_input\":{{\"questions\":[{{\"question\":\"{question}\"}}]}}}}");
        Assert.Equal(question, line["tool_input"]!["questions"]![0]!["question"]!.GetValue<string>());
    }

    [Fact]
    public void WritesOneLine()
    {
        var text = HookPayload.ToLine("{\"session_id\":\"s1\",\"prompt\":\"two\\nlines\"}", "UserPromptSubmit", "claude", NoEnv, @"C:\w")!;
        Assert.DoesNotContain('\n', text);
        Assert.Contains("two\\nlines", text);
    }
}
```

`tests/Faqra.Core.Tests/Agents/AgentEventTests.cs`:

```csharp
using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class AgentEventTests
{
    [Fact]
    public void ReadsTheFieldsTheBoardUses()
    {
        var e = AgentEvent.TryParse("""
            {"hook_event_name":"Stop","session_id":"s1","cwd":"C:\\code\\faqra","faqra_agent":"claude",
             "last_assistant_message":"All done.","term_program":"vscode","claude_entrypoint":"claude-vscode"}
            """.ReplaceLineEndings(""))!;
        Assert.Equal("Stop", e.Event);
        Assert.Equal("s1", e.SessionId);
        Assert.Equal(@"C:\code\faqra", e.Cwd);
        Assert.Equal("claude", e.Agent);
        Assert.Equal("All done.", e.LastAssistantMessage);
        Assert.Equal("vscode", e.TermProgram);
        Assert.Equal("claude-vscode", e.Entrypoint);
    }

    [Fact]
    public void KeepsTheToolInput()
    {
        var e = AgentEvent.TryParse("{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"s1\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}")!;
        Assert.Equal("Bash", e.ToolName);
        Assert.Equal("npm test", e.ToolInput!["command"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"session_id\":\"s1\"}")]
    [InlineData("{\"hook_event_name\":42}")]
    [InlineData("nope")]
    public void RefusesALineWithoutAnEventName(string line) => Assert.Null(AgentEvent.TryParse(line));

    [Fact]
    public void DefaultsTheSessionAndAgent()
    {
        var e = AgentEvent.TryParse("{\"hook_event_name\":\"Notification\",\"message\":\"Claude needs your permission\"}")!;
        Assert.Equal("unknown", e.SessionId);
        Assert.Equal("claude", e.Agent);
        Assert.Equal("Claude needs your permission", e.Message);
    }

    [Fact]
    public void NamesThePipePerUserUnlessOverridden()
    {
        Assert.Equal("faqra-agents-S-1-5-21-1", AgentPipe.Name("S-1-5-21-1", _ => null));
        Assert.Equal("test-pipe", AgentPipe.Name("S-1-5-21-1", name => name == AgentPipe.OverrideVariable ? "test-pipe" : null));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~HookPayloadTests|FullyQualifiedName~AgentEventTests"`
Expected: build error, `The name 'HookPayload' does not exist in the current context`.

- [ ] **Step 3: Write the implementation**

`src/Faqra.Core/Agents/AgentJson.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text.Encodings.Web;
using System.Text.Json;

namespace Faqra.Core.Agents;

/// <summary>JSON options shared by the relay, the hub and the config editor.</summary>
public static class AgentJson
{
    /// <summary>One line, characters kept as written: what crosses the pipe.</summary>
    public static readonly JsonSerializerOptions Compact = new() { WriteIndented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Two-space indent, characters kept as written: what Claude Code's own settings look like.</summary>
    public static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
}
```

`src/Faqra.Core/Agents/HookPayload.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Ported from Coucou's relay (windows/hook/src/main.rs and normalize.rs), https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé).

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

/// <summary>
/// What the relay does to a hook payload before it crosses the pipe: names the event, tags the agent
/// and the terminal, and trims strings so a line stays small.
/// </summary>
public static class HookPayload
{
    public const int MaxString = 2000;
    public const int MaxEditString = 256 * 1024;
    public const int MaxEditTotal = 512 * 1024;
    public const string Ellipsis = "…";

    private static readonly HashSet<string> EditTools = new(StringComparer.Ordinal) { "Edit", "MultiEdit", "Write" };
    private static readonly HashSet<string> EditFields = new(StringComparer.Ordinal) { "old_string", "new_string", "content" };

    /// <summary>Environment variables copied into the payload, by the field they become.</summary>
    private static readonly (string Field, string Variable)[] EnvironmentFields =
    [
        ("term_program", "TERM_PROGRAM"),
        ("wt_session", "WT_SESSION"),
        ("claude_entrypoint", "CLAUDE_CODE_ENTRYPOINT"),
    ];

    /// <summary>The line sent to Faqra for this stdin, or null when stdin is not a JSON object.</summary>
    public static string? ToLine(string stdin, string eventArg, string agent, Func<string, string?> env, string processCwd)
    {
        JsonObject? payload;
        try
        {
            payload = JsonNode.Parse(stdin.TrimStart('\uFEFF')) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
        if (payload is null)
        {
            return null;
        }
        Normalize(payload, eventArg, agent, env, processCwd);
        return payload.ToJsonString(AgentJson.Compact);
    }

    private static void Normalize(JsonObject payload, string eventArg, string agent, Func<string, string?> env, string processCwd)
    {
        if (StringOf(payload["hook_event_name"]) is null && eventArg.Length > 0)
        {
            payload["hook_event_name"] = eventArg;
        }
        payload["faqra_agent"] = agent;
        if (StringOf(payload["cwd"]) is null)
        {
            payload["cwd"] = processCwd;
        }
        foreach (var (field, variable) in EnvironmentFields)
        {
            if (env(variable) is { Length: > 0 } value)
            {
                payload[field] = value;
            }
        }

        var tool = StringOf(payload["tool_name"]);
        var isEdit = StringOf(payload["hook_event_name"]) == "PostToolUse" && tool is not null && EditTools.Contains(tool);
        var editBudget = MaxEditTotal;
        var truncated = false;
        foreach (var key in payload.Select(pair => pair.Key).ToList())
        {
            var child = payload[key];
            if (key == "tool_input" && tool == "AskUserQuestion")
            {
                continue; // the answers echo the questions back whole
            }
            if (key == "tool_input" && isEdit && child is JsonObject input)
            {
                TrimEdit(input, ref editBudget, ref truncated);
                continue;
            }
            TrimChild(payload, key, child, MaxString);
        }
        if (truncated)
        {
            payload["faqra_diff_truncated"] = true;
        }
    }

    private static string? StringOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    private static void TrimChild(JsonObject parent, string key, JsonNode? child, int limit)
    {
        switch (child)
        {
            case JsonObject obj:
                foreach (var inner in obj.Select(pair => pair.Key).ToList())
                {
                    TrimChild(obj, inner, obj[inner], limit);
                }
                break;
            case JsonArray array:
                TrimArray(array, limit);
                break;
            case JsonValue value when StringOf(value) is { } text && text.Length > limit:
                parent[key] = Cut(text, limit);
                break;
        }
    }

    private static void TrimArray(JsonArray array, int limit)
    {
        for (var i = 0; i < array.Count; i++)
        {
            switch (array[i])
            {
                case JsonObject obj:
                    foreach (var inner in obj.Select(pair => pair.Key).ToList())
                    {
                        TrimChild(obj, inner, obj[inner], limit);
                    }
                    break;
                case JsonArray nested:
                    TrimArray(nested, limit);
                    break;
                case JsonValue value when StringOf(value) is { } text && text.Length > limit:
                    array[i] = Cut(text, limit);
                    break;
            }
        }
    }

    /// <summary>Edit bodies keep up to 256 KB each and 512 KB together; everything else the usual 2,000.</summary>
    private static void TrimEdit(JsonObject obj, ref int budget, ref bool truncated)
    {
        foreach (var key in obj.Select(pair => pair.Key).ToList())
        {
            var child = obj[key];
            if (EditFields.Contains(key) && StringOf(child) is { } text)
            {
                var limit = Math.Max(0, Math.Min(MaxEditString, budget));
                if (text.Length > limit)
                {
                    obj[key] = Cut(text, limit);
                    truncated = true;
                    budget = 0;
                }
                else
                {
                    budget -= text.Length;
                }
                continue;
            }
            if (child is JsonArray array)
            {
                foreach (var item in array.OfType<JsonObject>())
                {
                    TrimEdit(item, ref budget, ref truncated);
                }
                continue;
            }
            TrimChild(obj, key, child, MaxString);
        }
    }

    private static string Cut(string text, int limit)
    {
        var length = limit;
        if (length > 0 && char.IsHighSurrogate(text[length - 1]))
        {
            length--; // never split a surrogate pair
        }
        return string.Concat(text.AsSpan(0, length), Ellipsis);
    }
}
```

`src/Faqra.Core/Agents/AgentEvent.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

/// <summary>One hook event as it arrives over the pipe.</summary>
public sealed record AgentEvent(
    string Event,
    string SessionId,
    string Agent,
    string Cwd,
    string? ToolName,
    JsonObject? ToolInput,
    string? Prompt,
    string? Message,
    string? LastAssistantMessage,
    string? NotificationType,
    string? TermProgram,
    string? Entrypoint)
{
    public static AgentEvent? TryParse(string line)
    {
        JsonObject? obj;
        try
        {
            obj = JsonNode.Parse(line) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
        if (obj is null || Text(obj, "hook_event_name") is not { Length: > 0 } name)
        {
            return null;
        }
        return new AgentEvent(
            name,
            Text(obj, "session_id") ?? "unknown",
            Text(obj, "faqra_agent") ?? "claude",
            Text(obj, "cwd") ?? string.Empty,
            Text(obj, "tool_name"),
            obj["tool_input"] as JsonObject,
            Text(obj, "prompt"),
            Text(obj, "message"),
            Text(obj, "last_assistant_message"),
            Text(obj, "notification_type"),
            Text(obj, "term_program"),
            Text(obj, "claude_entrypoint"));
    }

    private static string? Text(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
```

`src/Faqra.Core/Agents/AgentPipe.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

namespace Faqra.Core.Agents;

/// <summary>The pipe between the relay and Faqra: one per Windows user, so two users never share one.</summary>
public static class AgentPipe
{
    public const string Prefix = "faqra-agents-";

    /// <summary>Tests point both ends at a private pipe with this environment variable.</summary>
    public const string OverrideVariable = "FAQRA_AGENTS_PIPE";

    public static string Name(string userSid, Func<string, string?> env) =>
        env(OverrideVariable) is { Length: > 0 } name ? name : Prefix + userSid;
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~HookPayloadTests|FullyQualifiedName~AgentEventTests"`
Expected: PASS (20 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Faqra.Core/Agents tests/Faqra.Core.Tests/Agents
git commit -m "feat(windows): hook payload trimming and pipe events for Faqra Agents"
```

---

### Task 3: The session board

**Files:**
- Create: `src/Faqra.Core/Agents/AgentBoard.cs`, `src/Faqra.Core/Agents/AgentOrbStyles.cs`
- Create: `tests/Faqra.Core.Tests/Agents/AgentBoardTests.cs`

**Interfaces:**
- Consumes: `AgentEvent` (Task 2), `OrbLook` (Task 1).
- Produces:
  - `enum AgentState { Idle, Thinking, Working, Searching, Compacting, Background, Approval, Question, Error, RateLimited, Finished }`
  - `enum AgentStepKind { Prompt, Read, Edit, Run, Search, WebSearch, WebFetch, Subagent, SubagentDone, Plan, Tool, Failed }`
  - `sealed record AgentStep(DateTimeOffset At, AgentStepKind Kind, string Detail)`
  - `sealed record AgentSession(string Id, string Agent, string Cwd, AgentState State, ImmutableList<AgentStep> Steps, string? LastMessage, int Subagents, DateTimeOffset StartedAt, DateTimeOffset UpdatedAt, DateTimeOffset? FinishedAt)` with `string Project`
  - `sealed record AgentBoard(ImmutableDictionary<string, AgentSession> Sessions)` with `static AgentBoard Empty`, `AgentBoard Apply(AgentEvent e, DateTimeOffset now)`, `AgentBoard Tick(DateTimeOffset now)`, `IReadOnlyList<AgentSession> Ordered`, `AgentSession? MostUrgent`, `bool IsActive` (any session not Idle), `static TimeSpan FinishedHold` (5.2 s), `static TimeSpan IdleExpiry` (12 h), `const int MaxSteps = 20`
  - `enum AgentTone { Neutral, Secondary, Caution, Accent, Critical, Success }`, `readonly record struct AgentOrbStyle(OrbLook Look, AgentTone Tone, double Speed)`, `static class AgentOrbStyles { AgentOrbStyle For(AgentState state); int Urgency(AgentState state) }`

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.Core.Tests/Agents/AgentBoardTests.cs`:

```csharp
using Faqra.Core.Agents;
using Faqra.Core.Agents.Orb;

namespace Faqra.Core.Tests.Agents;

public class AgentBoardTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private static AgentEvent E(string name, string session = "s1", string? tool = null, string? input = null,
        string? prompt = null, string? message = null, string? last = null)
    {
        var obj = new System.Text.Json.Nodes.JsonObject { ["hook_event_name"] = name, ["session_id"] = session, ["cwd"] = @"C:\code\faqra" };
        if (tool is not null) obj["tool_name"] = tool;
        if (input is not null) obj["tool_input"] = System.Text.Json.Nodes.JsonNode.Parse(input);
        if (prompt is not null) obj["prompt"] = prompt;
        if (message is not null) obj["message"] = message;
        if (last is not null) obj["last_assistant_message"] = last;
        return AgentEvent.TryParse(obj.ToJsonString())!;
    }

    private static AgentSession Only(AgentBoard board) => Assert.Single(board.Sessions.Values);

    [Fact]
    public void AnyEventStartsASession()
    {
        var board = AgentBoard.Empty.Apply(E("PreToolUse", tool: "Bash", input: "{\"command\":\"npm test\"}"), T0);
        var session = Only(board);
        Assert.Equal("faqra", session.Project);
        Assert.Equal(AgentState.Working, session.State);
        Assert.Equal(new AgentStep(T0, AgentStepKind.Run, "npm test"), session.Steps[^1]);
    }

    [Fact]
    public void APromptStartsThinking()
    {
        var session = Only(AgentBoard.Empty.Apply(E("UserPromptSubmit", prompt: "fix the tray crash\nplease"), T0));
        Assert.Equal(AgentState.Thinking, session.State);
        Assert.Equal(new AgentStep(T0, AgentStepKind.Prompt, "fix the tray crash"), session.Steps[^1]);
    }

    [Theory]
    [InlineData("Read", "{\"file_path\":\"C:\\\\code\\\\a\\\\Tray.cs\"}", AgentState.Working, AgentStepKind.Read, "Tray.cs")]
    [InlineData("Edit", "{\"file_path\":\"src/b.cs\"}", AgentState.Working, AgentStepKind.Edit, "b.cs")]
    [InlineData("Write", "{\"file_path\":\"notes.md\"}", AgentState.Working, AgentStepKind.Edit, "notes.md")]
    [InlineData("Grep", "{\"pattern\":\"TrayIcon\"}", AgentState.Searching, AgentStepKind.Search, "TrayIcon")]
    [InlineData("Glob", "{\"pattern\":\"**/*.cs\"}", AgentState.Searching, AgentStepKind.Search, "**/*.cs")]
    [InlineData("WebSearch", "{\"query\":\"velopack uninstall\"}", AgentState.Searching, AgentStepKind.WebSearch, "velopack uninstall")]
    [InlineData("WebFetch", "{\"url\":\"https://docs.velopack.io/a/b\"}", AgentState.Searching, AgentStepKind.WebFetch, "docs.velopack.io")]
    [InlineData("Task", "{\"description\":\"Review the diff\"}", AgentState.Working, AgentStepKind.Subagent, "Review the diff")]
    [InlineData("TodoWrite", "{}", AgentState.Working, AgentStepKind.Plan, "")]
    [InlineData("mcp__x__y", "{}", AgentState.Working, AgentStepKind.Tool, "mcp__x__y")]
    public void ToolsBecomeSteps(string tool, string input, AgentState state, AgentStepKind kind, string detail)
    {
        var session = Only(AgentBoard.Empty.Apply(E("PreToolUse", tool: tool, input: input), T0));
        Assert.Equal(state, session.State);
        Assert.Equal(kind, session.Steps[^1].Kind);
        Assert.Equal(detail, session.Steps[^1].Detail);
    }

    [Fact]
    public void ALongCommandIsCutToOneShortLine()
    {
        var session = Only(AgentBoard.Empty.Apply(E("PreToolUse", tool: "Bash", input: "{\"command\":\"dotnet test Faqra.sln -c Release --filter Requires!=AudioDevice\\nsecond\"}"), T0));
        Assert.Equal("dotnet test Faqra.sln -c Release…", session.Steps[^1].Detail);
    }

    [Fact]
    public void PermissionAndQuestionWaitForTheOwner()
    {
        var board = AgentBoard.Empty.Apply(E("PermissionRequest", tool: "Bash", input: "{\"command\":\"rm x\"}"), T0);
        Assert.Equal(AgentState.Approval, Only(board).State);
        board = board.Apply(E("PostToolUse", tool: "Bash"), T0.AddSeconds(5));
        Assert.Equal(AgentState.Working, Only(board).State);
        board = board.Apply(E("PermissionRequest", tool: "AskUserQuestion", input: "{\"questions\":[]}"), T0.AddSeconds(6));
        Assert.Equal(AgentState.Question, Only(board).State);
    }

    [Theory]
    [InlineData("Claude hit the rate limit", AgentState.RateLimited)]
    [InlineData("Shall I also update the docs?", AgentState.Question)]
    [InlineData("Claude is waiting for your input", AgentState.Thinking)]
    public void NotificationsReadLikeCoucouReadsThem(string message, AgentState state)
    {
        var board = AgentBoard.Empty.Apply(E("UserPromptSubmit", prompt: "go"), T0);
        Assert.Equal(state, Only(board.Apply(E("Notification", message: message), T0.AddSeconds(1))).State);
    }

    [Fact]
    public void AFinishedTurnKeepsClaudesWordsAndRestsAfterFiveSeconds()
    {
        var board = AgentBoard.Empty.Apply(E("Stop", last: "Fixed it. Tests pass."), T0);
        Assert.Equal(AgentState.Finished, Only(board).State);
        Assert.Equal("Fixed it. Tests pass.", Only(board).LastMessage);
        Assert.Same(board, board.Tick(T0.AddSeconds(5)));
        Assert.Equal(AgentState.Idle, Only(board.Tick(T0.AddSeconds(5.2))).State);
    }

    [Fact]
    public void FailuresAndErrorsShow()
    {
        var board = AgentBoard.Empty.Apply(E("PostToolUseFailure", tool: "Bash"), T0);
        Assert.Equal(new AgentStep(T0, AgentStepKind.Failed, "Bash"), Only(board).Steps[^1]);
        Assert.Equal(AgentState.Error, Only(board.Apply(E("StopFailure", message: "API error"), T0)).State);
    }

    [Fact]
    public void SubagentsKeepAnIdleSessionInTheBackground()
    {
        var board = AgentBoard.Empty.Apply(E("Stop", last: "Started two reviewers."), T0)
            .Apply(E("SubagentStart"), T0.AddSeconds(1));
        board = board.Tick(T0.AddSeconds(7));
        Assert.Equal(AgentState.Background, Only(board).State);
        Assert.Equal(1, Only(board).Subagents);
        board = board.Apply(E("SubagentStop"), T0.AddSeconds(8));
        Assert.Equal(0, Only(board).Subagents);
        Assert.Equal(AgentState.Idle, Only(board).State);
    }

    [Fact]
    public void CompactingShowsThenRests()
    {
        var board = AgentBoard.Empty.Apply(E("PreCompact"), T0);
        Assert.Equal(AgentState.Compacting, Only(board).State);
        Assert.Equal(AgentState.Idle, Only(board.Apply(E("PostCompact"), T0)).State);
    }

    [Fact]
    public void SessionEndRemovesTheSessionAndUnknownEventsChangeNothing()
    {
        var board = AgentBoard.Empty.Apply(E("SessionStart"), T0);
        Assert.Same(board, board.Apply(E("SomethingNew"), T0));
        Assert.Empty(board.Apply(E("SessionEnd"), T0).Sessions);
    }

    [Fact]
    public void KeepsTheLastTwentySteps()
    {
        var board = AgentBoard.Empty;
        for (var i = 0; i < 25; i++)
        {
            board = board.Apply(E("PreToolUse", tool: "Bash", input: $"{{\"command\":\"step {i}\"}}"), T0.AddSeconds(i));
        }
        var steps = Only(board).Steps;
        Assert.Equal(AgentBoard.MaxSteps, steps.Count);
        Assert.Equal("step 5", steps[0].Detail);
        Assert.Equal("step 24", steps[^1].Detail);
    }

    [Fact]
    public void ForgetsASessionIdleForTwelveHours()
    {
        var board = AgentBoard.Empty.Apply(E("SessionStart"), T0);
        Assert.Single(board.Tick(T0 + AgentBoard.IdleExpiry - TimeSpan.FromSeconds(1)).Sessions);
        Assert.Empty(board.Tick(T0 + AgentBoard.IdleExpiry).Sessions);
    }

    [Fact]
    public void OrdersByUrgencyThenRecency()
    {
        var board = AgentBoard.Empty
            .Apply(E("SessionStart", "idle"), T0)
            .Apply(E("PreToolUse", "working", tool: "Bash", input: "{\"command\":\"x\"}"), T0.AddSeconds(1))
            .Apply(E("PermissionRequest", "ok", tool: "Bash", input: "{\"command\":\"y\"}"), T0.AddSeconds(2))
            .Apply(E("PermissionRequest", "question", tool: "AskUserQuestion", input: "{}"), T0.AddSeconds(3))
            .Apply(E("Stop", "done", last: "Done."), T0.AddSeconds(4));
        Assert.Equal(["ok", "question", "working", "done", "idle"], board.Ordered.Select(s => s.Id));
        Assert.Equal("ok", board.MostUrgent!.Id);
        Assert.True(board.IsActive);
        Assert.False(AgentBoard.Empty.Apply(E("SessionStart"), T0).IsActive);
    }

    [Theory]
    [InlineData(AgentState.Approval, OrbLook.Waiting, AgentTone.Caution)]
    [InlineData(AgentState.Question, OrbLook.Waiting, AgentTone.Accent)]
    [InlineData(AgentState.Error, OrbLook.RetryingSurge, AgentTone.Critical)]
    [InlineData(AgentState.RateLimited, OrbLook.Retrying, AgentTone.Caution)]
    [InlineData(AgentState.Working, OrbLook.Working, AgentTone.Neutral)]
    [InlineData(AgentState.Thinking, OrbLook.Reasoning, AgentTone.Neutral)]
    [InlineData(AgentState.Searching, OrbLook.Searching, AgentTone.Neutral)]
    [InlineData(AgentState.Compacting, OrbLook.Compacting, AgentTone.Neutral)]
    [InlineData(AgentState.Background, OrbLook.BackgroundSpiral, AgentTone.Secondary)]
    [InlineData(AgentState.Finished, OrbLook.Base, AgentTone.Success)]
    [InlineData(AgentState.Idle, OrbLook.Base, AgentTone.Secondary)]
    public void EachStateHasTheSpecsOrb(AgentState state, OrbLook look, AgentTone tone)
    {
        var style = AgentOrbStyles.For(state);
        Assert.Equal(look, style.Look);
        Assert.Equal(tone, style.Tone);
        Assert.Equal(state == AgentState.Idle ? 0.5 : 1, style.Speed);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~AgentBoardTests"`
Expected: build error, `The type or namespace name 'AgentBoard' could not be found`.

- [ ] **Step 3: Write the implementation**

`src/Faqra.Core/Agents/AgentBoard.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Event rules ported from Coucou's island (windows/src/island/hooks.ts), https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé), plus Searching, Compacting and Background.

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

public enum AgentState { Idle, Thinking, Working, Searching, Compacting, Background, Approval, Question, Error, RateLimited, Finished }

public enum AgentStepKind { Prompt, Read, Edit, Run, Search, WebSearch, WebFetch, Subagent, SubagentDone, Plan, Tool, Failed }

/// <summary>One ticker line: what happened, and the file, command or text it concerns.</summary>
public sealed record AgentStep(DateTimeOffset At, AgentStepKind Kind, string Detail);

public sealed record AgentSession(
    string Id,
    string Agent,
    string Cwd,
    AgentState State,
    ImmutableList<AgentStep> Steps,
    string? LastMessage,
    int Subagents,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? FinishedAt)
{
    /// <summary>The folder the session runs in, as the owner calls the project.</summary>
    public string Project => Cwd.Length == 0 ? Id : Path.GetFileName(Cwd.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : Cwd;
}

/// <summary>Every agent session Faqra knows about. Immutable: Apply and Tick return a new board.</summary>
public sealed record AgentBoard(ImmutableDictionary<string, AgentSession> Sessions)
{
    public const int MaxSteps = 20;
    private const int CommandChars = 40;
    private const int PromptChars = 60;

    public static readonly AgentBoard Empty = new(ImmutableDictionary<string, AgentSession>.Empty.WithComparers(StringComparer.Ordinal));

    /// <summary>How long Done shows before the session rests, Coucou's 5.2 s.</summary>
    public static readonly TimeSpan FinishedHold = TimeSpan.FromMilliseconds(5200);

    /// <summary>A session with no event for this long is gone (its terminal was closed without SessionEnd).</summary>
    public static readonly TimeSpan IdleExpiry = TimeSpan.FromHours(12);

    private static readonly HashSet<string> SearchTools = new(StringComparer.Ordinal) { "Grep", "Glob", "WebSearch", "WebFetch" };

    public bool IsActive => Sessions.Values.Any(session => session.State != AgentState.Idle);

    public IReadOnlyList<AgentSession> Ordered => Sessions.Values
        .OrderBy(session => AgentOrbStyles.Urgency(session.State))
        .ThenByDescending(session => session.UpdatedAt)
        .ThenBy(session => session.Id, StringComparer.Ordinal)
        .ToList();

    public AgentSession? MostUrgent => Ordered.FirstOrDefault();

    public AgentBoard Apply(AgentEvent e, DateTimeOffset now)
    {
        if (e.Event == "SessionEnd")
        {
            return Sessions.ContainsKey(e.SessionId) ? this with { Sessions = Sessions.Remove(e.SessionId) } : this;
        }
        var session = Sessions.TryGetValue(e.SessionId, out var known)
            ? known
            : new AgentSession(e.SessionId, e.Agent, e.Cwd, AgentState.Idle, [], null, 0, now, now, null);
        var next = Next(session, e, now);
        if (next is null)
        {
            return this;
        }
        return this with { Sessions = Sessions.SetItem(e.SessionId, next with { UpdatedAt = now, Cwd = e.Cwd.Length > 0 ? e.Cwd : next.Cwd }) };
    }

    public AgentBoard Tick(DateTimeOffset now)
    {
        var sessions = Sessions;
        foreach (var session in Sessions.Values)
        {
            if (now - session.UpdatedAt >= IdleExpiry)
            {
                sessions = sessions.Remove(session.Id);
            }
            else if (session.State == AgentState.Finished && session.FinishedAt is { } finished && now - finished >= FinishedHold)
            {
                sessions = sessions.SetItem(session.Id, session with { State = Resting(session) });
            }
        }
        return ReferenceEquals(sessions, Sessions) ? this : this with { Sessions = sessions };
    }

    private static AgentState Resting(AgentSession session) => session.Subagents > 0 ? AgentState.Background : AgentState.Idle;

    private static AgentSession? Next(AgentSession s, AgentEvent e, DateTimeOffset now) => e.Event switch
    {
        "SessionStart" => s,
        "UserPromptSubmit" => Step(s with { State = AgentState.Thinking, FinishedAt = null }, now, AgentStepKind.Prompt, OneLine(e.Prompt ?? string.Empty, PromptChars)),
        "PreToolUse" => ToolStep(s with { State = e.ToolName is { } t && SearchTools.Contains(t) ? AgentState.Searching : AgentState.Working }, e, now),
        "PostToolUse" => s with { State = AgentState.Working },
        "PostToolUseFailure" => Step(s with { State = AgentState.Working }, now, AgentStepKind.Failed, e.ToolName ?? string.Empty),
        "PermissionRequest" => s with { State = e.ToolName == "AskUserQuestion" ? AgentState.Question : AgentState.Approval },
        "Notification" => Notified(s, e.Message ?? string.Empty),
        "Stop" => s with { State = AgentState.Finished, FinishedAt = now, LastMessage = e.LastAssistantMessage ?? e.Message ?? s.LastMessage },
        "StopFailure" => s with { State = AgentState.Error, LastMessage = e.Message ?? s.LastMessage },
        "SubagentStart" => Step(s with { Subagents = s.Subagents + 1 }, now, AgentStepKind.Subagent, string.Empty),
        "SubagentStop" => SubagentDone(s, now),
        "PreCompact" => s with { State = AgentState.Compacting },
        "PostCompact" => s with { State = AgentState.Idle },
        _ => null,
    };

    private static AgentSession Notified(AgentSession s, string message)
    {
        if (message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            return s with { State = AgentState.RateLimited };
        }
        return message.TrimEnd().EndsWith('?') ? s with { State = AgentState.Question } : s;
    }

    private static AgentSession SubagentDone(AgentSession s, DateTimeOffset now)
    {
        var left = Math.Max(0, s.Subagents - 1);
        var state = s.State == AgentState.Background && left == 0 ? AgentState.Idle : s.State;
        return Step(s with { Subagents = left, State = state }, now, AgentStepKind.SubagentDone, string.Empty);
    }

    private static AgentSession ToolStep(AgentSession s, AgentEvent e, DateTimeOffset now)
    {
        var input = e.ToolInput;
        var (kind, detail) = e.ToolName switch
        {
            "Read" => (AgentStepKind.Read, FileName(Field(input, "file_path"))),
            "Edit" or "MultiEdit" or "Write" => (AgentStepKind.Edit, FileName(Field(input, "file_path"))),
            "NotebookEdit" => (AgentStepKind.Edit, FileName(Field(input, "notebook_path"))),
            "Bash" or "PowerShell" => (AgentStepKind.Run, OneLine(Field(input, "command"), CommandChars)),
            "Grep" or "Glob" => (AgentStepKind.Search, OneLine(Field(input, "pattern"), CommandChars)),
            "WebSearch" => (AgentStepKind.WebSearch, OneLine(Field(input, "query"), CommandChars)),
            "WebFetch" => (AgentStepKind.WebFetch, Host(Field(input, "url"))),
            "Task" or "Agent" => (AgentStepKind.Subagent, OneLine(Field(input, "description") is { Length: > 0 } d ? d : Field(input, "subagent_type"), CommandChars)),
            "TodoWrite" => (AgentStepKind.Plan, string.Empty),
            _ => (AgentStepKind.Tool, e.ToolName ?? string.Empty),
        };
        return Step(s, now, kind, detail);
    }

    private static AgentSession Step(AgentSession s, DateTimeOffset now, AgentStepKind kind, string detail)
    {
        var steps = s.Steps.Add(new AgentStep(now, kind, detail));
        return s with { Steps = steps.Count > MaxSteps ? steps.RemoveRange(0, steps.Count - MaxSteps) : steps };
    }

    private static string Field(JsonObject? input, string key) =>
        input?[key] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : string.Empty;

    private static string FileName(string path) => path.Length == 0 ? path : Path.GetFileName(path.Replace('\\', '/').TrimEnd('/'));

    private static string Host(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : OneLine(url, CommandChars);

    /// <summary>The first line, cut at a word boundary to at most <paramref name="limit"/> characters.</summary>
    private static string OneLine(string text, int limit)
    {
        var line = text.Trim().Split('\n', 2)[0].Trim();
        if (line.Length <= limit)
        {
            return line;
        }
        var cut = line[..limit];
        var space = cut.LastIndexOf(' ');
        return (space > limit / 2 ? cut[..space] : cut).TrimEnd() + "…";
    }
}
```

`src/Faqra.Core/Agents/AgentOrbStyles.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Urgency order from Coucou's MochiActivityState.swift (MIT, Copyright (c) 2026 Louis Raillé).

using Faqra.Core.Agents.Orb;

namespace Faqra.Core.Agents;

/// <summary>How urgent a state is, as a semantic colour role. Never the only signal: the orb's motion changes too.</summary>
public enum AgentTone { Neutral, Secondary, Caution, Accent, Critical, Success }

public readonly record struct AgentOrbStyle(OrbLook Look, AgentTone Tone, double Speed);

public static class AgentOrbStyles
{
    public static AgentOrbStyle For(AgentState state) => state switch
    {
        AgentState.Approval => new(OrbLook.Waiting, AgentTone.Caution, 1),
        AgentState.Question => new(OrbLook.Waiting, AgentTone.Accent, 1),
        AgentState.Error => new(OrbLook.RetryingSurge, AgentTone.Critical, 1),
        AgentState.RateLimited => new(OrbLook.Retrying, AgentTone.Caution, 1),
        AgentState.Working => new(OrbLook.Working, AgentTone.Neutral, 1),
        AgentState.Thinking => new(OrbLook.Reasoning, AgentTone.Neutral, 1),
        AgentState.Searching => new(OrbLook.Searching, AgentTone.Neutral, 1),
        AgentState.Compacting => new(OrbLook.Compacting, AgentTone.Neutral, 1),
        AgentState.Background => new(OrbLook.BackgroundSpiral, AgentTone.Secondary, 1),
        AgentState.Finished => new(OrbLook.Base, AgentTone.Success, 1),
        _ => new(OrbLook.Base, AgentTone.Secondary, 0.5),
    };

    /// <summary>Lower is more urgent: the owner's OK, a question, trouble, work, done, rest.</summary>
    public static int Urgency(AgentState state) => state switch
    {
        AgentState.Approval => 0,
        AgentState.Question => 1,
        AgentState.Error or AgentState.RateLimited => 2,
        AgentState.Working or AgentState.Thinking or AgentState.Searching or AgentState.Compacting or AgentState.Background => 3,
        AgentState.Finished => 4,
        _ => 5,
    };
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~AgentBoardTests"`
Expected: PASS (36 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Faqra.Core/Agents tests/Faqra.Core.Tests/Agents
git commit -m "feat(windows): agent session board with Coucou's state rules"
```

---
### Task 4: Editing Claude Code's settings safely

**Files:**
- Create: `src/Faqra.Core/Agents/Install/ClaudeHookConfig.cs`, `src/Faqra.Core/Agents/Install/UnifiedDiff.cs`, `src/Faqra.Core/Agents/Install/ConfigEdit.cs`
- Create: `tests/Faqra.Core.Tests/Agents/ClaudeHookConfigTests.cs`, `tests/Faqra.Core.Tests/Agents/UnifiedDiffTests.cs`

**Interfaces:**
- Consumes: `AgentJson.Indented` (Task 2).
- Produces:
  - `sealed class ConfigFormatException(string message) : Exception`
  - `readonly record struct HookStatus(int FaqraEvents, int CoucouEvents)` with `bool Installed`
  - `static class ClaudeHookConfig { const string Marker = "faqra-hook"; const string CoucouMarker = "coucou-hook"; IReadOnlyList<(string Event, int Timeout)> Events; string Install(string? json, string relayPath, bool removeCoucou); string Uninstall(string? json, bool removeCoucou); HookStatus Inspect(string? json) }`
  - `static class UnifiedDiff { enum LineKind { Hunk, Context, Removed, Added }; readonly record struct DiffLine(LineKind Kind, string Text); IReadOnlyList<DiffLine> Lines(string before, string after, int context = 3) }`
  - `static class ConfigEdit { string Fingerprint(ReadOnlySpan<byte> bytes); string BackupPath(string path, DateTime now, Func<string, bool> exists); string Decode(byte[] bytes) }`

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.Core.Tests/Agents/ClaudeHookConfigTests.cs`:

```csharp
using System.Text.Json.Nodes;
using Faqra.Core.Agents.Install;

namespace Faqra.Core.Tests.Agents;

public class ClaudeHookConfigTests
{
    private const string Relay = @"C:\Users\me\AppData\Local\Faqra\bin\faqra-hook.exe";

    // Shaped like a real settings.json: the owner's own hooks, permissions, and Coucou's leftovers.
    private const string Owner = """
        {
          "permissions": {
            "allow": [
              "Bash(npm test:*)"
            ]
          },
          "hooks": {
            "PostToolUse": [
              {
                "matcher": "Write|Edit",
                "hooks": [
                  {
                    "type": "command",
                    "command": "node \"C:/Users/me/.claude/activity/hook.js\"",
                    "timeout": 10
                  }
                ]
              },
              {
                "hooks": [
                  {
                    "type": "command",
                    "command": "\"C:/Users/me/AppData/Local/Coucou/bin/coucou-hook.exe\" PostToolUse",
                    "timeout": 10
                  }
                ]
              }
            ],
            "Stop": [
              {
                "hooks": [
                  {
                    "type": "command",
                    "command": "\"C:/Users/me/AppData/Local/Coucou/bin/coucou-hook.exe\" Stop",
                    "timeout": 10
                  }
                ]
              }
            ]
          }
        }
        """;

    private static JsonObject Parse(string json) => (JsonObject)JsonNode.Parse(json)!;

    private static IEnumerable<JsonObject> Handlers(JsonObject root, string name) =>
        ((JsonArray)root["hooks"]![name]!).OfType<JsonObject>().SelectMany(group => ((JsonArray)group["hooks"]!).OfType<JsonObject>());

    [Fact]
    public void InstallsEveryEventInExecForm()
    {
        var root = Parse(ClaudeHookConfig.Install(null, Relay, removeCoucou: false));
        foreach (var (name, timeout) in ClaudeHookConfig.Events)
        {
            var handler = Assert.Single(Handlers(root, name));
            Assert.Equal("command", handler["type"]!.GetValue<string>());
            Assert.Equal(Relay, handler["command"]!.GetValue<string>());
            Assert.Equal(name, Assert.Single((JsonArray)handler["args"]!)!.GetValue<string>());
            Assert.Equal(timeout, handler["timeout"]!.GetValue<int>());
        }
        Assert.Equal(14, ClaudeHookConfig.Events.Count);
    }

    [Fact]
    public void LeavesTheOwnersHooksAlone()
    {
        var before = Parse(Owner);
        var after = Parse(ClaudeHookConfig.Install(Owner, Relay, removeCoucou: false));
        Assert.True(JsonNode.DeepEquals(before["permissions"], after["permissions"]));
        var postToolUse = (JsonArray)after["hooks"]!["PostToolUse"]!;
        Assert.True(JsonNode.DeepEquals(before["hooks"]!["PostToolUse"]![0], postToolUse[0]));
        Assert.True(JsonNode.DeepEquals(before["hooks"]!["PostToolUse"]![1], postToolUse[1]));
        Assert.Equal(3, postToolUse.Count);
    }

    [Fact]
    public void RemovesCoucouOnlyWhenAsked()
    {
        var after = Parse(ClaudeHookConfig.Install(Owner, Relay, removeCoucou: true));
        var all = ClaudeHookConfig.Events.SelectMany(e => Handlers(after, e.Event)).Select(h => h["command"]!.GetValue<string>()).ToList();
        Assert.DoesNotContain(all, command => command.Contains("coucou-hook"));
        Assert.Contains(all, command => command.Contains("activity/hook.js"));
        Assert.Equal(2, ((JsonArray)after["hooks"]!["PostToolUse"]!).Count);
    }

    [Fact]
    public void ReinstallingKeepsOneEntryPerEvent()
    {
        var twice = ClaudeHookConfig.Install(ClaudeHookConfig.Install(Owner, Relay, false), Relay, false);
        Assert.Single(Handlers(Parse(twice), "SessionStart"));
        Assert.Equal(new HookStatus(14, 2), ClaudeHookConfig.Inspect(twice));
        Assert.True(ClaudeHookConfig.Inspect(twice).Installed);
    }

    [Fact]
    public void UninstallingRestoresTheOwnersFile()
    {
        var installed = ClaudeHookConfig.Install(Owner, Relay, removeCoucou: false);
        Assert.True(JsonNode.DeepEquals(Parse(Owner), Parse(ClaudeHookConfig.Uninstall(installed, removeCoucou: false))));
        Assert.Equal(new HookStatus(0, 0), ClaudeHookConfig.Inspect(ClaudeHookConfig.Uninstall(installed, removeCoucou: true)));
    }

    [Theory]
    [InlineData("{ // a comment\n}")]
    [InlineData("{\"a\": 1,}")]
    [InlineData("[]")]
    [InlineData("{\"hooks\": []}")]
    [InlineData("{\"hooks\": {\"Stop\": {}}}")]
    public void RefusesAFileItCannotEditSafely(string json) =>
        Assert.Throws<ConfigFormatException>(() => ClaudeHookConfig.Install(json, Relay, false));

    [Fact]
    public void WritesInTheFilesOwnStyle()
    {
        var crlf = ClaudeHookConfig.Install("{\r\n  \"a\": \"é <b> 'x' &\"\r\n}\r\n", Relay, false);
        Assert.Contains("\"é <b> 'x' &\"", crlf);
        Assert.DoesNotContain("\n", crlf.Replace("\r\n", string.Empty));
        Assert.EndsWith("}\r\n", crlf);
        var lf = ClaudeHookConfig.Install("{\n  \"a\": 1\n}", Relay, false);
        Assert.DoesNotContain("\r", lf);
        Assert.EndsWith("}", lf);
    }

    [Fact]
    public void ALeaveAloneRoundTripChangesNothing()
    {
        Assert.Equal(Owner.ReplaceLineEndings("\n"), ClaudeHookConfig.Uninstall(Owner.ReplaceLineEndings("\n"), removeCoucou: false));
    }
}
```

`tests/Faqra.Core.Tests/Agents/UnifiedDiffTests.cs`:

```csharp
using Faqra.Core.Agents.Install;
using static Faqra.Core.Agents.Install.UnifiedDiff;

namespace Faqra.Core.Tests.Agents;

public class UnifiedDiffTests
{
    private static string Text(params string[] lines) => string.Join("\n", lines) + "\n";

    [Fact]
    public void SameTextHasNoDiff() => Assert.Empty(Lines(Text("a", "b"), Text("a", "b")));

    [Fact]
    public void AChangeShowsWithThreeLinesAround()
    {
        var before = Text("l1", "l2", "l3", "l4", "l5", "l6", "l7", "l8", "l9", "l10");
        var after = Text("l1", "l2", "l3", "l4", "L5", "l6", "l7", "l8", "l9", "l10");
        Assert.Equal(
        [
            new(LineKind.Hunk, "@@ -2,7 +2,7 @@"),
            new(LineKind.Context, "l2"), new(LineKind.Context, "l3"), new(LineKind.Context, "l4"),
            new(LineKind.Removed, "l5"), new(LineKind.Added, "L5"),
            new(LineKind.Context, "l6"), new(LineKind.Context, "l7"), new(LineKind.Context, "l8"),
        ], Lines(before, after));
    }

    [Fact]
    public void ANewFileIsAllAdded()
    {
        Assert.Equal([new(LineKind.Hunk, "@@ -0,0 +1,2 @@"), new(LineKind.Added, "{"), new(LineKind.Added, "}")], Lines(string.Empty, Text("{", "}")));
    }

    [Fact]
    public void FarApartChangesMakeTwoHunks()
    {
        var before = Text(Enumerable.Range(1, 20).Select(i => $"l{i}").ToArray());
        var after = Text(Enumerable.Range(1, 20).Select(i => i is 2 or 19 ? $"L{i}" : $"l{i}").ToArray());
        Assert.Equal(2, Lines(before, after).Count(line => line.Kind == LineKind.Hunk));
    }

    [Fact]
    public void LineEndingsDoNotCountAsChanges() => Assert.Empty(Lines("a\r\nb\r\n", "a\nb\n"));
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~ClaudeHookConfigTests|FullyQualifiedName~UnifiedDiffTests"`
Expected: build error, `The type or namespace name 'Install' does not exist in the namespace 'Faqra.Core.Agents'`.

- [ ] **Step 3: Write the implementation**

`src/Faqra.Core/Agents/Install/ClaudeHookConfig.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Event list and ownership rule follow Coucou's hooks.rs, https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé). Faqra uses the exec form (command + args), so no shell starts.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents.Install;

/// <summary>The file is not plain JSON, or its hooks are shaped in a way Faqra will not guess at.</summary>
public sealed class ConfigFormatException(string message) : Exception(message);

/// <summary>How many of Faqra's events are registered, and how many events still carry Coucou's hooks.</summary>
public readonly record struct HookStatus(int FaqraEvents, int CoucouEvents)
{
    public bool Installed => FaqraEvents >= ClaudeHookConfig.Events.Count;
}

/// <summary>Adds, removes and counts Faqra's entries in Claude Code's settings.json, leaving every other byte's meaning alone.</summary>
public static class ClaudeHookConfig
{
    public const string Marker = "faqra-hook";
    public const string CoucouMarker = "coucou-hook";

    /// <summary>Every event Faqra listens to, with Claude Code's timeout in seconds. Stop allows the A3 reply window.</summary>
    public static readonly IReadOnlyList<(string Event, int Timeout)> Events =
    [
        ("SessionStart", 10), ("SessionEnd", 10), ("UserPromptSubmit", 10), ("PreToolUse", 10), ("PostToolUse", 10),
        ("PostToolUseFailure", 10), ("PermissionRequest", 120), ("Notification", 10), ("Stop", 600), ("StopFailure", 10),
        ("SubagentStart", 10), ("SubagentStop", 10), ("PreCompact", 10), ("PostCompact", 10),
    ];

    public static string Install(string? json, string relayPath, bool removeCoucou)
    {
        var (root, style) = Parse(json);
        var hooks = HooksOf(root, create: true)!;
        if (removeCoucou)
        {
            Remove(hooks, CoucouMarker);
        }
        Remove(hooks, Marker);
        foreach (var (name, timeout) in Events)
        {
            hooks[name] ??= new JsonArray();
            if (hooks[name] is not JsonArray groups)
            {
                throw new ConfigFormatException($"\"hooks.{name}\" is not a list");
            }
            groups.Add(new JsonObject
            {
                ["hooks"] = new JsonArray(new JsonObject
                {
                    ["type"] = "command",
                    ["command"] = relayPath,
                    ["args"] = new JsonArray(name),
                    ["timeout"] = timeout,
                }),
            });
        }
        return Write(root, style);
    }

    public static string Uninstall(string? json, bool removeCoucou)
    {
        var (root, style) = Parse(json);
        if (HooksOf(root, create: false) is { } hooks)
        {
            var removed = Remove(hooks, Marker) | (removeCoucou && Remove(hooks, CoucouMarker));
            if (removed && hooks.Count == 0)
            {
                root.Remove("hooks");
            }
        }
        return Write(root, style);
    }

    public static HookStatus Inspect(string? json)
    {
        var (root, _) = Parse(json);
        return HooksOf(root, create: false) is { } hooks
            ? new HookStatus(CountEvents(hooks, Marker), CountEvents(hooks, CoucouMarker))
            : new HookStatus(0, 0);
    }

    private static (JsonObject Root, TextStyle Style) Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (new JsonObject(), new TextStyle("\n", TrailingNewline: true));
        }
        var text = json.TrimStart('\uFEFF');
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new ConfigFormatException(ex.Message);
        }
        if (node is not JsonObject root)
        {
            throw new ConfigFormatException("the file is not a JSON object");
        }
        return (root, new TextStyle(text.Contains("\r\n") ? "\r\n" : "\n", text.EndsWith('\n')));
    }

    private static JsonObject? HooksOf(JsonObject root, bool create)
    {
        if (root["hooks"] is null)
        {
            if (!create)
            {
                return null;
            }
            root["hooks"] = new JsonObject();
        }
        return root["hooks"] as JsonObject ?? throw new ConfigFormatException("\"hooks\" is not an object");
    }

    /// <summary>Removes every handler whose command contains the marker; empties it leaves behind go too. True when anything went.</summary>
    private static bool Remove(JsonObject hooks, string marker)
    {
        var removed = false;
        foreach (var name in hooks.Select(pair => pair.Key).ToList())
        {
            if (hooks[name] is not JsonArray groups)
            {
                continue;
            }
            var touched = false;
            for (var g = groups.Count - 1; g >= 0; g--)
            {
                if (groups[g] is not JsonObject group || group["hooks"] is not JsonArray handlers)
                {
                    continue;
                }
                var before = handlers.Count;
                for (var h = handlers.Count - 1; h >= 0; h--)
                {
                    if (handlers[h] is JsonObject handler && IsMarked(handler, marker))
                    {
                        handlers.RemoveAt(h);
                    }
                }
                if (handlers.Count < before)
                {
                    touched = true;
                    if (handlers.Count == 0)
                    {
                        groups.RemoveAt(g);
                    }
                }
            }
            if (touched && groups.Count == 0)
            {
                hooks.Remove(name);
            }
            removed |= touched;
        }
        return removed;
    }

    private static int CountEvents(JsonObject hooks, string marker) => hooks.Count(pair =>
        pair.Value is JsonArray groups && groups.OfType<JsonObject>().Any(group =>
            group["hooks"] is JsonArray handlers && handlers.OfType<JsonObject>().Any(handler => IsMarked(handler, marker))));

    private static bool IsMarked(JsonObject handler, string marker) =>
        handler["command"] is JsonValue value && value.GetValueKind() == JsonValueKind.String
        && value.GetValue<string>().Contains(marker, StringComparison.OrdinalIgnoreCase);

    private static string Write(JsonObject root, TextStyle style)
    {
        // .NET 8 indents with the platform's newline; normalise, then use the file's own.
        var text = root.ToJsonString(AgentJson.Indented).Replace("\r\n", "\n");
        if (style.TrailingNewline)
        {
            text += "\n";
        }
        return style.Newline == "\n" ? text : text.Replace("\n", style.Newline);
    }

    private readonly record struct TextStyle(string Newline, bool TrailingNewline);
}
```

`src/Faqra.Core/Agents/Install/UnifiedDiff.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

namespace Faqra.Core.Agents.Install;

/// <summary>A line diff in unified form, for showing the owner exactly what an install changes.</summary>
public static class UnifiedDiff
{
    /// <summary>Above this many line pairs the diff gives up on alignment and shows a full replacement.</summary>
    private const long MaxCells = 4_000_000;

    public enum LineKind { Hunk, Context, Removed, Added }

    public readonly record struct DiffLine(LineKind Kind, string Text);

    private enum Op { Equal, Remove, Add }

    public static IReadOnlyList<DiffLine> Lines(string before, string after, int context = 3)
    {
        var a = Split(before);
        var b = Split(after);
        var ops = Script(a, b);
        var aBefore = new int[ops.Count + 1];
        var bBefore = new int[ops.Count + 1];
        for (var i = 0; i < ops.Count; i++)
        {
            aBefore[i + 1] = aBefore[i] + (ops[i].Op == Op.Add ? 0 : 1);
            bBefore[i + 1] = bBefore[i] + (ops[i].Op == Op.Remove ? 0 : 1);
        }

        var result = new List<DiffLine>();
        var index = 0;
        while (index < ops.Count)
        {
            if (ops[index].Op == Op.Equal)
            {
                index++;
                continue;
            }
            var start = Math.Max(0, index - context);
            var end = index;
            while (true)
            {
                while (end < ops.Count && ops[end].Op != Op.Equal)
                {
                    end++;
                }
                var next = end;
                while (next < ops.Count && ops[next].Op == Op.Equal)
                {
                    next++;
                }
                if (next < ops.Count && next - end <= 2 * context)
                {
                    end = next;
                    continue;
                }
                end = Math.Min(ops.Count, end + context);
                break;
            }
            var aCount = aBefore[end] - aBefore[start];
            var bCount = bBefore[end] - bBefore[start];
            var aStart = aBefore[start] + (aCount == 0 ? 0 : 1);
            var bStart = bBefore[start] + (bCount == 0 ? 0 : 1);
            result.Add(new DiffLine(LineKind.Hunk, $"@@ -{aStart},{aCount} +{bStart},{bCount} @@"));
            for (var i = start; i < end; i++)
            {
                var kind = ops[i].Op switch { Op.Remove => LineKind.Removed, Op.Add => LineKind.Added, _ => LineKind.Context };
                result.Add(new DiffLine(kind, ops[i].Text));
            }
            index = end;
        }
        return result;
    }

    private static string[] Split(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }
        var lines = text.Replace("\r\n", "\n").Split('\n');
        return lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    private static List<(Op Op, string Text)> Script(string[] a, string[] b)
    {
        var ops = new List<(Op, string)>();
        if ((long)a.Length * b.Length > MaxCells)
        {
            ops.AddRange(a.Select(line => (Op.Remove, line)));
            ops.AddRange(b.Select(line => (Op.Add, line)));
            return ops;
        }
        var lcs = new int[a.Length + 1, b.Length + 1];
        for (var x = a.Length - 1; x >= 0; x--)
        {
            for (var y = b.Length - 1; y >= 0; y--)
            {
                lcs[x, y] = a[x] == b[y] ? lcs[x + 1, y + 1] + 1 : Math.Max(lcs[x + 1, y], lcs[x, y + 1]);
            }
        }
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (a[i] == b[j])
            {
                ops.Add((Op.Equal, a[i]));
                i++;
                j++;
            }
            else if (lcs[i + 1, j] >= lcs[i, j + 1])
            {
                ops.Add((Op.Remove, a[i++]));
            }
            else
            {
                ops.Add((Op.Add, b[j++]));
            }
        }
        while (i < a.Length)
        {
            ops.Add((Op.Remove, a[i++]));
        }
        while (j < b.Length)
        {
            ops.Add((Op.Add, b[j++]));
        }
        return ops;
    }
}
```

`src/Faqra.Core/Agents/Install/ConfigEdit.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Backup naming follows Coucou's config_file.rs (MIT, Copyright (c) 2026 Louis Raillé).

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Faqra.Core.Agents.Install;

public static class ConfigEdit
{
    /// <summary>Identifies the exact bytes a preview was made from, so a later change is noticed before writing.</summary>
    public static string Fingerprint(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary><c>settings.json.bak-20261009-173005</c>, then <c>-1</c>, <c>-2</c> while the name is taken.</summary>
    public static string BackupPath(string path, DateTime now, Func<string, bool> exists)
    {
        var stem = $"{path}.bak-{now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}";
        if (!exists(stem))
        {
            return stem;
        }
        for (var i = 1; ; i++)
        {
            var candidate = $"{stem}-{i}";
            if (!exists(candidate))
            {
                return candidate;
            }
        }
    }

    public static string Decode(byte[] bytes) => new UTF8Encoding(false).GetString(bytes).TrimStart('\uFEFF');
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~ClaudeHookConfigTests|FullyQualifiedName~UnifiedDiffTests"`
Expected: PASS (17 tests). If `ALeaveAloneRoundTripChangesNothing` fails, print both strings and fix `Write` until System.Text.Json's indented output matches Claude Code's two-space style; do not loosen the test.

- [ ] **Step 5: Commit**

```bash
git add src/Faqra.Core/Agents/Install tests/Faqra.Core.Tests/Agents
git commit -m "feat(windows): install and remove Faqra's Claude Code hooks without touching the owner's"
```

---

### Task 5: The relay, `faqra-hook.exe`

**Files:**
- Create: `src/Faqra.Hook/Faqra.Hook.csproj`, `src/Faqra.Hook/Program.cs`, `src/Faqra.Hook/Relay.cs`
- Modify: `Faqra.sln` (add the project), `tests/Faqra.Services.Tests/Faqra.Services.Tests.csproj` (reference it)
- Create: `tests/Faqra.Services.Tests/Agents/RelayTests.cs`

**Interfaces:**
- Consumes: `HookPayload.ToLine`, `AgentPipe.Name` (Task 2).
- Produces: `public static class Relay { const int ConnectBudgetMs = 300; const int RunBudgetMs = 2000; int Run(string[] args, Stream stdin, Func<string, string?> env, string cwd, string pipeName) }`; executable `faqra-hook.exe` taking the event name as its only argument.

- [ ] **Step 1: Create the project and add it to the solution**

`src/Faqra.Hook/Faqra.Hook.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <AssemblyName>faqra-hook</AssemblyName>
    <RootNamespace>Faqra.Hook</RootNamespace>
    <!-- It starts on every hook event, so it carries nothing it does not need. -->
    <InvariantGlobalization>true</InvariantGlobalization>
    <UseSystemResourceKeys>true</UseSystemResourceKeys>
    <PublishReadyToRun>true</PublishReadyToRun>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../Faqra.Core/Faqra.Core.csproj" />
  </ItemGroup>
</Project>
```

Run: `dotnet sln Faqra.sln add src/Faqra.Hook/Faqra.Hook.csproj`
Expected: `Project 'src\Faqra.Hook\Faqra.Hook.csproj' added to the solution.`

Add to `tests/Faqra.Services.Tests/Faqra.Services.Tests.csproj`, in the existing `ProjectReference` item group:

```xml
    <ProjectReference Include="../../src/Faqra.Hook/Faqra.Hook.csproj" />
```

- [ ] **Step 2: Write the failing tests**

`tests/Faqra.Services.Tests/Agents/RelayTests.cs`:

```csharp
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using Faqra.Hook;

namespace Faqra.Services.Tests.Agents;

public class RelayTests
{
    private static readonly Func<string, string?> NoEnv = _ => null;

    private static string PipeName() => "faqra-test-" + Guid.NewGuid().ToString("N");

    private static Stream Stdin(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    private static NamedPipeServerStream Server(string name, int inBuffer = 0) =>
        new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, inBuffer, 0);

    [Fact]
    public async Task SendsOneTrimmedLine()
    {
        var name = PipeName();
        using var server = Server(name);
        var received = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            return await reader.ReadLineAsync();
        });

        var exit = Relay.Run(["PreToolUse"], Stdin("{\"session_id\":\"s1\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test\"}}"), NoEnv, @"C:\work", name);

        Assert.Equal(0, exit);
        var line = JsonNode.Parse((await received.WaitAsync(TimeSpan.FromSeconds(5)))!)!;
        Assert.Equal("PreToolUse", line["hook_event_name"]!.GetValue<string>());
        Assert.Equal("claude", line["faqra_agent"]!.GetValue<string>());
        Assert.Equal("npm test", line["tool_input"]!["command"]!.GetValue<string>());
    }

    [Fact]
    public void ExitsAtOnceWhenFaqraIsClosed()
    {
        Relay.Run(["Stop"], Stdin("{}"), NoEnv, @"C:\work", PipeName()); // warm up the JIT
        var clock = Stopwatch.StartNew();
        Assert.Equal(0, Relay.Run(["Stop"], Stdin("{\"session_id\":\"s1\"}"), NoEnv, @"C:\work", PipeName()));
        // No pipe means no Faqra: waiting the 300 ms connect budget here would slow every tool call.
        Assert.InRange(clock.ElapsedMilliseconds, 0, 150);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1]")]
    public async Task SendsNothingForGarbage(string stdin)
    {
        var name = PipeName();
        using var server = Server(name);
        var connected = server.WaitForConnectionAsync();
        Assert.Equal(0, Relay.Run(["Stop"], Stdin(stdin), NoEnv, @"C:\work", name));
        var winner = await Task.WhenAny(connected, Task.Delay(500));
        Assert.NotSame(connected, winner);
    }

    [Fact]
    public async Task GivesUpOnAServerThatNeverReads()
    {
        var name = PipeName();
        using var server = Server(name, inBuffer: 4096);
        var connected = server.WaitForConnectionAsync();
        // Every field stays under the 2,000-character cut, so the line stays far larger than the pipe's buffer.
        var many = "{\"session_id\":\"s1\"," + string.Join(",", Enumerable.Range(0, 2000).Select(i => $"\"f{i}\":\"{new string('x', 1500)}\"")) + "}";
        var clock = Stopwatch.StartNew();
        Assert.Equal(0, Relay.Run(["UserPromptSubmit"], Stdin(many), NoEnv, @"C:\work", name));
        Assert.InRange(clock.ElapsedMilliseconds, Relay.RunBudgetMs - 100, Relay.RunBudgetMs + 1500);
        await connected;
    }
}
```

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet test tests/Faqra.Services.Tests --filter "FullyQualifiedName~RelayTests"`
Expected: build error, `The type or namespace name 'Relay' could not be found`.

- [ ] **Step 4: Write the relay**

`src/Faqra.Hook/Relay.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of coucou-hook (windows/hook/src/main.rs) in Coucou, https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé).

using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using Faqra.Core.Agents;

namespace Faqra.Hook;

/// <summary>
/// Claude Code runs this once per hook event. It hands the event to Faqra and gets out of the way: whatever
/// happens it exits 0 and prints nothing, so a session never waits on Faqra or changes because of it.
/// </summary>
public static partial class Relay
{
    public const int ConnectBudgetMs = 300;
    public const int RunBudgetMs = 2000;

    /// <summary>Larger than any hook payload; anything bigger is not read whole.</summary>
    private const int MaxStdinBytes = 32 * 1024 * 1024;

    public static int Run(string[] args, Stream stdin, Func<string, string?> env, string cwd, string pipeName)
    {
        var eventArg = args.Length > 0 ? args[0] : string.Empty;
        if (ReadAll(stdin) is not { } text || HookPayload.ToLine(text, eventArg, "claude", env, cwd) is not { } line)
        {
            return 0;
        }
        // A Faqra that stops reading is not worth a slower session: the send gets a fixed budget.
        Task.Run(() => Send(pipeName, line)).Wait(RunBudgetMs);
        return 0;
    }

    private static void Send(string pipeName, string line)
    {
        try
        {
            // NamedPipeClientStream.Connect keeps retrying a pipe that does not exist until its timeout, and
            // Faqra being closed is the common case. WaitNamedPipe returns at once when there is no pipe and
            // waits only while every instance is busy, so a false answer means: give up now.
            if (!WaitNamedPipe(@"\\.\pipe\" + pipeName, ConnectBudgetMs))
            {
                return;
            }
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
            pipe.Connect(ConnectBudgetMs);
            pipe.Write(Encoding.UTF8.GetBytes(line + "\n"));
            pipe.Flush();
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            // Faqra is closed, busy, or the pipe belongs to someone else: the session carries on as if Faqra did not exist.
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "WaitNamedPipeW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WaitNamedPipe(string name, uint timeoutMs);

    private static string? ReadAll(Stream stdin)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int read;
        while ((read = stdin.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > MaxStdinBytes)
            {
                return null;
            }
            buffer.Write(chunk, 0, read);
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
```

`src/Faqra.Hook/Program.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Security.Principal;
using Faqra.Core.Agents;

namespace Faqra.Hook;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            Func<string, string?> env = Environment.GetEnvironmentVariable;
            var sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
            return Relay.Run(args, Console.OpenStandardInput(), env, Environment.CurrentDirectory, AgentPipe.Name(sid, env));
        }
        catch (Exception)
        {
            return 0; // a hook that fails must never fail the session
        }
    }
}
```

- [ ] **Step 5: Run the tests to see them pass**

Run: `dotnet test tests/Faqra.Services.Tests --filter "FullyQualifiedName~RelayTests"`
Expected: PASS (6 tests).

- [ ] **Step 6: Commit**

```bash
git add src/Faqra.Hook Faqra.sln tests/Faqra.Services.Tests
git commit -m "feat(windows): faqra-hook relay hands Claude Code events to Faqra and never blocks"
```

---

### Task 6: The hub, config files on disk, relay deployment

**Files:**
- Create: `src/Faqra.Services/Agents/AgentHub.cs`, `src/Faqra.Services/Agents/ConfigFile.cs`, `src/Faqra.Services/Agents/RelayDeployment.cs`
- Create: `tests/Faqra.Services.Tests/Agents/AgentHubTests.cs`, `tests/Faqra.Services.Tests/Agents/ConfigFileTests.cs`, `tests/Faqra.Services.Tests/Agents/RelayDeploymentTests.cs`, `tests/Faqra.Services.Tests/Agents/SerialContext.cs`

**Interfaces:**
- Consumes: `AgentEvent`, `AgentBoard` (Tasks 2-3); `ConfigEdit`, `UnifiedDiff`, `ConfigFormatException` (Task 4).
- Produces:
  - `sealed class AgentHub(string pipeName, SynchronizationContext context, Func<DateTimeOffset> now, string? logPath = null) : IDisposable` with `AgentBoard Board`, `bool IsRunning`, `event Action? Changed` (raised on the context), `void SetRunning(bool running)`, `const int MaxLineBytes = 1048576`
  - `sealed record ConfigPreview(string Path, bool Exists, string Fingerprint, string Before, string After, IReadOnlyList<UnifiedDiff.DiffLine> Diff, string? BackupPath)` with `bool Changes`; `sealed class ConfigChangedException : Exception`; `static class ConfigFile { ConfigPreview Preview(string path, Func<string?, string> transform, DateTime now); void Apply(ConfigPreview preview) }`
  - `static class RelayDeployment { const string FileName = "faqra-hook.exe"; bool Ensure(string sourceDirectory, string targetPath) }`

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.Services.Tests/Agents/SerialContext.cs`:

```csharp
using System.Collections.Concurrent;

namespace Faqra.Services.Tests.Agents;

/// <summary>Runs every post on one thread, in order, like the UI dispatcher does.</summary>
internal sealed class SerialContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
    private readonly Thread _thread;

    public SerialContext()
    {
        _thread = new Thread(() =>
        {
            foreach (var (callback, state) in _queue.GetConsumingEnumerable())
            {
                callback(state);
            }
        }) { IsBackground = true };
        _thread.Start();
    }

    public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

    public void Dispose() => _queue.CompleteAdding();
}
```

`tests/Faqra.Services.Tests/Agents/AgentHubTests.cs`:

```csharp
using System.IO.Pipes;
using System.Text;
using Faqra.Core.Agents;
using Faqra.Services.Agents;

namespace Faqra.Services.Tests.Agents;

public class AgentHubTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private static async Task SendAsync(string pipe, string line)
    {
        using var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(2000);
        await client.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"));
        await client.FlushAsync();
    }

    private static async Task<AgentBoard> WaitFor(AgentHub hub, Func<AgentBoard, bool> done)
    {
        for (var i = 0; i < 100 && !done(hub.Board); i++)
        {
            await Task.Delay(50);
        }
        return hub.Board;
    }

    [Fact]
    public async Task FoldsEventsIntoTheBoard()
    {
        var pipe = "faqra-test-" + Guid.NewGuid().ToString("N");
        using var context = new SerialContext();
        using var hub = new AgentHub(pipe, context, () => T0);
        var changes = 0;
        hub.Changed += () => Interlocked.Increment(ref changes);
        hub.SetRunning(true);

        await SendAsync(pipe, "{\"hook_event_name\":\"UserPromptSubmit\",\"session_id\":\"s1\",\"cwd\":\"C:\\\\code\\\\faqra\",\"prompt\":\"go\"}");

        var board = await WaitFor(hub, b => b.Sessions.Count == 1);
        Assert.Equal(AgentState.Thinking, board.Sessions["s1"].State);
        Assert.True(changes >= 1);
    }

    [Fact]
    public async Task AcceptsManyConnectionsAtOnce()
    {
        var pipe = "faqra-test-" + Guid.NewGuid().ToString("N");
        using var context = new SerialContext();
        using var hub = new AgentHub(pipe, context, () => T0);
        hub.SetRunning(true);

        await Task.WhenAll(Enumerable.Range(0, 40).Select(i =>
            SendAsync(pipe, $"{{\"hook_event_name\":\"SessionStart\",\"session_id\":\"s{i}\"}}")));

        Assert.Equal(40, (await WaitFor(hub, b => b.Sessions.Count == 40)).Sessions.Count);
    }

    [Fact]
    public async Task IgnoresGarbageAndStopsCleanly()
    {
        var pipe = "faqra-test-" + Guid.NewGuid().ToString("N");
        using var context = new SerialContext();
        using var hub = new AgentHub(pipe, context, () => T0);
        hub.SetRunning(true);
        await SendAsync(pipe, "not json");
        await SendAsync(pipe, "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"ok\"}");
        Assert.Single((await WaitFor(hub, b => b.Sessions.Count == 1)).Sessions);

        hub.SetRunning(false);
        Assert.False(hub.IsRunning);
        Assert.Empty((await WaitFor(hub, b => b.Sessions.Count == 0)).Sessions);
        using var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
        Assert.Throws<TimeoutException>(() => client.Connect(300));
    }

    [Fact]
    public async Task WritesALogWithoutTheOwnersText()
    {
        var pipe = "faqra-test-" + Guid.NewGuid().ToString("N");
        var log = Path.Combine(Path.GetTempPath(), $"faqra-agents-{Guid.NewGuid():N}.log");
        using var context = new SerialContext();
        using (var hub = new AgentHub(pipe, context, () => T0, log))
        {
            hub.SetRunning(true);
            await SendAsync(pipe, "{\"hook_event_name\":\"UserPromptSubmit\",\"session_id\":\"abcdef123456\",\"prompt\":\"secret plans\"}");
            await WaitFor(hub, b => b.Sessions.Count == 1);
            await Task.Delay(200);
        }
        var text = File.ReadAllText(log);
        Assert.Contains("UserPromptSubmit", text);
        Assert.Contains("abcdef12", text);
        Assert.DoesNotContain("secret plans", text);
        File.Delete(log);
    }
}
```

`tests/Faqra.Services.Tests/Agents/ConfigFileTests.cs`:

```csharp
using System.Text;
using Faqra.Core.Agents.Install;
using Faqra.Services.Agents;

namespace Faqra.Services.Tests.Agents;

public sealed class ConfigFileTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 9, 17, 30, 5);
    private readonly string _dir = Directory.CreateTempSubdirectory("faqra-config-").FullName;

    private string SettingsPath => Path.Combine(_dir, "settings.json");

    private static string AddKey(string? json) => (json is null ? "{\n" : json.TrimEnd().TrimEnd('}')) + "  ,\"faqra\": 1\n}\n";

    [Fact]
    public void PreviewShowsTheEditAndWritesNothing()
    {
        File.WriteAllText(SettingsPath, "{\n  \"a\": 1\n}\n");
        var preview = ConfigFile.Preview(SettingsPath, AddKey, Now);
        Assert.True(preview.Changes);
        Assert.NotEmpty(preview.Diff);
        Assert.Equal(SettingsPath + ".bak-20261009-173005", preview.BackupPath);
        Assert.Equal("{\n  \"a\": 1\n}\n", File.ReadAllText(SettingsPath));
        Assert.False(File.Exists(preview.BackupPath));
    }

    [Fact]
    public void ApplyKeepsAByteExactBackup()
    {
        var original = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("{\r\n  \"a\": 1\r\n}\r\n")).ToArray();
        File.WriteAllBytes(SettingsPath, original);
        var preview = ConfigFile.Preview(SettingsPath, AddKey, Now);
        ConfigFile.Apply(preview);
        Assert.Equal(original, File.ReadAllBytes(preview.BackupPath!));
        Assert.Equal(preview.After, File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void ApplyRefusesAFileThatChangedSinceThePreview()
    {
        File.WriteAllText(SettingsPath, "{\n  \"a\": 1\n}\n");
        var preview = ConfigFile.Preview(SettingsPath, AddKey, Now);
        File.WriteAllText(SettingsPath, "{\n  \"a\": 2\n}\n");
        Assert.Throws<ConfigChangedException>(() => ConfigFile.Apply(preview));
        Assert.Equal("{\n  \"a\": 2\n}\n", File.ReadAllText(SettingsPath));
        Assert.False(File.Exists(preview.BackupPath));
    }

    [Fact]
    public void ApplyCreatesAMissingFileWithoutABackup()
    {
        var path = Path.Combine(_dir, "new", "settings.json");
        var preview = ConfigFile.Preview(path, AddKey, Now);
        Assert.Null(preview.BackupPath);
        ConfigFile.Apply(preview);
        Assert.Equal(preview.After, File.ReadAllText(path));
    }

    [Fact]
    public void PreviewPassesAFormatProblemThrough()
    {
        File.WriteAllText(SettingsPath, "{ // comment\n}");
        Assert.Throws<ConfigFormatException>(() => ConfigFile.Preview(SettingsPath, json => ClaudeHookConfig.Install(json, "x", false), Now));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
```

`tests/Faqra.Services.Tests/Agents/RelayDeploymentTests.cs`:

```csharp
using Faqra.Services.Agents;

namespace Faqra.Services.Tests.Agents;

public sealed class RelayDeploymentTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("faqra-relay-").FullName;

    private string Source => Path.Combine(_dir, "app");

    private string Target => Path.Combine(_dir, "bin", RelayDeployment.FileName);

    private void Ship(string content)
    {
        Directory.CreateDirectory(Source);
        File.WriteAllText(Path.Combine(Source, RelayDeployment.FileName), content);
    }

    [Fact]
    public void CopiesTheRelayBesideTheApp()
    {
        Ship("v1");
        Assert.True(RelayDeployment.Ensure(Source, Target));
        Assert.Equal("v1", File.ReadAllText(Target));
    }

    [Fact]
    public void ReplacesAnOlderRelayAndKeepsAnIdenticalOne()
    {
        Ship("v1");
        RelayDeployment.Ensure(Source, Target);
        var stamp = File.GetLastWriteTimeUtc(Target);
        Assert.True(RelayDeployment.Ensure(Source, Target));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(Target));
        Ship("v2");
        Assert.True(RelayDeployment.Ensure(Source, Target));
        Assert.Equal("v2", File.ReadAllText(Target));
    }

    [Fact]
    public void WithoutASourceItReportsWhetherOneIsInPlace()
    {
        Assert.False(RelayDeployment.Ensure(Source, Target));
        Directory.CreateDirectory(Path.GetDirectoryName(Target)!);
        File.WriteAllText(Target, "old");
        Assert.True(RelayDeployment.Ensure(Source, Target));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test tests/Faqra.Services.Tests --filter "FullyQualifiedName~AgentHubTests|FullyQualifiedName~ConfigFileTests|FullyQualifiedName~RelayDeploymentTests"`
Expected: build error, `The type or namespace name 'Agents' does not exist in the namespace 'Faqra.Services'`.

- [ ] **Step 3: Write the implementation**

`src/Faqra.Services/Agents/AgentHub.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of Coucou's pipe server (windows/src-tauri/src/pipe.rs), https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé).

using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using Faqra.Core.Agents;

namespace Faqra.Services.Agents;

/// <summary>
/// Listens on the agents pipe and folds every event into <see cref="Board"/>. The board is read and replaced
/// only on the given context's thread (the UI thread in the app), and <see cref="Changed"/> is raised there.
/// </summary>
public sealed class AgentHub : IDisposable
{
    public const int MaxLineBytes = 1024 * 1024;
    private const long MaxLogBytes = 1024 * 1024;
    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private readonly string _pipeName;
    private readonly SynchronizationContext _context;
    private readonly Func<DateTimeOffset> _now;
    private readonly string? _logPath;
    private CancellationTokenSource? _running;
    private Timer? _tick;
    private bool _disposed;

    public AgentHub(string pipeName, SynchronizationContext context, Func<DateTimeOffset> now, string? logPath = null)
    {
        _pipeName = pipeName;
        _context = context;
        _now = now;
        _logPath = logPath;
    }

    public AgentBoard Board { get; private set; } = AgentBoard.Empty;

    public bool IsRunning => _running is not null;

    public event Action? Changed;

    /// <summary>Starts or stops listening; stopping forgets every session. Call on the context's thread.</summary>
    public void SetRunning(bool running)
    {
        if (running)
        {
            Start();
        }
        else
        {
            Stop();
        }
    }

    private void Start()
    {
        if (_running is not null || _disposed)
        {
            return;
        }
        _running = new CancellationTokenSource();
        var token = _running.Token;
        _ = Task.Run(() => AcceptLoop(token));
        _tick = new Timer(_ => Post(TickBoard), null, TickInterval, TickInterval);
    }

    private void Stop()
    {
        if (_running is null)
        {
            return;
        }
        _running.Cancel();
        _running.Dispose();
        _running = null;
        _tick?.Dispose();
        _tick = null;
        Post(() =>
        {
            Board = AgentBoard.Empty;
            Changed?.Invoke();
        });
    }

    private async Task AcceptLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                server?.Dispose();
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Another process holds the name (or a burst used every instance): wait and try again.
                server?.Dispose();
                Trace.TraceWarning($"Faqra agents pipe: {ex.Message}");
                try
                {
                    await Task.Delay(RetryDelay, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                continue;
            }
            var connection = server;
            _ = Task.Run(() => Serve(connection, token), CancellationToken.None);
        }
    }

    private async Task Serve(NamedPipeServerStream connection, CancellationToken token)
    {
        using (connection)
        {
            string? line;
            try
            {
                line = await ReadLine(connection, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            {
                return;
            }
            if (line is null || AgentEvent.TryParse(line) is not { } e)
            {
                return;
            }
            Post(() =>
            {
                Board = Board.Apply(e, _now());
                Log(e);
                Changed?.Invoke();
            });
        }
    }

    private static async Task<string?> ReadLine(Stream stream, CancellationToken token)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(ReadBudget);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, budget.Token).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            var newline = Array.IndexOf(chunk, (byte)'\n', 0, read);
            buffer.Write(chunk, 0, newline >= 0 ? newline : read);
            if (newline >= 0)
            {
                break;
            }
            if (buffer.Length > MaxLineBytes)
            {
                return null;
            }
        }
        return buffer.Length == 0 ? null : Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private void TickBoard()
    {
        var next = Board.Tick(_now());
        if (!ReferenceEquals(next, Board))
        {
            Board = next;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// One line per event: time, event, session, tool and the state it led to. Never the prompt, command,
    /// path or Claude's words, so the log can be shared when something goes wrong.
    /// </summary>
    private void Log(AgentEvent e)
    {
        if (_logPath is null)
        {
            return;
        }
        try
        {
            if (File.Exists(_logPath) && new FileInfo(_logPath).Length > MaxLogBytes)
            {
                File.Move(_logPath, _logPath + ".1", overwrite: true);
            }
            var session = e.SessionId.Length > 8 ? e.SessionId[..8] : e.SessionId;
            var state = Board.Sessions.TryGetValue(e.SessionId, out var s) ? s.State.ToString() : "gone";
            File.AppendAllText(_logPath, $"{_now():O} {e.Event} {session} {e.ToolName ?? "-"} {state}\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Faqra agents log: {ex.Message}");
        }
    }

    private void Post(Action action) => _context.Post(_ =>
    {
        if (!_disposed)
        {
            action();
        }
    }, null);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        Stop();
        _disposed = true;
    }
}
```

`src/Faqra.Services/Agents/ConfigFile.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Preview, fingerprint and backup follow Coucou's config_file.rs (MIT, Copyright (c) 2026 Louis Raillé).

using System.Text;
using Faqra.Core.Agents.Install;

namespace Faqra.Services.Agents;

/// <summary>An edit to a config file, worked out but not written.</summary>
public sealed record ConfigPreview(
    string Path, bool Exists, string Fingerprint, string Before, string After,
    IReadOnlyList<UnifiedDiff.DiffLine> Diff, string? BackupPath)
{
    public bool Changes => Before != After;
}

/// <summary>The file changed between the preview and the click, so the preview no longer describes it.</summary>
public sealed class ConfigChangedException() : Exception("The file changed since it was previewed.");

public static class ConfigFile
{
    public static ConfigPreview Preview(string path, Func<string?, string> transform, DateTime now)
    {
        var exists = File.Exists(path);
        var bytes = exists ? File.ReadAllBytes(path) : [];
        var before = exists ? ConfigEdit.Decode(bytes) : null;
        var after = transform(before);
        var text = before ?? string.Empty;
        return new ConfigPreview(path, exists, ConfigEdit.Fingerprint(bytes), text, after,
            UnifiedDiff.Lines(text, after), exists ? ConfigEdit.BackupPath(path, now, File.Exists) : null);
    }

    /// <summary>Writes the previewed text, keeping the old file byte for byte as the backup.</summary>
    public static void Apply(ConfigPreview preview)
    {
        var exists = File.Exists(preview.Path);
        var bytes = exists ? File.ReadAllBytes(preview.Path) : [];
        if (exists != preview.Exists || ConfigEdit.Fingerprint(bytes) != preview.Fingerprint)
        {
            throw new ConfigChangedException();
        }
        Directory.CreateDirectory(Path.GetDirectoryName(preview.Path)!);
        var temp = preview.Path + ".faqra-tmp";
        File.WriteAllText(temp, preview.After, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (exists)
        {
            File.Replace(temp, preview.Path, preview.BackupPath);
        }
        else
        {
            File.Move(temp, preview.Path);
        }
    }
}
```

`src/Faqra.Services/Agents/RelayDeployment.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Diagnostics;
using System.Security.Cryptography;

namespace Faqra.Services.Agents;

/// <summary>
/// Claude Code's hooks name one fixed path, so the relay shipped beside the app is copied there whenever it
/// differs. A relay that is running right now can be renamed but not overwritten, so the old one moves aside.
/// </summary>
public static class RelayDeployment
{
    public const string FileName = "faqra-hook.exe";

    /// <summary>True when a relay is in place at <paramref name="targetPath"/> afterwards.</summary>
    public static bool Ensure(string sourceDirectory, string targetPath)
    {
        var source = Path.Combine(sourceDirectory, FileName);
        if (!File.Exists(source))
        {
            return File.Exists(targetPath);
        }
        if (File.Exists(targetPath) && Same(source, targetPath))
        {
            return true;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            var fresh = targetPath + ".new";
            File.Copy(source, fresh, overwrite: true);
            if (File.Exists(targetPath))
            {
                File.Move(targetPath, targetPath + ".old", overwrite: true);
            }
            File.Move(fresh, targetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Faqra could not update {targetPath}: {ex.Message}");
        }
        return File.Exists(targetPath);
    }

    private static bool Same(string a, string b)
    {
        if (new FileInfo(a).Length != new FileInfo(b).Length)
        {
            return false;
        }
        using var first = File.OpenRead(a);
        using var second = File.OpenRead(b);
        return SHA256.HashData(first).AsSpan().SequenceEqual(SHA256.HashData(second));
    }
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test tests/Faqra.Services.Tests --filter "FullyQualifiedName~AgentHubTests|FullyQualifiedName~ConfigFileTests|FullyQualifiedName~RelayDeploymentTests"`
Expected: PASS (12 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Faqra.Services/Agents tests/Faqra.Services.Tests/Agents
git commit -m "feat(windows): agents hub, safe config writes and relay deployment"
```

---
### Task 7: Register the feature, the module, the page and the strings

**Files:**
- Modify: `src/Faqra.Core/Features/AppFeature.cs`, `src/Faqra.Core/Features/FeatureCatalog.cs`, `src/Faqra.Core/Features/FeatureWindowsSupport.cs`, `src/Faqra.Core/Features/FeaturePreset.cs`, `src/Faqra.Core/Defaults/DefaultsMigrations.cs`, `src/Faqra.Core/Localization/FeatureHubStrings.EnUS.cs`, `src/Faqra.Core/Localization/Strings.EnUS.cs`, `src/Faqra.Core/Settings/SettingsPage.cs`, `src/Faqra.Core/Island/IslandModule.cs`, `src/Faqra.Core/AppPaths.cs`
- Create: `src/Faqra.Core/Localization/AgentsStrings.cs`, `src/Faqra.Core/Localization/AgentsStrings.EnUS.cs`, `src/Faqra.Core/Agents/AgentsText.cs`
- Create: `tests/Faqra.Core.Tests/Agents/AgentsTextTests.cs`, `tests/Faqra.Core.Tests/Agents/AgentsRegistrationTests.cs`
- Modify (counts): `tests/Faqra.Core.Tests/FeatureCatalogTests.cs:10,61`, `tests/Faqra.Core.Tests/IslandModuleTests.cs:13-15,55-56,77`, `tests/Faqra.Core.Tests/FeaturePresetTests.cs:28`, `tests/Faqra.App.Tests/UiRenderTests.cs:74,76,144`

**Interfaces:**
- Consumes: `AgentSession`, `AgentStep`, `AgentState`, `AgentStepKind` (Task 3).
- Produces: `AppFeature.FaqraAgents`; `IslandModule.FaqraAgents`; `SettingsPage.Agents`; `AppPaths.AgentsRelayFile`, `AppPaths.AgentsLogFile`, `AppPaths.ClaudeSettingsFile`; `DefaultsMigrations.InstallAgentsOnce(ISettingsStore)`; `sealed partial class AgentsStrings` with `static AgentsStrings For(AppLanguage)` and `static AgentsStrings EnUS`; `static class AgentsText { string State(AgentSession, AgentsStrings); string Step(AgentStep, AgentsStrings); string Status(AgentSession, AgentsStrings); string Elapsed(TimeSpan, AgentsStrings) }`.

- [ ] **Step 1: Write the failing tests**

`tests/Faqra.Core.Tests/Agents/AgentsRegistrationTests.cs`:

```csharp
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Island;
using Faqra.Core.Localization;
using Faqra.Core.Settings;

namespace Faqra.Core.Tests.Agents;

public class AgentsRegistrationTests
{
    [Fact]
    public void TheFeatureIsFaqrasOwnAndShipsOn()
    {
        Assert.Equal("faqraAgents", AppFeature.FaqraAgents.RawValue());
        Assert.Equal("featureAvailable.faqraAgents", AppFeature.FaqraAgents.AvailabilityKey());
        Assert.Equal(FeatureGroup.Tools, AppFeature.FaqraAgents.Group());
        Assert.True(FeatureWindowsSupport.IsBuilt(AppFeature.FaqraAgents));
        Assert.True(AppFeature.FaqraAgents.IsSupported());
        Assert.Contains(AppFeature.FaqraAgents, FeaturePresets.FirstRunFeatures);
        Assert.Equal("Agents", FeatureHubStrings.For(AppLanguage.EnUS).FeatureTitles[AppFeature.FaqraAgents]);
    }

    [Fact]
    public void TheModuleAndPageRideOnTheFeature()
    {
        Assert.Equal("faqraAgents", IslandModule.FaqraAgents.RawValue());
        Assert.Equal('g', IslandModule.FaqraAgents.ShortcutKey());
        Assert.False(IslandModule.FaqraAgents.IsAvailable(_ => false));
        Assert.True(IslandModule.FaqraAgents.IsAvailable(feature => feature == AppFeature.FaqraAgents));
        Assert.Equal([AppFeature.FaqraAgents], SettingsDirectory.Gate(SettingsPage.Agents));
        Assert.Equal(SettingsPage.Agents, SettingsDirectory.Destination(AppFeature.FaqraAgents));
        Assert.Equal("Agents", Strings.EnUS.SettingsPageTitles[SettingsPage.Agents]);
    }

    [Fact]
    public void AnExistingInstallGetsAgentsOnceAndKeepsAnUninstall()
    {
        var store = DefaultsStore.InMemory();
        store.Set(AppFeature.FaqraAgents.AvailabilityKey(), false);
        DefaultsMigrations.InstallAgentsOnce(store);
        Assert.True(store.Bool(AppFeature.FaqraAgents.AvailabilityKey()));
        store.Set(AppFeature.FaqraAgents.AvailabilityKey(), false);
        DefaultsMigrations.InstallAgentsOnce(store);
        Assert.False(store.Bool(AppFeature.FaqraAgents.AvailabilityKey()));
    }
}
```

`tests/Faqra.Core.Tests/Agents/AgentsTextTests.cs`:

```csharp
using System.Collections.Immutable;
using Faqra.Core.Agents;
using Faqra.Core.Localization;

namespace Faqra.Core.Tests.Agents;

public class AgentsTextTests
{
    private static readonly AgentsStrings S = AgentsStrings.EnUS;
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private static AgentSession Session(AgentState state, int subagents = 0, params AgentStep[] steps) =>
        new("s1", "claude", @"C:\code\faqra", state, steps.ToImmutableList(), null, subagents, T0, T0, null);

    [Theory]
    [InlineData(AgentStepKind.Read, "Tray.cs", "Reads Tray.cs")]
    [InlineData(AgentStepKind.Edit, "Tray.cs", "Edits Tray.cs")]
    [InlineData(AgentStepKind.Run, "npm test", "Runs npm test")]
    [InlineData(AgentStepKind.Search, "TrayIcon", "Searches for TrayIcon")]
    [InlineData(AgentStepKind.WebSearch, "velopack", "Searches the web for velopack")]
    [InlineData(AgentStepKind.WebFetch, "docs.velopack.io", "Opens docs.velopack.io")]
    [InlineData(AgentStepKind.Subagent, "Review the diff", "Starts a subagent: Review the diff")]
    [InlineData(AgentStepKind.Subagent, "", "Starts a subagent")]
    [InlineData(AgentStepKind.SubagentDone, "", "A subagent finished")]
    [InlineData(AgentStepKind.Plan, "", "Updates its plan")]
    [InlineData(AgentStepKind.Tool, "mcp__x__y", "Uses mcp__x__y")]
    [InlineData(AgentStepKind.Failed, "Bash", "Bash failed")]
    [InlineData(AgentStepKind.Prompt, "fix the tray", "Asked: fix the tray")]
    public void StepsReadAsShortSentences(AgentStepKind kind, string detail, string text) =>
        Assert.Equal(text, AgentsText.Step(new AgentStep(T0, kind, detail), S));

    [Fact]
    public void TheStatusShowsTheLatestStepWhileWorking()
    {
        Assert.Equal("Runs npm test", AgentsText.Status(Session(AgentState.Working, 0, new AgentStep(T0, AgentStepKind.Run, "npm test")), S));
        Assert.Equal("Needs your OK", AgentsText.Status(Session(AgentState.Approval, 0, new AgentStep(T0, AgentStepKind.Run, "rm x")), S));
        Assert.Equal("Working", AgentsText.Status(Session(AgentState.Working), S));
        Assert.Equal("Subagents running: 2", AgentsText.Status(Session(AgentState.Background, 2), S));
    }

    [Theory]
    [InlineData(20, "now")]
    [InlineData(150, "2 min")]
    [InlineData(7300, "2 h")]
    public void ElapsedIsCoarse(int seconds, string text) =>
        Assert.Equal(text, AgentsText.Elapsed(TimeSpan.FromSeconds(seconds), S));

    [Fact]
    public void NoStringUsesADash() =>
        Assert.All(typeof(AgentsStrings).GetProperties().Where(p => p.PropertyType == typeof(string)),
            p => Assert.DoesNotMatch("[\u2013\u2014]", (string)p.GetValue(S)!));
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test tests/Faqra.Core.Tests --filter "FullyQualifiedName~AgentsRegistrationTests|FullyQualifiedName~AgentsTextTests"`
Expected: build error, `'AppFeature' does not contain a definition for 'FaqraAgents'`.

- [ ] **Step 3: Register the feature**

In `src/Faqra.Core/Features/AppFeature.cs`, change the enum's doc comment sentence `All 66 upstream members are kept, including the ones that have no Windows meaning, so settings stay compatible with upstream backups.` to `All 66 upstream members are kept, including the ones that have no Windows meaning, so settings stay compatible with upstream backups. Faqra's own features follow them, with Faqra-prefixed raw values that can never collide with an upstream one.` and replace the last enum line:

```csharp
    // System monitor, one entry per metric family
    MonitorCPU, MonitorGPU, MonitorMemory, MonitorNetwork, MonitorDisk, MonitorPower, FanControl,
    // Faqra only
    FaqraAgents,
}
```

In `src/Faqra.Core/Features/FeatureCatalog.cs` `SymbolName`, before the `_ => throw` arm:

```csharp
        AppFeature.FaqraAgents => "sparkles",
```

In `src/Faqra.Core/Features/FeatureWindowsSupport.cs`, add `or AppFeature.FaqraAgents` to the `Supported` arm of `Classify` (after `AppFeature.MonitorPower`) and to `IsBuilt` (after `AppFeature.CommandBar`).

In `src/Faqra.Core/Features/FeaturePreset.cs`, change `FirstRunFeatures` to:

```csharp
    public static IReadOnlySet<AppFeature> FirstRunFeatures { get; } = FeaturePreset.Essential.Features()
        .Concat([AppFeature.Notch, AppFeature.NotchTimer, AppFeature.FaqraAgents])
        .ToHashSet();
```

and add to its doc comment: `Faqra's Agents ships installed too: it does nothing until the owner installs Claude Code's hooks.`

In `src/Faqra.Core/Defaults/DefaultsMigrations.cs`, call `InstallAgentsOnce(store);` after `InstallIslandOnce(store);` in `Run`, and add below `InstallIslandOnce`:

```csharp
    /// <summary>
    /// Faqra's own migration. Installs Agents once for anyone whose first run predates it; the marker
    /// makes it one-time, so a later uninstall is respected.
    /// </summary>
    public static void InstallAgentsOnce(ISettingsStore store)
    {
        const string marker = "faqraAgentsInstalled";
        if (store.Bool(marker))
        {
            return;
        }
        store.Set(marker, true);
        store.Set(DefaultsKey.FeatureAvailable("faqraAgents"), true);
    }
```

In `src/Faqra.Core/Localization/FeatureHubStrings.EnUS.cs`, after `[AppFeature.FanControl] = "Fan Control",` add `[AppFeature.FaqraAgents] = "Agents",` and after the FanControl description line add:

```csharp
            [AppFeature.FaqraAgents] = "Watch Claude Code sessions from the island, each with a thinking orb that shows what it is doing.",
```

In `src/Faqra.Core/Localization/Strings.EnUS.cs`, after `[Settings.SettingsPage.Notch] = "Island",` add `[Settings.SettingsPage.Agents] = "Agents",`.

In `src/Faqra.Core/Settings/SettingsPage.cs`: add `Agents` to the enum right after `Notch`; add the sidebar row after the CommandBar row:

```csharp
        new(SettingsPage.Agents, SettingsSection.Utilities, "\uE99A"),      // Robot
```

add `SettingsPage.Agents => [AppFeature.FaqraAgents],` to `Gate` (after the CommandBar arm) and `AppFeature.FaqraAgents => SettingsPage.Agents,` to `Destination` (after the CommandBar arm).

In `src/Faqra.Core/Island/IslandModule.cs`: append `FaqraAgents` to the enum (`..., Camera, Downloads, FaqraAgents,`), and add before each `_ => throw` arm:

```csharp
        IslandModule.FaqraAgents => "faqraAgents",          // RawValue
        IslandModule.FaqraAgents => 'g',                    // ShortcutKey
        IslandModule.FaqraAgents => "\uE99A",               // Glyph: Robot
        IslandModule.FaqraAgents => isAvailable(AppFeature.FaqraAgents), // IsAvailable
```

(one arm in each of the four switches, each with the value shown; the trailing comments name the switch and are not part of the code).

In `src/Faqra.Core/AppPaths.cs`, after `SettingsFile`:

```csharp
    /// <summary>The relay Claude Code's hooks run. A fixed path, so updates never break the hooks.</summary>
    public static string AgentsRelayFile => Path.Combine(LocalDataDirectory, "bin", "faqra-hook.exe");

    /// <summary>One line per agent event: names and states only, never prompts, commands or paths.</summary>
    public static string AgentsLogFile => Path.Combine(LocalDataDirectory, "agents.log");

    /// <summary>Claude Code's user settings, where its hooks are registered.</summary>
    public static string ClaudeSettingsFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");
```

- [ ] **Step 4: Write the strings and text helpers**

`src/Faqra.Core/Localization/AgentsStrings.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

namespace Faqra.Core.Localization;

/// <summary>Every string Faqra Agents shows. Format strings take string.Format arguments.</summary>
public sealed partial class AgentsStrings
{
    // Settings page
    public required string PageCaption { get; init; }
    public required string HooksSection { get; init; }
    public required string HooksInstalled { get; init; }
    public required string HooksNotInstalled { get; init; }
    public required string HooksOutdated { get; init; }
    public required string HooksCaptionFormat { get; init; }
    public required string CoucouLeftoversFormat { get; init; }
    public required string ReviewInstall { get; init; }
    public required string ReviewRemove { get; init; }
    public required string RelayMissing { get; init; }
    public required string FileUnreadableFormat { get; init; }
    public required string Installed { get; init; }
    public required string Removed { get; init; }

    // Review dialog
    public required string ReviewTitle { get; init; }
    public required string ReviewBackupFormat { get; init; }
    public required string ReviewNewFile { get; init; }
    public required string ReviewRemoveCoucou { get; init; }
    public required string ReviewConfirmInstall { get; init; }
    public required string ReviewConfirmRemove { get; init; }
    public required string ReviewCancel { get; init; }
    public required string ReviewNoChange { get; init; }
    public required string FileChanged { get; init; }
    public required string WriteFailedFormat { get; init; }

    // Island
    public required string EmptyTitle { get; init; }
    public required string EmptyHintNotInstalled { get; init; }
    public required string EmptyHintInstalled { get; init; }
    public required string OpenSettings { get; init; }
    public required string ActivityHeader { get; init; }
    public required string LastMessageHeader { get; init; }

    // States
    public required string StateIdle { get; init; }
    public required string StateThinking { get; init; }
    public required string StateWorking { get; init; }
    public required string StateSearching { get; init; }
    public required string StateCompacting { get; init; }
    public required string StateBackgroundFormat { get; init; }
    public required string StateApproval { get; init; }
    public required string StateQuestion { get; init; }
    public required string StateError { get; init; }
    public required string StateRateLimited { get; init; }
    public required string StateFinished { get; init; }

    // Ticker steps
    public required string StepPromptFormat { get; init; }
    public required string StepReadFormat { get; init; }
    public required string StepEditFormat { get; init; }
    public required string StepRunFormat { get; init; }
    public required string StepSearchFormat { get; init; }
    public required string StepWebSearchFormat { get; init; }
    public required string StepWebFetchFormat { get; init; }
    public required string StepSubagentFormat { get; init; }
    public required string StepSubagent { get; init; }
    public required string StepSubagentDone { get; init; }
    public required string StepPlan { get; init; }
    public required string StepToolFormat { get; init; }
    public required string StepFailedFormat { get; init; }

    // Elapsed time
    public required string ElapsedNow { get; init; }
    public required string ElapsedMinutesFormat { get; init; }
    public required string ElapsedHoursFormat { get; init; }

    public static AgentsStrings For(AppLanguage language) => language switch
    {
        // Translations pending: every language resolves to English for now (see Strings.Aliases.cs).
        _ => EnUS,
    };
}
```

`src/Faqra.Core/Localization/AgentsStrings.EnUS.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

namespace Faqra.Core.Localization;

public sealed partial class AgentsStrings
{
    public static AgentsStrings EnUS { get; } = new()
    {
        PageCaption = "Faqra watches your Claude Code sessions through hooks in Claude's settings. Nothing there changes until you review the exact edit and confirm it.",
        HooksSection = "Claude Code",
        HooksInstalled = "Hooks installed",
        HooksNotInstalled = "Hooks not installed",
        HooksOutdated = "Hooks need reinstalling",
        HooksCaptionFormat = "In {0}",
        CoucouLeftoversFormat = "Coucou's hooks still run on {0} of Claude Code's events. Installing can remove them.",
        ReviewInstall = "Review and install",
        ReviewRemove = "Remove hooks",
        RelayMissing = "Faqra's relay is missing from its folder, so the hooks can't be installed. Reinstall Faqra.",
        FileUnreadableFormat = "Faqra won't edit Claude's settings because they aren't plain JSON: {0}",
        Installed = "Hooks installed. New Claude Code sessions appear in the island.",
        Removed = "Hooks removed.",
        ReviewTitle = "Review the change to Claude's settings",
        ReviewBackupFormat = "Faqra saves the current file as {0} before writing.",
        ReviewNewFile = "Claude's settings file doesn't exist yet, so Faqra creates it.",
        ReviewRemoveCoucou = "Also remove Coucou's hooks",
        ReviewConfirmInstall = "Install hooks",
        ReviewConfirmRemove = "Remove hooks",
        ReviewCancel = "Cancel",
        ReviewNoChange = "Nothing to change.",
        FileChanged = "Claude's settings changed since this preview. Here is the new version.",
        WriteFailedFormat = "Faqra couldn't write Claude's settings: {0}",
        EmptyTitle = "No agent sessions",
        EmptyHintNotInstalled = "Install the Claude Code hooks in Settings to see sessions here.",
        EmptyHintInstalled = "Start Claude Code and its sessions appear here.",
        OpenSettings = "Open settings",
        ActivityHeader = "Activity",
        LastMessageHeader = "Claude said",
        StateIdle = "Idle",
        StateThinking = "Thinking",
        StateWorking = "Working",
        StateSearching = "Searching",
        StateCompacting = "Compacting",
        StateBackgroundFormat = "Subagents running: {0}",
        StateApproval = "Needs your OK",
        StateQuestion = "Has a question",
        StateError = "Error",
        StateRateLimited = "Rate limited",
        StateFinished = "Done",
        StepPromptFormat = "Asked: {0}",
        StepReadFormat = "Reads {0}",
        StepEditFormat = "Edits {0}",
        StepRunFormat = "Runs {0}",
        StepSearchFormat = "Searches for {0}",
        StepWebSearchFormat = "Searches the web for {0}",
        StepWebFetchFormat = "Opens {0}",
        StepSubagentFormat = "Starts a subagent: {0}",
        StepSubagent = "Starts a subagent",
        StepSubagentDone = "A subagent finished",
        StepPlan = "Updates its plan",
        StepToolFormat = "Uses {0}",
        StepFailedFormat = "{0} failed",
        ElapsedNow = "now",
        ElapsedMinutesFormat = "{0} min",
        ElapsedHoursFormat = "{0} h",
    };
}
```

`src/Faqra.Core/Agents/AgentsText.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Globalization;
using Faqra.Core.Localization;

namespace Faqra.Core.Agents;

/// <summary>Turns sessions and steps into the short lines the island shows.</summary>
public static class AgentsText
{
    public static string State(AgentSession session, AgentsStrings s) => session.State switch
    {
        AgentState.Thinking => s.StateThinking,
        AgentState.Working => s.StateWorking,
        AgentState.Searching => s.StateSearching,
        AgentState.Compacting => s.StateCompacting,
        AgentState.Background => Format(s.StateBackgroundFormat, session.Subagents),
        AgentState.Approval => s.StateApproval,
        AgentState.Question => s.StateQuestion,
        AgentState.Error => s.StateError,
        AgentState.RateLimited => s.StateRateLimited,
        AgentState.Finished => s.StateFinished,
        _ => s.StateIdle,
    };

    public static string Step(AgentStep step, AgentsStrings s) => step.Kind switch
    {
        AgentStepKind.Prompt => Format(s.StepPromptFormat, step.Detail),
        AgentStepKind.Read => Format(s.StepReadFormat, step.Detail),
        AgentStepKind.Edit => Format(s.StepEditFormat, step.Detail),
        AgentStepKind.Run => Format(s.StepRunFormat, step.Detail),
        AgentStepKind.Search => Format(s.StepSearchFormat, step.Detail),
        AgentStepKind.WebSearch => Format(s.StepWebSearchFormat, step.Detail),
        AgentStepKind.WebFetch => Format(s.StepWebFetchFormat, step.Detail),
        AgentStepKind.Subagent => step.Detail.Length > 0 ? Format(s.StepSubagentFormat, step.Detail) : s.StepSubagent,
        AgentStepKind.SubagentDone => s.StepSubagentDone,
        AgentStepKind.Plan => s.StepPlan,
        AgentStepKind.Failed => Format(s.StepFailedFormat, step.Detail),
        _ => Format(s.StepToolFormat, step.Detail),
    };

    /// <summary>The status line: the latest step while working or searching, the state otherwise.</summary>
    public static string Status(AgentSession session, AgentsStrings s) =>
        session.State is AgentState.Working or AgentState.Searching && session.Steps.Count > 0
            ? Step(session.Steps[^1], s)
            : State(session, s);

    public static string Elapsed(TimeSpan span, AgentsStrings s) =>
        span < TimeSpan.FromMinutes(1) ? s.ElapsedNow
        : span < TimeSpan.FromHours(1) ? Format(s.ElapsedMinutesFormat, (int)span.TotalMinutes)
        : Format(s.ElapsedHoursFormat, (int)span.TotalHours);

    private static string Format(string format, object value) => string.Format(CultureInfo.CurrentCulture, format, value);
}
```

- [ ] **Step 5: Update the tests that count features and modules**

- `tests/Faqra.Core.Tests/FeatureCatalogTests.cs:10`: rename the test to `CatalogHasEveryUpstreamFeaturePlusFaqrasOwn` and expect `67`. Line 61: expect `67`.
- `tests/Faqra.Core.Tests/IslandModuleTests.cs`: lines 13-15, 55-56 and 77: `13` becomes `14`.
- `tests/Faqra.Core.Tests/FeaturePresetTests.cs:28`: `10` becomes `11`, and add `Assert.Contains(AppFeature.FaqraAgents, FeaturePresets.FirstRunFeatures);` after line 30.
- `tests/Faqra.App.Tests/UiRenderTests.cs:74`: `66` becomes `67`; line 76: `"10 of 12 features installed"` becomes `"11 of 13 features installed"`; line 144: `12` becomes `13`.

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test Faqra.sln`
Expected: PASS everywhere. The new tests: `AgentsRegistrationTests` 3, `AgentsTextTests` 18. If a `switch` over `AppFeature` or `IslandModule` throws `ArgumentOutOfRangeException` somewhere not listed above, add the `FaqraAgents` arm there.

- [ ] **Step 7: Commit**

```bash
git add src/Faqra.Core tests
git commit -m "feat(windows): register Faqra Agents as a feature, island module and settings page"
```

---

### Task 8: The orb on screen (`OrbView`)

**Files:**
- Create: `src/Faqra.App/Agents/OrbView.cs`, `src/Faqra.App/Agents/AgentInk.cs`
- Create: `tests/Faqra.App.Tests/AgentsRenderTests.cs`

**Interfaces:**
- Consumes: `OrbModel`, `OrbLook` (Task 1); `AgentOrbStyle`, `AgentTone`, `AgentState`, `AgentOrbStyles` (Task 3); `IslandPalette` (existing).
- Produces: `sealed class OrbView : FrameworkElement` with dependency properties `OrbLook Look`, `double Diameter` (default 20), `Brush Ink`, `double Speed` (default 1), `void Apply(AgentOrbStyle style, Brush ink)`, `const double StillTime = 1500`; `static class AgentInk { Brush For(AgentTone tone) }`.

- [ ] **Step 1: Write the failing test**

`tests/Faqra.App.Tests/AgentsRenderTests.cs`:

```csharp
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Faqra.App.Agents;
using Faqra.Core.Agents;

namespace Faqra.App.Tests;

public class AgentsRenderTests
{
    private static readonly string OutputDirectory =
        Environment.GetEnvironmentVariable("FAQRA_UI_SHOTS") ?? Path.Combine(Path.GetTempPath(), "faqra-ui");

    /// <summary>Renders on a transparent ground, saves a PNG, and returns how many pixels carry ink.</summary>
    internal static int RenderInk(FrameworkElement element, string name, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        Directory.CreateDirectory(OutputDirectory);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(OutputDirectory, $"{name}.png")))
        {
            encoder.Save(stream);
        }
        var pixels = new byte[(int)width * (int)height * 4];
        bitmap.CopyPixels(pixels, (int)width * 4, 0);
        var inked = 0;
        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0)
            {
                inked++;
            }
        }
        return inked;
    }

    [Fact]
    public void EveryStateDrawsItsOrb() => StaThread.Run(() =>
    {
        foreach (var state in Enum.GetValues<AgentState>())
        {
            var style = AgentOrbStyles.For(state);
            var orb = new OrbView { Diameter = 64 };
            orb.Apply(style, AgentInk.For(style.Tone));
            Assert.True(RenderInk(orb, $"agents-orb-{state}", 64, 64) > 200, $"{state} drew too little");
        }
    });

    [Fact]
    public void TheOrbTakesItsDiameter() => StaThread.Run(() =>
    {
        var orb = new OrbView { Diameter = 18 };
        orb.Measure(new Size(100, 100));
        Assert.Equal(new Size(18, 18), orb.DesiredSize);
    });
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test tests/Faqra.App.Tests --filter "FullyQualifiedName~AgentsRenderTests"`
Expected: build error, `The type or namespace name 'Agents' does not exist in the namespace 'Faqra.App'`.

- [ ] **Step 3: Write the view**

`src/Faqra.App/Agents/AgentInk.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Windows.Media;
using Faqra.App.Island.Modules;
using Faqra.Core.Agents;

namespace Faqra.App.Agents;

/// <summary>
/// The orb's ink on the island's fixed dark surface. Caution, critical and success are Windows 11's
/// dark-theme semantic colours; the rest come from <see cref="IslandPalette"/>.
/// </summary>
public static class AgentInk
{
    public static readonly Brush Caution = Frozen(0xFC, 0xE1, 0x00);
    public static readonly Brush Critical = Frozen(0xFF, 0x99, 0xA4);
    public static readonly Brush Success = Frozen(0x6C, 0xCB, 0x5F);

    public static Brush For(AgentTone tone) => tone switch
    {
        AgentTone.Caution => Caution,
        AgentTone.Critical => Critical,
        AgentTone.Success => Success,
        AgentTone.Accent => IslandPalette.Accent,
        AgentTone.Secondary => IslandPalette.Secondary,
        _ => IslandPalette.Primary,
    };

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
```

`src/Faqra.App/Agents/OrbView.cs`:

```csharp
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
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test tests/Faqra.App.Tests --filter "FullyQualifiedName~AgentsRenderTests"`
Expected: PASS (2 tests). Open `%TEMP%\faqra-ui\agents-orb-*.png` and check each is a dotted sphere in its tone (Approval yellow, Question blue, Error pink, Finished green, others light grey).

- [ ] **Step 5: Commit**

```bash
git add src/Faqra.App/Agents tests/Faqra.App.Tests/AgentsRenderTests.cs
git commit -m "feat(windows): draw Thinking Orbs in WPF"
```

---

### Task 9: The Agents island module, the collapsed pill, and the app wiring

**Files:**
- Create: `src/Faqra.App/Island/Modules/AgentsModule.cs`
- Modify: `src/Faqra.App/Island/Modules/IdleViews.cs` (add `IdleAgentsView`), `src/Faqra.App/Island/Modules/SectionPicker.cs` (title), `src/Faqra.App/Island/IslandController.cs` (constructor, titles, modules, idle content, change handler), `src/Faqra.App/AppServices.cs` (hub, binding, dispose)
- Modify: `tests/Faqra.App.Tests/AgentsRenderTests.cs` (module renders)

**Interfaces:**
- Consumes: `AgentHub` (Task 6), `OrbView`, `AgentInk` (Task 8), `AgentsStrings`, `AgentsText`, `IslandModule.FaqraAgents`, `AppPaths.*` (Task 7), `RelayDeployment` (Task 6), `ClaudeHookConfig.Inspect` (Task 4).
- Produces: `AppServices.Agents` (`AgentHub`); `sealed class AgentsModule(AgentHub hub, Func<bool> hooksInstalled) : UserControl`; `sealed class IdleAgentsView(AgentSession session) : UserControl`; `IslandController(ISettingsStore, FeatureRuntime, NowPlayingService, SystemMonitor, AppVolumeMixer, AgentHub)`.

- [ ] **Step 1: Write the failing render tests**

Append to `tests/Faqra.App.Tests/AgentsRenderTests.cs`, inside the class:

```csharp
    private static AgentHub HubWith(params string[] lines)
    {
        var hub = new AgentHub("faqra-test-unused", SynchronizationContext.Current ?? new SynchronizationContext(),
            () => new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
        var board = AgentBoard.Empty;
        foreach (var line in lines)
        {
            board = board.Apply(AgentEvent.TryParse(line)!, new DateTimeOffset(2026, 10, 9, 11, 58, 0, TimeSpan.Zero));
        }
        hub.ReplaceBoardForTests(board);
        return hub;
    }

    [Fact]
    public void TheModuleListsSessionsMostUrgentFirst() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(Core.Defaults.DefaultsStore.InMemory());
        using var hub = HubWith(
            "{\"hook_event_name\":\"PreToolUse\",\"session_id\":\"a\",\"cwd\":\"C:\\\\code\\\\faqra\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"dotnet test\"}}",
            "{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"b\",\"cwd\":\"C:\\\\code\\\\site\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"rm -rf dist\"}}");
        var module = new AgentsModule(hub, () => true);
        var host = new System.Windows.Controls.Border { Background = Island.Modules.IslandPalette.Surface, Child = module, Width = 412, Height = 400 };
        Assert.True(RenderInk(host, "island-agents", 412, 400) > 0);
        var texts = AllText(module);
        Assert.True(texts.IndexOf("site") < texts.IndexOf("faqra"), "the waiting session comes first");
        Assert.Contains("Needs your OK", texts);
        Assert.Contains("Runs dotnet test", texts);
    });

    [Fact]
    public void TheEmptyModuleSaysHowToStart() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(Core.Defaults.DefaultsStore.InMemory());
        using var hub = HubWith();
        var module = new AgentsModule(hub, () => false);
        var host = new System.Windows.Controls.Border { Background = Island.Modules.IslandPalette.Surface, Child = module, Width = 412, Height = 300 };
        RenderInk(host, "island-agents-empty", 412, 300);
        Assert.Contains("Install the Claude Code hooks", AllText(module));
    });

    [Fact]
    public void ThePillShowsTheMostUrgentState() => StaThread.Run(() =>
    {
        var board = AgentBoard.Empty.Apply(AgentEvent.TryParse("{\"hook_event_name\":\"PermissionRequest\",\"session_id\":\"b\",\"tool_name\":\"Bash\"}")!, DateTimeOffset.Now);
        var view = new IdleAgentsView(board.MostUrgent!);
        var host = new System.Windows.Controls.Border { Background = Island.Modules.IslandPalette.Surface, Child = view, Width = 135, Height = 24 };
        RenderInk(host, "island-agents-pill", 135, 24);
        Assert.Contains("Needs your OK", AllText(view));
    });

    private static string AllText(DependencyObject root)
    {
        var builder = new System.Text.StringBuilder();
        void Walk(DependencyObject node)
        {
            if (node is System.Windows.Controls.TextBlock block)
            {
                builder.Append(block.Text).Append('\n');
            }
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            {
                Walk(child);
            }
        }
        Walk(root);
        return builder.ToString();
    }
```

and add `using Faqra.App.Island.Modules;` and `using Faqra.Services.Agents;` to the file's usings.

Add to `src/Faqra.Services/Agents/AgentHub.cs`, below `SetRunning` (tests build a board without a pipe; `Faqra.Services` already grants `InternalsVisibleTo` to the services tests, so make this one public and name it plainly):

```csharp
    /// <summary>For render tests: shows a prepared board without a pipe. Never called by the app.</summary>
    public void ReplaceBoardForTests(AgentBoard board) => Board = board;
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test tests/Faqra.App.Tests --filter "FullyQualifiedName~AgentsRenderTests"`
Expected: build error, `The type or namespace name 'AgentsModule' could not be found`.

- [ ] **Step 3: Write the collapsed pill view**

Append to `src/Faqra.App/Island/Modules/IdleViews.cs` (add `using Faqra.App.Agents;`, `using Faqra.Core.Agents;` and `using Faqra.Core.Localization;` to its usings):

```csharp
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
```

- [ ] **Step 4: Write the module**

`src/Faqra.App/Island/Modules/AgentsModule.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// The session list plays the role of Coucou's session ticker (windows/src/views), https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé), with a thinking orb in place of its mascot.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Faqra.App.Agents;
using Faqra.Core.Agents;
using Faqra.Core.Localization;
using Faqra.Services.Agents;

namespace Faqra.App.Island.Modules;

/// <summary>
/// Every agent session, most urgent first, with the focused one's activity and Claude's last words below.
/// Rows are kept and updated in place, so each orb keeps its motion while events arrive.
/// </summary>
public sealed class AgentsModule : UserControl
{
    private const int ActivityLines = 6;
    private static readonly FontFamily TextFont = new("Segoe UI Variable Text, Segoe UI");
    private static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas");

    private readonly AgentHub _hub;
    private readonly Func<bool> _hooksInstalled;
    private readonly AgentsStrings _s = AgentsStrings.For(L10n.Shared.Language);
    private readonly Dictionary<string, SessionRow> _rows = new(StringComparer.Ordinal);
    private readonly StackPanel _list = new();
    private readonly StackPanel _detail = new() { Margin = new Thickness(0, 12, 0, 0) };
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private string? _focused;

    public AgentsModule(AgentHub hub, Func<bool> hooksInstalled)
    {
        _hub = hub;
        _hooksInstalled = hooksInstalled;
        var body = new StackPanel();
        body.Children.Add(_list);
        body.Children.Add(_detail);
        Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _clock.Tick += (_, _) => RefreshTimes();
        Render();
        _hub.Changed += Render;
        Loaded += (_, _) => _clock.Start();
        Unloaded += (_, _) =>
        {
            _clock.Stop();
            _hub.Changed -= Render;
        };
    }

    private void Render()
    {
        var sessions = _hub.Board.Ordered;
        if (sessions.Count == 0)
        {
            _rows.Clear();
            _list.Children.Clear();
            _detail.Children.Clear();
            _list.Children.Add(Empty());
            return;
        }
        if (_list.Children.Count > 0 && _list.Children[0] is not SessionRow)
        {
            _list.Children.Clear();
        }
        foreach (var gone in _rows.Keys.Except(sessions.Select(s => s.Id)).ToList())
        {
            _list.Children.Remove(_rows[gone]);
            _rows.Remove(gone);
        }
        if (_focused is null || !_rows.ContainsKey(_focused) && sessions.All(s => s.Id != _focused))
        {
            _focused = sessions[0].Id;
        }
        for (var i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            if (!_rows.TryGetValue(session.Id, out var row))
            {
                row = new SessionRow(id => Focus(id));
                _rows[session.Id] = row;
            }
            row.Update(session, _s, DateTimeOffset.Now, session.Id == _focused);
            var at = _list.Children.IndexOf(row);
            if (at != i)
            {
                if (at >= 0)
                {
                    _list.Children.RemoveAt(at);
                }
                _list.Children.Insert(i, row);
            }
        }
        RenderDetail(sessions.First(s => s.Id == _focused));
    }

    private void Focus(string id)
    {
        _focused = id;
        Render();
    }

    private void RefreshTimes()
    {
        foreach (var session in _hub.Board.Ordered)
        {
            if (_rows.TryGetValue(session.Id, out var row))
            {
                row.Update(session, _s, DateTimeOffset.Now, session.Id == _focused);
            }
        }
    }

    private void RenderDetail(AgentSession session)
    {
        _detail.Children.Clear();
        if (session.LastMessage is { Length: > 0 } message)
        {
            _detail.Children.Add(Label(_s.LastMessageHeader));
            _detail.Children.Add(new TextBlock
            {
                Text = message,
                MaxHeight = 54,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.WordEllipsis,
                FontFamily = TextFont,
                FontSize = 12,
                Foreground = IslandPalette.Primary,
                Margin = new Thickness(0, 2, 0, 10),
            });
        }
        if (session.Steps.Count == 0)
        {
            return;
        }
        _detail.Children.Add(Label(_s.ActivityHeader));
        foreach (var step in session.Steps.Reverse().Take(ActivityLines))
        {
            _detail.Children.Add(new TextBlock
            {
                Text = AgentsText.Step(step, _s),
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontFamily = TextFont,
                FontSize = 12,
                Foreground = IslandPalette.Secondary,
                Margin = new Thickness(0, 2, 0, 0),
            });
        }
    }

    private UIElement Empty()
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0) };
        var orb = new OrbView { Diameter = 48, HorizontalAlignment = HorizontalAlignment.Center };
        orb.Apply(AgentOrbStyles.For(AgentState.Idle), IslandPalette.Secondary);
        stack.Children.Add(orb);
        stack.Children.Add(new TextBlock
        {
            Text = _s.EmptyTitle,
            Margin = new Thickness(0, 12, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Center,
            FontFamily = TextFont,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = IslandPalette.Primary,
        });
        var installed = _hooksInstalled();
        stack.Children.Add(new TextBlock
        {
            Text = installed ? _s.EmptyHintInstalled : _s.EmptyHintNotInstalled,
            MaxWidth = 300,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontFamily = TextFont,
            FontSize = 12,
            Foreground = IslandPalette.Secondary,
        });
        if (!installed)
        {
            var open = new Button
            {
                Content = _s.OpenSettings,
                FontFamily = TextFont,
                FontSize = 12,
                MinWidth = 120,
                Height = 32,
                Margin = new Thickness(0, 14, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = IslandPalette.Primary,
                Background = IslandPalette.Fill,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Template = MusicModule.RoundButtonTemplate(16),
            };
            open.Click += (_, _) => App.ShowSettings(Core.Settings.SettingsPage.Agents);
            stack.Children.Add(open);
        }
        return stack;
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        FontFamily = TextFont,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = IslandPalette.Tertiary,
    };

    /// <summary>One session: orb, project, status, and how long since its last event.</summary>
    private sealed class SessionRow : Button
    {
        private readonly OrbView _orb = new() { Diameter = 20, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _project = new() { FontFamily = TextFont, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = IslandPalette.Primary, TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly TextBlock _status = new() { FontFamily = TextFont, FontSize = 12, Foreground = IslandPalette.Secondary, TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly TextBlock _elapsed = new() { FontFamily = MonoFont, FontSize = 11, Foreground = IslandPalette.Tertiary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        private string _id = string.Empty;

        public SessionRow(Action<string> focus)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(_project);
            text.Children.Add(_status);
            Grid.SetColumn(text, 1);
            Grid.SetColumn(_elapsed, 2);
            grid.Children.Add(_orb);
            grid.Children.Add(text);
            grid.Children.Add(_elapsed);
            Content = grid;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            Padding = new Thickness(10, 8, 10, 8);
            Margin = new Thickness(0, 0, 0, 4);
            BorderThickness = new Thickness(0);
            Cursor = System.Windows.Input.Cursors.Hand;
            Template = MusicModule.RoundButtonTemplate(10);
            Click += (_, _) => focus(_id);
        }

        public void Update(AgentSession session, AgentsStrings s, DateTimeOffset now, bool focused)
        {
            _id = session.Id;
            var style = AgentOrbStyles.For(session.State);
            _orb.Apply(style, AgentInk.For(style.Tone));
            _project.Text = session.Project;
            _status.Text = AgentsText.Status(session, s);
            _elapsed.Text = AgentsText.Elapsed(now - session.UpdatedAt, s);
            Background = focused ? IslandPalette.FillStrong : IslandPalette.Fill;
            System.Windows.Automation.AutomationProperties.SetName(this, $"{_project.Text}, {_status.Text}");
        }
    }
}
```

- [ ] **Step 5: Wire the island and the app**

In `src/Faqra.App/Island/Modules/SectionPicker.cs` `Title`, before `_ => module.RawValue()`:

```csharp
        IslandModule.FaqraAgents => "Agents",
```

In `src/Faqra.App/Island/IslandController.cs`:
- add `using Faqra.Core.Agents;` and `using Faqra.Services.Agents;`;
- add the field `private readonly AgentHub _agents;` after `_mixer`, and `private (string? Id, AgentState? State) _agentsShown;` after `_suspendedForFullscreen`;
- change the constructor signature to `public IslandController(ISettingsStore store, FeatureRuntime runtime, NowPlayingService nowPlaying, SystemMonitor monitor, Services.Audio.AppVolumeMixer mixer, AgentHub agents)`, assign `_agents = agents;` after `_mixer = mixer;`, and subscribe `_agents.Changed += OnAgentsChanged;` after `_nowPlaying.Changed += OnNowPlayingChanged;`;
- in `Dispose`, after `_nowPlaying.Changed -= OnNowPlayingChanged;` add `_agents.Changed -= OnAgentsChanged;`;
- replace `ShowsIdleContent` with:

```csharp
    private bool ShowsIdleContent() =>
        UrgentAgent() is not null
        || IdleContent() != IslandIdleContent.None
        && (IdleContent() != IslandIdleContent.Music || _nowPlaying.Current.HasTrack);

    /// <summary>The session the resting pill shows: any agent busy or waiting outranks music and battery.</summary>
    private AgentSession? UrgentAgent() =>
        _runtime.IsAvailable(AppFeature.FaqraAgents) && _agents.Board.IsActive ? _agents.Board.MostUrgent : null;
```

- in `ModuleTitle`, before `_ => module.RawValue()`: `IslandModule.FaqraAgents => hub.FeatureTitles[AppFeature.FaqraAgents],`
- in `BuildModule`, before the placeholder arm: `IslandModule.FaqraAgents => new AgentsModule(_agents, AgentHooksInstalled),`
- replace `BuildIdleContent` with:

```csharp
    private UIElement? BuildIdleContent()
    {
        var urgent = UrgentAgent();
        _agentsShown = (urgent?.Id, urgent?.State);
        if (urgent is not null)
        {
            return new IdleAgentsView(urgent);
        }
        return IdleContent() switch
        {
            IslandIdleContent.Music when _nowPlaying.Current.HasTrack => new IdleMusicView(_nowPlaying.Current),
            IslandIdleContent.Battery => new IdleBatteryView(),
            _ => null,
        };
    }

    private static bool AgentHooksInstalled()
    {
        try
        {
            return File.Exists(AppPaths.ClaudeSettingsFile)
                && Core.Agents.Install.ClaudeHookConfig.Inspect(File.ReadAllText(AppPaths.ClaudeSettingsFile)).Installed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Core.Agents.Install.ConfigFormatException)
        {
            return false;
        }
    }
```

- below `OnNowPlayingChanged`, add (the hub raises `Changed` on the UI thread):

```csharp
    /// <summary>
    /// Re-renders the resting pill only when the session or state it shows changes, so its orb keeps
    /// moving through the many events a busy session sends.
    /// </summary>
    private void OnAgentsChanged()
    {
        if (_window is null || _presentation != IslandPresentation.Collapsed)
        {
            return;
        }
        var urgent = UrgentAgent();
        if ((urgent?.Id, urgent?.State) != _agentsShown)
        {
            Render(animate: true);
        }
    }
```

(`IslandController.cs` needs `using System.IO;` and `using Faqra.Core;` if not already present; `AppFeature` is already imported.)

In `src/Faqra.App/AppServices.cs`:
- add `using System.Security.Principal;`, `using Faqra.Core.Agents;` and `using Faqra.Services.Agents;`;
- in the constructor, before `FeatureRuntime = new FeatureRuntime(...)`:

```csharp
        // Listens only once the feature is installed and the app has started its features (never in tests).
        Agents = new AgentHub(
            AgentPipe.Name(WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName, Environment.GetEnvironmentVariable),
            new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher),
            () => DateTimeOffset.Now,
            AppPaths.AgentsLogFile);
```

- change the island line to `Island = new IslandController(store, FeatureRuntime, NowPlaying, Monitor, Mixer, Agents);`
- add the property `public AgentHub Agents { get; }` after `KeepAwake`, and a field `private bool _featuresStarted;`;
- in `StartFeatures`, set `_featuresStarted = true;` as its first line;
- add to `Bindings()`:

```csharp
        [AppFeature.FaqraAgents] = () =>
        {
            if (!_featuresStarted)
            {
                return;
            }
            var on = FeatureRuntime.IsAvailable(AppFeature.FaqraAgents);
            if (on)
            {
                RelayDeployment.Ensure(AppContext.BaseDirectory, AppPaths.AgentsRelayFile);
            }
            Agents.SetRunning(on);
            if (FeatureRuntime.IsAvailable(AppFeature.Notch))
            {
                Island.SyncWithPreferences();
            }
        },
```

- in `Dispose`, add `Agents.Dispose();` right after `Island.Dispose();`.

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test Faqra.sln`
Expected: PASS, including `AgentsRenderTests` (5). Check `%TEMP%\faqra-ui\island-agents.png`, `island-agents-empty.png` and `island-agents-pill.png`.

- [ ] **Step 7: Commit**

```bash
git add src/Faqra.App src/Faqra.Services/Agents/AgentHub.cs tests/Faqra.App.Tests/AgentsRenderTests.cs
git commit -m "feat(windows): Agents island module and orb in the resting pill"
```

---

### Task 10: The Agents settings page and the review dialog

**Files:**
- Create: `src/Faqra.App/Settings/Pages/AgentsPage.cs`, `src/Faqra.App/Settings/ConfigReviewWindow.cs`
- Modify: `src/Faqra.App/Settings/SettingsWindow.xaml.cs` (`ImplementedPages`, `CreatePage`)
- Modify: `tests/Faqra.App.Tests/AgentsRenderTests.cs` (page and dialog renders)

**Interfaces:**
- Consumes: `ClaudeHookConfig`, `ConfigFormatException`, `HookStatus` (Task 4); `ConfigFile`, `ConfigPreview`, `ConfigChangedException`, `RelayDeployment` (Task 6); `AgentsStrings`, `AppPaths` (Task 7).
- Produces: `sealed class AgentsPage(string settingsPath, string relayPath) : UserControl` (parameterless constructor uses `AppPaths`); `sealed class ConfigReviewWindow : Wpf.Ui.Controls.FluentWindow` with `static bool Review(Window? owner, ConfigReviewWindow.Mode mode, string settingsPath, string relayPath, int coucouEvents)`; `enum Mode { Install, Remove }`; `ConfigReviewWindow(Mode mode, string settingsPath, string relayPath, int coucouEvents)` constructor (internal, for tests).

- [ ] **Step 1: Write the failing render tests**

Append to `AgentsRenderTests`:

```csharp
    [Fact]
    public void ThePageOffersAReviewedInstall() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(Core.Defaults.DefaultsStore.InMemory());
        var dir = Directory.CreateTempSubdirectory("faqra-page-").FullName;
        var settings = Path.Combine(dir, "settings.json");
        File.WriteAllText(settings, "{\n  \"hooks\": {\n    \"Stop\": [\n      {\n        \"hooks\": [\n          {\n            \"type\": \"command\",\n            \"command\": \"\\\"C:/x/coucou-hook.exe\\\" Stop\"\n          }\n        ]\n      }\n    ]\n  }\n}\n");
        var relay = Path.Combine(dir, "faqra-hook.exe");
        File.WriteAllText(relay, "stub");
        var page = new AgentsPage(settings, relay);
        RenderInk(page, "settings-agents", 840, 560);
        var texts = AllText(page);
        Assert.Contains("Hooks not installed", texts);
        Assert.Contains("Coucou's hooks still run on 1 of", texts);
        Directory.Delete(dir, recursive: true);
    });

    [Fact]
    public void TheReviewShowsTheDiffBeforeWriting() => StaThread.Run(() =>
    {
        using var services = AppServices.StartWith(Core.Defaults.DefaultsStore.InMemory());
        var dir = Directory.CreateTempSubdirectory("faqra-review-").FullName;
        var settings = Path.Combine(dir, "settings.json");
        File.WriteAllText(settings, "{\n  \"a\": 1\n}\n");
        var review = new ConfigReviewWindow(ConfigReviewWindow.Mode.Install, settings, @"C:\x\faqra-hook.exe", coucouEvents: 0);
        RenderInk((FrameworkElement)review.Content, "settings-agents-review", 760, 560);
        var texts = AllText((DependencyObject)review.Content);
        Assert.Contains(texts.Split('\n'), line => line.StartsWith('+') && line.Contains("\"SessionStart\": ["));
        Assert.Contains("settings.json.bak-", texts);
        Assert.Equal("{\n  \"a\": 1\n}\n", File.ReadAllText(settings));
        review.Close();
        Directory.Delete(dir, recursive: true);
    });
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test tests/Faqra.App.Tests --filter "FullyQualifiedName~AgentsRenderTests"`
Expected: build error, `The type or namespace name 'AgentsPage' could not be found`.

- [ ] **Step 3: Write the review dialog**

`src/Faqra.App/Settings/ConfigReviewWindow.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// The review-before-write flow follows Coucou's settings window (windows/src-tauri/src/config_file.rs),
// https://github.com/Louis-CFM/coucou (MIT License, Copyright (c) 2026 Louis Raillé).

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Faqra.Core;
using Faqra.Core.Agents.Install;
using Faqra.Core.Localization;
using Faqra.Services.Agents;
using Wpf.Ui.Controls;

namespace Faqra.App.Settings;

/// <summary>
/// Shows the exact edit to Claude's settings and writes it only on confirm, after checking the file did
/// not change in the meantime. The old file is kept byte for byte as the backup.
/// </summary>
public sealed class ConfigReviewWindow : FluentWindow
{
    public enum Mode { Install, Remove }

    private static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas");

    private readonly Mode _mode;
    private readonly string _settingsPath;
    private readonly string _relayPath;
    private readonly AgentsStrings _s = AgentsStrings.For(L10n.Shared.Language);
    private readonly System.Windows.Controls.CheckBox _removeCoucou = new();
    private readonly System.Windows.Controls.TextBlock _backup = new() { TextWrapping = TextWrapping.Wrap };
    private readonly System.Windows.Controls.TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly StackPanel _diff = new();
    private readonly Wpf.Ui.Controls.Button _confirm = new() { Appearance = ControlAppearance.Primary, Margin = new Thickness(8, 0, 0, 0) };
    private ConfigPreview? _preview;

    internal ConfigReviewWindow(Mode mode, string settingsPath, string relayPath, int coucouEvents)
    {
        _mode = mode;
        _settingsPath = settingsPath;
        _relayPath = relayPath;
        Title = _s.ReviewTitle;
        Width = 760;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ExtendsContentIntoTitleBar = false;

        var page = new DockPanel { Margin = new Thickness(24) };
        var top = new StackPanel();
        top.Children.Add(Text(_s.ReviewTitle, "PageTitle"));
        _backup.Style = (Style)Application.Current.Resources["Caption"];
        top.Children.Add(_backup);
        if (coucouEvents > 0)
        {
            _removeCoucou.Content = _s.ReviewRemoveCoucou;
            _removeCoucou.IsChecked = true;
            _removeCoucou.Margin = new Thickness(0, 8, 0, 0);
            _removeCoucou.Click += (_, _) => Refresh();
            top.Children.Add(_removeCoucou);
        }
        DockPanel.SetDock(top, Dock.Top);
        page.Children.Add(top);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Wpf.Ui.Controls.Button { Content = _s.ReviewCancel, Appearance = ControlAppearance.Secondary };
        cancel.Click += (_, _) => { DialogResult = false; };
        _confirm.Content = mode == Mode.Install ? _s.ReviewConfirmInstall : _s.ReviewConfirmRemove;
        _confirm.Click += (_, _) => Confirm();
        buttons.Children.Add(cancel);
        buttons.Children.Add(_confirm);
        var bottom = new StackPanel();
        bottom.Children.Add(_message);
        bottom.Children.Add(buttons);
        DockPanel.SetDock(bottom, Dock.Bottom);
        page.Children.Add(bottom);

        page.Children.Add(new Border
        {
            Margin = new Thickness(0, 12, 0, 0),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            Child = new ScrollViewer
            {
                Content = _diff,
                Padding = new Thickness(12),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
        });
        Content = page;
        Refresh();
    }

    /// <summary>Opens the review; true when the edit was written.</summary>
    public static bool Review(Window? owner, Mode mode, string settingsPath, string relayPath, int coucouEvents)
    {
        var window = new ConfigReviewWindow(mode, settingsPath, relayPath, coucouEvents) { Owner = owner };
        return window.ShowDialog() == true;
    }

    private string Transform(string? json) => _mode == Mode.Install
        ? ClaudeHookConfig.Install(json, _relayPath, _removeCoucou.IsChecked == true)
        : ClaudeHookConfig.Uninstall(json, _removeCoucou.IsChecked == true);

    private void Refresh()
    {
        try
        {
            _preview = ConfigFile.Preview(_settingsPath, Transform, DateTime.Now);
        }
        catch (ConfigFormatException ex)
        {
            _preview = null;
            Show(string.Format(System.Globalization.CultureInfo.CurrentCulture, _s.FileUnreadableFormat, ex.Message), isError: true);
            _confirm.IsEnabled = false;
            return;
        }
        _backup.Text = _preview.BackupPath is { } backup
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, _s.ReviewBackupFormat, backup)
            : _s.ReviewNewFile;
        _diff.Children.Clear();
        if (!_preview.Changes)
        {
            _diff.Children.Add(Line(_s.ReviewNoChange, UnifiedDiff.LineKind.Context));
        }
        foreach (var line in _preview.Diff)
        {
            var prefix = line.Kind switch { UnifiedDiff.LineKind.Removed => "-", UnifiedDiff.LineKind.Added => "+", UnifiedDiff.LineKind.Context => " ", _ => string.Empty };
            _diff.Children.Add(Line(prefix + line.Text, line.Kind));
        }
        _confirm.IsEnabled = _preview.Changes;
    }

    private void Confirm()
    {
        if (_preview is null)
        {
            return;
        }
        try
        {
            ConfigFile.Apply(_preview);
            DialogResult = true;
        }
        catch (ConfigChangedException)
        {
            Refresh();
            Show(_s.FileChanged, isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Show(string.Format(System.Globalization.CultureInfo.CurrentCulture, _s.WriteFailedFormat, ex.Message), isError: true);
        }
    }

    private void Show(string text, bool isError)
    {
        _message.Text = text;
        _message.Foreground = (Brush)Application.Current.Resources[isError ? "SystemFillColorCriticalBrush" : "TextFillColorSecondaryBrush"];
    }

    private static System.Windows.Controls.TextBlock Line(string text, UnifiedDiff.LineKind kind) => new()
    {
        Text = text,
        FontFamily = MonoFont,
        FontSize = 12,
        Foreground = (Brush)Application.Current.Resources[kind == UnifiedDiff.LineKind.Hunk ? "TextFillColorTertiaryBrush" : "TextFillColorPrimaryBrush"],
        Background = kind switch
        {
            UnifiedDiff.LineKind.Added => (Brush)Application.Current.Resources["SystemFillColorSuccessBackgroundBrush"],
            UnifiedDiff.LineKind.Removed => (Brush)Application.Current.Resources["SystemFillColorCriticalBackgroundBrush"],
            _ => Brushes.Transparent,
        },
    };

    private static System.Windows.Controls.TextBlock Text(string text, string style) =>
        new() { Text = text, Style = (Style)Application.Current.Resources[style] };
}
```

- [ ] **Step 4: Write the page and register it**

`src/Faqra.App/Settings/Pages/AgentsPage.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of Coucou's Settings → Claude Code → Install hooks (windows/README.md, "Claude Code"),
// https://github.com/Louis-CFM/coucou (MIT License, Copyright (c) 2026 Louis Raillé).

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Faqra.Core;
using Faqra.Core.Agents.Install;
using Faqra.Core.Localization;
using Wpf.Ui.Controls;

namespace Faqra.App.Settings.Pages;

/// <summary>Whether Claude Code's hooks are installed, and the reviewed install and removal.</summary>
public sealed class AgentsPage : UserControl
{
    private readonly string _settingsPath;
    private readonly string _relayPath;
    private readonly AgentsStrings _s = AgentsStrings.For(L10n.Shared.Language);
    private readonly StackPanel _page = new();

    public AgentsPage()
        : this(AppPaths.ClaudeSettingsFile, AppPaths.AgentsRelayFile)
    {
    }

    internal AgentsPage(string settingsPath, string relayPath)
    {
        _settingsPath = settingsPath;
        _relayPath = relayPath;
        Content = _page;
        Build(note: null);
    }

    private void Build(string? note)
    {
        _page.Children.Clear();
        _page.Children.Add(Text(L10n.Shared.S.SettingsPageTitles[Core.Settings.SettingsPage.Agents], "PageTitle"));
        var caption = Text(_s.PageCaption, "Caption");
        caption.TextWrapping = TextWrapping.Wrap;
        caption.MaxWidth = 560;
        caption.HorizontalAlignment = HorizontalAlignment.Left;
        _page.Children.Add(caption);
        _page.Children.Add(Text(_s.HooksSection, "SectionHeader"));

        HookStatus status;
        string? problem = null;
        try
        {
            status = ClaudeHookConfig.Inspect(File.Exists(_settingsPath) ? File.ReadAllText(_settingsPath) : null);
        }
        catch (ConfigFormatException ex)
        {
            status = new HookStatus(0, 0);
            problem = Format(_s.FileUnreadableFormat, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            status = new HookStatus(0, 0);
            problem = ex.Message;
        }

        var title = status.Installed ? _s.HooksInstalled : status.FaqraEvents > 0 ? _s.HooksOutdated : _s.HooksNotInstalled;
        var header = new StackPanel();
        header.Children.Add(Text(title, "Body"));
        header.Children.Add(Text(Format(_s.HooksCaptionFormat, _settingsPath), "Caption"));

        var relayReady = File.Exists(_relayPath);
        var action = new Wpf.Ui.Controls.Button
        {
            Content = status.Installed ? _s.ReviewRemove : _s.ReviewInstall,
            Appearance = status.Installed ? ControlAppearance.Secondary : ControlAppearance.Primary,
            IsEnabled = problem is null && (status.Installed || relayReady),
        };
        action.Click += (_, _) =>
        {
            var mode = status.Installed ? ConfigReviewWindow.Mode.Remove : ConfigReviewWindow.Mode.Install;
            if (ConfigReviewWindow.Review(Window.GetWindow(this), mode, _settingsPath, _relayPath, status.CoucouEvents))
            {
                Build(mode == ConfigReviewWindow.Mode.Install ? _s.Installed : _s.Removed);
            }
        };
        _page.Children.Add(new CardControl
        {
            Icon = new SymbolIcon(SymbolRegular.Bot24),
            Header = header,
            Content = action,
            Margin = new Thickness(0, 0, 0, 0),
        });

        foreach (var line in new[]
        {
            status.CoucouEvents > 0 ? Format(_s.CoucouLeftoversFormat, status.CoucouEvents) : null,
            !relayReady && !status.Installed ? _s.RelayMissing : null,
            problem,
            note,
        })
        {
            if (line is null)
            {
                continue;
            }
            var text = Text(line, "Caption");
            text.TextWrapping = TextWrapping.Wrap;
            text.Margin = new Thickness(0, 8, 0, 0);
            text.MaxWidth = 560;
            text.HorizontalAlignment = HorizontalAlignment.Left;
            _page.Children.Add(text);
        }
    }

    private static string Format(string format, object value) => string.Format(CultureInfo.CurrentCulture, format, value);

    private static System.Windows.Controls.TextBlock Text(string text, string style) =>
        new() { Text = text, Style = (Style)Application.Current.Resources[style] };
}
```

In `src/Faqra.App/Settings/SettingsWindow.xaml.cs`, add `SettingsPage.Agents` to `ImplementedPages` (after `SettingsPage.CommandBar`) and to `CreatePage` (after the CommandBar arm):

```csharp
        SettingsPage.Agents => new AgentsPage(),
```

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test Faqra.sln`
Expected: PASS, `AgentsRenderTests` now 7. Look at `settings-agents.png` and `settings-agents-review.png`. If a theme resource key (`SystemFillColorSuccessBackgroundBrush`, `SystemFillColorCriticalBackgroundBrush`, `SystemFillColorCriticalBrush`, `CardStrokeColorDefaultBrush`) is missing from WPF UI 4.3's dictionaries, the render test throws `ResourceReferenceKeyNotFoundException`; open `%USERPROFILE%\.nuget\packages\wpf-ui\4.3.0` and use the key WPF UI actually defines for that role.

- [ ] **Step 6: Commit**

```bash
git add src/Faqra.App/Settings tests/Faqra.App.Tests/AgentsRenderTests.cs
git commit -m "feat(windows): Agents settings page with a reviewed install of Claude Code's hooks"
```

---

### Task 11: Ship it to the test build and check it against a real Claude Code

**Files:**
- Create: `THIRD-PARTY-NOTICES.md` (in `Windows/`)
- Modify: `design-system.md`, `KNOWN-ISSUES.md` (in `Windows/`)

- [ ] **Step 1: Write the notices**

`THIRD-PARTY-NOTICES.md`:

```markdown
# Third-party notices

Faqra is GPL-3.0-or-later. It includes code ported from these MIT-licensed projects; their notices follow.

## Thinking Orbs

The orb drawing in `src/Faqra.Core/Agents/Orb/OrbModel.cs` and `src/Faqra.App/Agents/OrbView.cs` is ported
from https://github.com/yogesharc/thinking-orbs. The reference frames in
`tests/Faqra.Core.Tests/Agents/Data/orb-reference.json` were recorded from it by `tools/orb-reference/gen.mjs`.

MIT License

Copyright (c) 2026 Yogesh

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
documentation files (the "Software"), to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and
to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of
the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO
THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
IN THE SOFTWARE.

## Coucou

The agent hook contract, relay protocol, session rules and reviewed config install in `src/Faqra.Core/Agents`,
`src/Faqra.Hook`, `src/Faqra.Services/Agents` and the Agents settings page are ported from
https://github.com/Louis-CFM/coucou. Only Coucou's code is used: its names, its Mochi character, icons and sounds
are not part of Faqra (see Coucou's LICENSE-ASSETS.md).

MIT License

Copyright (c) 2026 Louis Raillé

(The same permission notice and disclaimer as above.)
```

- [ ] **Step 2: Record the design decisions**

In `design-system.md`, add a `## Components` section before `## Verification`:

```markdown
## Components

| Component | Source | What changed |
|---|---|---|
| Thinking orb (`OrbView`) | [Thinking Orbs](https://21st.dev/@yogesharc/components/thinking-orbs), yogesharc, MIT | Ported from SVG to a WPF `FrameworkElement`; the math is unchanged and tested frame for frame against the original. Ink comes from the agent's tone (island palette plus Windows 11's dark caution, critical and success colours), never from a hard-coded per-orb colour. Holds one frame when Windows' animations are off. |
```

and add rows to the deviations table:

```markdown
| (No upstream equivalent) Agents module, from Coucou | A Faqra-only island module and feature, persisted as `faqraAgents` | Watching Claude Code is the owner's request; the Faqra prefix keeps it from ever colliding with an upstream module or feature. |
| Coucou's Mochi mascot | A thinking orb whose motion is the state and whose ink is the urgency | Mochi is not licensed for reuse, and an orb carries the same state with one glyph. |
| Island content follows the music or battery setting | An agent that is busy or waiting takes the resting pill first | A session that needs the owner outranks what is playing. |
```

- [ ] **Step 3: Full suite, then publish the relay alone and measure it**

Run: `dotnet test Faqra.sln`
Expected: PASS.

Run (PowerShell): `dotnet publish src/Faqra.Hook/Faqra.Hook.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o $env:TEMP\faqra-relay-check`

Then measure 30 cold starts with nothing listening (PowerShell):

```powershell
$exe = "$env:TEMP\faqra-relay-check\faqra-hook.exe"
$payload = '{"session_id":"s1","hook_event_name":"PreToolUse","tool_name":"Bash","tool_input":{"command":"echo hi"}}'
$times = 1..30 | ForEach-Object { (Measure-Command { $payload | & $exe PreToolUse }).TotalMilliseconds }
"median {0:N0} ms, max {1:N0} ms" -f ($times | Sort-Object)[15], ($times | Measure-Object -Maximum).Maximum
```

Expected: with nothing listening the relay returns as soon as it has started and read stdin, so this median is the relay's own start-up cost. Record it. Step 6 repeats the loop with Faqra listening; that is the number the spec's 80 ms budget applies to. If it passes 80 ms, record it in `KNOWN-ISSUES.md` with the http-hook fallback named in the spec; do not build the fallback in A1.

- [ ] **Step 4: Check the real settings file survives a round trip**

Copy (never edit) the owner's file and run the transform on the copy:

```powershell
$copy = "$env:TEMP\faqra-settings-roundtrip.json"
Copy-Item "$env:USERPROFILE\.claude\settings.json" $copy
```

Write a scratch console in the session scratchpad that references `src/Faqra.Core/Faqra.Core.csproj` and prints `ClaudeHookConfig.Uninstall(text, false) == text`, the `UnifiedDiff.Lines(text, ClaudeHookConfig.Install(text, relay, true))` output, and `ClaudeHookConfig.Inspect(text)`. Expected: the round trip prints `True`; the install diff adds Faqra's 14 events and removes only lines containing `coucou-hook`; Inspect reports `HookStatus { FaqraEvents = 0, CoucouEvents = 12 }`. If the round trip is not identical, fix `ClaudeHookConfig.Write` until it is, add the difference as a case to `ClaudeHookConfigTests`, and rerun.

- [ ] **Step 5: Publish to the test build (ask first)**

The owner runs `C:\Users\Tigre\Faqra-test\Faqra.exe` day to day. Ask the owner before stopping it. With their OK, stop it (`Stop-Process -Name Faqra`), then publish both executables into the folder:

```powershell
dotnet publish src/Faqra.App/Faqra.App.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o C:\Users\Tigre\Faqra-test
dotnet publish src/Faqra.Hook/Faqra.Hook.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o C:\Users\Tigre\Faqra-test
Start-Process C:\Users\Tigre\Faqra-test\Faqra.exe
```

Expected: `%LOCALAPPDATA%\Faqra\bin\faqra-hook.exe` exists within a few seconds of launch (the Agents binding deployed it), and `[System.IO.Directory]::GetFiles('\\.\pipe\') | Select-String faqra-agents` lists the pipe.

- [ ] **Step 6: Watch a real session in a scratch project (ask first)**

This runs Claude Code with the owner's account. Ask before running it. With their OK, create a scratch folder whose project settings point at the deployed relay, so the owner's own `~/.claude/settings.json` is untouched:

```powershell
$p = "$env:TEMP\faqra-agents-live"; New-Item -ItemType Directory -Force "$p\.claude" | Out-Null
"hello" | Set-Content "$p\README.md"
$relay = "$env:LOCALAPPDATA\Faqra\bin\faqra-hook.exe".Replace('\','\\')
$events = 'SessionStart','SessionEnd','UserPromptSubmit','PreToolUse','PostToolUse','PostToolUseFailure','PermissionRequest','Notification','Stop','StopFailure','SubagentStart','SubagentStop','PreCompact','PostCompact'
$hooks = ($events | ForEach-Object { "`"$_`": [{`"hooks`": [{`"type`": `"command`", `"command`": `"$relay`", `"args`": [`"$_`"], `"timeout`": 10}]}]" }) -join ', '
"{`"hooks`": {$hooks}}" | Set-Content "$p\.claude\settings.local.json"
Push-Location $p; claude -p "Read README.md, then run: echo hi" --allowedTools "Read,Bash(echo:*)"; Pop-Location
```

Expected: `%LOCALAPPDATA%\Faqra\agents.log` gains lines for `SessionStart`, `UserPromptSubmit` (state `Thinking`), `PreToolUse Read` (`Working`), `PreToolUse Bash` (`Working`), `Stop` (`Finished`), `SessionEnd` (`gone`), in that order; none contains the prompt or the command. While it runs, the island's resting pill shows the orb (verify with the UI Automation dump in `%TEMP%\faqra-live`, or by the island's width changing from the camera size to the content size). Then measure the relay with Faqra listening (repeat Step 3's loop against `%LOCALAPPDATA%\Faqra\bin\faqra-hook.exe`, with `FAQRA_AGENTS_PIPE` unset) and record the median. Delete the scratch folder afterwards.

- [ ] **Step 7: The owner installs the real hooks**

Ask the owner to open Settings, Agents, press "Review and install", read the diff (Faqra's 14 events added, Coucou's 12 removed, nothing else), and confirm. Then check: `settings.json.bak-*` exists beside their settings and matches the previous file byte for byte, `ClaudeHookConfig.Inspect` on the new file reports 14 and 0, and a new Claude Code session in Antigravity shows up in the island.

- [ ] **Step 8: Write down what is open, commit and push**

Add an A1 entry to `KNOWN-ISSUES.md` under "Gaps a user will notice": approvals, questions, replies and go-to-window arrive in A2 and A3 (the island only watches); the relay medians measured in Steps 3 and 6; any orb frame cost noticed. Then:

```bash
git add THIRD-PARTY-NOTICES.md design-system.md KNOWN-ISSUES.md
git commit -m "docs(windows): Faqra Agents A1 notices, design record and open items"
git push
```

Save to memory: A1 done, the measured relay medians, whether the owner installed the hooks, and that A2 (approvals, questions, alerts, go to window) is next.
