// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

namespace Faqra.Core.Localization;

/// <summary>The island Home's words.</summary>
public sealed partial class HomeStrings
{
    public required string Title { get; init; }
    public required string ItemNowPlaying { get; init; }
    public required string ItemAgents { get; init; }
    public required string ItemTimer { get; init; }
    public required string ItemSystem { get; init; }
    public required string ItemBattery { get; init; }
    public required string NothingPlaying { get; init; }
    public required string PlayPause { get; init; }
    public required string NextTrack { get; init; }
    public required string NoAgents { get; init; }
    public required string MoreAgentsFormat { get; init; }
    public required string Open { get; init; }
    public required string NoTimer { get; init; }
    public required string TimerLeftFormat { get; init; }
    public required string TimerPaused { get; init; }
    public required string CpuFormat { get; init; }
    public required string MemoryFormat { get; init; }
    public required string BatteryCharging { get; init; }
    public required string BatteryOnBattery { get; init; }
    public required string Edit { get; init; }
    public required string Done { get; init; }
    public required string MoveUpFormat { get; init; }
    public required string MoveDownFormat { get; init; }
    public required string RemoveFormat { get; init; }
    public required string AddFormat { get; init; }
    public required string AddHeader { get; init; }
    public required string Empty { get; init; }

    public static HomeStrings For(AppLanguage language) => language switch
    {
        // Translations pending: every language resolves to English for now (see Strings.Aliases.cs).
        _ => EnUS,
    };
}
