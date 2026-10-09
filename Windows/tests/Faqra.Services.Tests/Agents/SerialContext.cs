using System.Collections.Concurrent;

namespace Faqra.Services.Tests.Agents;

/// <summary>Runs every post on one thread, in order, like the UI dispatcher does.</summary>
internal sealed class SerialContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
    private readonly Thread _thread;

    public SerialContext()
    {
        _thread = new Thread(() =>
        {
            foreach (var (callback, state) in _queue.GetConsumingEnumerable())
            {
                callback(state);
            }
        }) { IsBackground = true };
        _thread.Start();
    }

    public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

    public void Dispose() => _queue.CompleteAdding();
}
