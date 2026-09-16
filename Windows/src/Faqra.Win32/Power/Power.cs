// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of IOKit.ps in Sources/Vorssaint/Services/Metrics.

using System.Runtime.InteropServices;

namespace Faqra.Win32.Power;

[StructLayout(LayoutKind.Sequential)]
internal struct SYSTEM_POWER_STATUS
{
    public byte ACLineStatus;
    public byte BatteryFlag;
    public byte BatteryLifePercent;
    public byte SystemStatusFlag;
    public int BatteryLifeTime;
    public int BatteryFullLifeTime;
}

/// <summary>Charge level and whether the machine is on mains. Percent is -1 when there is no battery.</summary>
public readonly record struct BatteryReading(int Percent, bool Charging, bool HasBattery);

public static class Power
{
    private const byte NoBattery = 128;
    private const byte PercentUnknown = 255;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    public static BatteryReading BatteryStatus()
    {
        if (!GetSystemPowerStatus(out var status))
        {
            return new BatteryReading(-1, false, false);
        }
        var hasBattery = (status.BatteryFlag & NoBattery) == 0;
        var percent = status.BatteryLifePercent == PercentUnknown ? -1 : status.BatteryLifePercent;
        return new BatteryReading(hasBattery ? percent : -1, status.ACLineStatus == 1, hasBattery);
    }
}
