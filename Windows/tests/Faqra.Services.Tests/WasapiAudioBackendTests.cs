using Faqra.Services.Audio;
using Faqra.Win32.Display;

namespace Faqra.Services.Tests;

// Live-machine checks: read-only against the real audio stack.
public class WasapiAudioBackendTests
{
    private static T OnAudioThread<T>(Func<IAudioBackend, T> read)
    {
        using var thread = new AudioThread();
        using var done = new ManualResetEventSlim();
        T result = default!;
        Exception? error = null;
        thread.Post(() =>
        {
            try
            {
                using var backend = new WasapiAudioBackend();
                result = read(backend);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                done.Set();
            }
        });
        Assert.True(done.Wait(TimeSpan.FromSeconds(10)), "audio thread did not answer");
        if (error is not null)
        {
            throw new Xunit.Sdk.XunitException(error.ToString());
        }
        return result;
    }

    [Fact]
    public void ListsOutputsAndTheDefaultIsOneOfThem()
    {
        var (devices, defaultId) = OnAudioThread(b => (b.Devices(), b.DefaultDeviceId()));
        Assert.NotEmpty(devices);
        Assert.Contains(devices, d => d.Id == defaultId);
        Assert.All(devices, d => Assert.False(string.IsNullOrWhiteSpace(d.Name)));
    }

    [Fact]
    public void SessionsHaveExecutableIdsAndSaneVolumes()
    {
        var sessions = OnAudioThread(b => b.Sessions());
        Assert.All(sessions, s =>
        {
            Assert.InRange(s.Volume, 0, 1);
            Assert.False(string.IsNullOrWhiteSpace(s.Name));
            if (s.ExeId is not null)
            {
                Assert.EndsWith(".exe", s.ExeId);
            }
        });
    }

    [Fact]
    public void DefaultOutputLevelIsReadable()
    {
        var level = OnAudioThread(b => b.DefaultDeviceId() is { } id ? b.OutputLevel(id) : null);
        Assert.NotNull(level);
        Assert.InRange(level.Value.Volume, 0, 1);
    }

    [Fact]
    public void DisplayTopologyReadsEveryActiveDisplay()
    {
        var flags = DisplayTopology.BuiltInFlags();
        Assert.NotNull(flags);
        Assert.NotEmpty(flags);
    }
}
