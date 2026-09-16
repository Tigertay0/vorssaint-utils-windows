// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Smoke tests against the live machine: every reader must return plausible, finite numbers.

using Faqra.Win32.Perf;

namespace Faqra.Services.Tests;

public class Win32ReadersTests
{
    [Fact]
    public void MemoryStatusReportsAvailableWithinTotal()
    {
        var memory = MemoryStatus.Read();

        Assert.NotNull(memory);
        Assert.True(memory.Value.TotalBytes > 0);
        Assert.InRange(memory.Value.AvailableBytes, 1UL, memory.Value.TotalBytes);
        Assert.InRange(memory.Value.LoadPercent, 0U, 100U);
    }

    [Fact]
    public void DiskSpaceReportsFreeWithinTotalForTheSystemDrive()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory)!;

        var disk = DiskSpace.Read(root);

        Assert.NotNull(disk);
        Assert.True(disk.Value.TotalBytes > 0);
        Assert.InRange(disk.Value.FreeBytes, 0UL, disk.Value.TotalBytes);
    }

    [Fact]
    public void DiskSpaceIsNullForAMissingVolume()
    {
        Assert.Null(DiskSpace.Read(@"\\?\Volume{00000000-0000-0000-0000-000000000000}\"));
    }

    [Fact]
    public void NetworkInterfacesListsAtLeastOneWithMonotonicCounters()
    {
        var first = NetworkInterfaces.Read();
        var second = NetworkInterfaces.Read();

        Assert.NotEmpty(first);
        foreach (var later in second)
        {
            if (first.FirstOrDefault(i => i.Luid == later.Luid) is { Luid: not 0 } earlier)
            {
                Assert.True(later.InOctets >= earlier.InOctets);
                Assert.True(later.OutOctets >= earlier.OutOctets);
            }
        }
    }

    [Fact]
    public void PdhProcessorUtilityIsFiniteAfterTwoCollections()
    {
        using var query = PdhQuery.Open();
        Assert.NotNull(query);
        var counter = query.AddCounter(@"\Processor Information(_Total)\% Processor Utility");
        Assert.True(counter >= 0);

        query.Collect();
        Thread.Sleep(250);
        query.Collect();
        var value = query.ReadDouble(counter);

        Assert.NotNull(value);
        Assert.True(double.IsFinite(value.Value));
        Assert.True(value.Value >= 0);
    }

    [Fact]
    public void PdhWildcardCounterReturnsFiniteInstances()
    {
        using var query = PdhQuery.Open();
        Assert.NotNull(query);
        var counter = query.AddCounter(@"\Processor Information(*)\% Processor Utility");
        Assert.True(counter >= 0);

        query.Collect();
        Thread.Sleep(250);
        query.Collect();
        var instances = query.ReadInstances(counter);

        Assert.NotEmpty(instances);
        Assert.All(instances, instance => Assert.True(double.IsFinite(instance.Value)));
        Assert.Contains(instances, instance => instance.Name == "_Total");
    }

    [Fact]
    public void PdhRejectsAnUnknownCounter()
    {
        using var query = PdhQuery.Open();
        Assert.NotNull(query);

        Assert.Equal(-1, query.AddCounter(@"\No Such Object(_Total)\Nothing"));
    }
}
