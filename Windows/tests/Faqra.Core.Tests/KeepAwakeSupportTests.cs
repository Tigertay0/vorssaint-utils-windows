using Faqra.Core.KeepAwake;
using Faqra.Core.Localization;

namespace Faqra.Core.Tests;

// Upstream has no tests for keep awake; these are written against the function bodies in
// Sources/Vorssaint/Services/KeepAwakeAutomationSupport.swift, KeepAwakeManager.swift (479-487, 738-745),
// MenuPanelView.swift remainingText (2794-2802) and KeepAwakeStrings.swift activeStatus (24-29).
public class KeepAwakeSupportTests
{
    private static HashSet<KeepAwakeCondition> Set(params KeepAwakeCondition[] items) => [.. items];

    [Theory]
    [InlineData(new bool[0], false)]
    [InlineData(new[] { true }, false)]
    [InlineData(new[] { false }, true)]
    [InlineData(new[] { true, false }, true)]
    [InlineData(new[] { true, true }, false)]
    public void HasExternalDisplay_WhenAnyDisplayIsNotBuiltIn(bool[] builtInFlags, bool expected) =>
        Assert.Equal(expected, KeepAwakeAutomationSupport.HasExternalDisplay(builtInFlags));

    [Theory]
    [InlineData(new string[0], new[] { "x" }, false)]
    [InlineData(new[] { "a" }, new string[0], false)]
    [InlineData(new[] { "a" }, new[] { "b" }, false)]
    [InlineData(new[] { "a" }, new[] { "a" }, true)]
    [InlineData(new[] { "a", "b" }, new[] { "c", "b" }, true)]
    public void SelectedAppsAreRunning(string[] selected, string[] running, bool expected) =>
        Assert.Equal(expected, KeepAwakeAutomationSupport.SelectedAppsAreRunning(selected, running));

    [Fact]
    public void MatchingConditions_EachNeedsItsSwitchAndItsSignal()
    {
        for (var mask = 0; mask < 64; mask++)
        {
            bool Bit(int i) => (mask & (1 << i)) != 0;
            var result = KeepAwakeAutomationSupport.MatchingConditions(Bit(0), Bit(1), Bit(2), Bit(3), Bit(4), Bit(5));
            Assert.Equal(Bit(0) && Bit(1), result.Contains(KeepAwakeCondition.ExternalDisplay));
            Assert.Equal(Bit(2) && Bit(3), result.Contains(KeepAwakeCondition.Power));
            Assert.Equal(Bit(4) && Bit(5), result.Contains(KeepAwakeCondition.RunningApps));
        }
    }

    [Theory]
    [InlineData(false, false, false, false, KeepAwakeAutomationAction.None)]
    [InlineData(false, true, true, true, KeepAwakeAutomationAction.Deactivate)]
    [InlineData(true, false, true, true, KeepAwakeAutomationAction.Deactivate)]
    [InlineData(true, false, true, false, KeepAwakeAutomationAction.None)]
    [InlineData(true, true, true, false, KeepAwakeAutomationAction.None)]
    [InlineData(true, true, false, false, KeepAwakeAutomationAction.Activate)]
    public void Action_TruthTable(bool featureAvailable, bool hasMatches, bool sessionActive, bool automaticSessionActive, KeepAwakeAutomationAction expected)
    {
        var matches = hasMatches ? new HashSet<KeepAwakeCondition> { KeepAwakeCondition.Power } : [];
        Assert.Equal(expected, KeepAwakeAutomationSupport.Action(featureAvailable, matches, sessionActive, automaticSessionActive));
    }

    [Theory]
    [InlineData(0, true, 3, true)]
    [InlineData(10, false, 3, true)]
    [InlineData(10, true, 10, false)]
    [InlineData(10, true, 11, true)]
    public void AutomaticSessionAllowedByBatteryProtection(int limit, bool onBattery, int percent, bool expected) =>
        Assert.Equal(expected, KeepAwakeAutomationSupport.AutomaticSessionAllowed(limit, onBattery, percent));

    [Theory]
    [InlineData(10, true, true, 10, true)]
    [InlineData(10, true, true, 9, true)]
    [InlineData(10, true, true, 11, false)]
    [InlineData(10, true, false, 5, false)]
    [InlineData(10, false, true, 5, false)]
    [InlineData(0, true, true, 1, false)]
    public void BatteryShouldEndSession(int limit, bool active, bool onBattery, int percent, bool expected) =>
        Assert.Equal(expected, KeepAwakeAutomationSupport.BatteryShouldEndSession(limit, active, onBattery, percent));

    [Theory]
    [InlineData(-4, "0 s")]
    [InlineData(0, "0 s")]
    [InlineData(5, "5 s")]
    [InlineData(59, "59 s")]
    [InlineData(60, "1 min 00 s")]
    [InlineData(65, "1 min 05 s")]
    [InlineData(3599, "59 min 59 s")]
    [InlineData(3600, "1 h 00 min")]
    [InlineData(7325, "2 h 02 min")]
    public void RemainingText_ThreeTiers(int seconds, string expected) =>
        Assert.Equal(expected, KeepAwakeFormat.Remaining(seconds));

    [Fact]
    public void ActiveStatus_SingleConditionsHaveTheirOwnText()
    {
        var s = KeepAwakeStrings.EnUS;
        Assert.Equal(s.ExternalDisplayActive, KeepAwakeFormat.ActiveStatus(Set(KeepAwakeCondition.ExternalDisplay), s));
        Assert.Equal(s.PowerActive, KeepAwakeFormat.ActiveStatus(Set(KeepAwakeCondition.Power), s));
        Assert.Equal(s.RunningAppsActive, KeepAwakeFormat.ActiveStatus(Set(KeepAwakeCondition.RunningApps), s));
    }

    [Fact]
    public void ActiveStatus_NoneOrSeveralUseTheGenericText()
    {
        var s = KeepAwakeStrings.EnUS;
        Assert.Equal(s.AutomationActive, KeepAwakeFormat.ActiveStatus(Set(), s));
        Assert.Equal(s.AutomationActive, KeepAwakeFormat.ActiveStatus(Set(KeepAwakeCondition.ExternalDisplay, KeepAwakeCondition.Power), s));
    }

    [Fact]
    public void KeepAwakeCatalogHasNoEmptyStrings()
    {
        foreach (var property in typeof(KeepAwakeStrings).GetProperties().Where(p => p.PropertyType == typeof(string) && !p.GetMethod!.IsStatic))
        {
            Assert.False(string.IsNullOrWhiteSpace((string?)property.GetValue(KeepAwakeStrings.EnUS)), property.Name);
        }
    }

    [Fact]
    public void MixerCatalogHasNoEmptyStrings()
    {
        foreach (var property in typeof(MixerStrings).GetProperties().Where(p => p.PropertyType == typeof(string) && !p.GetMethod!.IsStatic))
        {
            Assert.False(string.IsNullOrWhiteSpace((string?)property.GetValue(MixerStrings.EnUS)), property.Name);
        }
    }

    [Fact]
    public void Tooltip_AutomationSessionShowsItsCondition() =>
        Assert.Equal("Active while connected to power",
            KeepAwakeFormat.Tooltip(active: true, KeepAwakeTrigger.Automation, Set(KeepAwakeCondition.Power), endTimeText: "15:45", Strings.EnUS, KeepAwakeStrings.EnUS));

    [Fact]
    public void Tooltip_TimedManualSessionShowsTheEndTime() =>
        Assert.Equal("Faqra: awake until 15:45",
            KeepAwakeFormat.Tooltip(active: true, KeepAwakeTrigger.Manual, Set(), endTimeText: "15:45", Strings.EnUS, KeepAwakeStrings.EnUS));

    [Fact]
    public void Tooltip_Idle() =>
        Assert.Equal("Faqra: normal sleep",
            KeepAwakeFormat.Tooltip(active: false, null, Set(), endTimeText: null, Strings.EnUS, KeepAwakeStrings.EnUS));
}
