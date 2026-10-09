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
    public static bool Ensure(string sourceDirectory, string targetPath) =>
        Ensure(sourceDirectory, targetPath, (from, to) => File.Move(from, to));

    /// <summary>Same as the public overload; <paramref name="place"/> moves the fresh copy into place (a test seam).</summary>
    internal static bool Ensure(string sourceDirectory, string targetPath, Action<string, string> place)
    {
        var source = Path.Combine(sourceDirectory, FileName);
        if (!File.Exists(source))
        {
            return File.Exists(targetPath);
        }
        var fresh = targetPath + ".new";
        try
        {
            if (File.Exists(targetPath) && Same(source, targetPath))
            {
                return true;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(source, fresh, overwrite: true);
            var aside = MoveAside(targetPath);
            try
            {
                place(fresh, targetPath);
            }
            catch
            {
                if (aside is not null && !File.Exists(targetPath))
                {
                    File.Move(aside, targetPath);
                }
                throw;
            }
            DeleteStale(targetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Faqra could not update {targetPath}: {ex.Message}");
            TryDelete(fresh);
        }
        return File.Exists(targetPath);
    }

    /// <summary>Renames a relay that may be running to a name of its own, so it never blocks the next update.</summary>
    private static string? MoveAside(string targetPath)
    {
        if (!File.Exists(targetPath))
        {
            return null;
        }
        var aside = $"{targetPath}.old-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        File.Move(targetPath, aside, overwrite: true);
        return aside;
    }

    private static void DeleteStale(string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        var name = Path.GetFileName(targetPath);
        foreach (var stale in Directory.EnumerateFiles(directory, name + ".old*"))
        {
            TryDelete(stale);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Still running or locked: a later update removes it.
            Trace.TraceInformation($"Faqra left {path} in place: {ex.Message}");
        }
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
