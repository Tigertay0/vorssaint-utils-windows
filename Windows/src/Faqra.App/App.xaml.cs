// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the accessory-app lifecycle of Sources/Vorssaint/App/AppDelegate.swift

using System.Windows;
using Faqra.App.Tray;

namespace Faqra.App;

/// <summary>Tray-only application: no main window, lives until Quit is chosen.</summary>
public partial class App : Application
{
    private StatusItemController? _statusItem;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _statusItem = new StatusItemController();
        _statusItem.Show();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _statusItem?.Dispose();
        _statusItem = null;
    }
}
