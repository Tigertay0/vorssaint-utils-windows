// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/KeepAwakeAutomationSupport.swift and the battery rules of
// Services/KeepAwakeManager.swift (automaticSessionAllowedByBatteryProtection 479-487, checkBattery 738-745).

namespace Faqra.Core.KeepAwake;

public enum KeepAwakeCondition { ExternalDisplay, Power, RunningApps }

public enum KeepAwakeAutomationAction { None, Activate, Deactivate }

public enum KeepAwakeTrigger { Manual, Automation }

public enum KeepAwakeEndReason { Manual, Timer, Battery, Quit }

public static class KeepAwakeAutomationSupport
{
    /// <summary>Battery protection is checked this often while a session holds the PC awake.</summary>
    public static readonly TimeSpan BatteryCheckInterval = TimeSpan.FromSeconds(30);

    public static bool HasExternalDisplay(IEnumerable<bool> builtInFlags) => builtInFlags.Contains(false);

    public static bool SelectedAppsAreRunning(IReadOnlyCollection<string> selected, IEnumerable<string> running)
    {
        if (selected.Count == 0)
        {
            return false;
        }
        var set = selected.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return running.Any(set.Contains);
    }

    public static IReadOnlySet<KeepAwakeCondition> MatchingConditions(
        bool externalDisplayEnabled, bool externalDisplayConnected,
        bool powerEnabled, bool connectedToPower,
        bool runningAppsEnabled, bool selectedAppsRunning)
    {
        var matches = new HashSet<KeepAwakeCondition>();
        if (externalDisplayEnabled && externalDisplayConnected)
        {
            matches.Add(KeepAwakeCondition.ExternalDisplay);
        }
        if (powerEnabled && connectedToPower)
        {
            matches.Add(KeepAwakeCondition.Power);
        }
        if (runningAppsEnabled && selectedAppsRunning)
        {
            matches.Add(KeepAwakeCondition.RunningApps);
        }
        return matches;
    }

    public static KeepAwakeAutomationAction Action(
        bool featureAvailable, IReadOnlySet<KeepAwakeCondition> matchingConditions, bool sessionActive, bool automaticSessionActive)
    {
        if (!featureAvailable || matchingConditions.Count == 0)
        {
            return automaticSessionActive ? KeepAwakeAutomationAction.Deactivate : KeepAwakeAutomationAction.None;
        }
        return sessionActive ? KeepAwakeAutomationAction.None : KeepAwakeAutomationAction.Activate;
    }

    /// <summary>A new automatic session may start unless the PC is on battery at or below the limit.</summary>
    public static bool AutomaticSessionAllowed(int limit, bool onBattery, int percent) =>
        limit <= 0 || !onBattery || percent > limit;

    public static bool BatteryShouldEndSession(int limit, bool active, bool onBattery, int percent) =>
        limit > 0 && active && onBattery && percent <= limit;
}
