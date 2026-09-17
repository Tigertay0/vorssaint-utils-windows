using Faqra.Core.Defaults;
using Faqra.Core.KeepAwake;
using Faqra.Services.KeepAwake;
using Faqra.Win32.Power;

namespace Faqra.Services.Tests;

public class KeepAwakeManagerTests
{
    private sealed class InlineContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => d(state);
    }

    private sealed class FakeEnvironment : IKeepAwakeEnvironment
    {
        public bool ExternalDisplayConnected => false;
        public PowerState Power { get; set; } = new(false, false, 100);
        public bool SelectedAppsRunning => false;
    }

    // Live: the execution state is real, so every test clears it on the way out.
    [Fact]
    public void Activate_RequestsSystemAndDisplay_ThenClears()
    {
        var store = DefaultsStore.InMemory(RegisteredDefaults.All);
        using var manager = new KeepAwakeManager(store, () => true, new InlineContext(), new FakeEnvironment());
        manager.Session.MarkRecoveryCompleted();

        manager.Session.Activate(15);
        Assert.Equal((true, true), manager.AppliedState);

        store.Set(DefaultsKey.KeepAwakeAllowDisplaySleep, true);
        Assert.Equal((true, false), manager.AppliedState);

        manager.Session.Deactivate(KeepAwakeEndReason.Manual);
        Assert.Equal((false, false), manager.AppliedState);
    }

    // Live regression: the end timer fired a hair before the wall-clock end, the tick saw the session
    // still running, nothing re-armed the timer, and the PC stayed awake past the end.
    [Fact]
    public void EndTimerFiringEarly_RearmsInsteadOfGivingUp()
    {
        var store = DefaultsStore.InMemory(RegisteredDefaults.All);
        using var manager = new KeepAwakeManager(store, () => true, new InlineContext(), new FakeEnvironment());
        manager.Session.Activate(15);
        manager.DisarmEndTimerForTest();

        manager.OnEndTimer();

        Assert.True(manager.Session.IsActive);
        Assert.True(manager.EndTimerArmed);
        manager.Session.Deactivate(KeepAwakeEndReason.Manual);
        Assert.False(manager.EndTimerArmed);
    }

    [Fact]
    public void SyncWithFeatures_UninstallEndsTheSession()
    {
        var store = DefaultsStore.InMemory(RegisteredDefaults.All);
        var available = true;
        using var manager = new KeepAwakeManager(store, () => available, new InlineContext(), new FakeEnvironment());
        manager.Session.Activate(0);
        available = false;
        manager.SyncWithFeatures();
        Assert.False(manager.Session.IsActive);
        Assert.Equal((false, false), manager.AppliedState);
    }

    [Fact]
    public void ChangingAnAutomationSwitch_Evaluates()
    {
        var store = DefaultsStore.InMemory(RegisteredDefaults.All);
        var env = new FakeEnvironment { Power = new PowerState(true, false, 90) };
        using var manager = new KeepAwakeManager(store, () => true, new InlineContext(), env);
        manager.Session.MarkRecoveryCompleted();

        store.Set(DefaultsKey.KeepAwakeConnectedToPower, true);
        Assert.True(manager.Session.IsActive);
        Assert.Equal(KeepAwakeTrigger.Automation, manager.Session.Trigger);
    }

    [Fact]
    public void Dispose_ReleasesTheExecutionState()
    {
        var store = DefaultsStore.InMemory(RegisteredDefaults.All);
        var manager = new KeepAwakeManager(store, () => true, new InlineContext(), new FakeEnvironment());
        manager.Session.Activate(0);
        manager.Dispose();
        Assert.Equal((false, false), manager.AppliedState);
        Assert.True(ExecutionState.Clear());
    }
}
