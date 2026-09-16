using Faqra.Core.Features;
using Faqra.Core.Island;

namespace Faqra.Core.Tests;

public class IslandSystemCardsTests
{
    [Fact]
    public void CardsFollowUpstreamOrderAndBatteryNeedsABattery()
    {
        Assert.Equal(
            [IslandSystemCard.Cpu, IslandSystemCard.Gpu, IslandSystemCard.Memory, IslandSystemCard.Network, IslandSystemCard.Disk],
            IslandSystemCards.Available(_ => true, hasBattery: false));
        Assert.Equal(
            [IslandSystemCard.Cpu, IslandSystemCard.Gpu, IslandSystemCard.Memory, IslandSystemCard.Battery, IslandSystemCard.Network, IslandSystemCard.Disk],
            IslandSystemCards.Available(_ => true, hasBattery: true));
    }

    [Fact]
    public void UninstalledMetricsDropTheirCards()
    {
        var cards = IslandSystemCards.Available(feature => feature is AppFeature.MonitorCPU or AppFeature.MonitorDisk, hasBattery: true);

        Assert.Equal([IslandSystemCard.Cpu, IslandSystemCard.Disk], cards);
    }

    [Theory]
    [InlineData(5, 3, 2)]
    [InlineData(6, 3, 2)]
    [InlineData(5, 2, 3)]
    [InlineData(0, 3, 0)]
    public void RowsFillTheGrid(int cards, int columns, int rows) =>
        Assert.Equal(rows, IslandSystemCards.Rows(cards, columns));
}
