// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

public sealed record AgentOption(string Label, string Description);

public sealed record AgentQuestion(string Text, string Header, bool MultiSelect, IReadOnlyList<AgentOption> Options);

/// <summary>Claude Code's AskUserQuestion, read for a card, and the owner's picks turned into its answers.</summary>
public static class AgentQuestions
{
    /// <summary>How Claude Code joins several picked labels (hooks reference, AskUserQuestion answers).</summary>
    public const string Separator = ", ";

    /// <summary>
    /// True: the owner's own words go back as the answer itself. False: they go back as a denial Claude reads (the
    /// spec's fallback, "Free-text answers"). Settled by A2's live check; see Task 9.
    /// </summary>
    public static bool OwnTextInAnswers { get; } = true;

    /// <summary>
    /// Every question with its options. Empty when any question lacks its text or repeats another's: answers are
    /// keyed by that text, so such a question can only be answered in Claude Code.
    /// </summary>
    public static IReadOnlyList<AgentQuestion> Parse(JsonObject? toolInput)
    {
        if (toolInput?["questions"] is not JsonArray items || items.Count == 0)
        {
            return [];
        }
        var questions = new List<AgentQuestion>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item is not JsonObject question || Text(question["question"]) is not { Length: > 0 } text || !seen.Add(text))
            {
                return [];
            }
            var options = (question["options"] as JsonArray)?
                .OfType<JsonObject>()
                .Select(option => (Label: Text(option["label"]), Description: Text(option["description"]) ?? string.Empty))
                .Where(option => option.Label is { Length: > 0 })
                .Select(option => new AgentOption(option.Label!, option.Description))
                .ToList() ?? [];
            var multi = question["multiSelect"] is JsonValue flag && flag.GetValueKind() == JsonValueKind.True;
            questions.Add(new AgentQuestion(text, Text(question["header"]) ?? string.Empty, multi, options));
        }
        return questions;
    }

    /// <summary>
    /// One question's answer: the picked labels in the order Claude listed them, then the owner's own words. A
    /// single-choice question takes the owner's words over a pick. Null when nothing is chosen.
    /// </summary>
    public static string? Compose(AgentQuestion question, IReadOnlyCollection<string> picked, string? ownText)
    {
        var own = ownText?.Trim() ?? string.Empty;
        var labels = question.Options.Select(option => option.Label).Where(picked.Contains).ToList();
        if (!question.MultiSelect)
        {
            return own.Length > 0 ? own : labels.FirstOrDefault();
        }
        if (own.Length > 0)
        {
            labels.Add(own);
        }
        return labels.Count > 0 ? string.Join(Separator, labels) : null;
    }

    /// <summary>Each question's answer keyed by its text, or null until every question has one.</summary>
    public static IReadOnlyDictionary<string, string>? Answers(IReadOnlyList<AgentQuestion> questions, IReadOnlyList<string?> composed)
    {
        if (questions.Count == 0 || composed.Count != questions.Count || composed.Any(string.IsNullOrWhiteSpace))
        {
            return null;
        }
        return questions.Zip(composed).ToDictionary(pair => pair.First.Text, pair => pair.Second!, StringComparer.Ordinal);
    }

    /// <summary>What Claude reads when the owner's words cannot go back as answers: each question and its reply.</summary>
    public static string AsDenialMessage(IReadOnlyDictionary<string, string> answers) =>
        "The user answered in Faqra instead of picking an option:\n"
        + string.Join("\n", answers.Select(pair => $"Q: {pair.Key}\nA: {pair.Value}"));

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
