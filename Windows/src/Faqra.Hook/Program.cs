// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Security.Principal;
using Faqra.Core.Agents;

namespace Faqra.Hook;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            Func<string, string?> env = Environment.GetEnvironmentVariable;
            var sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
            // The raw stream, not Console.Out: Console.Out encodes with the console's code page, and Claude Code
            // reads the reply as UTF-8 (a question or an answer may carry any character).
            using var stdout = Console.OpenStandardOutput();
            return Relay.Run(args, Console.OpenStandardInput(), stdout, env, Environment.CurrentDirectory, AgentPipe.Name(sid, env));
        }
        catch (Exception)
        {
            return 0; // a hook that fails must never fail the session
        }
    }
}
