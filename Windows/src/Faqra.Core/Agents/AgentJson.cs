// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text.Encodings.Web;
using System.Text.Json;

namespace Faqra.Core.Agents;

/// <summary>JSON options shared by the relay, the hub and the config editor.</summary>
public static class AgentJson
{
    /// <summary>One line, characters kept as written: what crosses the pipe.</summary>
    public static readonly JsonSerializerOptions Compact = new() { WriteIndented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Two-space indent, characters kept as written: what Claude Code's own settings look like.</summary>
    public static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
}
