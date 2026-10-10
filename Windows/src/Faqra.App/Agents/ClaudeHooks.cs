// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.IO;
using Faqra.Core;
using Faqra.Core.Agents.Install;

namespace Faqra.App.Agents;

/// <summary>Which hooks Claude Code's settings hold, for the island: Faqra's (installed?) and Coucou's (watch only?).</summary>
internal static class ClaudeHooks
{
    public static HookStatus Read() => Read(AppPaths.ClaudeSettingsFile);

    /// <summary>The hooks in <paramref name="path"/>; none when the file is missing or not plain UTF-8 JSON.</summary>
    public static HookStatus Read(string path)
    {
        try
        {
            return ClaudeHookConfig.Inspect(File.Exists(path) ? ConfigEdit.Decode(File.ReadAllBytes(path)) : null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ConfigFormatException)
        {
            return new HookStatus(0, 0);
        }
    }
}
