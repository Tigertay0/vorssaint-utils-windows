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
        var clock = ReplayClock(index, step, samples.Max());
        for (var n = 0; n < clock.Length; n++)
        {
            var dots = model.Frame(clock[n]);
            var t = n * step;
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

    // The original's clock adds up float timestamps, so a frame meant for 1100 ms lands a hair under or over it,
    // and Reasoning's hop at every 220 ms edge falls on one frame or the next. Replay the recording's clock
    // (gen.mjs: now starts at 1000, each case runs its own speed 1 + index * 1e-9) so the edges fall where they did.
    private static double[] ReplayClock(int caseIndex, int step, int last)
    {
        const double start = 1000;
        const double maxFrameMs = 100;
        var frames = last / step;
        var now = start;
        var times = new double[frames + 1];
        for (var c = 0; c <= caseIndex; c++)
        {
            var speed = 1 + c * 1e-9;
            var lastNow = now;
            double t = 0;
            for (var n = 1; n <= frames; n++)
            {
                now += step / speed;
                if (now > lastNow)
                {
                    t += Math.Min(now - lastNow, maxFrameMs) * speed;
                    lastNow = now;
                }
                times[n] = t;
            }
        }
        return times;
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
