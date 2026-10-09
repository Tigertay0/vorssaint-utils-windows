// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of NSStatusItem in Sources/Vorssaint/App/StatusItemController.swift

using System.Runtime.InteropServices;
using Faqra.Win32.Native;

namespace Faqra.Win32.Tray;

/// <summary>
/// One notification-area icon. A stable GUID lets Windows remember the icon's pinned position
/// across launches; because the shell binds that GUID to the executable path, adding falls back
/// to the classic window+id identity when the GUID is refused (e.g. after the exe moved).
/// Shell refusals are reported, never thrown: the taskbar turns calls away for a few seconds after a
/// display wake, and a throw from a redraw used to end the process.
/// The shell copies the HICON on add/modify, so callers may destroy theirs afterwards.
/// </summary>
public sealed unsafe class TrayIcon : IDisposable
{
    private const int TipCapacity = 128;

    private readonly IntPtr _hwnd;
    private readonly uint _id;
    private readonly Guid? _guid;
    private readonly INotifyIconShell _shell;
    private bool _usesGuid;
    private bool _added;
    private bool _shownBefore;

    public TrayIcon(IntPtr ownerWindow, uint id, Guid? guid = null)
        : this(ownerWindow, id, guid, NotifyIconShell.Instance)
    {
    }

    internal TrayIcon(IntPtr ownerWindow, uint id, Guid? guid, INotifyIconShell shell)
    {
        _hwnd = ownerWindow;
        _id = id;
        _guid = guid;
        _shell = shell;
    }

    public bool IsAdded => _added;

    /// <summary>Forget the shell-side state, e.g. after Explorer restarted and dropped every icon.</summary>
    public void MarkRemoved() => _added = false;

    /// <summary>
    /// Puts the icon in the tray. Returns false when the shell refused, which is routine around sign-in,
    /// display wake and Explorer restarts; the caller tries again on its next update.
    /// </summary>
    public bool Add(IntPtr hIcon, string tooltip)
    {
        // A call to a hung taskbar can be queued and run later; sent now, the fallback below could leave
        // a second icon behind once Explorer catches up.
        if (!_shell.IsTaskbarResponsive())
        {
            return false;
        }
        // After a refused update the icon is usually still in the tray under the identity it was shown with.
        var guidFirst = _guid.HasValue && (_usesGuid || !_shownBefore);
        if (!TryShow(guidFirst, hIcon, tooltip) && !(_guid.HasValue && TryShow(!guidFirst, hIcon, tooltip)))
        {
            return false;
        }

        var version = Identity(_usesGuid);
        version.uVersionOrTimeout = Shell32.NOTIFYICON_VERSION_4;
        if (!_shell.NotifyIcon(Shell32.NIM_SETVERSION, ref version))
        {
            // Without version 4 the shell sends the old callback layout, which TrayMessageWindow misreads;
            // take the icon back out so the next update adds it again.
            var shown = Identity(_usesGuid);
            _shell.NotifyIcon(Shell32.NIM_DELETE, ref shown);
            return false;
        }
        _added = true;
        _shownBefore = true;
        return true;
    }

    /// <summary>Redraws the icon, adding it first when needed. Returns false while the shell refuses.</summary>
    public bool Update(IntPtr hIcon, string tooltip)
    {
        if (_added)
        {
            var data = Identity(_usesGuid);
            data.uFlags |= Shell32.NIF_ICON | Shell32.NIF_TIP | Shell32.NIF_SHOWTIP;
            data.hIcon = hIcon;
            WriteTip(ref data, tooltip);
            if (_shell.NotifyIcon(Shell32.NIM_MODIFY, ref data))
            {
                return true;
            }
            // The shell forgot us (Explorer restarted before TaskbarCreated reached us) or is busy (display wake).
            _added = false;
        }
        return Add(hIcon, tooltip);
    }

    /// <summary>Adds the icon under one identity, or adopts it when it outlived a refused update.</summary>
    private bool TryShow(bool useGuid, IntPtr hIcon, string tooltip)
    {
        var data = Identity(useGuid);
        data.uFlags |= Shell32.NIF_MESSAGE | Shell32.NIF_ICON | Shell32.NIF_TIP | Shell32.NIF_SHOWTIP;
        data.uCallbackMessage = TrayMessageWindow.CallbackMessage;
        data.hIcon = hIcon;
        WriteTip(ref data, tooltip);
        if (!_shell.NotifyIcon(Shell32.NIM_ADD, ref data) && !_shell.NotifyIcon(Shell32.NIM_MODIFY, ref data))
        {
            return false;
        }
        _usesGuid = useGuid;
        return true;
    }

    /// <summary>
    /// Shows a Windows notification from this icon (upstream posts a user notification). Returns false
    /// when the icon is not in the tray or the shell refused.
    /// </summary>
    public bool ShowNotification(string title, string text)
    {
        if (!_added)
        {
            return false;
        }
        var data = Identity(_usesGuid);
        data.uFlags |= Shell32.NIF_INFO;
        data.dwInfoFlags = Shell32.NIIF_USER | Shell32.NIIF_LARGE_ICON;
        CopyInto(data.szInfo, 256, text);
        CopyInto(data.szInfoTitle, 64, title);
        return _shell.NotifyIcon(Shell32.NIM_MODIFY, ref data);
    }

    private static void CopyInto(char* destination, int capacity, string value)
    {
        var span = new Span<char>(destination, capacity);
        span.Clear();
        var length = Math.Min(value.Length, capacity - 1);
        if (length > 0 && length < value.Length && char.IsHighSurrogate(value[length - 1]))
        {
            length--;
        }
        value.AsSpan(0, length).CopyTo(span);
    }

    public void Remove()
    {
        if (!_added)
        {
            return;
        }
        var data = Identity(_usesGuid);
        _shell.NotifyIcon(Shell32.NIM_DELETE, ref data);
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
