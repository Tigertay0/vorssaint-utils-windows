// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of Sources/NowPlayingAdapter in upstream, which exists only to work around
// MediaRemote's code-signature check. Windows exposes the same information publicly through
// GlobalSystemMediaTransportControls, so the adapter concept disappears.

using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Faqra.Services.Media;

/// <summary>What the system reports about the track playing right now.</summary>
public sealed record NowPlaying(string Title, string Artist, bool IsPlaying, byte[]? Artwork)
{
    public bool HasTrack => !string.IsNullOrWhiteSpace(Title);
}

/// <summary>
/// Reads and controls the system's current media session. Every call is guarded: no session, a
/// session that refuses a property, and WinRT throwing on a session that just vanished are all
/// normal and must leave the island showing its idle state rather than an error.
/// </summary>
public sealed class NowPlayingService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;

    /// <summary>Raised when the track or its playback state changed.</summary>
    public event Action? Changed;

    public NowPlaying Current { get; private set; } = new(string.Empty, string.Empty, false, null);

    public async Task StartAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += OnSessionChanged;
            AttachSession();
            await RefreshAsync();
        }
        catch (Exception)
        {
            // No media session support on this machine; the island falls back to its battery or blank idle.
        }
    }

    public async Task RefreshAsync()
    {
        var session = _session;
        if (session is null)
        {
            Current = new NowPlaying(string.Empty, string.Empty, false, null);
            Changed?.Invoke();
            return;
        }
        try
        {
            var properties = await session.TryGetMediaPropertiesAsync();
            var playback = session.GetPlaybackInfo();
            var artwork = await ReadThumbnailAsync(properties?.Thumbnail);
            Current = new NowPlaying(
                properties?.Title ?? string.Empty,
                properties?.Artist ?? string.Empty,
                playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                artwork);
        }
        catch (Exception)
        {
            Current = new NowPlaying(string.Empty, string.Empty, false, null);
        }
        Changed?.Invoke();
    }

    public Task<bool> TogglePlayPauseAsync() => TryAsync(session => session.TryTogglePlayPauseAsync());

    public Task<bool> NextAsync() => TryAsync(session => session.TrySkipNextAsync());

    public Task<bool> PreviousAsync() => TryAsync(session => session.TrySkipPreviousAsync());

    private async Task<bool> TryAsync(Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> action)
    {
        var session = _session;
        if (session is null)
        {
            return false;
        }
        try
        {
            return await action(session);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<byte[]?> ReadThumbnailAsync(IRandomAccessStreamReference? reference)
    {
        if (reference is null)
        {
            return null;
        }
        try
        {
            using var stream = await reference.OpenReadAsync();
            var buffer = new Windows.Storage.Streams.Buffer((uint)stream.Size);
            await stream.ReadAsync(buffer, (uint)stream.Size, InputStreamOptions.None);
            using var reader = DataReader.FromBuffer(buffer);
            var bytes = new byte[buffer.Length];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void OnSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, object args)
    {
        AttachSession();
        _ = RefreshAsync();
    }

    private void AttachSession()
    {
        DetachSession();
        try
        {
            _session = _manager?.GetCurrentSession();
            if (_session is not null)
            {
                _session.MediaPropertiesChanged += OnMediaChanged;
                _session.PlaybackInfoChanged += OnPlaybackChanged;
            }
        }
        catch (Exception)
        {
            _session = null;
        }
    }

    private void DetachSession()
    {
        if (_session is null)
        {
            return;
        }
        try
        {
            _session.MediaPropertiesChanged -= OnMediaChanged;
            _session.PlaybackInfoChanged -= OnPlaybackChanged;
        }
        catch (Exception)
        {
            // The session is already gone; nothing to detach from.
        }
        _session = null;
    }

    private void OnMediaChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) =>
        _ = RefreshAsync();

    private void OnPlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) =>
        _ = RefreshAsync();

    public void Dispose()
    {
        DetachSession();
        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= OnSessionChanged;
            _manager = null;
        }
    }
}
