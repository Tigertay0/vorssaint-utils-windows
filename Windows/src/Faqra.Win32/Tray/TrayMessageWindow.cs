// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of the NSStatusItem button target in Sources/Vorssaint/App/StatusItemController.swift

using System.ComponentModel;
using System.Runtime.InteropServices;
using Faqra.Win32.Native;

namespace Faqra.Win32.Tray;

/// <summary>A tray callback decoded from the NOTIFYICON_VERSION_4 message layout.</summary>
public readonly record struct TrayCallback(uint IconId, uint Event, int X, int Y)
{
    public bool IsSelect => Event is User32.NIN_SELECT or User32.NIN_KEYSELECT;
    public bool IsContextMenu => Event == User32.WM_CONTEXTMENU;
}

/// <summary>
/// Hidden top-level window that receives tray icon callbacks and the shell's TaskbarCreated
/// broadcast. It is a real (never shown) window rather than a message-only one because
/// broadcasts are not delivered to HWND_MESSAGE windows.
/// Single-thread contract: construct, pump and dispose on the UI thread only; the instance map
/// below is not synchronized because Win32 delivers a window's messages on its creating thread.
/// </summary>
public sealed class TrayMessageWindow : IDisposable
{
    private const string ClassName = "FaqraTrayMessageWindow";
    public const uint CallbackMessage = User32.WM_USER + 1;

    private static readonly WndProc StaticProc = StaticWndProc;
    private static readonly IntPtr StaticProcPointer = Marshal.GetFunctionPointerForDelegate(StaticProc);
    private static readonly Dictionary<IntPtr, TrayMessageWindow> Instances = new();
    private static readonly uint TaskbarCreatedMessage = User32.RegisterWindowMessageW("TaskbarCreated");
    private static bool s_classRegistered;

    public IntPtr Handle { get; private set; }

    public event Action<TrayCallback>? Callback;
    public event Action? TaskbarCreated;

    public TrayMessageWindow()
    {
        EnsureClassRegistered();
        var hInstance = Kernel32.GetModuleHandleW(null);
        Handle = User32.CreateWindowExW(0, ClassName, "Faqra tray", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        if (Handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWindowExW failed for the tray window");
        }
        Instances[Handle] = this;
    }

    private static void EnsureClassRegistered()
    {
        if (s_classRegistered)
        {
            return;
        }
        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = StaticProcPointer,
            hInstance = Kernel32.GetModuleHandleW(null),
            lpszClassName = ClassName,
        };
        if (User32.RegisterClassExW(ref wc) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "RegisterClassExW failed for the tray window");
        }
        s_classRegistered = true;
    }

    private static IntPtr StaticWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (Instances.TryGetValue(hWnd, out var window))
        {
            if (msg == CallbackMessage)
            {
                var callback = new TrayCallback(
                    IconId: (uint)User32.HiWord(lParam),
                    Event: (uint)(ushort)User32.LoWord(lParam),
                    X: User32.LoWord(wParam),
                    Y: User32.HiWord(wParam));
                window.Callback?.Invoke(callback);
                return IntPtr.Zero;
            }
            if (msg == TaskbarCreatedMessage)
            {
                window.TaskbarCreated?.Invoke();
                return IntPtr.Zero;
            }
        }
        return User32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (Handle == IntPtr.Zero)
        {
            return;
        }
        Instances.Remove(Handle);
        User32.DestroyWindow(Handle);
        Handle = IntPtr.Zero;
    }
}
