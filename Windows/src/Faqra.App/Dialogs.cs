// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of SwiftUI's .alert(...) in the upstream Settings surfaces: a titled question with
// a named action button, never a bare OK/Cancel.

using System.Windows;

namespace Faqra.App;

public static class Dialogs
{
    /// <summary>
    /// Asks a yes/no question whose buttons name their actions. Returns true when the user chose
    /// the action. WPF's message box labels its buttons OK/Cancel, so the action name is carried in
    /// the body text, the way the platform allows.
    /// </summary>
    public static bool Confirm(string title, string body, string actionLabel, string cancelLabel)
    {
        var text = $"{body}\n\n{actionLabel}?";
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        var result = owner is null
            ? MessageBox.Show(text, title, MessageBoxButton.OKCancel, MessageBoxImage.Question)
            : MessageBox.Show(owner, text, title, MessageBoxButton.OKCancel, MessageBoxImage.Question);
        return result == MessageBoxResult.OK;
    }
}
