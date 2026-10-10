// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

public enum AgentRequestKind { Approval, Question }

/// <summary>A permission request a card shows: what Claude wants, and what an Always click would save.</summary>
public sealed record AgentRequest(
    string Id,
    string SessionId,
    AgentRequestKind Kind,
    string ToolName,
    string Detail,
    IReadOnlyList<AgentQuestion> Questions,
    IReadOnlyList<string> AlwaysRules,
    bool AlwaysAcceptsEdits,
    DateTimeOffset ReceivedAt)
{
    public bool CanAlways => Kind == AgentRequestKind.Approval && (AlwaysRules.Count > 0 || AlwaysAcceptsEdits);

    /// <summary>The card for a PermissionRequest event. Reads the tool input, so a payload with repeated keys throws ArgumentException.</summary>
    public static AgentRequest From(string id, AgentEvent e, DateTimeOffset now)
    {
        var tool = e.ToolName ?? string.Empty;
        if (tool == "AskUserQuestion")
        {
            return new AgentRequest(id, e.SessionId, AgentRequestKind.Question, tool, string.Empty,
                AgentQuestions.Parse(e.ToolInput), [], false, now);
        }
        return new AgentRequest(id, e.SessionId, AgentRequestKind.Approval, tool, DetailOf(tool, e.ToolInput), [],
            PermissionReply.AlwaysRuleLabels(e.PermissionSuggestions), PermissionReply.AlwaysAcceptsEdits(e.PermissionSuggestions), now);
    }

    private static string DetailOf(string tool, JsonObject? input) => tool switch
    {
        "Bash" or "PowerShell" => Field(input, "command"),
        "Read" or "Edit" or "MultiEdit" or "Write" => Field(input, "file_path"),
        "NotebookEdit" => Field(input, "notebook_path"),
        "WebFetch" => Field(input, "url"),
        "WebSearch" => Field(input, "query"),
        "Grep" or "Glob" => Field(input, "pattern"),
        _ => Field(input, "description"),
    };

    private static string Field(JsonObject? input, string key) =>
        input?[key] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : string.Empty;
}
