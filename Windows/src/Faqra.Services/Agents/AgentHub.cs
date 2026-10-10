// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of Coucou's pipe server (windows/src-tauri/src/pipe.rs), https://github.com/Louis-CFM/coucou
// (MIT License, Copyright (c) 2026 Louis Raillé).

using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using Faqra.Core.Agents;

namespace Faqra.Services.Agents;

/// <summary>
/// Listens on the agents pipe and folds every event into <see cref="Board"/>. A permission request keeps its
/// connection open while a card shows it, and the owner's choice goes back on that same connection. The board and the
/// requests are read and replaced only on the given context's thread (the UI thread in the app), and every event the
/// hub raises is raised there.
/// </summary>
public sealed class AgentHub : IDisposable
{
    public const int MaxLineBytes = 1024 * 1024;
    private const long MaxLogBytes = 1024 * 1024;
    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    /// <summary>How long a card waits for the owner: under the relay's 110 s, so Faqra always answers or lets go first.</summary>
    public static readonly TimeSpan DecisionTimeout = TimeSpan.FromSeconds(108);

    /// <summary>Events after which a waiting request no longer matters: the turn moved on without it.</summary>
    private static readonly HashSet<string> TurnEnds = new(StringComparer.Ordinal) { "UserPromptSubmit", "Stop", "StopFailure", "SessionEnd" };

    private readonly string _pipeName;
    private readonly SynchronizationContext _context;
    private readonly Func<DateTimeOffset> _now;
    private readonly string? _logPath;
    private readonly Dictionary<string, TaskCompletionSource<AgentDecision?>> _waiting = new(StringComparer.Ordinal);
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

    /// <summary>The requests cards are showing, oldest first.</summary>
    public ImmutableList<AgentRequest> Requests { get; private set; } = ImmutableList<AgentRequest>.Empty;

    public bool IsRunning => _running is not null;

    public event Action? Changed;

    /// <summary>A request now waits on a card. Raised after <see cref="Changed"/>.</summary>
    public event Action<AgentRequest>? RequestArrived;

    /// <summary>A session finished a turn with something to say. Raised after <see cref="Changed"/>.</summary>
    public event Action<AgentSession>? TurnFinished;

    /// <summary>
    /// Whether a card can show a request right now, asked on the context's thread as each one arrives. When it says no,
    /// the request is let go at once and Claude Code asks in its own UI. Nothing is held until the island sets it.
    /// </summary>
    public Func<bool> CanAsk { get; set; } = () => false;

    /// <summary>How long a card waits; tests shorten it.</summary>
    internal TimeSpan DecisionWait { get; set; } = DecisionTimeout;

    /// <summary>For render tests: shows a prepared board without a pipe. Never called by the app.</summary>
    public void ReplaceBoardForTests(AgentBoard board)
    {
        Board = board;
        Changed?.Invoke();
    }

    /// <summary>For render tests: shows prepared requests without a pipe; nothing waits on them. Never called by the app.</summary>
    public void ReplaceRequestsForTests(params AgentRequest[] requests)
    {
        Requests = ImmutableList.Create(requests);
        Changed?.Invoke();
    }

    /// <summary>Sends the owner's choice to the waiting relay; false when the request no longer waits. Call on the context's thread.</summary>
    public bool Answer(string requestId, AgentDecision decision) => Settle(requestId, decision);

    /// <summary>Lets a request go without a choice, so Claude Code asks in its own UI. Call on the context's thread.</summary>
    public void Release(string requestId) => Settle(requestId, null);

    /// <summary>The owner has seen a session's latest answer. Call on the context's thread.</summary>
    public void MarkRead(string sessionId)
    {
        var next = Board.MarkRead(sessionId);
        if (!ReferenceEquals(next, Board))
        {
            Board = next;
            Changed?.Invoke();
        }
    }

    /// <summary>Starts or stops listening; stopping forgets every session and lets every request go. Call on the context's thread.</summary>
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
            foreach (var waiting in _waiting.Values)
            {
                waiting.TrySetResult(null);
            }
            _waiting.Clear();
            Requests = Requests.Clear();
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
            if (e.Event == "PermissionRequest")
            {
                await ServeRequest(connection, e, token).ConfigureAwait(false);
                return;
            }
            Post(() =>
            {
                // A run that was stopped meanwhile must not put sessions back on the emptied board.
                if (token.IsCancellationRequested)
                {
                    return;
                }
                Board = Board.Apply(e, _now());
                Log(e);
                if (TurnEnds.Contains(e.Event))
                {
                    ReleaseSession(e.SessionId);
                }
                Changed?.Invoke();
                if (e.Event == "Stop" && Board.Sessions.TryGetValue(e.SessionId, out var session) && session.Unread)
                {
                    TurnFinished?.Invoke(session);
                }
            });
        }
    }

    /// <summary>
    /// Shows the request on a card and waits for the owner, the relay hanging up (Claude Code took the answer in its own
    /// UI, or ended the hook), the turn moving on, the timeout, or a stop. Only a choice is written back; anything else
    /// closes the connection without a word, and Claude Code asks in its own UI.
    /// </summary>
    private async Task ServeRequest(NamedPipeServerStream connection, AgentEvent e, CancellationToken token)
    {
        var id = Guid.NewGuid().ToString("N");
        var decided = new TaskCompletionSource<AgentDecision?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var shown = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() => shown.TrySetResult(!token.IsCancellationRequested && Open(id, e, decided)));
        bool isShown;
        try
        {
            isShown = await shown.Task.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (!isShown)
        {
            return;
        }

        AgentDecision? decision;
        using (var waiting = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            var hangUp = HangUp(connection, waiting.Token);
            var timeout = Task.Delay(DecisionWait, waiting.Token);
            var first = await Task.WhenAny(decided.Task, hangUp, timeout).ConfigureAwait(false);
            decision = first == decided.Task ? await decided.Task.ConfigureAwait(false) : null;
            // Stop watching for the hang-up before writing on the same pipe.
            waiting.Cancel();
            await hangUp.ConfigureAwait(false);
        }
        if (decision is null)
        {
            Post(() => Settle(id, null));
            return;
        }
        try
        {
            await connection.WriteAsync(Encoding.UTF8.GetBytes(decision.ToLine() + "\n")).ConfigureAwait(false);
            await connection.FlushAsync().ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The relay left between the click and the write: Claude Code asks in its own UI.
        }
    }

    /// <summary>Folds the request into the board and, when a card can show it, starts waiting on it. Runs on the context's thread.</summary>
    private bool Open(string id, AgentEvent e, TaskCompletionSource<AgentDecision?> decided)
    {
        var now = _now();
        Board = Board.Apply(e, now);
        Log(e);
        AgentRequest? request = null;
        if (CanAsk())
        {
            try
            {
                request = AgentRequest.From(id, e, now);
            }
            catch (ArgumentException)
            {
                // Repeated keys in the tool input: no card for a request Faqra cannot read whole.
            }
        }
        if (request is not null)
        {
            Requests = Requests.Add(request);
            _waiting[id] = decided;
        }
        Changed?.Invoke();
        if (request is not null)
        {
            RequestArrived?.Invoke(request);
        }
        return request is not null;
    }

    /// <summary>Ends a waiting request with the owner's choice, or none. Runs on the context's thread.</summary>
    private bool Settle(string requestId, AgentDecision? decision)
    {
        if (!_waiting.Remove(requestId, out var waiting))
        {
            return false;
        }
        var request = Requests.Find(r => r.Id == requestId);
        Requests = Requests.RemoveAll(r => r.Id == requestId);
        if (decision is not null && request is not null)
        {
            Board = Board.Answered(request.SessionId, allowed: decision.Kind != AgentDecisionKind.Deny, _now());
        }
        waiting.TrySetResult(decision);
        if (request is not null)
        {
            LogDecision(request, decision);
        }
        Changed?.Invoke();
        return true;
    }

    private void ReleaseSession(string sessionId)
    {
        foreach (var request in Requests.Where(r => r.SessionId == sessionId).ToList())
        {
            Settle(request.Id, null);
        }
    }

    /// <summary>Completes when the relay closes its end: it exited, or Claude Code ended the hook.</summary>
    private static async Task HangUp(Stream connection, CancellationToken token)
    {
        var one = new byte[1];
        try
        {
            while (await connection.ReadAsync(one, token).ConfigureAwait(false) > 0)
            {
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // Cancelled because the wait is over, or the pipe broke because the relay left: either way, done.
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
    /// One line per event: time, event, session, tool and the state it led to. Never the prompt, command, path,
    /// answer or Claude's words, so the log can be shared when something goes wrong.
    /// </summary>
    private void Log(AgentEvent e)
    {
        var state = Board.Sessions.TryGetValue(e.SessionId, out var s) ? s.State.ToString() : "gone";
        WriteLog($"{_now():O} {e.Event} {Short(e.SessionId)} {e.ToolName ?? "-"} {state}");
    }

    /// <summary>What became of a request: allow, always, deny, answered or released. Never the answer itself.</summary>
    private void LogDecision(AgentRequest request, AgentDecision? decision)
    {
        var outcome = decision?.Kind switch
        {
            AgentDecisionKind.Allow => "allow",
            AgentDecisionKind.Always => "always",
            AgentDecisionKind.Deny => "deny",
            AgentDecisionKind.Answer => "answered",
            _ => "released",
        };
        WriteLog($"{_now():O} Decision {Short(request.SessionId)} {request.ToolName} {outcome}");
    }

    private static string Short(string sessionId) => sessionId.Length > 8 ? sessionId[..8] : sessionId;

    private void WriteLog(string line)
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
            File.AppendAllText(_logPath, line + "\n");
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
