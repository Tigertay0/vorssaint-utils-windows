// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the card list of Sources/Vorssaint/UI/Notch/NotchSystemView.swift and systemCardCount in
// Services/Notch/NotchSupport.swift (lines 532-537).

using Faqra.Core.Features;

namespace Faqra.Core.Island;

public enum IslandSystemCard { Cpu, Gpu, Memory, Battery, Network, Disk }

public static class IslandSystemCards
{
    /// <summary>The cards the System module shows, in upstream's order; each needs its metric installed.</summary>
    public static IReadOnlyList<IslandSystemCard> Available(Func<AppFeature, bool> isAvailable, bool hasBattery)
    {
        var cards = new List<IslandSystemCard>(6);
        if (isAvailable(AppFeature.MonitorCPU)) cards.Add(IslandSystemCard.Cpu);
        if (isAvailable(AppFeature.MonitorGPU)) cards.Add(IslandSystemCard.Gpu);
        if (isAvailable(AppFeature.MonitorMemory)) cards.Add(IslandSystemCard.Memory);
        if (hasBattery && isAvailable(AppFeature.MonitorPower)) cards.Add(IslandSystemCard.Battery);
        if (isAvailable(AppFeature.MonitorNetwork)) cards.Add(IslandSystemCard.Network);
        if (isAvailable(AppFeature.MonitorDisk)) cards.Add(IslandSystemCard.Disk);
        return cards;
    }

    public static int Rows(int cardCount, int columns) => columns <= 0 ? 0 : (cardCount + columns - 1) / columns;
}
