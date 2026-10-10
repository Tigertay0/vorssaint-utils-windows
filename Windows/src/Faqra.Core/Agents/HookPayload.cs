// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Ported from Coucou's relay (windows/hook/src/main.rs and normalize.rs), https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé).

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents;

/// <summary>
/// What the relay does to a hook payload before it crosses the pipe: names the event, tags the agent
/// and the terminal, and trims strings so a line stays small.
/// </summary>
public static class HookPayload
{
    public const int MaxString = 2000;
    public const int MaxEditString = 256 * 1024;
    public const int MaxEditTotal = 512 * 1024;
    public const string Ellipsis = "…";

    private static readonly HashSet<string> EditTools = new(StringComparer.Ordinal) { "Edit", "MultiEdit", "Write" };
    private static readonly HashSet<string> EditFields = new(StringComparer.Ordinal) { "old_string", "new_string", "content" };

    /// <summary>Environment variables copied into the payload, by the field they become.</summary>
    private static readonly (string Field, string Variable)[] EnvironmentFields =
    [
        ("term_program", "TERM_PROGRAM"),
        ("wt_session", "WT_SESSION"),
        ("claude_entrypoint", "CLAUDE_CODE_ENTRYPOINT"),
    ];

    /// <summary>The line sent to Faqra for this stdin, or null when stdin is not a JSON object.</summary>
    public static string? ToLine(string stdin, string eventArg, string agent, Func<string, string?> env, string processCwd)
    {
        try
        {
            if (JsonNode.Parse(stdin.TrimStart('﻿')) is not JsonObject payload)
            {
                return null;
            }
            Normalize(payload, eventArg, agent, env, processCwd);
            return payload.ToJsonString(AgentJson.Compact);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            // ArgumentException: .NET 8 reports duplicate property names this way, and only when
            // the object is first read, so the whole walk stays inside the try.
            return null;
        }
    }

    private static void Normalize(JsonObject payload, string eventArg, string agent, Func<string, string?> env, string processCwd)
    {
        if (StringOf(payload["hook_event_name"]) is null && eventArg.Length > 0)
        {
            payload["hook_event_name"] = eventArg;
        }
        payload["faqra_agent"] = agent;
        if (StringOf(payload["cwd"]) is null)
        {
            payload["cwd"] = processCwd;
        }
        foreach (var (field, variable) in EnvironmentFields)
        {
            if (env(variable) is { Length: > 0 } value)
            {
                payload[field] = value;
            }
        }

        var tool = StringOf(payload["tool_name"]);
        var isEdit = StringOf(payload["hook_event_name"]) == "PostToolUse" && tool is not null && EditTools.Contains(tool);
        var isPermission = StringOf(payload["hook_event_name"]) == "PermissionRequest";
        var editBudget = MaxEditTotal;
        var truncated = false;
        var inputBudget = MaxEditTotal;
        var inputTruncated = false;
        foreach (var key in payload.Select(pair => pair.Key).ToList())
        {
            var child = payload[key];
            if (key == "tool_input" && tool == "AskUserQuestion")
            {
                continue; // the answers echo the questions back whole
            }
            if (isPermission && key == "permission_suggestions")
            {
                continue; // small, and the Always label must match the rule the relay writes
            }
            if (isPermission && key == "tool_input" && child is not null)
            {
                payload[key] = TrimWhole(child, ref inputBudget, ref inputTruncated);
                continue;
            }
            if (key == "tool_input" && isEdit && child is JsonObject input)
            {
                TrimEdit(input, ref editBudget, ref truncated);
                continue;
            }
            TrimChild(payload, key, child, MaxString);
        }
        if (truncated)
        {
            payload["faqra_diff_truncated"] = true;
        }
        if (inputTruncated)
        {
            payload["faqra_input_truncated"] = true;
        }
    }

    /// <summary>
    /// A permission request's input is what the owner approves, so every string keeps up to 256 KB and the whole
    /// input 512 KB; anything cut is reported so the card can refuse to offer Allow.
    /// </summary>
    private static JsonNode TrimWhole(JsonNode node, ref int budget, ref bool truncated)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(pair => pair.Key).ToList())
                {
                    if (obj[key] is { } inner)
                    {
                        obj[key] = TrimWhole(inner, ref budget, ref truncated);
                    }
                }
                return obj;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    if (array[i] is { } item)
                    {
                        array[i] = TrimWhole(item, ref budget, ref truncated);
                    }
                }
                return array;
            case JsonValue value when StringOf(value) is { } text:
                var limit = Math.Max(0, Math.Min(MaxEditString, budget));
                if (text.Length <= limit)
                {
                    budget -= text.Length;
                    return value;
                }
                truncated = true;
                budget -= limit;
                return JsonValue.Create(Cut(text, limit))!;
            default:
                return node;
        }
    }

    private static string? StringOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    private static void TrimChild(JsonObject parent, string key, JsonNode? child, int limit)
    {
        switch (child)
        {
            case JsonObject obj:
                foreach (var inner in obj.Select(pair => pair.Key).ToList())
                {
                    TrimChild(obj, inner, obj[inner], limit);
                }
                break;
            case JsonArray array:
                TrimArray(array, limit);
                break;
            case JsonValue value when StringOf(value) is { } text && text.Length > limit:
                parent[key] = Cut(text, limit);
                break;
        }
    }

    private static void TrimArray(JsonArray array, int limit)
    {
        for (var i = 0; i < array.Count; i++)
        {
            switch (array[i])
            {
                case JsonObject obj:
                    foreach (var inner in obj.Select(pair => pair.Key).ToList())
                    {
                        TrimChild(obj, inner, obj[inner], limit);
                    }
                    break;
                case JsonArray nested:
                    TrimArray(nested, limit);
                    break;
                case JsonValue value when StringOf(value) is { } text && text.Length > limit:
                    array[i] = Cut(text, limit);
                    break;
            }
        }
    }

    /// <summary>Edit bodies keep up to 256 KB each and 512 KB together; everything else the usual 2,000.</summary>
    private static void TrimEdit(JsonObject obj, ref int budget, ref bool truncated)
    {
        foreach (var key in obj.Select(pair => pair.Key).ToList())
        {
            var child = obj[key];
            if (EditFields.Contains(key) && StringOf(child) is { } text)
            {
                var limit = Math.Max(0, Math.Min(MaxEditString, budget));
                if (text.Length > limit)
                {
                    obj[key] = Cut(text, limit);
                    truncated = true;
                    budget -= limit;
                }
                else
                {
                    budget -= text.Length;
                }
                continue;
            }
            if (child is JsonArray array)
            {
                foreach (var item in array.OfType<JsonObject>())
                {
                    TrimEdit(item, ref budget, ref truncated);
                }
                continue;
            }
            TrimChild(obj, key, child, MaxString);
        }
    }

    private static string Cut(string text, int limit)
    {
        var length = limit;
        if (length > 0 && char.IsHighSurrogate(text[length - 1]))
        {
            length--; // never split a surrogate pair
        }
        return string.Concat(text.AsSpan(0, length), Ellipsis);
    }
}
