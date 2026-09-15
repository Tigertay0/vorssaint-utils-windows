// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of NSStatusItem in Sources/Vorssaint/App/StatusItemController.swift

using System.ComponentModel;
using System.Runtime.InteropServices;
using Faqra.Win32.Native;

namespace Faqra.Win32.Tray;

/// <summary>
/// One notification-area icon. A stable GUID lets Windows remember the icon's pinned position
/// across launches; because the shell binds that GUID to the executable path, adding falls back
/// to the classic window+id identity when the GUID is refused (e.g. after the exe moved).
/// The shell copies the HICON on add/modify, so callers may destroy theirs afterwards.
/// </summary>
public sealed unsafe class TrayIcon : IDisposable
{
    private const int TipCapacity = 128;

    private readonly IntPtr _hwnd;
    private readonly uint _id;
    private readonly Guid? _guid;
    private bool _usesGuid;
    private bool _added;

    public TrayIcon(IntPtr ownerWindow, uint id, Guid? guid = null)
    {
        _hwnd = ownerWindow;
        _id = id;
        _guid = guid;
    }

    public bool IsAdded => _added;

    /// <summary>Forget the shell-side state, e.g. after Explorer restarted and dropped every icon.</summary>
    public void MarkRemoved() => _added = false;

    public void Add(IntPtr hIcon, string tooltip)
    {
        var data = Identity(useGuid: _guid.HasValue);
        data.uFlags |= Shell32.NIF_MESSAGE | Shell32.NIF_ICON | Shell32.NIF_TIP | Shell32.NIF_SHOWTIP;
        data.uCallbackMessage = TrayMessageWindow.CallbackMessage;
        data.hIcon = hIcon;
        WriteTip(ref data, tooltip);

        if (Shell32.Shell_NotifyIconW(Shell32.NIM_ADD, ref data))
        {
            _usesGuid = _guid.HasValue;
        }
        else if (_guid.HasValue)
        {
            data.uFlags &= ~Shell32.NIF_GUID;
            data.guidItem = default;
            if (!Shell32.Shell_NotifyIconW(Shell32.NIM_ADD, ref data))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Shell_NotifyIcon(NIM_ADD) failed");
            }
            _usesGuid = false;
        }
        else
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Shell_NotifyIcon(NIM_ADD) failed");
        }
        _added = true;

        var version = Identity(_usesGuid);
        version.uVersionOrTimeout = Shell32.NOTIFYICON_VERSION_4;
        Shell32.Shell_NotifyIconW(Shell32.NIM_SETVERSION, ref version);
    }

    public void Update(IntPtr hIcon, string tooltip)
    {
        if (!_added)
        {
            Add(hIcon, tooltip);
            return;
        }
        var data = Identity(_usesGuid);
        data.uFlags |= Shell32.NIF_ICON | Shell32.NIF_TIP | Shell32.NIF_SHOWTIP;
        data.hIcon = hIcon;
        WriteTip(ref data, tooltip);
        if (!Shell32.Shell_NotifyIconW(Shell32.NIM_MODIFY, ref data))
        {
            // The shell forgot us (Explorer restart without TaskbarCreated reaching us yet); re-add.
            _added = false;
            Add(hIcon, tooltip);
        }
    }

    public void Remove()
    {
        if (!_added)
        {
            return;
        }
        var data = Identity(_usesGuid);
        Shell32.Shell_NotifyIconW(Shell32.NIM_DELETE, ref data);
        _added = false;
    }

    /// <summary>The icon's screen rectangle; false when it is hidden in the overflow flyout.</summary>
    public bool TryGetRect(out RECT rect)
    {
        var identifier = new NOTIFYICONIDENTIFIER
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
            hWnd = _usesGuid ? IntPtr.Zero : _hwnd,
            uID = _usesGuid ? 0 : _id,
            guidItem = _usesGuid ? _guid!.Value : default,
        };
        return Shell32.Shell_NotifyIconGetRect(ref identifier, out rect) == 0;
    }

    private NOTIFYICONDATAW Identity(bool useGuid)
    {
        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = _hwnd,
            uID = _id,
        };
        if (useGuid && _guid.HasValue)
        {
            data.uFlags |= Shell32.NIF_GUID;
            data.guidItem = _guid.Value;
        }
        return data;
    }

    private static void WriteTip(ref NOTIFYICONDATAW data, string tooltip)
    {
        fixed (char* tip = data.szTip)
        {
            var span = new Span<char>(tip, TipCapacity);
            span.Clear();
            var length = Math.Min(tooltip.Length, TipCapacity - 1);
            if (length > 0 && length < tooltip.Length && char.IsHighSurrogate(tooltip[length - 1]))
            {
                length--; // never split a surrogate pair at the truncation point
            }
            tooltip.AsSpan(0, length).CopyTo(span);
        }
    }

    public void Dispose() => Remove();
}
