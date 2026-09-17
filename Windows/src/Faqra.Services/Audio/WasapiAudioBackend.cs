// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the CoreAudio HAL plumbing in Sources/Vorssaint/Services/Audio/AppVolumeMixer.swift
// (property listeners 185-218, readSnapshot 971-1122, outputDevices 1600-1692, output volume 1764-1801)
// over Windows' Core Audio: MMDevice endpoints, IAudioSessionManager2 sessions and IAudioEndpointVolume.

using System.Diagnostics;
using Faqra.Core.Mixer;
using Faqra.Win32.Audio;
using Faqra.Win32.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Faqra.Services.Audio;

/// <summary>Core Audio for the mixer. Create, call and dispose on the mixer's MTA audio thread only.</summary>
public sealed class WasapiAudioBackend : IAudioBackend, IMMNotificationClient
{
    private const int FormFactorHeadphones = 3;
    private const int FormFactorHeadset = 5;
    private static readonly PropertyKey FormFactorKey = new(new Guid("1da5d803-d492-4edd-8c23-e0c0ffee7f0e"), 0);

    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly Dictionary<string, (MMDevice Device, AudioSessionManager Sessions)> _devices = [];
    private readonly Dictionary<string, string> _namesByPath = new(StringComparer.OrdinalIgnoreCase);
    private MMDevice? _levelDevice;
    private bool _devicesStale = true;

    public WasapiAudioBackend()
    {
        _enumerator.RegisterEndpointNotificationCallback(this);
    }

    public event Action? DevicesChanged;

    public event Action? SessionCreated;

    public event Action? OutputLevelChanged;

    public IReadOnlyList<AudioDeviceInfo> Devices()
    {
        SyncDevices();
        return _devices.Values
            .Select(d => new AudioDeviceInfo(d.Device.ID, d.Device.FriendlyName, IsHeadphones(d.Device)))
            .ToList();
    }

    public string? DefaultDeviceId()
    {
        if (!_enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
        {
            return null;
        }
        using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        return device.ID;
    }

    public IReadOnlyList<AudioSessionInfo> Sessions()
    {
        SyncDevices();
        var result = new List<AudioSessionInfo>();
        foreach (var (deviceId, entry) in _devices)
        {
            entry.Sessions.RefreshSessions();
            var sessions = entry.Sessions.Sessions;
            for (var i = 0; i < sessions.Count; i++)
            {
                using var session = sessions[i];
                if (session.State == AudioSessionState.AudioSessionStateExpired || session.IsSystemSoundsSession)
                {
                    continue;
                }
                var exeId = MixerRoutingSupport.ExecutableId(session.GetSessionIdentifier);
                var pid = (int)session.GetProcessID;
                var path = exeId is null ? null : ProcessImage.PathOf(pid);
                var volume = session.SimpleAudioVolume;
                result.Add(new AudioSessionInfo(
                    session.GetSessionInstanceIdentifier,
                    deviceId,
                    pid,
                    exeId,
                    path,
                    NameFor(session.DisplayName, path, pid, exeId),
                    session.State == AudioSessionState.AudioSessionStateActive,
                    volume.Volume,
                    volume.Mute));
            }
        }
        return result;
    }

    public void SetAppVolume(string exeId, double volume)
    {
        SyncDevices();
        var level = (float)Math.Clamp(volume, 0, 1);
        foreach (var entry in _devices.Values)
        {
            entry.Sessions.RefreshSessions();
            var sessions = entry.Sessions.Sessions;
            for (var i = 0; i < sessions.Count; i++)
            {
                using var session = sessions[i];
                if (session.State == AudioSessionState.AudioSessionStateExpired
                    || MixerRoutingSupport.ExecutableId(session.GetSessionIdentifier) != exeId)
                {
                    continue;
                }
                var simple = session.SimpleAudioVolume;
                simple.Volume = level;
                if (level > 0 && simple.Mute)
                {
                    simple.Mute = false;
                }
            }
        }
    }

    public OutputLevel? OutputLevel(string deviceId)
    {
        var device = LevelDevice(deviceId);
        if (device is null)
        {
            return null;
        }
        var endpoint = device.AudioEndpointVolume;
        return new OutputLevel(endpoint.MasterVolumeLevelScalar, endpoint.Mute);
    }

    public void SetOutputVolume(string deviceId, double volume)
    {
        if (LevelDevice(deviceId) is { } device)
        {
            device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)Math.Clamp(volume, 0, 1);
        }
    }

    public void SetOutputMuted(string deviceId, bool muted)
    {
        if (LevelDevice(deviceId) is { } device)
        {
            device.AudioEndpointVolume.Mute = muted;
        }
    }

    public int SetDefaultOutput(string deviceId) => PolicyConfig.SetDefaultOutput(deviceId);

    /// <summary>The device whose level is read and watched: the one asked for, subscribed once.</summary>
    private MMDevice? LevelDevice(string deviceId)
    {
        if (_levelDevice?.ID == deviceId)
        {
            return _levelDevice;
        }
        if (_levelDevice is not null)
        {
            _levelDevice.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification;
            _levelDevice.Dispose();
            _levelDevice = null;
        }
        try
        {
            var device = _enumerator.GetDevice(deviceId);
            device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotification;
            _levelDevice = device;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return null;
        }
        return _levelDevice;
    }

    private void OnVolumeNotification(AudioVolumeNotificationData data) => OutputLevelChanged?.Invoke();

    /// <summary>Keeps one session manager per active endpoint, so new-session notifications keep arriving.</summary>
    private void SyncDevices()
    {
        if (!_devicesStale)
        {
            return;
        }
        _devicesStale = false;
        var active = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
        var activeIds = active.Select(d => d.ID).ToHashSet();
        foreach (var id in _devices.Keys.Where(id => !activeIds.Contains(id)).ToList())
        {
            Release(_devices[id]);
            _devices.Remove(id);
        }
        foreach (var device in active)
        {
            if (_devices.ContainsKey(device.ID))
            {
                device.Dispose();
                continue;
            }
            var manager = device.AudioSessionManager;
            manager.OnSessionCreated += OnSessionCreated;
            _devices[device.ID] = (device, manager);
        }
    }

    private void OnSessionCreated(object sender, IAudioSessionControl newSession) => SessionCreated?.Invoke();

    private string NameFor(string? sessionName, string? path, int pid, string? exeId)
    {
        if (path is not null && _namesByPath.TryGetValue(path, out var cached) && string.IsNullOrWhiteSpace(sessionName))
        {
            return cached;
        }
        string? description = null;
        if (path is not null)
        {
            try
            {
                description = FileVersionInfo.GetVersionInfo(path).FileDescription;
            }
            catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException or IOException)
            {
                description = null;
            }
        }
        var processName = path is not null ? Path.GetFileNameWithoutExtension(path) : exeId is not null ? Path.GetFileNameWithoutExtension(exeId) : $"pid {pid}";
        var name = MixerRoutingSupport.DisplayName(sessionName, description, processName);
        if (path is not null)
        {
            _namesByPath[path] = name;
        }
        return name;
    }

    private static bool IsHeadphones(MMDevice device)
    {
        try
        {
            if (device.Properties.Contains(FormFactorKey)
                && device.Properties[FormFactorKey].Value is uint formFactor
                && formFactor is FormFactorHeadphones or FormFactorHeadset)
            {
                return true;
            }
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Fall back to the name.
        }
        return MixerRoutingSupport.OutputLooksLikeHeadphones(device.FriendlyName, device.ID);
    }

    private void Release((MMDevice Device, AudioSessionManager Sessions) entry)
    {
        entry.Sessions.OnSessionCreated -= OnSessionCreated;
        entry.Sessions.Dispose();
        entry.Device.Dispose();
    }

    // IMMNotificationClient: raised on a Core Audio worker thread.
    void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState) => MarkDevicesChanged();

    void IMMNotificationClient.OnDeviceAdded(string pwstrDeviceId) => MarkDevicesChanged();

    void IMMNotificationClient.OnDeviceRemoved(string deviceId) => MarkDevicesChanged();

    void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow == DataFlow.Render)
        {
            DevicesChanged?.Invoke();
        }
    }

    void IMMNotificationClient.OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
    }

    private void MarkDevicesChanged()
    {
        // Read on the audio thread at the next refresh; a bool write is atomic.
        _devicesStale = true;
        DevicesChanged?.Invoke();
    }

    public void Dispose()
    {
        _enumerator.UnregisterEndpointNotificationCallback(this);
        foreach (var entry in _devices.Values)
        {
            Release(entry);
        }
        _devices.Clear();
        if (_levelDevice is not null)
        {
            _levelDevice.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification;
            _levelDevice.Dispose();
            _levelDevice = null;
        }
        _enumerator.Dispose();
    }
}
