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
            return Relay.Run(args, Console.OpenStandardInput(), env, Environment.CurrentDirectory, AgentPipe.Name(sid, env));
        }
        catch (Exception)
        {
            return 0; // a hook that fails must never fail the session
        }
    }
}
