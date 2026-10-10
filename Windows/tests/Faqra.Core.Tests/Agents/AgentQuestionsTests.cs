using System.Text.Json.Nodes;
using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class AgentQuestionsTests
{
    private static JsonObject Input(string json) => (JsonObject)JsonNode.Parse(json)!;

    private static JsonObject Two() => Input("{\"questions\":[" +
        "{\"question\":\"Which framework?\",\"header\":\"Framework\",\"options\":[{\"label\":\"React\",\"description\":\"Component library\"},{\"label\":\"Vue\",\"description\":\"Progressive framework\"}],\"multiSelect\":false}," +
        "{\"question\":\"Which extras?\",\"header\":\"Extras\",\"options\":[{\"label\":\"Tests\"},{\"label\":\"Docs\"},{\"label\":\"Lint\"}],\"multiSelect\":true}]}");

    [Fact]
    public void ParsesEveryQuestionAndOption()
    {
        var questions = AgentQuestions.Parse(Two());
        Assert.Equal(2, questions.Count);
        Assert.Equal("Which framework?", questions[0].Text);
        Assert.Equal("Framework", questions[0].Header);
        Assert.False(questions[0].MultiSelect);
        Assert.Equal(new AgentOption("Vue", "Progressive framework"), questions[0].Options[1]);
        Assert.True(questions[1].MultiSelect);
        Assert.Equal(["Tests", "Docs", "Lint"], questions[1].Options.Select(o => o.Label));
        Assert.Equal(string.Empty, questions[1].Options[0].Description);
    }

    [Theory]
    [InlineData("{\"questions\":[{\"question\":\"Same?\"},{\"question\":\"Same?\"}]}")]
    [InlineData("{\"questions\":[{\"header\":\"No text\"}]}")]
    [InlineData("{\"questions\":[]}")]
    [InlineData("{\"questions\":\"nope\"}")]
    [InlineData("{}")]
    public void AQuestionNobodyCanAnswerMeansNoneAre(string json) => Assert.Empty(AgentQuestions.Parse(Input(json)));

    [Fact]
    public void NoInputMeansNoQuestions() => Assert.Empty(AgentQuestions.Parse(null));

    [Fact]
    public void OptionsWithoutALabelAreSkipped()
    {
        var questions = AgentQuestions.Parse(Input("{\"questions\":[{\"question\":\"Q?\",\"options\":[{\"description\":\"no label\"},{\"label\":\"Yes\"}]}]}"));
        Assert.Equal(["Yes"], questions[0].Options.Select(o => o.Label));
    }

    [Fact]
    public void ASingleChoiceTakesThePickOrTheOwnersWords()
    {
        var question = AgentQuestions.Parse(Two())[0];
        Assert.Equal("Vue", AgentQuestions.Compose(question, ["Vue"], null));
        Assert.Equal("Svelte", AgentQuestions.Compose(question, ["Vue"], "  Svelte "));
        Assert.Null(AgentQuestions.Compose(question, [], "   "));
        Assert.Null(AgentQuestions.Compose(question, ["Angular"], null));
    }

    [Fact]
    public void AMultiChoiceKeepsClaudesOrderThenTheOwnersWords()
    {
        var question = AgentQuestions.Parse(Two())[1];
        Assert.Equal("Tests, Lint", AgentQuestions.Compose(question, ["Lint", "Tests"], null));
        Assert.Equal("Tests, Lint, a changelog", AgentQuestions.Compose(question, ["Lint", "Tests", "Bogus"], "a changelog"));
        Assert.Equal("a changelog", AgentQuestions.Compose(question, [], "a changelog"));
        Assert.Null(AgentQuestions.Compose(question, [], null));
    }

    [Fact]
    public void AnswersOnlyOnceEveryQuestionHasOne()
    {
        var questions = AgentQuestions.Parse(Two());
        Assert.Null(AgentQuestions.Answers(questions, ["Vue", null]));
        Assert.Null(AgentQuestions.Answers(questions, ["Vue"]));
        Assert.Null(AgentQuestions.Answers([], []));
        var answers = AgentQuestions.Answers(questions, ["Vue", "Tests, Docs"])!;
        Assert.Equal("Vue", answers["Which framework?"]);
        Assert.Equal("Tests, Docs", answers["Which extras?"]);
    }

    [Fact]
    public void TheDenialFallbackNamesEachQuestionAndAnswer()
    {
        var message = AgentQuestions.AsDenialMessage(new Dictionary<string, string> { ["Which framework?"] = "Svelte" });
        Assert.Contains("Which framework?", message);
        Assert.Contains("Svelte", message);
    }
}
