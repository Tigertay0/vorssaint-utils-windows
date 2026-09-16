using Faqra.Core.Backup;
using Faqra.Core.Defaults;

namespace Faqra.Core.Tests;

public class SettingsBackupSupportTests
{
    [Fact]
    public void ExportKeys_ExcludeMachineStateAndIncludeUnregisteredPreferences()
    {
        var keys = SettingsBackupSupport.ExportKeys();
        Assert.Contains(DefaultsKey.Language, keys);
        Assert.Contains(DefaultsKey.HasOnboarded, keys);
        Assert.Contains(DefaultsKey.NotchEnabled, keys);
        Assert.Contains(DefaultsKey.FeatureAvailable("mixer"), keys);
        Assert.DoesNotContain(DefaultsKey.SettingsWindowWidth, keys);
        Assert.DoesNotContain(DefaultsKey.CommandBarUsage, keys);
        Assert.DoesNotContain(DefaultsKey.CommandBarFileScopes, keys); // registered yet machine-local
        Assert.DoesNotContain(DefaultsKey.MicMuteActive, keys);
    }

    [Fact]
    public void Payload_IsACompleteSnapshotWithEnvelope()
    {
        var store = DefaultsStore.InMemory();
        store.Set(DefaultsKey.Language, "de");
        store.Set(DefaultsKey.SettingsWindowWidth, 900.0);

        var payload = SettingsBackupSupport.Payload("0.1.0", store.Object);

        Assert.Equal(1L, payload[SettingsBackupSupport.FormatVersionKey]);
        Assert.Equal("0.1.0", payload[SettingsBackupSupport.AppVersionKey]);
        var settings = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(payload[SettingsBackupSupport.SettingsKey]);
        Assert.Equal("de", settings[DefaultsKey.Language]);
        Assert.Equal(true, settings[DefaultsKey.NotchEnabled]); // registered defaults are snapshotted too
        Assert.False(settings.ContainsKey(DefaultsKey.SettingsWindowWidth));
        Assert.False(settings.ContainsKey(DefaultsKey.PanelSectionOrder)); // unset, unregistered: absent
    }

    [Fact]
    public void SanitizedSettings_RejectsUnknownEnvelopesAndDropsBadEntries()
    {
        Assert.Null(SettingsBackupSupport.SanitizedSettings(new Dictionary<string, object>()));
        Assert.Null(SettingsBackupSupport.SanitizedSettings(new Dictionary<string, object>
        {
            [SettingsBackupSupport.FormatVersionKey] = 2L,
            [SettingsBackupSupport.SettingsKey] = new Dictionary<string, object>(),
        }));

        var payload = new Dictionary<string, object>
        {
            [SettingsBackupSupport.FormatVersionKey] = " 1 ",
            [SettingsBackupSupport.SettingsKey] = new Dictionary<string, object>
            {
                [DefaultsKey.NotchEnabled] = false,
                [DefaultsKey.MonitorInterval] = 5L,
                [DefaultsKey.HotkeyEnabled] = 1L,           // wrong type for a bool key
                [DefaultsKey.BatteryLimit] = true,          // wrong type for an int key
                [DefaultsKey.SettingsWindowWidth] = 900.0,  // never exported
                ["someFutureKey"] = "x",                    // unknown
                [DefaultsKey.PanelSectionOrder] = "mixer,system", // unregistered but allowed
                [DefaultsKey.NotchQuickAccessSide] = 3L,    // must be a string
            },
        };

        var settings = SettingsBackupSupport.SanitizedSettings(payload)!;

        Assert.Equal(
            [DefaultsKey.MonitorInterval, DefaultsKey.NotchEnabled, DefaultsKey.PanelSectionOrder],
            settings.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ValueLooksRight_DistinguishesBoolFromInteger()
    {
        Assert.True(SettingsBackupSupport.ValueLooksRight(DefaultsKey.HotkeyEnabled, true));
        Assert.False(SettingsBackupSupport.ValueLooksRight(DefaultsKey.HotkeyEnabled, 1L));
        Assert.True(SettingsBackupSupport.ValueLooksRight(DefaultsKey.BatteryLimit, 15L));
        Assert.False(SettingsBackupSupport.ValueLooksRight(DefaultsKey.BatteryLimit, true));
        Assert.True(SettingsBackupSupport.ValueLooksRight(DefaultsKey.MenuBarPreset, "dense"));
        Assert.False(SettingsBackupSupport.ValueLooksRight(DefaultsKey.MenuBarPreset, 4L));
        Assert.True(SettingsBackupSupport.ValueLooksRight("unregisteredKey", 42L));
    }
}
