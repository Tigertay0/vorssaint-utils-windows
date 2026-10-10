// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of coucou-hook (windows/hook/src/main.rs) in Coucou, https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé).

using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Faqra.Core.Agents;

namespace Faqra.Hook;

/// <summary>
/// Claude Code runs this once per hook event. It hands the event to Faqra and gets out of the way: it always exits 0, a
/// session never waits on a Faqra that is closed or slow, and only a permission request the owner decided on the island
/// gets a reply on stdout. Everything else prints nothing, exactly as if Faqra did not exist.
/// </summary>
public static partial class Relay
{
    public const int ConnectBudgetMs = 300;

    /// <summary>The whole run for an event nobody waits on: read stdin, connect and send.</summary>
    public const int RunBudgetMs = 2000;

    /// <summary>How long a permission request waits for the owner. Faqra lets go at 108 s; Claude Code's hook timeout is 120 s.</summary>
    public const int DecisionBudgetMs = 110_000;

    /// <summary>Larger than any hook payload; anything bigger is not read whole.</summary>
    private const int MaxStdinBytes = 32 * 1024 * 1024;

    private const int MaxReplyBytes = 1024 * 1024;

    public static int Run(string[] args, Stream stdin, Stream stdout, Func<string, string?> env, string cwd, string pipeName)
    {
        var clock = Stopwatch.StartNew();
        // The work runs on pool (background) threads, so a blocked read cannot keep the process alive once Main returns.
        var prepared = Task.Run(() => Prepare(args.Length > 0 ? args[0] : string.Empty, stdin, env, cwd));
        if (!prepared.Wait(RunBudgetMs) || prepared.Result is not { } outgoing)
        {
            return 0;
        }
        var talk = Task.Run(() => Talk(pipeName, outgoing.Line, outgoing.WaitsForAnswer));
        var budget = outgoing.WaitsForAnswer ? DecisionBudgetMs : Math.Max(0, RunBudgetMs - (int)clock.ElapsedMilliseconds);
        if (!talk.Wait(budget) || !outgoing.WaitsForAnswer)
        {
            return 0;
        }
        if (PermissionReply.Stdout(outgoing.Request, AgentDecision.TryParse(talk.Result)) is { } reply)
        {
            stdout.Write(Encoding.UTF8.GetBytes(reply + "\n"));
            stdout.Flush();
        }
        return 0;
    }

    /// <summary>The line for Faqra and, for a permission request, the untrimmed payload its reply is built from.</summary>
    private sealed record Outgoing(string Line, JsonObject Request, bool WaitsForAnswer);

    private static Outgoing? Prepare(string eventArg, Stream stdin, Func<string, string?> env, string cwd)
    {
        try
        {
            if (ReadAll(stdin) is not { } text || HookPayload.ToLine(text, eventArg, "claude", env, cwd) is not { } line)
            {
                return null;
            }
            // Only a permission request needs the payload again, whole: Claude reads its own question back.
            var maybe = eventArg == "PermissionRequest" || line.Contains("\"hook_event_name\":\"PermissionRequest\"", StringComparison.Ordinal);
            if (!maybe || JsonNode.Parse(text.TrimStart('\uFEFF')) is not JsonObject request)
            {
                return new Outgoing(line, new JsonObject(), false);
            }
            var name = request["hook_event_name"] is JsonValue value && value.GetValueKind() == JsonValueKind.String
                ? value.GetValue<string>()
                : eventArg;
            return new Outgoing(line, request, name == "PermissionRequest");
        }
        catch (Exception)
        {
            // A hook must never fail the session: whatever goes wrong, it carries on as if Faqra did not exist.
            return null;
        }
    }

    /// <summary>Sends the line and, when asked to, reads Faqra's one-line answer. Null for no answer, whatever the reason.</summary>
    private static string? Talk(string pipeName, string line, bool waitsForAnswer)
    {
        try
        {
            // NamedPipeClientStream.Connect keeps retrying a pipe that does not exist until its timeout, and
            // Faqra being closed is the common case. WaitNamedPipe returns at once when there is no pipe and
            // waits only while every instance is busy, so a false answer means: give up now.
            if (!WaitNamedPipe(@"\\.\pipe\" + pipeName, ConnectBudgetMs))
            {
                return null;
            }
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
            pipe.Connect(ConnectBudgetMs);
            pipe.Write(Encoding.UTF8.GetBytes(line + "\n"));
            pipe.Flush();
            return waitsForAnswer ? ReadReply(pipe) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Up to the first newline, or until Faqra hangs up; nothing when it hung up without a word.</summary>
    private static string? ReadReply(Stream pipe)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = pipe.Read(chunk, 0, chunk.Length)) > 0)
        {
            var newline = Array.IndexOf(chunk, (byte)'\n', 0, read);
            buffer.Write(chunk, 0, newline >= 0 ? newline : read);
            if (newline >= 0 || buffer.Length > MaxReplyBytes)
            {
                break;
            }
        }
        return buffer.Length == 0 || buffer.Length > MaxReplyBytes
            ? null
            : Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    [LibraryImport("kernel32.dll", EntryPoint = "WaitNamedPipeW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WaitNamedPipe(string name, uint timeoutMs);

    private static string? ReadAll(Stream stdin)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int read;
        while ((read = stdin.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > MaxStdinBytes)
            {
                return null;
            }
            buffer.Write(chunk, 0, read);
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
