// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// The runnable half of CommandBarEntry in Sources/Vorssaint/Services/CommandBar/CommandBarCatalog.swift
// (lines 7-155): the icon, live state, argument range, confirmation and the closure a row runs. The
// searchable half is Faqra.Core.CommandBar.CommandBarEntry.

using Faqra.Core.CommandBar;
using Faqra.Win32.Shell;
using Wpf.Ui.Controls;

namespace Faqra.App.CommandBar;

internal abstract record CommandBarIcon
{
    internal sealed record Symbol(SymbolRegular Glyph) : CommandBarIcon;

    internal sealed record App(InstalledApp Installed) : CommandBarIcon;

    /// <summary>The icon of an executable or folder on disk.</summary>
    internal sealed record File(string Path) : CommandBarIcon;
}

/// <summary>A number a row takes, typed after its name or asked for in argument mode.</summary>
/// <param name="Optional">Enter without a number runs the row's own default (keep awake toggles).</param>
internal sealed record CommandBarArgument(int Minimum, int Maximum, bool Optional);

internal sealed record CommandBarRow(
    CommandBarEntry Entry,
    CommandBarIcon Icon,
    Action<int?> Run,
    bool IsActive = false,
    string? AnswerValue = null,
    CommandBarArgument? Argument = null,
    string? ConfirmationPrompt = null,
    string? ShortcutText = null,
    bool ActivatesAnotherWindow = false);
