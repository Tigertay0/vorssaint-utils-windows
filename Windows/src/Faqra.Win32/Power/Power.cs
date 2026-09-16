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

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct SYSTEM_BATTERY_STATE
{
    public byte AcOnLine;
    public byte BatteryPresent;
    public byte Charging;
    public byte Discharging;
    public fixed byte Spare1[3];
    public byte Tag;
    public uint MaxCapacity;
    public uint RemainingCapacity;
    public int Rate;
    public uint EstimatedTime;
    public uint DefaultAlert1;
    public uint DefaultAlert2;
}

/// <summary>Charge level and whether the machine is on mains. Percent is -1 when there is no battery.</summary>
public readonly record struct BatteryReading(int Percent, bool Charging, bool HasBattery);

/// <summary>
/// The battery in more detail. <see cref="RateMilliwatts"/> is positive while charging and negative
/// while discharging; it and <see cref="SecondsRemaining"/> are null while Windows has no estimate.
/// </summary>
public readonly record struct BatteryDetails(
    bool HasBattery,
    bool OnMains,
    bool Charging,
    int? Percent,
    int? RateMilliwatts,
    int? SecondsRemaining);

public static unsafe class Power
{
    private const byte NoBattery = 128;
    private const byte PercentUnknown = 255;
    private const int SystemBatteryStateLevel = 5;
    private const int UnknownRate = unchecked((int)0x80000000);
    private const int UnknownLifeTime = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    [DllImport("powrprof.dll")]
    private static extern uint CallNtPowerInformation(
        int informationLevel, IntPtr inputBuffer, uint inputBufferLength, out SYSTEM_BATTERY_STATE outputBuffer, uint outputBufferLength);

    public static BatteryDetails Details()
    {
        if (!GetSystemPowerStatus(out var status))
        {
            return new BatteryDetails(false, true, false, null, null, null);
        }
        var hasBattery = (status.BatteryFlag & NoBattery) == 0;
        var onMains = status.ACLineStatus == 1;
        int? percent = hasBattery && status.BatteryLifePercent != PercentUnknown ? status.BatteryLifePercent : null;
        int? seconds = hasBattery && status.BatteryLifeTime != UnknownLifeTime && status.BatteryLifeTime > 0 ? status.BatteryLifeTime : null;

        int? rate = null;
        var charging = false;
        if (hasBattery
            && CallNtPowerInformation(SystemBatteryStateLevel, IntPtr.Zero, 0, out var state, (uint)sizeof(SYSTEM_BATTERY_STATE)) == 0
            && state.BatteryPresent != 0)
        {
            charging = state.Charging != 0;
            rate = state.Rate == UnknownRate || state.Rate == 0 ? null : state.Rate;
        }
        return new BatteryDetails(hasBattery, onMains, charging, percent, rate, seconds);
    }

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
