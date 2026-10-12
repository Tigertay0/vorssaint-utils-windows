using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class ReplyRulesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private static AgentSession Finished(string said, bool unread = true, int replies = 0, bool repliesOn = false) =>
        new AgentSession("s1", "claude", @"C:\code\faqra", AgentState.Finished, [], said, 0, T0, T0, T0)
        {
            Unread = unread,
            ConsecutiveReplies = replies,
            RepliesOn = repliesOn,
        };

    [Fact]
    public void NothingHoldsATurnNobodyAwaits() =>
        Assert.Null(ReplyRules.HoldFor(Finished("All 1,022 tests pass."), cardsCanShow: true, watching: false, opensOnAnswer: true, windowSeconds: 300));

    [Fact]
    public void AQuestionHoldsWhenTheIslandWillShowIt()
    {
        var asked = Finished("Done. Should I push the branch?");
        Assert.Equal(TimeSpan.FromSeconds(300), ReplyRules.HoldFor(asked, true, false, true, 300));
        // With "open on answer" off nobody would see the reply box, so Claude stops as usual.
        Assert.Null(ReplyRules.HoldFor(asked, true, false, false, 300));
        // A question from an earlier turn does not count when this turn said nothing new.
        Assert.Null(ReplyRules.HoldFor(asked with { Unread = false }, true, false, true, 300));
    }

    [Fact]
    public void WatchingOrTheSwitchHoldsAnyTurn()
    {
        var plain = Finished("Done.");
        Assert.NotNull(ReplyRules.HoldFor(plain, true, watching: true, opensOnAnswer: false, 300));
        Assert.NotNull(ReplyRules.HoldFor(plain with { RepliesOn = true }, true, false, false, 300));
    }

    [Fact]
    public void NoCardMeansNoHold() =>
        Assert.Null(ReplyRules.HoldFor(Finished("Which one?", repliesOn: true), cardsCanShow: false, watching: true, opensOnAnswer: true, 300));

    [Fact]
    public void TheCapStopsTheHold()
    {
        Assert.NotNull(ReplyRules.HoldFor(Finished("Again?", replies: AgentBoard.MaxConsecutiveReplies - 1), true, true, true, 300));
        Assert.Null(ReplyRules.HoldFor(Finished("Again?", replies: AgentBoard.MaxConsecutiveReplies), true, true, true, 300));
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(45, 45)]
    [InlineData(600, 480)]
    public void TheWindowStaysInsideTheRelaysBudget(int stored, int expected) =>
        Assert.Equal(TimeSpan.FromSeconds(expected), ReplyRules.HoldFor(Finished("Done."), true, true, true, stored));
}
