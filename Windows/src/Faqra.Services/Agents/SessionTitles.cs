// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Services.Agents;

/// <summary>
/// The name Claude Code shows for a conversation, read from the session's transcript: the owner's own name (/rename,
/// a "custom-title" line) when there is one, else Claude's title (an "ai-title" line). Claude Code appends both again as
/// the conversation goes on, so the newest wins and the end of the file nearly always holds them.
/// </summary>
public static class SessionTitles
{
    public const int MaxTitleLength = 120;
    private const int TailBytes = 256 * 1024;
    private const long MaxScanBytes = 8L * 1024 * 1024;

    /// <summary>Where Claude Code keeps transcripts: %USERPROFILE%\.claude\projects.</summary>
    public static string DefaultProjectsRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    /// <summary>The conversation's name, or null when it has none yet or the path is not a transcript under <paramref name="projectsRoot"/>.</summary>
    public static string? Read(string? transcriptPath, string projectsRoot)
    {
        if (string.IsNullOrEmpty(transcriptPath) || !IsTranscript(transcriptPath, projectsRoot))
        {
            return null;
        }
        try
        {
            using var file = new FileStream(transcriptPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var tail = Math.Max(0, file.Length - TailBytes);
            if (NewestFrom(file, tail) is { } title)
            {
                return title;
            }
            return tail > 0 && file.Length <= MaxScanBytes ? NewestFrom(file, 0) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsTranscript(string path, string root)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var under = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(under, StringComparison.OrdinalIgnoreCase) && full.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>The newest name from <paramref name="start"/> on. A line cut by the start position simply fails to parse.</summary>
    private static string? NewestFrom(FileStream file, long start)
    {
        file.Seek(start, SeekOrigin.Begin);
        using var reader = new StreamReader(file, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 64 * 1024, leaveOpen: true);
        string? custom = null;
        string? ai = null;
        while (reader.ReadLine() is { } line)
        {
            if (line.Contains("\"custom-title\"", StringComparison.Ordinal) && Field(line, "custom-title", "customTitle") is { } named)
            {
                custom = named;
            }
            else if (line.Contains("\"ai-title\"", StringComparison.Ordinal) && Field(line, "ai-title", "aiTitle") is { } titled)
            {
                ai = titled;
            }
        }
        return Clean(custom) ?? Clean(ai);
    }

    private static string? Field(string line, string type, string key)
    {
        try
        {
            return JsonNode.Parse(line) is JsonObject obj && Text(obj["type"]) == type ? Text(obj[key]) : null;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>One line, at most <see cref="MaxTitleLength"/> characters.</summary>
    private static string? Clean(string? title)
    {
        if (title is null)
        {
            return null;
        }
        var line = string.Join(' ', title.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (line.Length == 0)
        {
            return null;
        }
        return line.Length <= MaxTitleLength ? line : string.Concat(line.AsSpan(0, MaxTitleLength - 1).TrimEnd(), "…");
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
