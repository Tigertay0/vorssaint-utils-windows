// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/Localization.swift (final class L10n)

namespace Faqra.Core.Localization;

/// <summary>
/// Source of every user-facing string. Views subscribe to <see cref="Changed"/> so the whole
/// interface re-renders when the language changes. Persistence of the choice is wired by the
/// settings store, which sets <see cref="Language"/> at startup.
/// </summary>
public sealed class L10n
{
    public static L10n Shared { get; } = new();

    private AppLanguage _language = AppLanguages.SystemDefault();

    private L10n()
    {
    }

    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value)
            {
                return;
            }
            _language = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The current catalog.</summary>
    public Strings S => Strings.For(_language);

    public event EventHandler? Changed;
}
