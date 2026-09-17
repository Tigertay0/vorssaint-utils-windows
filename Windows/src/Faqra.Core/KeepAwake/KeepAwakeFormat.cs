// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors KeepAwakeCard.remainingText in Sources/Vorssaint/UI/MenuPanel/MenuPanelView.swift (2794-2802),
// activeStatus(for:) in Core/KeepAwakeStrings.swift (24-29) and the tooltip branch of
// App/StatusItemController.swift performRefresh (466-481).

using System.Globalization;
using Faqra.Core.Localization;
using Faqra.Core.Tray;

namespace Faqra.Core.KeepAwake;

public static class KeepAwakeFormat
{
    /// <summary>"2 h 02 min", "1 min 05 s" or "59 s".</summary>
    public static string Remaining(int totalSeconds)
    {
        var total = Math.Max(0, totalSeconds);
        var hours = total / 3600;
        var minutes = total % 3600 / 60;
        var seconds = total % 60;
        if (hours > 0)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} h {1:00} min", hours, minutes);
        }
        return minutes > 0
            ? string.Format(CultureInfo.InvariantCulture, "{0} min {1:00} s", minutes, seconds)
            : $"{seconds} s";
    }

    public static string ActiveStatus(IReadOnlySet<KeepAwakeCondition> conditions, KeepAwakeStrings s)
    {
        if (conditions.Count != 1)
        {
            return s.AutomationActive;
        }
        return conditions.First() switch
        {
            KeepAwakeCondition.ExternalDisplay => s.ExternalDisplayActive,
            KeepAwakeCondition.Power => s.PowerActive,
            _ => s.RunningAppsActive,
        };
    }

    public static string Tooltip(bool active, KeepAwakeTrigger? trigger, IReadOnlySet<KeepAwakeCondition> conditions, string? endTimeText, Strings s, KeepAwakeStrings ks) =>
        active && trigger == KeepAwakeTrigger.Automation
            ? ActiveStatus(conditions, ks)
            : StatusTooltip.For(active, endTimeText, s);

    /// <summary>The Windows short time for a UTC instant, in the user's zone ("15:45" or "3:45 PM").</summary>
    public static string ShortTime(DateTime utc, CultureInfo? culture = null) =>
        utc.ToLocalTime().ToString("t", culture ?? CultureInfo.CurrentCulture);

    /// <summary>Menu and picker title for a duration preset; the panel picker says "Indefinite", menus "Indefinitely".</summary>
    public static string DurationTitle(int minutes, Strings s, KeepAwakeStrings ks) =>
        minutes == 0 ? ks.Indefinite : ContextMenuBuilder.DurationTitle(minutes, s);
}
