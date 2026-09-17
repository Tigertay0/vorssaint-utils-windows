// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the CoreAudio HAL reads and writes in Sources/Vorssaint/Services/Audio/AppVolumeMixer.swift
// (readSnapshot 971-1122, outputDevices 1600-1659, setDefaultDevice 1721-1733, setSystemOutputVolume 1764-1795).

namespace Faqra.Services.Audio;

public sealed record AudioDeviceInfo(string Id, string Name, bool IsHeadphones);

/// <summary>One WASAPI audio session. <see cref="ExeId"/> is null for the system sounds session.</summary>
public sealed record AudioSessionInfo(
    string InstanceId,
    string DeviceId,
    int ProcessId,
    string? ExeId,
    string? ExePath,
    string Name,
    bool IsActive,
    double Volume,
    bool Muted);

public readonly record struct OutputLevel(double Volume, bool Muted);

/// <summary>
/// Everything the mixer asks of Windows. Every member is called on the mixer's audio thread; events may
/// be raised on any thread.
/// </summary>
public interface IAudioBackend : IDisposable
{
    /// <summary>Active render endpoints.</summary>
    IReadOnlyList<AudioDeviceInfo> Devices();

    string? DefaultDeviceId();

    /// <summary>Every live (not expired) session on every active render endpoint.</summary>
    IReadOnlyList<AudioSessionInfo> Sessions();

    /// <summary>Sets the volume of every session of one executable; a volume above zero also unmutes.</summary>
    void SetAppVolume(string exeId, double volume);

    OutputLevel? OutputLevel(string deviceId);

    void SetOutputVolume(string deviceId, double volume);

    void SetOutputMuted(string deviceId, bool muted);

    /// <summary>Makes the endpoint Windows' default output. Returns the HRESULT.</summary>
    int SetDefaultOutput(string deviceId);

    /// <summary>Devices appeared, disappeared, or the default changed.</summary>
    event Action? DevicesChanged;

    /// <summary>An app opened a new audio session.</summary>
    event Action? SessionCreated;

    /// <summary>The default output's volume or mute changed.</summary>
    event Action? OutputLevelChanged;
}

/// <summary>Where the mixer runs its Core Audio work. Tests run it inline.</summary>
public interface IAudioDispatcher : IDisposable
{
    void Post(Action action);

    void PostDelayed(Action action, TimeSpan delay);

    /// <summary>Seconds on a monotonic clock.</summary>
    double Now { get; }
}
