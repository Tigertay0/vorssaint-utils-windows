// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Runtime.InteropServices;

namespace Faqra.Win32.Agents;

public enum AlertKind { NeedsYou, Answered }

/// <summary>
/// Windows' own sounds, as the owner set them in the Sound control panel (Coucou's sounds are not licensed for reuse):
/// the notification sound when an agent needs the owner, the message sound when it answers. Silent when none is set.
/// </summary>
public static class AlertSound
{
    private const uint SND_ASYNC = 0x0001;
    private const uint SND_NODEFAULT = 0x0002;
    private const uint SND_ALIAS = 0x00010000;

    public static void Play(AlertKind kind) =>
        PlaySound(kind == AlertKind.NeedsYou ? "Notification.Default" : "Notification.IM", IntPtr.Zero, SND_ALIAS | SND_ASYNC | SND_NODEFAULT);

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(string sound, IntPtr module, uint flags);
}
