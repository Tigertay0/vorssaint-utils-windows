using System.IO.Pipes;
using System.Text;

namespace Faqra.Services.Tests.Agents;

/// <summary>Plays the relay against a hub: sends one line, then reads the one line Faqra answers (null when it hangs up).</summary>
internal sealed class FakeRelay : IDisposable
{
    private readonly NamedPipeClientStream _pipe;

    private FakeRelay(NamedPipeClientStream pipe) => _pipe = pipe;

    public static async Task<FakeRelay> SendAsync(string pipeName, string line)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
        await pipe.ConnectAsync(2000);
        await pipe.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"));
        await pipe.FlushAsync();
        return new FakeRelay(pipe);
    }

    public async Task<string?> ReplyAsync(TimeSpan within)
    {
        using var reader = new StreamReader(_pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
        return await reader.ReadLineAsync().WaitAsync(within);
    }

    public void Dispose() => _pipe.Dispose();
}

internal static class HubTestKit
{
    public static string NewPipe() => "faqra-test-" + Guid.NewGuid().ToString("N");

    /// <summary>Runs <paramref name="action"/> on the hub's thread, as the island would.</summary>
    public static Task<T> OnContext<T>(SynchronizationContext context, Func<T> action)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Post(_ =>
        {
            try
            {
                done.SetResult(action());
            }
            catch (Exception ex)
            {
                done.SetException(ex);
            }
        }, null);
        return done.Task;
    }

    public static async Task<bool> Eventually(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(50);
        }
        return condition();
    }
}
