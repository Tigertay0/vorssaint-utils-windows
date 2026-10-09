// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of Coucou's pipe server (windows/src-tauri/src/pipe.rs), https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé).

using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using Faqra.Core.Agents;

namespace Faqra.Services.Agents;

/// <summary>
/// Listens on the agents pipe and folds every event into <see cref="Board"/>. The board is read and replaced
/// only on the given context's thread (the UI thread in the app), and <see cref="Changed"/> is raised there.
/// </summary>
public sealed class AgentHub : IDisposable
{
    public const int MaxLineBytes = 1024 * 1024;
    private const long MaxLogBytes = 1024 * 1024;
    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private readonly string _pipeName;
    private readonly SynchronizationContext _context;
    private readonly Func<DateTimeOffset> _now;
    private readonly string? _logPath;
    private CancellationTokenSource? _running;
    private Timer? _tick;
    private bool _disposed;

    public AgentHub(string pipeName, SynchronizationContext context, Func<DateTimeOffset> now, string? logPath = null)
    {
        _pipeName = pipeName;
        _context = context;
        _now = now;
        _logPath = logPath;
    }

    public AgentBoard Board { get; private set; } = AgentBoard.Empty;

    public bool IsRunning => _running is not null;

    public event Action? Changed;

    /// <summary>For render tests: shows a prepared board without a pipe. Never called by the app.</summary>
    public void ReplaceBoardForTests(AgentBoard board) => Board = board;

    /// <summary>Starts or stops listening; stopping forgets every session. Call on the context's thread.</summary>
    public void SetRunning(bool running)
    {
        if (running)
        {
            Start();
        }
        else
        {
            Stop();
        }
    }

    private void Start()
    {
        if (_running is not null || _disposed)
        {
            return;
        }
        _running = new CancellationTokenSource();
        var token = _running.Token;
        _ = Task.Run(() => AcceptLoop(token));
        _tick = new Timer(_ => Post(TickBoard), null, TickInterval, TickInterval);
    }

    private void Stop()
    {
        if (_running is null)
        {
            return;
        }
        _running.Cancel();
        _running.Dispose();
        _running = null;
        _tick?.Dispose();
        _tick = null;
        Post(() =>
        {
            Board = AgentBoard.Empty;
            Changed?.Invoke();
        });
    }

    private async Task AcceptLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                server?.Dispose();
                return;
            }
            catch (Exception ex)
            {
                // Another process holds the name, a burst used every instance, or something unexpected: wait and try again.
                server?.Dispose();
                Trace.TraceWarning($"Faqra agents pipe: {ex.Message}");
                try
                {
                    await Task.Delay(RetryDelay, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                continue;
            }
            var connection = server;
            _ = Task.Run(() => Serve(connection, token), CancellationToken.None);
        }
    }

    /// <summary>Test seam: runs after a line parsed and before it is posted to the context.</summary>
    internal Action? BeforePost { get; set; }

    private async Task Serve(NamedPipeServerStream connection, CancellationToken token)
    {
        using (connection)
        {
            string? line;
            try
            {
                line = await ReadLine(connection, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            {
                return;
            }
            if (line is null || AgentEvent.TryParse(line) is not { } e)
            {
                return;
            }
            BeforePost?.Invoke();
            Post(() =>
            {
                // A run that was stopped meanwhile must not put sessions back on the emptied board.
                if (token.IsCancellationRequested)
                {
                    return;
                }
                Board = Board.Apply(e, _now());
                Log(e);
                Changed?.Invoke();
            });
        }
    }

    private static async Task<string?> ReadLine(Stream stream, CancellationToken token)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(ReadBudget);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, budget.Token).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            var newline = Array.IndexOf(chunk, (byte)'\n', 0, read);
            buffer.Write(chunk, 0, newline >= 0 ? newline : read);
            if (newline >= 0)
            {
                break;
            }
            if (buffer.Length > MaxLineBytes)
            {
                return null;
            }
        }
        return buffer.Length == 0 ? null : Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private void TickBoard()
    {
        var next = Board.Tick(_now());
        if (!ReferenceEquals(next, Board))
        {
            Board = next;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// One line per event: time, event, session, tool and the state it led to. Never the prompt, command,
    /// path or Claude's words, so the log can be shared when something goes wrong.
    /// </summary>
    private void Log(AgentEvent e)
    {
        if (_logPath is null)
        {
            return;
        }
        try
        {
            if (File.Exists(_logPath) && new FileInfo(_logPath).Length > MaxLogBytes)
            {
                File.Move(_logPath, _logPath + ".1", overwrite: true);
            }
            var session = e.SessionId.Length > 8 ? e.SessionId[..8] : e.SessionId;
            var state = Board.Sessions.TryGetValue(e.SessionId, out var s) ? s.State.ToString() : "gone";
            File.AppendAllText(_logPath, $"{_now():O} {e.Event} {session} {e.ToolName ?? "-"} {state}\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Faqra agents log: {ex.Message}");
        }
    }

    private void Post(Action action) => _context.Post(_ =>
    {
        if (!_disposed)
        {
            action();
        }
    }, null);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        Stop();
        _disposed = true;
    }
}
