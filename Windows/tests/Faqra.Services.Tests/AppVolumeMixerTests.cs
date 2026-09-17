using Faqra.Core.Defaults;
using Faqra.Services.Audio;

namespace Faqra.Services.Tests;

public class AppVolumeMixerTests
{
    private const int OwnPid = 999;

    private sealed class InlineDispatcher : IAudioDispatcher
    {
        private readonly List<Action> _delayed = [];

        public double Now { get; set; } = 100;

        public void Post(Action action) => action();

        public void PostDelayed(Action action, TimeSpan delay) => _delayed.Add(action);

        public void RunDelayed()
        {
            var pending = _delayed.ToList();
            _delayed.Clear();
            pending.ForEach(a => a());
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeBackend : IAudioBackend
    {
        public List<AudioDeviceInfo> DeviceList { get; } = [new("speakers", "Speakers", false), new("buds", "Galaxy Buds", true)];
        public string? Default { get; set; } = "speakers";
        public List<AudioSessionInfo> SessionList { get; } = [];
        public Dictionary<string, OutputLevel> Levels { get; } = new() { ["speakers"] = new(0.8, false), ["buds"] = new(0.5, false) };
        public List<(string Exe, double Volume)> AppWrites { get; } = [];
        public List<string> DefaultWrites { get; } = [];
        public bool Disposed { get; private set; }

        public IReadOnlyList<AudioDeviceInfo> Devices() => DeviceList.ToList();

        public string? DefaultDeviceId() => Default;

        public IReadOnlyList<AudioSessionInfo> Sessions() => SessionList.ToList();

        public void SetAppVolume(string exeId, double volume)
        {
            AppWrites.Add((exeId, volume));
            for (var i = 0; i < SessionList.Count; i++)
            {
                if (SessionList[i].ExeId == exeId)
                {
                    SessionList[i] = SessionList[i] with { Volume = volume, Muted = false };
                }
            }
        }

        public OutputLevel? OutputLevel(string deviceId) => Levels.TryGetValue(deviceId, out var level) ? level : null;

        public void SetOutputVolume(string deviceId, double volume) => Levels[deviceId] = Levels[deviceId] with { Volume = volume };

        public void SetOutputMuted(string deviceId, bool muted) => Levels[deviceId] = Levels[deviceId] with { Muted = muted };

        public int SetDefaultOutput(string deviceId)
        {
            DefaultWrites.Add(deviceId);
            Default = deviceId;
            return 0;
        }

        public event Action? DevicesChanged;
        public event Action? SessionCreated;
        public event Action? OutputLevelChanged;

        public void RaiseSessionCreated() => SessionCreated?.Invoke();

        public void RaiseDevicesChanged() => DevicesChanged?.Invoke();

        public void RaiseLevel() => OutputLevelChanged?.Invoke();

        public void Dispose() => Disposed = true;
    }

    private readonly DefaultsStore _store = DefaultsStore.InMemory(RegisteredDefaults.All);
    private readonly FakeBackend _backend = new();
    private readonly InlineDispatcher _dispatcher = new();
    private bool _available = true;

    private static AudioSessionInfo Session(string instance, string exe, string name, int pid = 10, bool active = false, double volume = 1, string device = "speakers") =>
        new(instance, device, pid, exe, $@"C:\Apps\{exe}", name, active, volume, false);

    private AppVolumeMixer Start()
    {
        var mixer = new AppVolumeMixer(_store, () => _available, () => _backend, _dispatcher, OwnPid);
        mixer.SyncWithPreferences();
        return mixer;
    }

    [Fact]
    public void Rows_OnePerExecutableSortedByName()
    {
        _backend.SessionList.Add(Session("1", "spotify.exe", "Spotify", active: true));
        _backend.SessionList.Add(Session("2", "chrome.exe", "Google Chrome", pid: 11));
        _backend.SessionList.Add(Session("3", "chrome.exe", "Google Chrome", pid: 12, device: "buds", active: true));
        var mixer = Start();

        var apps = mixer.Snapshot.Apps;
        Assert.Equal(["chrome.exe", "spotify.exe"], apps.Select(a => a.Id));
        Assert.True(apps[0].IsPlaying);
    }

    [Fact]
    public void Rows_SkipOwnProcessAndSystemSounds()
    {
        _backend.SessionList.Add(Session("1", "faqra.exe", "Faqra", pid: OwnPid));
        _backend.SessionList.Add(new AudioSessionInfo("2", "speakers", 0, null, null, "System", false, 1, false));
        var mixer = Start();
        Assert.Empty(mixer.Snapshot.Apps);
    }

    [Fact]
    public void MutedSessionReadsAsZero()
    {
        _backend.SessionList.Add(Session("1", "spotify.exe", "Spotify", volume: 0.7) with { Muted = true });
        var mixer = Start();
        Assert.Equal(0, mixer.Snapshot.Apps[0].Volume);
    }

    [Fact]
    public void SetVolume_WritesThatAppOnlyAndPersists()
    {
        _backend.SessionList.Add(Session("1", "spotify.exe", "Spotify"));
        _backend.SessionList.Add(Session("2", "chrome.exe", "Chrome", pid: 11));
        var mixer = Start();

        mixer.SetVolume("spotify.exe", 0.35);

        Assert.Equal([("spotify.exe", 0.35)], _backend.AppWrites);
        Assert.Equal(0.35, _store.DoubleMap(DefaultsKey.AppVolumes)!["spotify.exe"]);
        Assert.Equal(0.35, mixer.Snapshot.Apps.Single(a => a.Id == "spotify.exe").Volume);
        Assert.Equal(1, mixer.Snapshot.Apps.Single(a => a.Id == "chrome.exe").Volume);
    }

    [Fact]
    public void SetVolume_BackTo100RemovesTheSavedEntry()
    {
        _backend.SessionList.Add(Session("1", "spotify.exe", "Spotify"));
        var mixer = Start();
        mixer.SetVolume("spotify.exe", 0.4);
        mixer.SetVolume("spotify.exe", 1);
        Assert.False(_store.DoubleMap(DefaultsKey.AppVolumes)!.ContainsKey("spotify.exe"));
    }

    [Fact]
    public void SavedVolume_AppliedWhenTheAppOpensAudio()
    {
        _store.Set(DefaultsKey.AppVolumes, new Dictionary<string, double> { ["spotify.exe"] = 0.25 });
        var mixer = Start();
        Assert.Empty(_backend.AppWrites);

        _backend.SessionList.Add(Session("1", "spotify.exe", "Spotify"));
        _backend.RaiseSessionCreated();
        _dispatcher.RunDelayed();

        Assert.Equal([("spotify.exe", 0.25)], _backend.AppWrites);
        Assert.Equal(0.25, mixer.Snapshot.Apps[0].Volume);
    }

    [Fact]
    public void SavedVolume_NotReappliedToASessionAlreadySeen()
    {
        _store.Set(DefaultsKey.AppVolumes, new Dictionary<string, double> { ["spotify.exe"] = 0.25 });
        _backend.SessionList.Add(Session("1", "spotify.exe", "Spotify"));
        var mixer = Start();
        Assert.Single(_backend.AppWrites);

        // The user moves it in Windows' own mixer; a later refresh must not fight them.
        _backend.SessionList[0] = _backend.SessionList[0] with { Volume = 0.9 };
        _backend.RaiseLevel();
        _dispatcher.RunDelayed();
        Assert.Single(_backend.AppWrites);
        Assert.Equal(0.9, mixer.Snapshot.Apps[0].Volume);
    }

    [Fact]
    public void SavedBoost_PlaysAt100Percent()
    {
        _store.Set(DefaultsKey.AppVolumes, new Dictionary<string, double> { ["spotify.exe"] = 1.6 });
        _backend.SessionList.Add(Session("1", "spotify.exe", "Spotify", volume: 0.5));
        Start();
        Assert.Equal([("spotify.exe", 1.0)], _backend.AppWrites);
    }

    [Fact]
    public void ToggleMute_RemembersAndRestores()
    {
        _backend.SessionList.Add(Session("1", "spotify.exe", "Spotify", volume: 0.6));
        var mixer = Start();

        mixer.ToggleMute("spotify.exe");
        Assert.Equal(0, mixer.Snapshot.Apps[0].Volume);
        mixer.ToggleMute("spotify.exe");
        Assert.Equal(0.6, mixer.Snapshot.Apps[0].Volume);
    }

    [Fact]
    public void HideFromList_RemovesTheRowAndRemembersIt()
    {
        _backend.SessionList.Add(Session("1", "spotify.exe", "Spotify"));
        var mixer = Start();

        mixer.HideFromList("spotify.exe", "Spotify");
        Assert.Empty(mixer.Snapshot.Apps);
        Assert.Equal([new MixerHiddenApp("spotify.exe", "Spotify")], mixer.Snapshot.HiddenApps);

        mixer.ShowInList("spotify.exe");
        Assert.Single(mixer.Snapshot.Apps);
        Assert.Empty(mixer.Snapshot.HiddenApps);
    }

    [Fact]
    public void Devices_DefaultFirstWithItsLevel()
    {
        _backend.Default = "buds";
        var mixer = Start();
        Assert.Equal("buds", mixer.Snapshot.Devices[0].Id);
        Assert.True(mixer.Snapshot.Devices[0].IsDefault);
        Assert.Equal(0.5, mixer.Snapshot.OutputVolume);
    }

    [Fact]
    public void SetDefaultOutput_SwitchesWindowsAndRepublishes()
    {
        var mixer = Start();
        mixer.SetDefaultOutput("buds");
        Assert.Equal(["buds"], _backend.DefaultWrites);
        Assert.Equal("buds", mixer.Snapshot.CurrentOutputId);
    }

    [Fact]
    public void SetOutputVolume_UnmutesWhenRaised()
    {
        _backend.Levels["speakers"] = new OutputLevel(0.3, true);
        var mixer = Start();
        mixer.SetOutputVolume(0.6);
        Assert.Equal(new OutputLevel(0.6, false), _backend.Levels["speakers"]);
        Assert.Equal(false, mixer.Snapshot.OutputMuted);
    }

    [Fact]
    public void HeadphonesDisconnect_LowersSpeakersThenRestores()
    {
        _store.Set(DefaultsKey.MixerLowerVolumeOnHeadphonesDisconnect, true);
        _store.Set(DefaultsKey.MixerHeadphonesDisconnectVolumePercent, 25);
        _backend.Default = "buds";
        Start();

        _backend.DeviceList.RemoveAll(d => d.Id == "buds");
        _backend.Default = "speakers";
        _backend.RaiseDevicesChanged();
        _dispatcher.RunDelayed();
        Assert.Equal(0.25, _backend.Levels["speakers"].Volume);

        _backend.DeviceList.Add(new AudioDeviceInfo("buds", "Galaxy Buds", true));
        _backend.Default = "buds";
        _backend.RaiseDevicesChanged();
        _dispatcher.RunDelayed();
        Assert.Equal(0.8, _backend.Levels["speakers"].Volume);
    }

    [Fact]
    public void HeadphonesDisconnect_OffByDefault()
    {
        _backend.Default = "buds";
        Start();
        _backend.DeviceList.RemoveAll(d => d.Id == "buds");
        _backend.Default = "speakers";
        _backend.RaiseDevicesChanged();
        _dispatcher.RunDelayed();
        Assert.Equal(0.8, _backend.Levels["speakers"].Volume);
    }

    [Fact]
    public void Uninstalling_StopsAndDisposesTheBackend()
    {
        _backend.SessionList.Add(Session("1", "spotify.exe", "Spotify"));
        var mixer = Start();
        _available = false;
        mixer.SyncWithPreferences();
        Assert.True(_backend.Disposed);
        Assert.Same(MixerSnapshot.Empty, mixer.Snapshot);
    }

    [Fact]
    public void Changed_OnlyFiresWhenSomethingVisibleChanged()
    {
        _backend.SessionList.Add(Session("1", "spotify.exe", "Spotify"));
        var mixer = Start();
        var count = 0;
        mixer.Changed += _ => count++;
        _backend.RaiseLevel();
        _dispatcher.RunDelayed();
        Assert.Equal(0, count);
        mixer.SetVolume("spotify.exe", 0.5);
        Assert.Equal(1, count);
    }
}
