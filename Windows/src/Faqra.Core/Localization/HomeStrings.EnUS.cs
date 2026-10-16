// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

namespace Faqra.Core.Localization;

public sealed partial class HomeStrings
{
    public static HomeStrings EnUS { get; } = new()
    {
        Title = "Home",
        ItemNowPlaying = "Now playing",
        ItemAgents = "Agents",
        ItemTimer = "Timer",
        ItemSystem = "System",
        ItemBattery = "Battery",
        NothingPlaying = "Nothing playing",
        PlayPause = "Play or pause",
        NextTrack = "Next track",
        NoAgents = "No agents running",
        MoreAgentsFormat = "{0} more in Agents",
        Open = "Open",
        NoTimer = "No timer running",
        TimerLeftFormat = "{0} left",
        TimerPaused = "Paused",
        CpuFormat = "CPU {0}",
        MemoryFormat = "Memory {0}",
        BatteryCharging = "Charging",
        BatteryOnBattery = "On battery",
        Edit = "Edit Home",
        Done = "Done",
        MoveUpFormat = "Move {0} up",
        MoveDownFormat = "Move {0} down",
        RemoveFormat = "Remove {0}",
        AddFormat = "Add {0}",
        AddHeader = "Add to Home",
        Empty = "Nothing on Home yet. Edit Home to add rows.",
    };
}
