// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

public enum AgentDecisionKind { Allow, Always, Deny, Answer }

/// <summary>
/// What the owner chose on a card, as it travels from Faqra back to the waiting relay. Only the relay turns it into
/// Claude Code's reply (<see cref="PermissionReply"/>), so Claude's wire format lives in one place.
/// </summary>
public sealed class AgentDecision
{
    private AgentDecision(AgentDecisionKind kind, string? message, IReadOnlyDictionary<string, string>? answers)
    {
        Kind = kind;
        Message = message;
        Answers = answers;
    }

    public AgentDecisionKind Kind { get; }

    /// <summary>For Deny: what Claude is told. Null means <see cref="PermissionReply.DefaultDenyMessage"/>.</summary>
    public string? Message { get; }

    /// <summary>For Answer: each question's text and the answer to it.</summary>
    public IReadOnlyDictionary<string, string>? Answers { get; }

    public static AgentDecision Allow { get; } = new(AgentDecisionKind.Allow, null, null);

    public static AgentDecision Always { get; } = new(AgentDecisionKind.Always, null, null);

    public static AgentDecision Deny(string? message = null) => new(AgentDecisionKind.Deny, message, null);

    public static AgentDecision Answer(IReadOnlyDictionary<string, string> answers) => new(AgentDecisionKind.Answer, null, answers);

    public string ToLine()
    {
        var line = new JsonObject { ["decision"] = Word(Kind) };
        if (Message is not null)
        {
            line["message"] = Message;
        }
        if (Answers is not null)
        {
            var answers = new JsonObject();
            foreach (var (question, answer) in Answers)
            {
                answers[question] = answer;
            }
            line["answers"] = answers;
        }
        return line.ToJsonString(AgentJson.Compact);
    }

    /// <summary>The decision on a line, or null for anything that is not exactly one.</summary>
    public static AgentDecision? TryParse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }
        try
        {
            if (JsonNode.Parse(line) is not JsonObject obj || Text(obj["decision"]) is not { } word)
            {
                return null;
            }
            return word switch
            {
                "allow" => Allow,
                "always" => Always,
                "deny" => Deny(Text(obj["message"])),
                "answer" when obj["answers"] is JsonObject answers && ReadAnswers(answers) is { } read => Answer(read),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            // ArgumentException: .NET 8 reports repeated property names this way, when the object is first read.
            return null;
        }
    }

    private static Dictionary<string, string>? ReadAnswers(JsonObject answers)
    {
        var read = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (question, value) in answers)
        {
            if (Text(value) is not { } answer)
            {
                return null;
            }
            read[question] = answer;
        }
        return read;
    }

    private static string Word(AgentDecisionKind kind) => kind switch
    {
        AgentDecisionKind.Allow => "allow",
        AgentDecisionKind.Always => "always",
        AgentDecisionKind.Deny => "deny",
        _ => "answer",
    };

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
