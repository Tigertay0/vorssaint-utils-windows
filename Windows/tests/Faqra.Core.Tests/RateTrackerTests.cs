using Faqra.Core.Metrics;

namespace Faqra.Core.Tests;

public class RateTrackerTests
{
    /// <summary>
    /// Tests/MetricsTests.swift:14455-14529, the intermittent scenario: missing readings, a gap longer
    /// than ten seconds, and a counter reset.
    /// </summary>
    [Fact]
    public void NetworkRatesSurviveMissingReadingsGapsAndResets()
    {
        NetworkCounters?[] counters =
        [
            null, new(1_000_000, 500_000), new(1_000_200, 500_100), null, null, new(1_000_800, 500_400),
            null, new(2_000_000, 900_000), new(2_000_200, 900_100), new(0, 0), new(200, 100),
        ];
        double[] times = [0, 1, 2, 3, 4, 5, 6, 20, 21, 22, 23];
        double?[] expectedDown = [null, null, 200, null, null, 200, null, null, 200, 0, 200];
        ulong[] expectedTotalDown = [0, 0, 200, 200, 200, 800, 800, 800, 1_000, 1_000, 1_200];

        var tracker = new NetworkRateTracker();
        for (var i = 0; i < counters.Length; i++)
        {
            var reading = tracker.Sample(counters[i], times[i]);

            Assert.Equal(expectedDown[i], reading.DownBytesPerSec);
            Assert.Equal(expectedDown[i] / 2, reading.UpBytesPerSec);
            Assert.Equal(expectedTotalDown[i], reading.TotalDown);
            Assert.Equal(expectedTotalDown[i] / 2, reading.TotalUp);
        }
    }

    [Fact]
    public void DiskRatesNeedABaselineWithinFifteenSeconds()
    {
        var tracker = new DiskRateTracker();

        var first = tracker.Sample("C:", new DiskIOCounters(1_000, 500), 0);
        Assert.Null(first.ReadBytesPerSec);
        Assert.Equal(0UL, first.TotalRead);

        var second = tracker.Sample("C:", new DiskIOCounters(3_048, 1_524), 2);
        Assert.Equal(1_024, second.ReadBytesPerSec);
        Assert.Equal(512, second.WriteBytesPerSec);
        Assert.Equal(2_048UL, second.TotalRead);
        Assert.Equal(1_024UL, second.TotalWritten);

        var gap = tracker.Sample("C:", new DiskIOCounters(9_000, 9_000), 20);
        Assert.Null(gap.ReadBytesPerSec);
        Assert.Equal(2_048UL, gap.TotalRead);

        var reset = tracker.Sample("C:", new DiskIOCounters(10, 10), 21);
        Assert.Equal(0, reset.ReadBytesPerSec);
        Assert.Equal(2_048UL, reset.TotalRead);

        // Each disk keeps its own baseline.
        Assert.Null(tracker.Sample("D:", new DiskIOCounters(5, 5), 22).ReadBytesPerSec);
    }
}
