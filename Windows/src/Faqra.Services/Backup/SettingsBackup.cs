// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Services/SettingsBackup.swift. Upstream writes an XML property list;
// this port writes the same envelope as JSON, the format the rest of the app already persists.

using System.Text.Json;
using System.Text.Json.Nodes;
using Faqra.Core;
using Faqra.Core.Backup;
using Faqra.Core.Defaults;

namespace Faqra.Services.Backup;

public static class SettingsBackup
{
    public const string DefaultFileName = "Faqra Settings.json";

    /// <summary>Writes a complete snapshot: every exportable key, including untouched defaults.</summary>
    public static void Export(ISettingsStore store, string path)
    {
        var payload = SettingsBackupSupport.Payload(AppInfo.Version, store.Object);
        var root = new JsonObject
        {
            [SettingsBackupSupport.FormatVersionKey] = SettingsBackupSupport.FormatVersion,
            [SettingsBackupSupport.AppVersionKey] = AppInfo.Version,
            [SettingsBackupSupport.SettingsKey] = JsonSettingsFile.Encode(
                (IReadOnlyDictionary<string, object>)payload[SettingsBackupSupport.SettingsKey]),
        };
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Reads a backup, or null when the file is not a backup this version understands.</summary>
    public static IReadOnlyDictionary<string, object>? Read(string path)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject root)
            {
                return null;
            }
            var payload = new Dictionary<string, object>(StringComparer.Ordinal);
            if (root[SettingsBackupSupport.FormatVersionKey] is JsonValue version)
            {
                payload[SettingsBackupSupport.FormatVersionKey] = version.TryGetValue<long>(out var number)
                    ? number
                    : version.ToString();
            }
            if (root[SettingsBackupSupport.SettingsKey] is JsonObject settings)
            {
                payload[SettingsBackupSupport.SettingsKey] = (IReadOnlyDictionary<string, object>)JsonSettingsFile.Decode(settings);
            }
            return SettingsBackupSupport.SanitizedSettings(payload);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Clears every exportable key, then writes the file's. Clearing first is what makes an import
    /// a replacement rather than a merge; cleared keys fall back to registered defaults.
    /// </summary>
    public static void Apply(ISettingsStore store, IReadOnlyDictionary<string, object> settings)
    {
        foreach (var key in SettingsBackupSupport.ExportKeys())
        {
            store.Remove(key);
        }
        foreach (var (key, value) in settings)
        {
            switch (value)
            {
                case bool b: store.Set(key, b); break;
                case long l: store.Set(key, (int)l); break;
                case int i: store.Set(key, i); break;
                case double d: store.Set(key, d); break;
                case string s: store.Set(key, s); break;
                case byte[] bytes: store.Set(key, bytes); break;
                case IReadOnlyList<string> list: store.Set(key, list); break;
                case IReadOnlyDictionary<string, string> map: store.Set(key, map); break;
                case IReadOnlyDictionary<string, double> doubles: store.Set(key, doubles); break;
            }
        }
    }
}
