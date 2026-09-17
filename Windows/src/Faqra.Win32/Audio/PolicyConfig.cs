// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of AudioObjectSetPropertyData(kAudioHardwarePropertyDefaultOutputDevice) in
// Sources/Vorssaint/Services/Audio/AppVolumeMixer.swift (setDefaultDevice, 1721-1733).

using System.Runtime.InteropServices;

namespace Faqra.Win32.Audio;

/// <summary>
/// Sets the default audio endpoint through IPolicyConfig, the undocumented interface the Sound control
/// panel itself uses. Stable since Windows 7; there is no documented alternative.
/// </summary>
public static class PolicyConfig
{
    private static readonly Guid PolicyConfigClient = new("870af99c-171d-4f9e-af0d-e63df40c2bc9");

    private enum ERole
    {
        Console = 0,
        Multimedia = 1,
    }

    /// <summary>Makes the endpoint the default for apps and media, as Windows' own output picker does. Returns the HRESULT.</summary>
    public static int SetDefaultOutput(string deviceId)
    {
        var type = Type.GetTypeFromCLSID(PolicyConfigClient);
        if (type is null || Activator.CreateInstance(type) is not IPolicyConfig policy)
        {
            return unchecked((int)0x80004002); // E_NOINTERFACE
        }
        try
        {
            var hr = policy.SetDefaultEndpoint(deviceId, ERole.Console);
            return hr < 0 ? hr : policy.SetDefaultEndpoint(deviceId, ERole.Multimedia);
        }
        finally
        {
            Marshal.ReleaseComObject(policy);
        }
    }

    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(IntPtr a, IntPtr b);
        [PreserveSig] int GetDeviceFormat(IntPtr a, int b, IntPtr c);
        [PreserveSig] int ResetDeviceFormat(IntPtr a);
        [PreserveSig] int SetDeviceFormat(IntPtr a, IntPtr b, IntPtr c);
        [PreserveSig] int GetProcessingPeriod(IntPtr a, int b, IntPtr c, IntPtr d);
        [PreserveSig] int SetProcessingPeriod(IntPtr a, IntPtr b);
        [PreserveSig] int GetShareMode(IntPtr a, IntPtr b);
        [PreserveSig] int SetShareMode(IntPtr a, IntPtr b);
        [PreserveSig] int GetPropertyValue(IntPtr a, int b, IntPtr c, IntPtr d);
        [PreserveSig] int SetPropertyValue(IntPtr a, int b, IntPtr c, IntPtr d);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
        [PreserveSig] int SetEndpointVisibility(IntPtr a, int b);
    }
}
