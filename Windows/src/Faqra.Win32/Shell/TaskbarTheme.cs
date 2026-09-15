// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Security;
using Microsoft.Win32;

namespace Faqra.Win32.Shell;

/// <summary>Whether the taskbar uses the light theme, which decides the tray glyph's ink color.</summary>
public static class TaskbarTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
        }
        catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
