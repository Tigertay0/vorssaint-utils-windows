// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Backup naming follows Coucou's config_file.rs (MIT, Copyright (c) 2026 Louis Raillé).

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Faqra.Core.Agents.Install;

public static class ConfigEdit
{
    /// <summary>Identifies the exact bytes a preview was made from, so a later change is noticed before writing.</summary>
    public static string Fingerprint(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary><c>settings.json.bak-20261009-173005</c>, then <c>-1</c>, <c>-2</c> while the name is taken.</summary>
    public static string BackupPath(string path, DateTime now, Func<string, bool> exists)
    {
        var stem = $"{path}.bak-{now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}";
        if (!exists(stem))
        {
            return stem;
        }
        for (var i = 1; ; i++)
        {
            var candidate = $"{stem}-{i}";
            if (!exists(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>Decodes UTF-8 strictly: bytes that are not valid UTF-8 are refused rather than replaced, so a lossy copy is never written back.</summary>
    public static string Decode(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            throw new ConfigFormatException("the file is not valid UTF-8");
        }
    }
}
