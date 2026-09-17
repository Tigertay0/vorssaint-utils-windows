using Faqra.Core.Defaults;
using Faqra.Core.KeepAwake;

namespace Faqra.Core.Tests;

// The state machine of Sources/Vorssaint/Services/KeepAwakeManager.swift, minus the OS calls.
public class KeepAwakeSessionTests
{
    private sealed class FakeEnvironment : IKeepAwakeEnvironment
    {
        public bool ExternalDisplayConnected { get; set; }
        public PowerState Power { get; set; } = new(HasBattery: false, OnBattery: false, Percent: 100);
        public bool SelectedAppsRunning { get; set; }
    }

    private readonly DefaultsStore _store = DefaultsStore.InMemory();
    private readonly FakeEnvironment _env = new();
    private DateTime _now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
    private bool _available = true;
    private readonly List<KeepAwakeEndReason> _ended = [];

    private KeepAwakeSession Create()
    {
        var session = new KeepAwakeSession(_store, () => _available, _env, () => _now);
        session.SessionEnded += _ended.Add;
        session.MarkRecoveryCompleted();
        return session;
    }

    [Fact]
    public void Activate15_SetsEndAndHoldsTheAssertion()
    {
        var s = Create();
        s.Activate(15);
        Assert.True(s.IsActive);
        Assert.Equal(_now.AddMinutes(15), s.EndsAt);
        Assert.True(s.HoldsAssertion);
        Assert.True(s.KeepsDisplayOn);
        Assert.Equal(KeepAwakeTrigger.Manual, s.Trigger);
    }

    [Fact]
    public void AllowDisplaySleep_HoldsOnlyTheSystemAssertion()
    {
        _store.Set(DefaultsKey.KeepAwakeAllowDisplaySleep, true);
        var s = Create();
        s.Activate(0);
        Assert.True(s.HoldsAssertion);
        Assert.False(s.KeepsDisplayOn);
    }

    [Fact]
    public void ActivateZero_IsIndefinite()
    {
        var s = Create();
        s.Activate(0);
        Assert.True(s.IsActive);
        Assert.Null(s.EndsAt);
    }

    [Fact]
    public void ActivateWithUnknownDuration_FallsBackToIndefinite()
    {
        var s = Create();
        s.Activate(17);
        Assert.Null(s.EndsAt);
    }

    [Fact]
    public void ActivateWhileFeatureUninstalled_DoesNothing()
    {
        _available = false;
        var s = Create();
        s.Activate(15);
        Assert.False(s.IsActive);
    }

    [Fact]
    public void Toggle_StartsWithTheDefaultDurationThenStops()
    {
        _store.Set(DefaultsKey.DefaultDuration, 30);
        var s = Create();
        s.Toggle();
        Assert.Equal(_now.AddMinutes(30), s.EndsAt);
        s.Toggle();
        Assert.False(s.IsActive);
        Assert.Empty(_ended);
    }

    [Fact]
    public void Tick_BeforeTheEnd_KeepsGoing()
    {
        var s = Create();
        s.Activate(15);
        _now = _now.AddMinutes(14).AddSeconds(59);
        s.Tick();
        Assert.True(s.IsActive);
    }

    [Fact]
    public void Tick_AtTheEnd_EndsWithTimerReason()
    {
        var s = Create();
        s.Activate(15);
        _now = _now.AddMinutes(15);
        s.Tick();
        Assert.False(s.IsActive);
        Assert.False(s.HoldsAssertion);
        Assert.Equal([KeepAwakeEndReason.Timer], _ended);
    }

    [Fact]
    public void Extend_AddsToTheEnd()
    {
        var s = Create();
        s.Activate(15);
        _now = _now.AddMinutes(5);
        s.Extend(30);
        Assert.Equal(_now.AddMinutes(40), s.EndsAt);
    }

    [Fact]
    public void Extend_AnOverdueEndCountsFromNow()
    {
        var s = Create();
        s.Activate(15);
        var start = _now;
        _now = start.AddMinutes(20);
        s.Extend(15);
        Assert.Equal(_now.AddMinutes(15), s.EndsAt);
    }

    [Fact]
    public void Extend_IndefiniteSessionIsUnchanged()
    {
        var s = Create();
        s.Activate(0);
        s.Extend(15);
        Assert.Null(s.EndsAt);
    }

    [Fact]
    public void Battery_AtTheLimitOnBatteryEndsTheSession()
    {
        _store.Set(DefaultsKey.BatteryLimit, 10);
        _env.Power = new PowerState(true, true, 40);
        var s = Create();
        s.Activate(0);
        s.CheckBattery();
        Assert.True(s.IsActive);
        _env.Power = new PowerState(true, true, 10);
        s.CheckBattery();
        Assert.False(s.IsActive);
        Assert.Equal([KeepAwakeEndReason.Battery], _ended);
    }

    [Fact]
    public void Battery_PluggedInNeverEnds()
    {
        _env.Power = new PowerState(true, false, 3);
        var s = Create();
        s.Activate(0);
        s.CheckBattery();
        Assert.True(s.IsActive);
    }

    [Fact]
    public void Lock_WithPauseOn_ReleasesButKeepsTheSession()
    {
        _store.Set(DefaultsKey.KeepAwakePauseWhenLocked, true);
        var s = Create();
        s.Activate(60);
        s.ScreenLockChanged(true);
        Assert.True(s.IsActive);
        Assert.True(s.PausedForLock);
        Assert.False(s.HoldsAssertion);
        s.ScreenLockChanged(false);
        Assert.True(s.HoldsAssertion);
    }

    [Fact]
    public void Lock_WithPauseOff_KeepsHolding()
    {
        var s = Create();
        s.Activate(60);
        s.ScreenLockChanged(true);
        Assert.True(s.HoldsAssertion);
    }

    [Fact]
    public void Unlock_AfterTheEndPassed_EndsWithTimer()
    {
        _store.Set(DefaultsKey.KeepAwakePauseWhenLocked, true);
        var s = Create();
        s.Activate(15);
        s.ScreenLockChanged(true);
        // The PC slept through the end, so no tick arrived; unlocking settles it.
        _now = _now.AddMinutes(30);
        s.ScreenLockChanged(false);
        Assert.False(s.IsActive);
        Assert.Equal([KeepAwakeEndReason.Timer], _ended);
    }

    [Fact]
    public void ActivateWhileLocked_StartsPaused()
    {
        _store.Set(DefaultsKey.KeepAwakePauseWhenLocked, true);
        var s = Create();
        s.ScreenLockChanged(true);
        s.Activate(15);
        Assert.True(s.IsActive);
        Assert.False(s.HoldsAssertion);
    }

    [Fact]
    public void Automation_PowerStartsAnIndefiniteSessionAndEndsWithIt()
    {
        _store.Set(DefaultsKey.KeepAwakeConnectedToPower, true);
        _env.Power = new PowerState(true, false, 80);
        var s = Create();
        s.EvaluateAutomation();
        Assert.True(s.IsActive);
        Assert.Null(s.EndsAt);
        Assert.Equal(KeepAwakeTrigger.Automation, s.Trigger);
        Assert.Contains(KeepAwakeCondition.Power, s.ActiveConditions);

        _env.Power = new PowerState(true, true, 80);
        s.EvaluateAutomation();
        Assert.False(s.IsActive);
        Assert.Empty(_ended);
    }

    [Fact]
    public void Automation_WaitsForLaunchRecovery()
    {
        _store.Set(DefaultsKey.KeepAwakeConnectedToPower, true);
        _env.Power = new PowerState(true, false, 80);
        var s = new KeepAwakeSession(_store, () => true, _env, () => _now);
        s.EvaluateAutomation();
        Assert.False(s.IsActive);
    }

    [Fact]
    public void Automation_NeverTakesOverAManualSession()
    {
        var s = Create();
        s.Activate(15);
        _store.Set(DefaultsKey.KeepAwakeConnectedToPower, true);
        _env.Power = new PowerState(true, false, 80);
        s.EvaluateAutomation();
        Assert.Equal(KeepAwakeTrigger.Manual, s.Trigger);
        Assert.NotNull(s.EndsAt);
    }

    [Fact]
    public void Automation_BlockedByBatteryProtection()
    {
        _store.Set(DefaultsKey.KeepAwakeExternalDisplay, true);
        _store.Set(DefaultsKey.BatteryLimit, 20);
        _env.ExternalDisplayConnected = true;
        _env.Power = new PowerState(true, true, 15);
        var s = Create();
        s.EvaluateAutomation();
        Assert.False(s.IsActive);
    }

    [Fact]
    public void ManualOffDuringAutomation_StaysOffUntilConditionsClear()
    {
        _store.Set(DefaultsKey.KeepAwakeExternalDisplay, true);
        _env.ExternalDisplayConnected = true;
        var s = Create();
        s.EvaluateAutomation();
        Assert.True(s.IsActive);

        s.Toggle();
        Assert.False(s.IsActive);
        s.EvaluateAutomation();
        Assert.False(s.IsActive);

        _env.ExternalDisplayConnected = false;
        s.EvaluateAutomation();
        _env.ExternalDisplayConnected = true;
        s.EvaluateAutomation();
        Assert.True(s.IsActive);
    }

    [Fact]
    public void ChangingAnAutomationPreference_ClearsTheSuppression()
    {
        _store.Set(DefaultsKey.KeepAwakeExternalDisplay, true);
        _env.ExternalDisplayConnected = true;
        var s = Create();
        s.EvaluateAutomation();
        s.Toggle();
        s.AutomationPreferencesDidChange();
        Assert.True(s.IsActive);
    }

    [Fact]
    public void TimerEnd_ContinuesAsAutomationWhenAConditionHolds()
    {
        var s = Create();
        s.Activate(15);
        _store.Set(DefaultsKey.KeepAwakeConnectedToPower, true);
        _env.Power = new PowerState(true, false, 80);
        _now = _now.AddMinutes(15);
        s.Tick();
        Assert.True(s.IsActive);
        Assert.Equal(KeepAwakeTrigger.Automation, s.Trigger);
        Assert.Null(s.EndsAt);
        Assert.Empty(_ended);
    }

    [Fact]
    public void AutoStart_ActivatesWithTheDefaultDuration()
    {
        _store.Set(DefaultsKey.KeepAwakeAutoStart, true);
        _store.Set(DefaultsKey.DefaultDuration, 60);
        var s = Create();
        s.ActivateOnLaunchIfNeeded();
        Assert.Equal(_now.AddMinutes(60), s.EndsAt);
    }

    [Fact]
    public void AutoStart_OffDoesNothing()
    {
        var s = Create();
        s.ActivateOnLaunchIfNeeded();
        Assert.False(s.IsActive);
    }

    [Fact]
    public void Quit_EndsSilently()
    {
        var s = Create();
        s.Activate(15);
        s.Deactivate(KeepAwakeEndReason.Quit);
        Assert.False(s.IsActive);
        Assert.Empty(_ended);
    }

    [Fact]
    public void Changed_FiresOnEveryTransition()
    {
        var s = Create();
        var count = 0;
        s.Changed += () => count++;
        s.Activate(15);
        s.Extend(15);
        s.Deactivate(KeepAwakeEndReason.Manual);
        Assert.Equal(3, count);
    }
}
