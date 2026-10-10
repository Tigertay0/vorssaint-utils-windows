using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class AgentDecisionTests
{
    [Fact]
    public void EveryKindSurvivesTheWire()
    {
        Assert.Equal(AgentDecisionKind.Allow, AgentDecision.TryParse(AgentDecision.Allow.ToLine())!.Kind);
        Assert.Equal(AgentDecisionKind.Always, AgentDecision.TryParse(AgentDecision.Always.ToLine())!.Kind);

        var deny = AgentDecision.TryParse(AgentDecision.Deny("Not now").ToLine())!;
        Assert.Equal(AgentDecisionKind.Deny, deny.Kind);
        Assert.Equal("Not now", deny.Message);
        Assert.Null(AgentDecision.TryParse(AgentDecision.Deny().ToLine())!.Message);

        var answer = AgentDecision.TryParse(AgentDecision.Answer(new Dictionary<string, string> { ["Café?"] = "Oui" }).ToLine())!;
        Assert.Equal(AgentDecisionKind.Answer, answer.Kind);
        Assert.Equal("Oui", answer.Answers!["Café?"]);
    }

    [Fact]
    public void TheWireFormIsOneLine() =>
        Assert.DoesNotContain('\n', AgentDecision.Answer(new Dictionary<string, string> { ["a\nb"] = "c\nd" }).ToLine());

    [Fact]
    public void TheWireFormIsTheDocumentedOne()
    {
        Assert.Equal("{\"decision\":\"allow\"}", AgentDecision.Allow.ToLine());
        Assert.Equal("{\"decision\":\"always\"}", AgentDecision.Always.ToLine());
        Assert.Equal("{\"decision\":\"deny\",\"message\":\"No\"}", AgentDecision.Deny("No").ToLine());
        Assert.Equal("{\"decision\":\"answer\",\"answers\":{\"Q?\":\"A\"}}", AgentDecision.Answer(new Dictionary<string, string> { ["Q?"] = "A" }).ToLine());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("allow")]
    [InlineData("[1]")]
    [InlineData("{\"decision\":\"maybe\"}")]
    [InlineData("{\"permissionDecision\":\"allow\"}")]
    [InlineData("{\"decision\":\"answer\"}")]
    [InlineData("{\"decision\":\"answer\",\"answers\":{\"q\":1}}")]
    [InlineData("{\"decision\":\"allow\",\"decision\":\"deny\"}")]
    public void AnythingElseIsNoDecision(string? line) => Assert.Null(AgentDecision.TryParse(line));
}
