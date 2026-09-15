// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the tooltip branch of Sources/Vorssaint/App/StatusItemController.swift (lines 466-481)

using Faqra.Core.Localization;

namespace Faqra.Core.Tray;

/// <summary>The tray icon's hover text: idle, awake until a time, or awake indefinitely.</summary>
public static class StatusTooltip
{
    public static string For(bool keepAwakeActive, string? endTimeText, Strings s)
    {
        if (!keepAwakeActive)
        {
            return s.StatusIdleTooltip;
        }
        return endTimeText is null ? s.StatusActiveIndefinite : $"{s.StatusActiveUntil} {endTimeText}";
    }
}
