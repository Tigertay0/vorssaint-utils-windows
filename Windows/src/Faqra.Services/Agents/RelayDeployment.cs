// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Diagnostics;
using System.Security.Cryptography;

namespace Faqra.Services.Agents;

/// <summary>
/// Claude Code's hooks name one fixed path, so the relay shipped beside the app is copied there whenever it
/// differs. A relay that is running right now can be renamed but not overwritten, so the old one moves aside.
/// </summary>
public static class RelayDeployment
{
    public const string FileName = "faqra-hook.exe";

    /// <summary>True when a relay is in place at <paramref name="targetPath"/> afterwards.</summary>
    public static bool Ensure(string sourceDirectory, string targetPath)
    {
        var source = Path.Combine(sourceDirectory, FileName);
        if (!File.Exists(source))
        {
            return File.Exists(targetPath);
        }
        if (File.Exists(targetPath) && Same(source, targetPath))
        {
            return true;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            var fresh = targetPath + ".new";
            File.Copy(source, fresh, overwrite: true);
            if (File.Exists(targetPath))
            {
                File.Move(targetPath, targetPath + ".old", overwrite: true);
            }
            File.Move(fresh, targetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Faqra could not update {targetPath}: {ex.Message}");
        }
        return File.Exists(targetPath);
    }

    private static bool Same(string a, string b)
    {
        if (new FileInfo(a).Length != new FileInfo(b).Length)
        {
            return false;
        }
        using var first = File.OpenRead(a);
        using var second = File.OpenRead(b);
        return SHA256.HashData(first).AsSpan().SequenceEqual(SHA256.HashData(second));
    }
}
