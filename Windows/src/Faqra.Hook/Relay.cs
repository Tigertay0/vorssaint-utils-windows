// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of coucou-hook (windows/hook/src/main.rs) in Coucou, https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé).

using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using Faqra.Core.Agents;

namespace Faqra.Hook;

/// <summary>
/// Claude Code runs this once per hook event. It hands the event to Faqra and gets out of the way: whatever
/// happens it exits 0 and prints nothing, so a session never waits on Faqra or changes because of it.
/// </summary>
public static partial class Relay
{
    public const int ConnectBudgetMs = 300;
    public const int RunBudgetMs = 2000;

    /// <summary>Larger than any hook payload; anything bigger is not read whole.</summary>
    private const int MaxStdinBytes = 32 * 1024 * 1024;

    public static int Run(string[] args, Stream stdin, Func<string, string?> env, string cwd, string pipeName)
    {
        var eventArg = args.Length > 0 ? args[0] : string.Empty;
        if (ReadAll(stdin) is not { } text || HookPayload.ToLine(text, eventArg, "claude", env, cwd) is not { } line)
        {
            return 0;
        }
        // A Faqra that stops reading is not worth a slower session: the send gets a fixed budget.
        Task.Run(() => Send(pipeName, line)).Wait(RunBudgetMs);
        return 0;
    }

    private static void Send(string pipeName, string line)
    {
        try
        {
            // NamedPipeClientStream.Connect keeps retrying a pipe that does not exist until its timeout, and
            // Faqra being closed is the common case. WaitNamedPipe returns at once when there is no pipe and
            // waits only while every instance is busy, so a false answer means: give up now.
            if (!WaitNamedPipe(@"\\.\pipe\" + pipeName, ConnectBudgetMs))
            {
                return;
            }
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
            pipe.Connect(ConnectBudgetMs);
            pipe.Write(Encoding.UTF8.GetBytes(line + "\n"));
            pipe.Flush();
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            // Faqra is closed, busy, or the pipe belongs to someone else: the session carries on as if Faqra did not exist.
        }
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
