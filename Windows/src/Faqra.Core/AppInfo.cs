// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/AppInfo.swift (Copyright (C) 2026 Vorssaint)

using System.Reflection;

namespace Faqra.Core;

/// <summary>Static identity of the app, shared by UI, notifications and tooling.</summary>
public static class AppInfo
{
    public const string Name = "Faqra";
    public const string Copyright = "© 2026 Faqra contributors";
    public const string UpstreamCredit = "A Windows re-implementation of Vorssaint, GPL-3.0-or-later.";

    public static readonly Uri RepositoryUrl = new("https://github.com/Tigertay0/vorssaint-utils-windows");
    public static readonly Uri UpstreamRepositoryUrl = new("https://github.com/vorssaint/vorssaint-utils");

    private static readonly Lazy<string> VersionValue = new(ReadVersion);

    /// <summary>The informational version stamped at build time, or "dev" for a bare build.</summary>
    public static string Version => VersionValue.Value;

    /// <summary>True when the current version is a pre-release (e.g. 0.3.4-beta.1 or 0.3.4-rc.1).</summary>
    public static bool IsBeta => IsPreRelease(Version);

    public static bool IsPreRelease(string version)
    {
        var lowered = version.ToLowerInvariant();
        return lowered.Contains("-beta") || lowered.Contains("-rc") || lowered.Contains("-alpha");
    }

    private static string ReadVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppInfo).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
        {
            return "dev";
        }
        // Strip SourceLink build metadata ("1.2.3+abcdef") so the UI shows a clean version.
        var plus = informational.IndexOf('+');
        return plus > 0 ? informational[..plus] : informational;
    }
}
