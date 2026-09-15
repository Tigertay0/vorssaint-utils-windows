// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Support/SelfTest.swift

using Faqra.Core;
using Faqra.Win32.Display;
using Faqra.Win32.Power;

namespace Faqra.Services.Diagnostics;

/// <summary>Collects failures (core capabilities) and warnings (hardware-dependent readings).</summary>
public sealed class SelfTestContext
{
    public List<string> Failures { get; } = new();
    public List<string> Warnings { get; } = new();

    public void Fail(string what) => Failures.Add(what);

    public void Warn(string what) => Warnings.Add(what);
}

public delegate void SelfTestCheck(SelfTestContext context);

public sealed record SelfTestReport(IReadOnlyList<string> Failures, IReadOnlyList<string> Warnings)
{
    public bool Passed => Failures.Count == 0;
}

/// <summary>Quick subsystem check, run with <c>Faqra --selftest</c>. Gates the release pipeline.</summary>
public static class SelfTest
{
    private static readonly SelfTestCheck[] CoreChecks =
    [
        ExecutionStateRoundTrip,
        UptimeReading,
        SettingsDirectoryRoundTrip,
    ];

    public static SelfTestReport Run(IEnumerable<SelfTestCheck>? extraChecks = null)
    {
        var context = new SelfTestContext();
        foreach (var check in CoreChecks.Concat(extraChecks ?? []))
        {
            try
            {
                check(context);
            }
            catch (Exception ex)
            {
                context.Fail($"{check.Method.Name} threw {ex.GetType().Name}: {ex.Message}");
            }
        }
        return new SelfTestReport(context.Failures, context.Warnings);
    }

    /// <summary>Runs the checks, prints the upstream-style summary, returns the process exit code.</summary>
    public static int RunAndReport(TextWriter output, IEnumerable<SelfTestCheck>? extraChecks = null)
    {
        var report = Run(extraChecks);
        foreach (var warning in report.Warnings)
        {
            output.WriteLine($"SELFTEST WARNING: {warning}");
        }
        if (report.Passed)
        {
            output.WriteLine("SELFTEST OK");
            return 0;
        }
        output.WriteLine($"SELFTEST FAILED: {string.Join(", ", report.Failures)}");
        return 1;
    }

    private static void ExecutionStateRoundTrip(SelfTestContext context)
    {
        if (!ExecutionState.RoundTrip())
        {
            context.Fail("execution state");
        }
    }

    private static void UptimeReading(SelfTestContext context)
    {
        if (SystemMetrics.UptimeMilliseconds() == 0)
        {
            context.Fail("uptime reading");
        }
    }

    private static void SettingsDirectoryRoundTrip(SelfTestContext context)
    {
        var directory = AppPaths.EnsureLocalDataDirectory();
        var probe = Path.Combine(directory, $"selftest-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(probe, "ok");
        var readBack = File.ReadAllText(probe);
        File.Delete(probe);
        if (readBack != "ok")
        {
            context.Fail("settings directory round trip");
        }
    }
}
