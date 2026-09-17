// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/Audio/MixerRoutingSupport.swift and the volume bookkeeping of
// AppVolumeMixer.swift (setVolume 537-550, toggleMute 668-675, persistVolume 1486-1504, listener
// throttle 423-442, headphone disconnect 1169-1228). Per-app routing, taps and boost have no Windows
// counterpart and are left out; apps are identified by executable instead of bundle id.

using System.Globalization;
using System.Text;

namespace Faqra.Core.Mixer;

public static class MixerRoutingSupport
{
    /// <summary>Upstream's cap on stored identifiers.</summary>
    private const int MaxIdLength = 512;

    private const double UnityTolerance = 0.005;

    /// <summary>Below this a row counts as silent, so muting it restores instead.</summary>
    private const double AudibleThreshold = 0.001;

    /// <summary>A burst of audio notifications inside this window becomes one trailing refresh.</summary>
    public const double ListenerRefreshInterval = 0.2;

    private static readonly string[] HeadphoneTerms =
    [
        "headphone", "headphones", "headset", "earphone", "earphones", "earbud", "earbuds", "airpod", "airpods",
        "earpod", "earpods", "galaxy buds", "pixel buds", "beats", "bose qc", "sony wh", "sony wf", "jabra", "soundcore",
    ];

    /// <summary>A volume the UI would show as 100% is exactly 100% everywhere.</summary>
    public static bool IsUnity(double volume) => Math.Abs(volume - 1) < UnityTolerance;

    public static bool ShouldShowApp(bool isPlaying, double volume, bool hideInactiveApps) =>
        !hideInactiveApps || isPlaying || !IsUnity(volume);

    /// <summary>"75", " 75% " or "33,5" as a 0…max/100 fraction; null when the text is not a number.</summary>
    public static double? VolumeFraction(string text, int maximumPercent)
    {
        if (maximumPercent < 0)
        {
            return null;
        }
        var trimmed = text.Trim();
        if (trimmed.EndsWith('%'))
        {
            trimmed = trimmed[..^1].Trim();
        }
        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        if (separator != ".")
        {
            trimmed = trimmed.Replace(separator, ".", StringComparison.Ordinal);
        }
        if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) || !double.IsFinite(percent))
        {
            return null;
        }
        return Math.Clamp(percent, 0, maximumPercent) / 100;
    }

    /// <summary>Trimmed, non-empty, at most 512 characters, no control characters; else null.</summary>
    public static string? SanitizedId(string? raw)
    {
        var trimmed = raw?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxIdLength || trimmed.Any(char.IsControl))
        {
            return null;
        }
        return trimmed;
    }

    public static bool OutputLooksLikeHeadphones(string name, string id)
    {
        var folded = Fold($"{name} {id}");
        return HeadphoneTerms.Any(term => folded.Contains(term, StringComparison.Ordinal));
    }

    public static IReadOnlyDictionary<string, string> SanitizedHiddenApps(IReadOnlyDictionary<string, string>? raw)
    {
        var result = new Dictionary<string, string>();
        foreach (var (key, value) in raw ?? new Dictionary<string, string>())
        {
            if (SanitizedId(key) is { } id && SanitizedId(value) is { } name)
            {
                result[id] = name;
            }
        }
        return result;
    }

    public static bool IsHiddenFromMixer(string? persistenceId, IReadOnlySet<string> hiddenIds) =>
        persistenceId is not null && hiddenIds.Contains(persistenceId);

    public static bool DisplayOrderedBefore(string name, string id, string otherName, string otherId)
    {
        var order = string.Compare(name, otherName, CultureInfo.CurrentCulture, CompareOptions.IgnoreCase);
        return order != 0 ? order < 0 : string.CompareOrdinal(id, otherId) < 0;
    }

    public static bool DeviceDisplayOrderedBefore(bool isDefault, string name, string id, bool otherIsDefault, string otherName, string otherId) =>
        isDefault != otherIsDefault ? isDefault : DisplayOrderedBefore(name, id, otherName, otherId);

    /// <summary>Restores a lowered output only if the user has not changed its volume since.</summary>
    public static bool ShouldRestoreOutputVolume(double appliedVolume, double? currentVolume) =>
        currentVolume is { } current && Math.Abs(current - appliedVolume) < UnityTolerance;

    /// <summary>
    /// The lower-cased executable name inside a WASAPI session identifier
    /// ("{device}|\Device\...\Spotify.exe%b{guid}"), or null for system sessions ("#%b{guid}").
    /// </summary>
    public static string? ExecutableId(string sessionIdentifier)
    {
        var bar = sessionIdentifier.IndexOf('|');
        var path = bar >= 0 ? sessionIdentifier[(bar + 1)..] : sessionIdentifier;
        var marker = path.IndexOf("%b", StringComparison.Ordinal);
        if (marker >= 0)
        {
            path = path[..marker];
        }
        if (path.Length == 0 || path.StartsWith('#'))
        {
            return null;
        }
        var slash = path.LastIndexOfAny(['\\', '/']);
        return SanitizedId(path[(slash + 1)..].ToLowerInvariant());
    }

    /// <summary>
    /// The name an app declares for its session, else its file description, else its process name.
    /// Names that point into a resource DLL ("@%SystemRoot%\...,-202") are not names.
    /// </summary>
    public static string DisplayName(string? sessionName, string? fileDescription, string processName)
    {
        if (!string.IsNullOrWhiteSpace(sessionName) && !sessionName.TrimStart().StartsWith('@'))
        {
            return sessionName.Trim();
        }
        return !string.IsNullOrWhiteSpace(fileDescription) ? fileDescription.Trim() : processName;
    }

    /// <summary>The saved volumes after one app changes; 100% is never stored, so the key goes.</summary>
    public static IReadOnlyDictionary<string, double> VolumesAfterSet(IReadOnlyDictionary<string, double> saved, string persistenceId, double volume)
    {
        var sanitized = Defaults.DefaultsSanitizers.AppVolume(volume);
        var next = new Dictionary<string, double>(saved);
        if (IsUnity(sanitized))
        {
            next.Remove(persistenceId);
        }
        else
        {
            next[persistenceId] = sanitized;
        }
        return next;
    }

    /// <summary>What a stored volume plays at on Windows, where a session cannot go above 100%.</summary>
    public static double ApplicableVolume(double stored) => Math.Min(Defaults.DefaultsSanitizers.AppVolume(stored), 1);

    /// <summary>Mutes an audible row, remembering its level; unmutes a silent one to what it had.</summary>
    public static (double Volume, double? LastAudible) ToggleMute(double volume, double? lastAudible) =>
        volume > AudibleThreshold ? (0, volume) : (lastAudible ?? 1, lastAudible);

    /// <summary>
    /// How long to wait before refreshing after a notification: at once when the last refresh is a
    /// full window old (or the clock went backwards), else the rest of the window.
    /// </summary>
    public static double RefreshDelay(double? lastRefreshAt, double now, double window = ListenerRefreshInterval)
    {
        if (lastRefreshAt is not { } last)
        {
            return 0;
        }
        var elapsed = now - last;
        return elapsed < 0 || elapsed >= window ? 0 : window - elapsed;
    }

    public static bool ShouldLowerAfterHeadphonesDisconnect(
        bool enabled,
        bool previousWasHeadphones,
        bool previousStillPresent,
        bool newDefaultIsHeadphones,
        string? newDefaultId,
        string? lastLoweredId) =>
        enabled
        && previousWasHeadphones
        && !previousStillPresent
        && !newDefaultIsHeadphones
        && newDefaultId is not null
        && newDefaultId != lastLoweredId;

    /// <summary>Case- and accent-folded, with every run of non-alphanumerics collapsed to one space.</summary>
    private static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasSpace = false;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }
        return builder.ToString();
    }
}
