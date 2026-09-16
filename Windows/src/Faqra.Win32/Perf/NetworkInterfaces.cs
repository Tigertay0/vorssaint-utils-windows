// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of getifaddrs + if_data byte counters in Sources/Vorssaint/Services/SystemMonitor/SystemMonitor.swift.

using System.Runtime.InteropServices;

namespace Faqra.Win32.Perf;

/// <summary>One network interface's identity, state and cumulative byte counters.</summary>
public readonly record struct NetworkInterfaceReading(
    ulong Luid,
    string Alias,
    string Description,
    uint Type,
    bool IsUp,
    bool IsHardware,
    bool IsFilter,
    ulong InOctets,
    ulong OutOctets);

public static unsafe class NetworkInterfaces
{
    public const uint TypeSoftwareLoopback = 24;
    public const uint TypeTunnel = 131;

    private const int IfOperStatusUp = 1;
    private const int MaxStringLength = 257;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MIB_IF_ROW2
    {
        public ulong InterfaceLuid;
        public uint InterfaceIndex;
        public Guid InterfaceGuid;
        public fixed char Alias[MaxStringLength];
        public fixed char Description[MaxStringLength];
        public uint PhysicalAddressLength;
        public fixed byte PhysicalAddress[32];
        public fixed byte PermanentPhysicalAddress[32];
        public uint Mtu;
        public uint Type;
        public int TunnelType;
        public int MediaType;
        public int PhysicalMediumType;
        public int AccessType;
        public int DirectionType;
        public byte InterfaceAndOperStatusFlags;
        public int OperStatus;
        public int AdminStatus;
        public int MediaConnectState;
        public Guid NetworkGuid;
        public int ConnectionType;
        public ulong TransmitLinkSpeed;
        public ulong ReceiveLinkSpeed;
        public ulong InOctets;
        public ulong InUcastPkts;
        public ulong InNUcastPkts;
        public ulong InDiscards;
        public ulong InErrors;
        public ulong InUnknownProtos;
        public ulong InUcastOctets;
        public ulong InMulticastOctets;
        public ulong InBroadcastOctets;
        public ulong OutOctets;
        public ulong OutUcastPkts;
        public ulong OutNUcastPkts;
        public ulong OutDiscards;
        public ulong OutErrors;
        public ulong OutUcastOctets;
        public ulong OutMulticastOctets;
        public ulong OutBroadcastOctets;
        public ulong OutQLen;
    }

    /// <summary>The documented size of MIB_IF_ROW2 on 64-bit Windows; a mismatch means the layout is wrong.</summary>
    private const int ExpectedRowSize = 1352;

    [DllImport("iphlpapi.dll")]
    private static extern uint GetIfTable2(out IntPtr table);

    [DllImport("iphlpapi.dll")]
    private static extern void FreeMibTable(IntPtr memory);

    /// <summary>Every interface Windows knows about, or an empty list when the table cannot be read.</summary>
    public static IReadOnlyList<NetworkInterfaceReading> Read()
    {
        if (sizeof(MIB_IF_ROW2) != ExpectedRowSize || GetIfTable2(out var table) != 0 || table == IntPtr.Zero)
        {
            return [];
        }
        try
        {
            var count = (int)Marshal.ReadInt32(table);
            // MIB_IF_TABLE2 is a ULONG count followed by the rows, aligned to 8 bytes.
            var rows = (MIB_IF_ROW2*)(table + 8);
            var result = new NetworkInterfaceReading[count];
            for (var i = 0; i < count; i++)
            {
                var row = &rows[i];
                var flags = row->InterfaceAndOperStatusFlags;
                result[i] = new NetworkInterfaceReading(
                    row->InterfaceLuid,
                    new string(row->Alias),
                    new string(row->Description),
                    row->Type,
                    row->OperStatus == IfOperStatusUp,
                    IsHardware: (flags & 0x01) != 0,
                    IsFilter: (flags & 0x02) != 0,
                    row->InOctets,
                    row->OutOctets);
            }
            return result;
        }
        finally
        {
            FreeMibTable(table);
        }
    }
}
