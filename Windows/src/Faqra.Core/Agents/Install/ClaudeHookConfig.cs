// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Event list and ownership rule follow Coucou's hooks.rs, https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé). Faqra uses the exec form (command + args), so no shell starts.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Agents.Install;

/// <summary>The file is not plain JSON, or its hooks are shaped in a way Faqra will not guess at.</summary>
public sealed class ConfigFormatException(string message) : Exception(message);

/// <summary>How many of Faqra's events are registered, and how many events still carry Coucou's hooks.</summary>
public readonly record struct HookStatus(int FaqraEvents, int CoucouEvents)
{
    public bool Installed => FaqraEvents >= ClaudeHookConfig.Events.Count;
}

/// <summary>Adds, removes and counts Faqra's entries in Claude Code's settings.json, leaving every other byte's meaning alone.</summary>
public static class ClaudeHookConfig
{
    public const string Marker = "faqra-hook";
    public const string CoucouMarker = "coucou-hook";

    /// <summary>Every event Faqra listens to, with Claude Code's timeout in seconds. Stop allows the A3 reply window.</summary>
    public static readonly IReadOnlyList<(string Event, int Timeout)> Events =
    [
        ("SessionStart", 10), ("SessionEnd", 10), ("UserPromptSubmit", 10), ("PreToolUse", 10), ("PostToolUse", 10),
        ("PostToolUseFailure", 10), ("PermissionRequest", 120), ("Notification", 10), ("Stop", 600), ("StopFailure", 10),
        ("SubagentStart", 10), ("SubagentStop", 10), ("PreCompact", 10), ("PostCompact", 10),
    ];

    public static string Install(string? json, string relayPath, bool removeCoucou)
    {
        var (root, style) = Parse(json);
        var hooks = HooksOf(root, create: true)!;
        if (removeCoucou)
        {
            Remove(hooks, CoucouMarker);
        }
        Remove(hooks, Marker);
        foreach (var (name, timeout) in Events)
        {
            hooks[name] ??= new JsonArray();
            if (hooks[name] is not JsonArray groups)
            {
                throw new ConfigFormatException($"\"hooks.{name}\" is not a list");
            }
            groups.Add(new JsonObject
            {
                ["hooks"] = new JsonArray(new JsonObject
                {
                    ["type"] = "command",
                    ["command"] = relayPath,
                    ["args"] = new JsonArray(name),
                    ["timeout"] = timeout,
                }),
            });
        }
        return Write(root, style);
    }

    public static string Uninstall(string? json, bool removeCoucou)
    {
        var (root, style) = Parse(json);
        if (HooksOf(root, create: false) is { } hooks)
        {
            var removed = Remove(hooks, Marker) | (removeCoucou && Remove(hooks, CoucouMarker));
            if (removed && hooks.Count == 0)
            {
                root.Remove("hooks");
            }
        }
        return Write(root, style);
    }

    public static HookStatus Inspect(string? json)
    {
        var (root, _) = Parse(json);
        return HooksOf(root, create: false) is { } hooks
            ? new HookStatus(CountEvents(hooks, Marker), CountEvents(hooks, CoucouMarker))
            : new HookStatus(0, 0);
    }

    private static (JsonObject Root, TextStyle Style) Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (new JsonObject(), new TextStyle("\n", TrailingNewline: true));
        }
        var text = json.TrimStart('﻿');
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new ConfigFormatException(ex.Message);
        }
        if (node is not JsonObject root)
        {
            throw new ConfigFormatException("the file is not a JSON object");
        }
        return (root, new TextStyle(text.Contains("\r\n") ? "\r\n" : "\n", text.EndsWith('\n')));
    }

    private static JsonObject? HooksOf(JsonObject root, bool create)
    {
        if (root["hooks"] is null)
        {
            if (!create)
            {
                return null;
            }
            root["hooks"] = new JsonObject();
        }
        return root["hooks"] as JsonObject ?? throw new ConfigFormatException("\"hooks\" is not an object");
    }

    /// <summary>
    /// Removes every handler whose command contains the marker; empties it leaves behind go too. True when anything went.
    /// Non-list event values are skipped, not refused: only Install refuses them (for Faqra's own events, where it writes), so the owner's values stay untouched.
    /// </summary>
    private static bool Remove(JsonObject hooks, string marker)
    {
        var removed = false;
        foreach (var name in hooks.Select(pair => pair.Key).ToList())
        {
            if (hooks[name] is not JsonArray groups)
            {
                continue;
            }
            var touched = false;
            for (var g = groups.Count - 1; g >= 0; g--)
            {
                if (groups[g] is not JsonObject group || group["hooks"] is not JsonArray handlers)
                {
                    continue;
                }
                var before = handlers.Count;
                for (var h = handlers.Count - 1; h >= 0; h--)
                {
                    if (handlers[h] is JsonObject handler && IsMarked(handler, marker))
                    {
                        handlers.RemoveAt(h);
                    }
                }
                if (handlers.Count < before)
                {
                    touched = true;
                    if (handlers.Count == 0)
                    {
                        groups.RemoveAt(g);
                    }
                }
            }
            if (touched && groups.Count == 0)
            {
                hooks.Remove(name);
            }
            removed |= touched;
        }
        return removed;
    }

    private static int CountEvents(JsonObject hooks, string marker) => hooks.Count(pair =>
        pair.Value is JsonArray groups && groups.OfType<JsonObject>().Any(group =>
            group["hooks"] is JsonArray handlers && handlers.OfType<JsonObject>().Any(handler => IsMarked(handler, marker))));

    private static bool IsMarked(JsonObject handler, string marker) =>
        handler["command"] is JsonValue value && value.GetValueKind() == JsonValueKind.String
        && value.GetValue<string>().Contains(marker, StringComparison.OrdinalIgnoreCase);

    private static string Write(JsonObject root, TextStyle style)
    {
        // .NET 8 indents with the platform's newline; normalise, then use the file's own.
        var text = root.ToJsonString(AgentJson.Indented).Replace("\r\n", "\n");
        if (style.TrailingNewline)
        {
            text += "\n";
        }
        return style.Newline == "\n" ? text : text.Replace("\n", style.Newline);
    }

    private readonly record struct TextStyle(string Newline, bool TrailingNewline);
}
