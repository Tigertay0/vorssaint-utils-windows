// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using Faqra.Win32.Native;
using Microsoft.Win32.SafeHandles;

namespace Faqra.Win32.Icons;

/// <summary>An HICON that is destroyed when disposed.</summary>
public sealed class NativeIcon : SafeHandleZeroOrMinusOneIsInvalid
{
    public NativeIcon() : base(ownsHandle: true)
    {
    }

    public NativeIcon(IntPtr handle) : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    public IntPtr Handle => handle;

    protected override bool ReleaseHandle() => User32.DestroyIcon(handle);
}
