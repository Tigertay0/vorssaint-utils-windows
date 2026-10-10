// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Ported from Coucou's relay replies (windows/hook/src/reply.rs), https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé).

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

/// <summary>
/// What the relay prints back to Claude Code for a PermissionRequest, from Claude's documented hook output
/// (https://code.claude.com/docs/en/hooks). The rule that matters: nothing that allows anything is printed without a
/// decision a person made. With no decision the relay prints nothing and Claude Code asks in its own UI.
/// </summary>
public static class PermissionReply
{
    /// <summary>What Claude reads when the owner denies without saying why.</summary>
    public const string DefaultDenyMessage = "Denied from Faqra";

    /// <summary>Longest answer a question takes back, the relay's usual string cap.</summary>
    public const int MaxAnswerLength = 2000;

    private const string AskUserQuestion = "AskUserQuestion";

    /// <summary>The line to print for <paramref name="request"/>, or null to print nothing.</summary>
    public static string? Stdout(JsonObject request, AgentDecision? decision)
    {
        if (decision is null)
        {
            return null;
        }
        var isQuestion = Text(request["tool_name"]) == AskUserQuestion;
        var body = decision.Kind switch
        {
            AgentDecisionKind.Deny => Deny(decision.Message),
            AgentDecisionKind.Allow when !isQuestion => new JsonObject { ["behavior"] = "allow" },
            AgentDecisionKind.Always when !isQuestion => AllowAlways(request),
            AgentDecisionKind.Answer when isQuestion => Answered(request, decision.Answers!),
            _ => null,
        };
        if (body is null)
        {
            return null;
        }
        return new JsonObject
        {
            ["hookSpecificOutput"] = new JsonObject { ["hookEventName"] = "PermissionRequest", ["decision"] = body },
        }.ToJsonString(AgentJson.Compact);
    }

    /// <summary>True when the answers answer exactly the questions asked: one non-empty answer per question text, no extras.</summary>
    public static bool AnswersFit(JsonObject toolInput, IReadOnlyDictionary<string, string> answers)
    {
        if (toolInput["questions"] is not JsonArray questions || questions.Count == 0 || questions.Count != answers.Count)
        {
            return false;
        }
        var asked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in questions)
        {
            if (item is not JsonObject question || Text(question["question"]) is not { Length: > 0 } text || !asked.Add(text))
            {
                return false;
            }
            if (!answers.TryGetValue(text, out var answer) || string.IsNullOrWhiteSpace(answer) || answer.Length > MaxAnswerLength)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// The suggestions an Always click may apply: allow rules, always saved to this project's local settings, and
    /// accept-edits for this session only. Everything else Claude Code suggests (other modes, directories, deny or ask
    /// rules, the owner's user settings) is dropped.
    /// </summary>
    public static JsonArray AlwaysRules(JsonArray? suggestions)
    {
        var kept = new JsonArray();
        foreach (var entry in suggestions?.OfType<JsonObject>() ?? [])
        {
            var type = Text(entry["type"]);
            if (type == "addRules" && Text(entry["behavior"]) == "allow" && entry["rules"] is JsonArray rules && rules.Count > 0
                && rules.All(rule => rule is JsonObject r && Text(r["toolName"]) is { Length: > 0 }))
            {
                kept.Add(new JsonObject
                {
                    ["type"] = "addRules",
                    ["rules"] = rules.DeepClone(),
                    ["behavior"] = "allow",
                    ["destination"] = "localSettings",
                });
            }
            else if (type == "setMode" && Text(entry["mode"]) == "acceptEdits")
            {
                kept.Add(new JsonObject { ["type"] = "setMode", ["mode"] = "acceptEdits", ["destination"] = "session" });
            }
        }
        return kept;
    }

    /// <summary>The rules an Always click saves, as Claude Code writes them: "Bash(npm test:*)", "WebFetch".</summary>
    public static IReadOnlyList<string> AlwaysRuleLabels(JsonArray? suggestions) => AlwaysRules(suggestions)
        .OfType<JsonObject>()
        .Where(entry => Text(entry["type"]) == "addRules")
        .SelectMany(entry => ((JsonArray)entry["rules"]!).OfType<JsonObject>())
        .Select(rule => Text(rule["ruleContent"]) is { Length: > 0 } content ? $"{Text(rule["toolName"])}({content})" : Text(rule["toolName"])!)
        .ToList();

    /// <summary>True when an Always click also accepts edits for the rest of the session.</summary>
    public static bool AlwaysAcceptsEdits(JsonArray? suggestions) =>
        AlwaysRules(suggestions).OfType<JsonObject>().Any(entry => Text(entry["type"]) == "setMode");

    private static JsonObject Deny(string? message) => new()
    {
        ["behavior"] = "deny",
        ["message"] = string.IsNullOrWhiteSpace(message) ? DefaultDenyMessage : message,
    };

    private static JsonObject AllowAlways(JsonObject request)
    {
        var body = new JsonObject { ["behavior"] = "allow" };
        var rules = AlwaysRules(request["permission_suggestions"] as JsonArray);
        if (rules.Count > 0)
        {
            body["updatedPermissions"] = rules;
        }
        return body;
    }

    /// <summary>Claude Code takes an answered question as its own input with an answers map added; nothing else changes.</summary>
    private static JsonObject? Answered(JsonObject request, IReadOnlyDictionary<string, string> answers)
    {
        if (request["tool_input"] is not JsonObject input || !AnswersFit(input, answers))
        {
            return null;
        }
        var updated = (JsonObject)input.DeepClone();
        var map = new JsonObject();
        foreach (var (question, answer) in answers)
        {
            map[question] = answer;
        }
        updated["answers"] = map;
        return new JsonObject { ["behavior"] = "allow", ["updatedInput"] = updated };
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
