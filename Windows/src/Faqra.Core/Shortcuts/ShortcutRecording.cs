// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors RecorderButton.handleRecordingKey in Sources/Vorssaint/UI/ShortcutRecorderButton.swift
// (lines 211-255), minus AppKit. Stage 1 records only role shortcuts, which cannot be cleared, so a
// bare Delete does nothing like upstream's rows without a clearAction.

namespace Faqra.Core.Shortcuts;

public enum RecordingOutcome
{
    /// <summary>Keep listening (a modifier went down, or a key that means nothing here).</summary>
    Waiting,
    Cancel,
    Invalid,
    Reserved,
    Captured,
}

public readonly record struct RecordingResult(RecordingOutcome Outcome, GlobalShortcut? Shortcut = null);

public static class ShortcutRecording
{
    private const int VkEscape = 0x1B;
    private const int VkBack = 0x08;
    private const int VkDelete = 0x2E;

    public static RecordingResult Handle(int virtualKey, ShortcutModifiers modifiers)
    {
        if (GlobalShortcut.IsModifierKey(virtualKey))
        {
            return new(RecordingOutcome.Waiting);
        }
        var candidate = new GlobalShortcut(virtualKey, modifiers);
        if (virtualKey == VkEscape && !candidate.HasPrimaryModifier)
        {
            return new(RecordingOutcome.Cancel);
        }
        if (virtualKey is VkBack or VkDelete && !candidate.HasPrimaryModifier)
        {
            return new(RecordingOutcome.Waiting);
        }
        if (!candidate.IsValid)
        {
            return new(RecordingOutcome.Invalid);
        }
        if (WindowsReservedShortcuts.IsReserved(candidate))
        {
            return new(RecordingOutcome.Reserved, candidate);
        }
        return new(RecordingOutcome.Captured, candidate);
    }
}
