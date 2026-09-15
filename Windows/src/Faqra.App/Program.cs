// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/main.swift (CLI dispatch before the app runs)

using Faqra.Services.Diagnostics;
using Faqra.Win32.Diagnostics;

namespace Faqra.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (HasFlag(args, "--selftest"))
        {
            ConsoleAttach.TryAttachParent();
            return SelfTest.RunAndReport(Console.Out, AppSelfTestChecks.All);
        }

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }

    private static bool HasFlag(string[] args, string flag) =>
        args.Any(arg => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));
}
