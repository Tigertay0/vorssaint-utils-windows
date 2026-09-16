// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors AdvancedSettings in Sources/Vorssaint/UI/Settings/AdvancedSettings.swift (backup section)

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Faqra.Core;
using Faqra.Core.Localization;
using Faqra.Services.Backup;
using Microsoft.Win32;

namespace Faqra.App.Settings.Pages;

public partial class AdvancedPage : UserControl
{
    private const string FileFilter = "Faqra settings (*.json)|*.json";

    public AdvancedPage()
    {
        InitializeComponent();
        var s = L10n.Shared.S;
        TitleText.Text = s.SettingsPageTitles[Core.Settings.SettingsPage.Advanced];
        BackupHeader.Text = s.BackupTitle;
        BackupDescription.Text = s.BackupDescription;
        ExportButton.Content = s.BackupExportButton;
        ImportButton.Content = s.BackupImportButton;
        SettingsPathLabel.Text = "Settings file";
        SettingsPathText.Text = AppPaths.SettingsFile;
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { FileName = SettingsBackup.DefaultFileName, Filter = FileFilter };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        var store = AppServices.Current.Store;
        store.Flush();
        try
        {
            SettingsBackup.Export(store, dialog.FileName);
            ShowStatus(L10n.Shared.S.BackupExported, success: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus(ex.Message, success: false);
        }
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        var s = L10n.Shared.S;
        var dialog = new OpenFileDialog { Filter = FileFilter, CheckFileExists = true };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        var settings = SettingsBackup.Read(dialog.FileName);
        if (settings is null)
        {
            ShowStatus(s.BackupInvalidFile, success: false);
            return;
        }
        if (!Dialogs.Confirm(s.BackupImportConfirmTitle, s.BackupImportConfirmBody, s.BackupImportAction, s.Cancel))
        {
            return;
        }
        SettingsBackup.Apply(AppServices.Current.Store, settings);
        AppServices.Current.Store.Flush();
        AppRelauncher.Relaunch();
    }

    private void ShowStatus(string message, bool success)
    {
        StatusText.Text = message;
        StatusText.Foreground = success
            ? (Brush)FindResource("SystemFillColorSuccessBrush")
            : (Brush)FindResource("SystemFillColorCautionBrush");
        StatusText.Visibility = Visibility.Visible;
    }
}
