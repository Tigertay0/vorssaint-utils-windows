// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/Audio/AppVolumeMixer.swift on top of WASAPI sessions: one row per app,
// saved volumes re-applied whenever an app opens audio, mute that remembers the last audible level, hidden
// apps, the default output switch, the system output level and the headphone-disconnect protection.
// Process taps, boost above 100% and per-app output routing have no Windows counterpart and are left out.

using Faqra.Core.Defaults;
using Faqra.Core.Mixer;

namespace Faqra.Services.Audio;

public sealed record MixerApp(string Id, string Name, string? ExePath, bool IsPlaying, double Volume);

public sealed record MixerOutputDevice(string Id, string Name, bool IsDefault, bool IsHeadphones);

public sealed record MixerHiddenApp(string Id, string Name);

public sealed record MixerSnapshot(
    IReadOnlyList<MixerApp> Apps,
    IReadOnlyList<MixerOutputDevice> Devices,
    string? CurrentOutputId,
    double? OutputVolume,
    bool? OutputMuted,
    IReadOnlyList<MixerHiddenApp> HiddenApps,
    string? OutputSwitchError)
{
    public static readonly MixerSnapshot Empty = new([], [], null, null, null, [], null);
}

public sealed class AppVolumeMixer : IDisposable
{
    /// <summary>While a mixer surface is visible, playing state and outside volume changes are read this often.</summary>
    private static readonly TimeSpan VisiblePollInterval = TimeSpan.FromSeconds(1);

    private readonly ISettingsStore _store;
    private readonly Func<bool> _featureAvailable;
    private readonly Func<IAudioBackend> _backendFactory;
    private readonly IAudioDispatcher _dispatcher;
    private readonly int _ownProcessId;

    // Audio-thread state.
    private IAudioBackend? _backend;
    private readonly HashSet<string> _seenSessions = [];
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, double> _lastAudible = new();
    private double? _lastRefreshAt;
    private bool _refreshScheduled;
    private int _visibleCount;
    private bool _pollScheduled;
    private string? _previousDefaultId;
    private bool _previousDefaultWasHeadphones;
    private string? _lastLoweredId;
    private (string Id, double Previous, double Applied)? _lowered;
    private string? _switchError;
    private readonly Dictionary<string, double> _pendingAppVolumes = [];
    private double? _pendingOutputVolume;

    private volatile MixerSnapshot _snapshot = MixerSnapshot.Empty;

    public AppVolumeMixer(ISettingsStore store, Func<bool> featureAvailable, Func<IAudioBackend> backendFactory, IAudioDispatcher dispatcher, int ownProcessId)
    {
        _store = store;
        _featureAvailable = featureAvailable;
        _backendFactory = backendFactory;
        _dispatcher = dispatcher;
        _ownProcessId = ownProcessId;
    }

    public MixerSnapshot Snapshot => _snapshot;

    /// <summary>Raised on the audio thread after every refresh that changed what a surface would show.</summary>
    public event Action<MixerSnapshot>? Changed;

    /// <summary>Starts or stops the whole service with the feature, like upstream's syncWithPreferences.</summary>
    public void SyncWithPreferences()
    {
        var wanted = _featureAvailable();
        _dispatcher.Post(() =>
        {
            if (wanted && _backend is null)
            {
                StartOnAudioThread();
            }
            else if (!wanted && _backend is not null)
            {
                StopOnAudioThread();
            }
        });
    }

    /// <summary>A panel or island surface showing the mixer; while any is visible the mixer polls.</summary>
    public void SetVisible(bool visible)
    {
        _dispatcher.Post(() =>
        {
            _visibleCount = Math.Max(0, _visibleCount + (visible ? 1 : -1));
            if (visible)
            {
                RefreshNow();
                SchedulePoll();
            }
        });
    }

    public void SetVolume(string appId, double volume)
    {
        var sanitized = DefaultsSanitizers.AppVolume(volume);
        Persist(appId, sanitized);
        if (sanitized > 0.001)
        {
            _lastAudible[appId] = sanitized;
        }
        _dispatcher.Post(() =>
        {
            // Drags send many values; only the newest per app reaches Windows.
            var first = !_pendingAppVolumes.ContainsKey(appId);
            _pendingAppVolumes[appId] = sanitized;
            if (first)
            {
                _dispatcher.Post(() => DrainAppVolume(appId));
            }
        });
    }

    /// <summary>Mutes an audible app, remembering its level; unmutes a silent one to that level. Any thread.</summary>
    public void ToggleMute(string appId)
    {
        var app = _snapshot.Apps.FirstOrDefault(a => a.Id == appId);
        if (app is null)
        {
            return;
        }
        double? remembered = _lastAudible.TryGetValue(appId, out var last) ? last : null;
        var (volume, lastAudible) = MixerRoutingSupport.ToggleMute(app.Volume, remembered);
        if (lastAudible is { } audible)
        {
            _lastAudible[appId] = audible;
        }
        SetVolume(appId, volume);
    }

    public void SetOutputVolume(double volume)
    {
        var clamped = double.IsFinite(volume) ? Math.Clamp(volume, 0, 1) : 0;
        _dispatcher.Post(() =>
        {
            var first = _pendingOutputVolume is null;
            _pendingOutputVolume = clamped;
            if (first)
            {
                _dispatcher.Post(DrainOutputVolume);
            }
        });
    }

    public void ToggleOutputMute() =>
        _dispatcher.Post(() =>
        {
            if (_backend is null || _snapshot.CurrentOutputId is not { } id || _snapshot.OutputMuted is not { } muted)
            {
                return;
            }
            _backend.SetOutputMuted(id, !muted);
            RefreshNow();
        });

    public void SetDefaultOutput(string deviceId) =>
        _dispatcher.Post(() =>
        {
            if (_backend is null || MixerRoutingSupport.SanitizedId(deviceId) is not { } id)
            {
                return;
            }
            var hr = _backend.SetDefaultOutput(id);
            _switchError = hr < 0 ? $"0x{hr:X8}" : null;
            RefreshNow();
        });

    public void HideFromList(string appId, string name)
    {
        var hidden = new Dictionary<string, string>(MixerRoutingSupport.SanitizedHiddenApps(_store.StringMap(DefaultsKey.MixerHiddenApps)));
        if (MixerRoutingSupport.SanitizedId(appId) is { } id && MixerRoutingSupport.SanitizedId(name) is { } label)
        {
            hidden[id] = label;
            _store.Set(DefaultsKey.MixerHiddenApps, hidden);
        }
        _dispatcher.Post(RefreshNow);
    }

    public void ShowInList(string appId)
    {
        var hidden = new Dictionary<string, string>(MixerRoutingSupport.SanitizedHiddenApps(_store.StringMap(DefaultsKey.MixerHiddenApps)));
        if (hidden.Remove(appId))
        {
            _store.Set(DefaultsKey.MixerHiddenApps, hidden);
        }
        _dispatcher.Post(RefreshNow);
    }

    private void Persist(string appId, double volume)
    {
        var saved = _store.DoubleMap(DefaultsKey.AppVolumes) ?? new Dictionary<string, double>();
        _store.Set(DefaultsKey.AppVolumes, MixerRoutingSupport.VolumesAfterSet(saved, appId, volume));
    }

    private void StartOnAudioThread()
    {
        _backend = _backendFactory();
        _backend.DevicesChanged += ScheduleRefresh;
        _backend.SessionCreated += ScheduleRefresh;
        _backend.OutputLevelChanged += ScheduleRefresh;
        _seenSessions.Clear();
        _previousDefaultId = null;
        RefreshNow();
        SchedulePoll();
    }

    private void StopOnAudioThread()
    {
        if (_backend is null)
        {
            return;
        }
        RestoreLoweredOutput();
        _backend.DevicesChanged -= ScheduleRefresh;
        _backend.SessionCreated -= ScheduleRefresh;
        _backend.OutputLevelChanged -= ScheduleRefresh;
        _backend.Dispose();
        _backend = null;
        _lastAudible.Clear();
        Publish(MixerSnapshot.Empty);
    }

    /// <summary>Called from any thread by backend notifications; coalesces a burst into one refresh.</summary>
    private void ScheduleRefresh() =>
        _dispatcher.Post(() =>
        {
            if (_refreshScheduled || _backend is null)
            {
                return;
            }
            _refreshScheduled = true;
            var delay = MixerRoutingSupport.RefreshDelay(_lastRefreshAt, _dispatcher.Now);
            _dispatcher.PostDelayed(() =>
            {
                _refreshScheduled = false;
                RefreshNow();
            }, TimeSpan.FromSeconds(delay));
        });

    private void SchedulePoll()
    {
        if (_pollScheduled || _visibleCount == 0 || _backend is null)
        {
            return;
        }
        _pollScheduled = true;
        _dispatcher.PostDelayed(() =>
        {
            _pollScheduled = false;
            if (_visibleCount > 0 && _backend is not null)
            {
                RefreshNow();
                SchedulePoll();
            }
        }, VisiblePollInterval);
    }

    private void DrainAppVolume(string appId)
    {
        if (_pendingAppVolumes.Remove(appId, out var volume) && _backend is not null)
        {
            _backend.SetAppVolume(appId, MixerRoutingSupport.ApplicableVolume(volume));
            RefreshNow();
        }
    }

    private void DrainOutputVolume()
    {
        if (_pendingOutputVolume is not { } volume)
        {
            return;
        }
        _pendingOutputVolume = null;
        if (_backend is null || _snapshot.CurrentOutputId is not { } id)
        {
            return;
        }
        _backend.SetOutputVolume(id, volume);
        // Asking for volume means asking for sound.
        if (volume > 0 && _snapshot.OutputMuted == true)
        {
            _backend.SetOutputMuted(id, false);
        }
        RefreshNow();
    }

    private void RefreshNow()
    {
        if (_backend is not { } backend)
        {
            return;
        }
        _lastRefreshAt = _dispatcher.Now;

        var deviceInfos = backend.Devices();
        var defaultId = backend.DefaultDeviceId();
        HandleDefaultOutputChange(backend, deviceInfos, defaultId);

        var sessions = ApplySavedVolumesToNewSessions(backend, backend.Sessions());

        var hiddenMap = MixerRoutingSupport.SanitizedHiddenApps(_store.StringMap(DefaultsKey.MixerHiddenApps));
        var hiddenIds = hiddenMap.Keys.ToHashSet();
        var apps = sessions
            .Where(s => s.ExeId is not null && s.ProcessId != _ownProcessId)
            .GroupBy(s => s.ExeId!)
            .Where(g => !MixerRoutingSupport.IsHiddenFromMixer(g.Key, hiddenIds))
            .Select(g => new MixerApp(
                g.Key,
                g.Select(s => s.Name).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? g.Key,
                g.Select(s => s.ExePath).FirstOrDefault(p => p is not null),
                g.Any(s => s.IsActive),
                g.Max(s => s.Muted ? 0 : s.Volume)))
            .ToList();
        apps.Sort((a, b) => MixerRoutingSupport.DisplayOrderedBefore(a.Name, a.Id, b.Name, b.Id) ? -1
            : MixerRoutingSupport.DisplayOrderedBefore(b.Name, b.Id, a.Name, a.Id) ? 1 : 0);

        var devices = deviceInfos
            .Select(d => new MixerOutputDevice(d.Id, d.Name, d.Id == defaultId, d.IsHeadphones))
            .ToList();
        devices.Sort((a, b) => MixerRoutingSupport.DeviceDisplayOrderedBefore(a.IsDefault, a.Name, a.Id, b.IsDefault, b.Name, b.Id) ? -1
            : MixerRoutingSupport.DeviceDisplayOrderedBefore(b.IsDefault, b.Name, b.Id, a.IsDefault, a.Name, a.Id) ? 1 : 0);

        var level = defaultId is null ? null : backend.OutputLevel(defaultId);
        var hidden = hiddenMap.Select(p => new MixerHiddenApp(p.Key, p.Value)).ToList();
        Publish(new MixerSnapshot(apps, devices, defaultId, level?.Volume, level?.Muted, hidden, _switchError));
    }

    /// <summary>Upstream re-applies a saved volume the moment an app shows up with audio.</summary>
    private IReadOnlyList<AudioSessionInfo> ApplySavedVolumesToNewSessions(IAudioBackend backend, IReadOnlyList<AudioSessionInfo> sessions)
    {
        var saved = _store.DoubleMap(DefaultsKey.AppVolumes);
        var live = new HashSet<string>();
        var toApply = new HashSet<string>();
        foreach (var session in sessions)
        {
            live.Add(session.InstanceId);
            if (_seenSessions.Add(session.InstanceId)
                && session.ExeId is { } exe
                && session.ProcessId != _ownProcessId
                && saved is not null
                && saved.TryGetValue(exe, out var volume)
                && !MixerRoutingSupport.IsUnity(volume))
            {
                toApply.Add(exe);
            }
        }
        _seenSessions.IntersectWith(live);
        foreach (var exe in toApply)
        {
            backend.SetAppVolume(exe, MixerRoutingSupport.ApplicableVolume(saved![exe]));
        }
        if (toApply.Count == 0)
        {
            return sessions;
        }
        // Re-read so the published rows show the level just applied.
        var refreshed = backend.Sessions();
        foreach (var session in refreshed)
        {
            _seenSessions.Add(session.InstanceId);
        }
        return refreshed;
    }

    private void HandleDefaultOutputChange(IAudioBackend backend, IReadOnlyList<AudioDeviceInfo> devices, string? defaultId)
    {
        if (defaultId == _previousDefaultId)
        {
            return;
        }
        var current = devices.FirstOrDefault(d => d.Id == defaultId);
        var previousId = _previousDefaultId;
        var previousWasHeadphones = _previousDefaultWasHeadphones;
        _previousDefaultId = defaultId;
        _previousDefaultWasHeadphones = current?.IsHeadphones ?? false;
        if (previousId is null)
        {
            return;
        }

        if (current is { IsHeadphones: true } && _lowered is { } lowered)
        {
            // Headphones are back: put the lowered output where it was, unless the user moved it since.
            var level = backend.OutputLevel(lowered.Id);
            if (MixerRoutingSupport.ShouldRestoreOutputVolume(lowered.Applied, level?.Volume))
            {
                backend.SetOutputVolume(lowered.Id, lowered.Previous);
            }
            _lowered = null;
            _lastLoweredId = null;
            return;
        }

        var enabled = _store.Bool(DefaultsKey.MixerLowerVolumeOnHeadphonesDisconnect);
        var previousStillPresent = devices.Any(d => d.Id == previousId && d.IsHeadphones);
        if (!MixerRoutingSupport.ShouldLowerAfterHeadphonesDisconnect(
                enabled, previousWasHeadphones, previousStillPresent, current?.IsHeadphones ?? false, defaultId, _lastLoweredId)
            || defaultId is null)
        {
            return;
        }
        var percent = DefaultsSanitizers.MixerHeadphonesDisconnectVolumePercent(_store.Int(DefaultsKey.MixerHeadphonesDisconnectVolumePercent));
        var applied = percent / 100.0;
        var before = backend.OutputLevel(defaultId);
        _lastLoweredId = defaultId;
        if (before is { } b && b.Volume > applied)
        {
            backend.SetOutputVolume(defaultId, applied);
            _lowered = (defaultId, b.Volume, applied);
        }
    }

    private void RestoreLoweredOutput()
    {
        if (_backend is null || _lowered is not { } lowered)
        {
            return;
        }
        var level = _backend.OutputLevel(lowered.Id);
        if (MixerRoutingSupport.ShouldRestoreOutputVolume(lowered.Applied, level?.Volume))
        {
            _backend.SetOutputVolume(lowered.Id, lowered.Previous);
        }
        _lowered = null;
    }

    private void Publish(MixerSnapshot snapshot)
    {
        if (SnapshotsEqual(_snapshot, snapshot))
        {
            return;
        }
        _snapshot = snapshot;
        Changed?.Invoke(snapshot);
    }

    private static bool SnapshotsEqual(MixerSnapshot a, MixerSnapshot b) =>
        a.Apps.SequenceEqual(b.Apps)
        && a.Devices.SequenceEqual(b.Devices)
        && a.HiddenApps.SequenceEqual(b.HiddenApps)
        && a.CurrentOutputId == b.CurrentOutputId
        && a.OutputVolume == b.OutputVolume
        && a.OutputMuted == b.OutputMuted
        && a.OutputSwitchError == b.OutputSwitchError;

    public void Dispose()
    {
        using var done = new ManualResetEventSlim();
        _dispatcher.Post(() =>
        {
            StopOnAudioThread();
            done.Set();
        });
        done.Wait(TimeSpan.FromSeconds(2));
        _dispatcher.Dispose();
    }
}
