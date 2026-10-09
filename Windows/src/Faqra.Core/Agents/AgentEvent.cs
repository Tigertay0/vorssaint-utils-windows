// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

/// <summary>One hook event as it arrives over the pipe.</summary>
public sealed record AgentEvent(
    string Event,
    string SessionId,
    string Agent,
    string Cwd,
    string? ToolName,
    JsonObject? ToolInput,
    string? Prompt,
    string? Message,
    string? LastAssistantMessage,
    string? NotificationType,
    string? TermProgram,
    string? Entrypoint)
{
    public static AgentEvent? TryParse(string line)
    {
        JsonObject? obj;
        try
        {
            obj = JsonNode.Parse(line) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
        if (obj is null || Text(obj, "hook_event_name") is not { Length: > 0 } name)
        {
            return null;
        }
        return new AgentEvent(
            name,
            Text(obj, "session_id") ?? "unknown",
            Text(obj, "faqra_agent") ?? "claude",
            Text(obj, "cwd") ?? string.Empty,
            Text(obj, "tool_name"),
            obj["tool_input"] as JsonObject,
            Text(obj, "prompt"),
            Text(obj, "message"),
            Text(obj, "last_assistant_message"),
            Text(obj, "notification_type"),
            Text(obj, "term_program"),
            Text(obj, "claude_entrypoint"));
    }

    private static string? Text(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
