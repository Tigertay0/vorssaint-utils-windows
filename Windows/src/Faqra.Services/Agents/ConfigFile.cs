// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Preview, fingerprint and backup follow Coucou's config_file.rs (MIT, Copyright (c) 2026 Louis Raillé).

using System.Text;
using Faqra.Core.Agents.Install;

namespace Faqra.Services.Agents;

/// <summary>An edit to a config file, worked out but not written.</summary>
public sealed record ConfigPreview(
    string Path, bool Exists, string Fingerprint, string Before, string After,
    IReadOnlyList<UnifiedDiff.DiffLine> Diff, string? BackupPath)
{
    public bool Changes => Before != After;
}

/// <summary>The file changed between the preview and the click, so the preview no longer describes it.</summary>
public sealed class ConfigChangedException() : Exception("The file changed since it was previewed.");

public static class ConfigFile
{
    public static ConfigPreview Preview(string path, Func<string?, string> transform, DateTime now)
    {
        var exists = File.Exists(path);
        var bytes = exists ? File.ReadAllBytes(path) : [];
        var before = exists ? ConfigEdit.Decode(bytes) : null;
        var after = transform(before);
        var text = before ?? string.Empty;
        return new ConfigPreview(path, exists, ConfigEdit.Fingerprint(bytes), text, after,
            UnifiedDiff.Lines(text, after), exists ? ConfigEdit.BackupPath(path, now, File.Exists) : null);
    }

    /// <summary>Writes the previewed text, keeping the old file byte for byte as the backup.</summary>
    public static void Apply(ConfigPreview preview)
    {
        var exists = File.Exists(preview.Path);
        var bytes = exists ? File.ReadAllBytes(preview.Path) : [];
        if (exists != preview.Exists || ConfigEdit.Fingerprint(bytes) != preview.Fingerprint)
        {
            throw new ConfigChangedException();
        }
        Directory.CreateDirectory(Path.GetDirectoryName(preview.Path)!);
        var temp = preview.Path + ".faqra-tmp";
        File.WriteAllText(temp, preview.After, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (exists)
        {
            File.Replace(temp, preview.Path, preview.BackupPath);
        }
        else
        {
            File.Move(temp, preview.Path);
        }
    }
}
