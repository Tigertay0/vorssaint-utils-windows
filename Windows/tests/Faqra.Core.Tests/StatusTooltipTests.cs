using Faqra.Core.Localization;
using Faqra.Core.Tray;

namespace Faqra.Core.Tests;

public class StatusTooltipTests
{
    [Fact]
    public void Idle_UsesIdleText() =>
        Assert.Equal("Faqra: normal sleep", StatusTooltip.For(false, "14:32", Strings.EnUS));

    [Fact]
    public void ActiveWithEnd_AppendsTime() =>
        Assert.Equal("Faqra: awake until 14:32", StatusTooltip.For(true, "14:32", Strings.EnUS));

    [Fact]
    public void ActiveWithoutEnd_IsIndefinite() =>
        Assert.Equal("Faqra: awake indefinitely", StatusTooltip.For(true, null, Strings.EnUS));
}
