using Faqra.Core.Mixer;

namespace Faqra.Core.Tests;

// Ported from Tests/MetricsTests.swift lines 7492-8147 (MixerRoutingSupport), with the Windows
// adaptations named on each test: apps are keyed by executable, volume stops at 100%, no Finder row.
public class MixerRoutingSupportTests
{
    [Theory]
    [InlineData("40", 200, 0.4)]
    [InlineData(" 75% ", 100, 0.75)]
    [InlineData("250", 200, 2.0)]
    [InlineData("-10", 100, 0.0)]
    [InlineData("33.5", 100, 0.335)]
    public void VolumeFraction_ParsesAndClamps(string text, int max, double expected) =>
        Assert.Equal(expected, MixerRoutingSupport.VolumeFraction(text, max)!.Value, 6);

    [Theory]
    [InlineData("loud")]
    [InlineData("nan")]
    [InlineData("")]
    public void VolumeFraction_RejectsNonNumbers(string text) =>
        Assert.Null(MixerRoutingSupport.VolumeFraction(text, 100));

    [Fact]
    public void VolumeFraction_RejectsNegativeMaximum() =>
        Assert.Null(MixerRoutingSupport.VolumeFraction("10", -1));

    [Theory]
    [InlineData(1.0, true)]
    [InlineData(0.996, true)]
    [InlineData(1.004, true)]
    [InlineData(0.99, false)]
    public void IsUnity_ToleratesWhatRoundsTo100(double volume, bool expected) =>
        Assert.Equal(expected, MixerRoutingSupport.IsUnity(volume));

    [Fact]
    public void ShouldShowApp_FilterChangesNothingUntilEnabled() =>
        Assert.True(MixerRoutingSupport.ShouldShowApp(isPlaying: false, volume: 1, hideInactiveApps: false));

    [Fact]
    public void ShouldShowApp_IdleUncustomizedAppCanBeHidden() =>
        Assert.False(MixerRoutingSupport.ShouldShowApp(isPlaying: false, volume: 1, hideInactiveApps: true));

    [Fact]
    public void ShouldShowApp_PlayingAppStaysVisible() =>
        Assert.True(MixerRoutingSupport.ShouldShowApp(isPlaying: true, volume: 1, hideInactiveApps: true));

    [Fact]
    public void ShouldShowApp_CustomVolumeKeepsInactiveAppVisible() =>
        Assert.True(MixerRoutingSupport.ShouldShowApp(isPlaying: false, volume: 0.75, hideInactiveApps: true));

    [Theory]
    [InlineData(" Speakers ", "Speakers")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("bad\nuid", null)]
    public void SanitizedId_TrimsAndRejectsControlCharacters(string raw, string? expected) =>
        Assert.Equal(expected, MixerRoutingSupport.SanitizedId(raw));

    [Fact]
    public void SanitizedId_RejectsOverlongIds() =>
        Assert.Null(MixerRoutingSupport.SanitizedId(new string('a', 513)));

    [Theory]
    [InlineData("AirPods Pro", "", true)]
    [InlineData("Sony WH-1000XM5", "", true)]
    [InlineData("Headphones (Oculus Virtual Audio Device)", "", true)]
    [InlineData("MacBook Pro Speakers", "BuiltInSpeakerDevice", false)]
    [InlineData("JBL Flip", "", false)]
    [InlineData("Speakers (High Definition Audio Device)", "{0.0.0.00000000}.{c14e}", false)]
    public void OutputLooksLikeHeadphones_MatchesNames(string name, string id, bool expected) =>
        Assert.Equal(expected, MixerRoutingSupport.OutputLooksLikeHeadphones(name, id));

    [Fact]
    public void SanitizedHiddenApps_KeepsOnlyRealEntries()
    {
        var raw = new Dictionary<string, string>
        {
            ["spotify.exe"] = "Spotify",
            [""] = "Nameless",
            ["silent.exe"] = "",
            ["bad\nid"] = "Broken",
        };
        Assert.Equal(new Dictionary<string, string> { ["spotify.exe"] = "Spotify" }, MixerRoutingSupport.SanitizedHiddenApps(raw));
    }

    [Fact]
    public void IsHiddenFromMixer_HiddenAppStaysOut() =>
        Assert.True(MixerRoutingSupport.IsHiddenFromMixer("spotify.exe", new HashSet<string> { "spotify.exe" }));

    [Fact]
    public void IsHiddenFromMixer_OtherAppsStayVisible() =>
        Assert.False(MixerRoutingSupport.IsHiddenFromMixer("chrome.exe", new HashSet<string> { "spotify.exe" }));

    [Fact]
    public void IsHiddenFromMixer_RowWithNothingToRememberIsAlwaysListed() =>
        Assert.False(MixerRoutingSupport.IsHiddenFromMixer(null, new HashSet<string> { "spotify.exe" }));

    [Fact]
    public void DisplayOrder_ByNameCaseInsensitive() =>
        Assert.True(MixerRoutingSupport.DisplayOrderedBefore("Music", "music.exe", "Safari", "safari.exe"));

    [Fact]
    public void DisplayOrder_EqualNamesOrderDeterministicallyById()
    {
        Assert.True(MixerRoutingSupport.DisplayOrderedBefore("safari", "a", "Safari", "b"));
        Assert.False(MixerRoutingSupport.DisplayOrderedBefore("Safari", "b", "safari", "a"));
    }

    [Fact]
    public void DeviceOrder_DefaultFirst() =>
        Assert.True(MixerRoutingSupport.DeviceDisplayOrderedBefore(true, "Zeta", "z", false, "Alpha", "a"));

    [Fact]
    public void DeviceOrder_IdenticalNamesOrderById()
    {
        Assert.True(MixerRoutingSupport.DeviceDisplayOrderedBefore(false, "AirPods Pro", "aa", false, "AirPods Pro", "bb"));
        Assert.False(MixerRoutingSupport.DeviceDisplayOrderedBefore(false, "AirPods Pro", "bb", false, "AirPods Pro", "aa"));
    }

    [Theory]
    [InlineData(0.25, 0.25, true)]
    [InlineData(0.25, 0.6, false)]
    [InlineData(0.25, null, false)]
    public void ShouldRestoreOutputVolume_OnlyWhenUserLeftItAlone(double applied, double? current, bool expected) =>
        Assert.Equal(expected, MixerRoutingSupport.ShouldRestoreOutputVolume(applied, current));

    // Windows: the executable name is the persistence id (Windows' own mixer remembers apps by executable).
    [Theory]
    [InlineData(@"{0.0.0.00000000}.{c14e}|\Device\HarddiskVolume3\Program Files\WindowsApps\SpotifyAB.SpotifyMusic_1.300.277.0_x64__zpdn\Spotify.exe%b{00000000-0000-0000-0000-000000000000}", "spotify.exe")]
    [InlineData(@"{0.0.0.00000000}.{c14e}|\Device\HarddiskVolume3\Users\Tigre\AppData\Local\Perplexity\Comet\Application\comet.exe%b{00000000-0000-0000-0000-000000000000}", "comet.exe")]
    [InlineData(@"{0.0.0.00000000}.{c14e}|#%b{A9EF3FD9-4240-455E-A4D5-F2B3301887B2}", null)]
    [InlineData("", null)]
    public void ExecutableId_ComesFromTheSessionIdentifier(string sessionIdentifier, string? expected) =>
        Assert.Equal(expected, MixerRoutingSupport.ExecutableId(sessionIdentifier));

    [Theory]
    [InlineData("Spotify", "Spotify", "Spotify", "Spotify")]
    [InlineData("@%SystemRoot%\\System32\\AudioSrv.Dll,-202", "Host", "svchost", "Host")]
    [InlineData("", "Google Chrome", "chrome", "Google Chrome")]
    [InlineData("", "", "Wispr Flow", "Wispr Flow")]
    [InlineData("  ", null, "comet", "comet")]
    public void DisplayName_PrefersSessionThenDescriptionThenProcess(string session, string? description, string process, string expected) =>
        Assert.Equal(expected, MixerRoutingSupport.DisplayName(session, description, process));

    [Fact]
    public void VolumesAfterSet_StoresChangedVolume()
    {
        var saved = new Dictionary<string, double> { ["chrome.exe"] = 0.5 };
        var next = MixerRoutingSupport.VolumesAfterSet(saved, "spotify.exe", 0.35);
        Assert.Equal(0.35, next["spotify.exe"]);
        Assert.Equal(0.5, next["chrome.exe"]);
        Assert.False(saved.ContainsKey("spotify.exe"));
    }

    [Fact]
    public void VolumesAfterSet_UnityIsNeverStored()
    {
        var saved = new Dictionary<string, double> { ["spotify.exe"] = 0.4 };
        var next = MixerRoutingSupport.VolumesAfterSet(saved, "spotify.exe", 0.999);
        Assert.False(next.ContainsKey("spotify.exe"));
    }

    [Fact]
    public void VolumesAfterSet_ClampsAndSanitizes()
    {
        Assert.Equal(0.0, MixerRoutingSupport.VolumesAfterSet(new Dictionary<string, double>(), "a.exe", -1)["a.exe"]);
        Assert.False(MixerRoutingSupport.VolumesAfterSet(new Dictionary<string, double>(), "a.exe", double.NaN).ContainsKey("a.exe"));
    }

    // Windows: a session volume cannot exceed 100%, so a stored boost from a backup plays at 100%.
    [Theory]
    [InlineData(1.5, 1.0)]
    [InlineData(0.4, 0.4)]
    [InlineData(-2, 0.0)]
    [InlineData(double.PositiveInfinity, 1.0)]
    public void ApplicableVolume_CapsAt100Percent(double stored, double expected) =>
        Assert.Equal(expected, MixerRoutingSupport.ApplicableVolume(stored));

    [Fact]
    public void ToggleMute_AudibleRowMutesAndRemembers()
    {
        var (volume, remembered) = MixerRoutingSupport.ToggleMute(0.6, lastAudible: null);
        Assert.Equal(0, volume);
        Assert.Equal(0.6, remembered);
    }

    [Fact]
    public void ToggleMute_SilentRowRestoresLastAudible()
    {
        var (volume, remembered) = MixerRoutingSupport.ToggleMute(0, lastAudible: 0.6);
        Assert.Equal(0.6, volume);
        Assert.Equal(0.6, remembered);
    }

    [Fact]
    public void ToggleMute_SilentRowWithNoMemoryRestoresFull() =>
        Assert.Equal(1, MixerRoutingSupport.ToggleMute(0.0005, lastAudible: null).Volume);

    [Theory]
    [InlineData(null, 100.0, 0.0)]
    [InlineData(99.95, 100.0, 0.15)]
    [InlineData(99.5, 100.0, 0.0)]
    [InlineData(101.0, 100.0, 0.0)]
    public void RefreshDelay_CoalescesBurstsIntoOneTrailingRefresh(double? last, double now, double expected) =>
        Assert.Equal(expected, MixerRoutingSupport.RefreshDelay(last, now, window: 0.2), 6);

    [Fact]
    public void LowerOnHeadphonesDisconnect_LowersWhenHeadphonesLeave() =>
        Assert.True(MixerRoutingSupport.ShouldLowerAfterHeadphonesDisconnect(
            enabled: true, previousWasHeadphones: true, previousStillPresent: false,
            newDefaultIsHeadphones: false, newDefaultId: "speakers", lastLoweredId: null));

    [Fact]
    public void LowerOnHeadphonesDisconnect_OffByPreference() =>
        Assert.False(MixerRoutingSupport.ShouldLowerAfterHeadphonesDisconnect(
            enabled: false, previousWasHeadphones: true, previousStillPresent: false,
            newDefaultIsHeadphones: false, newDefaultId: "speakers", lastLoweredId: null));

    [Fact]
    public void LowerOnHeadphonesDisconnect_ManualSwitchWhileHeadphonesStayConnectedDoesNothing() =>
        Assert.False(MixerRoutingSupport.ShouldLowerAfterHeadphonesDisconnect(
            enabled: true, previousWasHeadphones: true, previousStillPresent: true,
            newDefaultIsHeadphones: false, newDefaultId: "speakers", lastLoweredId: null));

    [Fact]
    public void LowerOnHeadphonesDisconnect_NeverLowersTheSameOutputTwice() =>
        Assert.False(MixerRoutingSupport.ShouldLowerAfterHeadphonesDisconnect(
            enabled: true, previousWasHeadphones: true, previousStillPresent: false,
            newDefaultIsHeadphones: false, newDefaultId: "speakers", lastLoweredId: "speakers"));

    [Fact]
    public void LowerOnHeadphonesDisconnect_SwitchToOtherHeadphonesDoesNothing() =>
        Assert.False(MixerRoutingSupport.ShouldLowerAfterHeadphonesDisconnect(
            enabled: true, previousWasHeadphones: true, previousStillPresent: false,
            newDefaultIsHeadphones: true, newDefaultId: "buds", lastLoweredId: null));
}
