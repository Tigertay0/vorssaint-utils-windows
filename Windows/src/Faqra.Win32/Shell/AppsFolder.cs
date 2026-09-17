// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of InstalledApps (folder walk + Spotlight) behind the command bar's app rows in
// Sources/Vorssaint/Services/CommandBar/CommandBarService.swift. Windows keeps one list of everything
// the Start menu can launch, desktop and packaged apps alike: the shell's AppsFolder.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Faqra.Win32.Shell;

/// <summary>A launchable app. <paramref name="ParsingName"/> is an AUMID or a file-system path.</summary>
public sealed record InstalledApp(string Name, string ParsingName);

public static class AppsFolder
{
    private static readonly Guid FolderIdAppsFolder = new("1e87508d-89c2-42f0-8a7e-645a0f50ca58");
    private static readonly Guid BhidEnumItems = new("94f60519-2850-4924-aa5a-d15e84868039");
    private static readonly Guid IidIShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
    private static readonly Guid IidIEnumShellItems = new("70629033-e363-4a28-a567-0db78006e6d7");

    private const uint SIGDN_NORMALDISPLAY = 0;
    private const uint SIGDN_PARENTRELATIVEPARSING = 0x80018001;
    private const int SIIGBF_BIGGERSIZEOK = 0x1;
    private const int SIIGBF_ICONONLY = 0x4;

    /// <summary>Every app in the Start menu's All apps list. Slow (tens of ms); call off the UI thread.</summary>
    public static IReadOnlyList<InstalledApp> Enumerate()
    {
        var apps = new List<InstalledApp>();
        var folderId = FolderIdAppsFolder;
        var iidItem = IidIShellItem;
        if (SHGetKnownFolderItem(ref folderId, 0, IntPtr.Zero, ref iidItem, out var folder) != 0)
        {
            return apps;
        }
        try
        {
            var bhid = BhidEnumItems;
            var iidEnum = IidIEnumShellItems;
            folder.BindToHandler(IntPtr.Zero, ref bhid, ref iidEnum, out var enumerator);
            try
            {
                while (enumerator.Next(1, out var item, out var fetched) == 0 && fetched == 1)
                {
                    try
                    {
                        var name = DisplayName(item, SIGDN_NORMALDISPLAY);
                        var parsing = DisplayName(item, SIGDN_PARENTRELATIVEPARSING);
                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(parsing))
                        {
                            apps.Add(new InstalledApp(name, parsing));
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(item);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }
        catch (COMException)
        {
            // A broken shell namespace yields whatever was read so far.
        }
        finally
        {
            Marshal.ReleaseComObject(folder);
        }
        return apps;
    }

    /// <summary>Starts the app the way the Start menu does; false when the shell refused.</summary>
    public static bool Launch(InstalledApp app)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{app.ParsingName}") { UseShellExecute = false });
            return process is not null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>The app's icon as an HBITMAP the caller must DeleteObject, or IntPtr.Zero.</summary>
    public static IntPtr IconBitmap(InstalledApp app, int pixels) => ShellIconBitmap($"shell:AppsFolder\\{app.ParsingName}", pixels);

    /// <summary>The shell icon of a file, folder or shell path as an HBITMAP the caller must DeleteObject, or IntPtr.Zero.</summary>
    public static IntPtr ShellIconBitmap(string parsingName, int pixels)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        if (SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iid, out var factory) != 0)
        {
            return IntPtr.Zero;
        }
        try
        {
            return factory.GetImage(new SIZE { cx = pixels, cy = pixels }, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK, out var bitmap) == 0 ? bitmap : IntPtr.Zero;
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
    }

    private static string? DisplayName(IShellItem item, uint type)
    {
        if (item.GetDisplayName(type, out var pointer) != 0 || pointer == IntPtr.Zero)
        {
            return null;
        }
        try
        {
            return Marshal.PtrToStringUni(pointer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IEnumShellItems ppv);

        void GetParent(out IShellItem ppsi);

        [PreserveSig]
        int GetDisplayName(uint sigdnName, out IntPtr ppszName);

        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport, Guid("70629033-e363-4a28-a567-0db78006e6d7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumShellItems
    {
        [PreserveSig]
        int Next(uint celt, out IShellItem rgelt, out uint pceltFetched);

        void Skip(uint celt);

        void Reset();

        void Clone(out IEnumShellItems ppenum);
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, int flags, out IntPtr phbm);
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderItem(ref Guid rfid, uint flags, IntPtr hToken, ref Guid riid, out IShellItem ppv);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string pszPath, IntPtr pbc, ref Guid riid, out IShellItemImageFactory ppv);
}
