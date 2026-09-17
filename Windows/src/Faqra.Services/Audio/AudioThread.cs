// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of halQueue in Sources/Vorssaint/Services/Audio/AppVolumeMixer.swift: Core Audio calls can
// block when the audio service stalls, so they never run on the UI thread.

using System.Collections.Concurrent;
using System.Diagnostics;

namespace Faqra.Services.Audio;

/// <summary>One long-lived MTA thread that owns every Core Audio object the mixer creates.</summary>
public sealed class AudioThread : IAudioDispatcher
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public AudioThread()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Faqra audio" };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    public double Now => _clock.Elapsed.TotalSeconds;

    public void Post(Action action)
    {
        if (!_queue.IsAddingCompleted)
        {
            try
            {
                _queue.Add(action);
            }
            catch (InvalidOperationException)
            {
                // Shutting down between the check and the add; the work no longer matters.
            }
        }
    }

    public void PostDelayed(Action action, TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
        {
            Post(action);
            return;
        }
        Timer? timer = null;
        timer = new Timer(_ =>
        {
            timer?.Dispose();
            Post(action);
        }, null, delay, Timeout.InfiniteTimeSpan);
    }

    private void Run()
    {
        try
        {
            RunQueue();
        }
        catch (ObjectDisposedException)
        {
            // Dispose gave up waiting on a stalled call; the thread just ends.
        }
    }

    private void RunQueue()
    {
        foreach (var action in _queue.GetConsumingEnumerable())
        {
            try
            {
                action();
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
            {
                // A device or session vanished mid-call. The next notification or poll refreshes the state.
                Debug.WriteLine($"Faqra audio: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        // A Core Audio call can hang; then the background thread is left to die with the process
        // rather than having its queue disposed under it.
        if (_thread.Join(TimeSpan.FromSeconds(2)))
        {
            _queue.Dispose();
        }
    }
}
