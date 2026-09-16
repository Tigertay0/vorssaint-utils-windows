using System.Globalization;
using Faqra.Core.Metrics;

namespace Faqra.Core.Tests;

// Cases ported from Tests/MetricsTests.swift; line numbers in comments.
public class MetricFormatTests
{
    private static readonly CultureInfo Posix = CultureInfo.InvariantCulture;
    private static readonly CultureInfo Brazil = new("pt-BR");

    [Theory]
    [InlineData(0UL, "0 B")]                      // 98
    [InlineData(512UL, "512 B")]                  // 99
    [InlineData(1024UL, "1.0 KB")]                // 100
    [InlineData(1536UL, "1.5 KB")]                // 101
    [InlineData(10UL * 1024, "10 KB")]            // 109
    [InlineData(1024UL * 1024, "1.0 MB")]         // 110
    [InlineData(3UL * 1024 * 1024 * 1024, "3.0 GB")] // 111
    public void Bytes(ulong value, string expected) => Assert.Equal(expected, MetricFormat.Bytes(value, Posix));

    [Fact]
    public void DecimalsFollowTheRegionalFormat()
    {
        Assert.Equal("1,5 KB", MetricFormat.Bytes(1536, Brazil));                          // 105
        Assert.Equal("21 °C", MetricFormat.Temperature(21.4, TemperatureUnit.Celsius, Brazil)); // 106
    }

    [Theory]
    [InlineData(245_107_195_904UL, "245 GB")]     // 112
    [InlineData(1_000_204_845_056UL, "1.0 TB")]   // 113
    [InlineData(123_456_789_000UL, "123 GB")]     // 114
    [InlineData(999UL, "999 B")]
    public void DiskBytesUseBase1000(ulong value, string expected) => Assert.Equal(expected, MetricFormat.DiskBytes(value, Posix));

    [Fact]
    public void DiskBytesPreciseShowsTwoDecimalsForTerabytes() =>
        Assert.Equal("14.88 TB", MetricFormat.DiskBytesPrecise(14_878_047_232_000, Posix)); // 115

    [Theory]
    [InlineData(0d, "0 B/s")]                     // 118
    [InlineData(2d * 1024 * 1024, "2.0 MB/s")]    // 119
    [InlineData(1500d * 1024, "1.5 MB/s")]        // 120
    [InlineData(double.NaN, "0 B/s")]
    [InlineData(-5d, "0 B/s")]
    public void BytesPerSec(double value, string expected) => Assert.Equal(expected, MetricFormat.BytesPerSec(value, Posix));

    [Theory]
    [InlineData(0d, "0B")]                        // 122
    [InlineData(320d * 1024, "320K")]             // 123
    [InlineData(1.2 * 1024 * 1024, "1.2M")]       // 124
    [InlineData(1022d, "1022B")]                  // 125
    [InlineData(1023.4, "1023B")]                 // 126
    [InlineData(1023.6, "1.0K")]                  // 127
    [InlineData(9.96 * 1024, "10K")]              // 128
    [InlineData(1023.6 * 1024, "1.0M")]           // 129
    [InlineData(9.96 * 1024 * 1024, "10M")]       // 130
    [InlineData(double.PositiveInfinity, "0B")]
    public void BytesPerSecCompact(double value, string expected) =>
        Assert.Equal(expected, MetricFormat.BytesPerSecCompact(value, Posix));

    [Fact]
    public void Watts()
    {
        Assert.Equal("8.5 W", MetricFormat.Watts(8.5, Posix));    // 1756
        Assert.Equal("23 W", MetricFormat.Watts(23.4, Posix));    // 1757
        Assert.Equal("9W", MetricFormat.WattsCompact(8.6, Posix)); // 1758
    }

    [Fact]
    public void SystemPowerFallsBackToBatteryDrainOnlyOnBattery()
    {
        Assert.Equal(3, MetricFormat.SystemPowerWatts(3, 10, externalConnected: true));    // 844
        Assert.Null(MetricFormat.SystemPowerWatts(null, 10, externalConnected: true));     // 848
        Assert.Equal(4, MetricFormat.SystemPowerWatts(null, -4, externalConnected: false)); // 852
        Assert.Null(MetricFormat.SystemPowerWatts(null, -4, externalConnected: true));     // 856
    }

    [Theory]
    [InlineData(0d, "0%")]       // 1759
    [InlineData(0.125, "13%")]   // 1760
    [InlineData(1d, "100%")]     // 1761
    [InlineData(1.4, "100%")]    // 1762
    [InlineData(-0.2, "0%")]     // 1763
    [InlineData(double.NaN, "100%")] // Swift min(1, NaN) is 1
    public void Percent(double fraction, string expected) => Assert.Equal(expected, MetricFormat.Percent(fraction));

    [Fact]
    public void BoundedPercentage()
    {
        Assert.Equal(100, MetricFormat.BoundedPercentage(130), 4); // 1764
        Assert.Equal(0, MetricFormat.BoundedPercentage(-5), 4);    // 1766
        Assert.Equal(0, MetricFormat.BoundedPercentage(double.NaN), 4);
    }

    [Fact]
    public void MenuBarMemoryPercent()
    {
        Assert.Equal("79%", MetricFormat.MenuBarMemoryPercent(79, 100)); // 1788
        Assert.Equal("--%", MetricFormat.MenuBarMemoryPercent(null, 100)); // 1790
        Assert.Equal("--%", MetricFormat.MenuBarMemoryPercent(79, null)); // 1792
        Assert.Equal("--%", MetricFormat.MenuBarMemoryPercent(79, 0));    // 1794
    }

    [Fact]
    public void SelectedMemoryFallsBackToUsed()
    {
        Assert.Equal(7UL, MetricFormat.SelectedMemory(12UL, 7UL, "app"));     // 2131
        Assert.Equal(12UL, MetricFormat.SelectedMemory(12UL, 7UL, "unknown")); // 2133
    }

    [Fact]
    public void StabilizedGpuUsageCapsRisesAndEasesFalls()
    {
        Assert.Equal(0.23, MetricFormat.StabilizedGpuUsage(0.03, 0.80), 4);  // 1796
        Assert.Equal(0.43, MetricFormat.StabilizedGpuUsage(0.23, 0.80), 4);  // 1798
        Assert.Equal(0.275, MetricFormat.StabilizedGpuUsage(0.60, 0.10), 4); // 1800
        Assert.Equal(1.0, MetricFormat.StabilizedGpuUsage(null, 1.4), 4);    // 1802
        Assert.Equal(0.0, MetricFormat.StabilizedGpuUsage(null, double.NaN), 4);
    }

    [Fact]
    public void Temperatures()
    {
        Assert.Equal("0 °C", MetricFormat.Temperature(0, TemperatureUnit.Celsius, Posix));      // 1804
        Assert.Equal("32 °F", MetricFormat.Temperature(0, TemperatureUnit.Fahrenheit, Posix));  // 1805
        Assert.Equal("106 °F", MetricFormat.Temperature(41, TemperatureUnit.Fahrenheit, Posix)); // 1806
        Assert.Equal("50°", MetricFormat.TemperatureCompact(49.6, TemperatureUnit.Celsius, Posix));    // 1807
        Assert.Equal("121°", MetricFormat.TemperatureCompact(49.6, TemperatureUnit.Fahrenheit, Posix)); // 1808
        Assert.Equal("°C", MetricFormat.TemperatureUnitSuffix(TemperatureUnit.Celsius));    // 1809
        Assert.Equal("°F", MetricFormat.TemperatureUnitSuffix(TemperatureUnit.Fahrenheit)); // 1810
    }

    [Theory]
    [InlineData(0, "0min")]                                 // 2058
    [InlineData(59, "0min")]
    [InlineData(60, "1min")]
    [InlineData(3_600, "1h 0min")]
    [InlineData(93_600, "1d 2h")]
    [InlineData(8 * 86_400 + 21 * 3_600 + 8 * 60, "8d 21h")] // 2064
    [InlineData(-10, "0min")]
    public void Uptime(long seconds, string expected) => Assert.Equal(expected, MetricFormat.Uptime(seconds));

    [Fact]
    public void RatesFromCumulativeCounters()
    {
        var (down, up) = MetricFormat.NetSpeed(new NetworkCounters(1000, 500), new NetworkCounters(3048, 1524), 2); // 14408
        Assert.Equal(1024, down, 4);
        Assert.Equal(512, up, 4);
        Assert.Equal((0d, 0d), MetricFormat.NetSpeed(new NetworkCounters(1000, 500), new NetworkCounters(3048, 1524), 0)); // 14412
        Assert.Equal((0d, 0d), MetricFormat.NetSpeed(new NetworkCounters(3048, 1524), new NetworkCounters(1000, 500), 2)); // 14416

        var (read, write) = MetricFormat.DiskSpeed(new DiskIOCounters(1_000, 500), new DiskIOCounters(3_048, 1_524), 2); // 180
        Assert.Equal(1024, read, 4);
        Assert.Equal(512, write, 4);
        Assert.Equal((0d, 0d), MetricFormat.DiskSpeed(new DiskIOCounters(3_048, 1_524), new DiskIOCounters(2_000, 800), 2)); // 187
    }

    [Fact]
    public void TemperatureUnitRawValues()
    {
        Assert.Equal(TemperatureUnit.Fahrenheit, TemperatureUnits.FromRawValue("fahrenheit"));
        Assert.Equal(TemperatureUnit.Celsius, TemperatureUnits.FromRawValue("kelvin"));
        Assert.Equal("celsius", TemperatureUnit.Celsius.RawValue());
    }
}
