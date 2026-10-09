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
