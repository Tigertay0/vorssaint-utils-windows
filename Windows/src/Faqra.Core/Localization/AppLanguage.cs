// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/Localization.swift (AppLanguage)

using System.Globalization;

namespace Faqra.Core.Localization;

/// <summary>Languages the interface can use. Raw values are persisted; never rename them.</summary>
public enum AppLanguage
{
    EnUS, PtBR, Tr, Ru, Es, De, Fr, It, Ja, Ko, ZhHans, ZhTW, ZhHK,
}

public static class AppLanguages
{
    public static readonly IReadOnlyList<AppLanguage> All = Enum.GetValues<AppLanguage>();

    public static string RawValue(this AppLanguage language) => language switch
    {
        AppLanguage.EnUS => "en-US",
        AppLanguage.PtBR => "pt-BR",
        AppLanguage.Tr => "tr",
        AppLanguage.Ru => "ru",
        AppLanguage.Es => "es",
        AppLanguage.De => "de",
        AppLanguage.Fr => "fr",
        AppLanguage.It => "it",
        AppLanguage.Ja => "ja",
        AppLanguage.Ko => "ko",
        AppLanguage.ZhHans => "zh-Hans",
        AppLanguage.ZhTW => "zh-TW",
        AppLanguage.ZhHK => "zh-HK",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
    };

    public static AppLanguage? FromRawValue(string? raw)
    {
        if (raw is null)
        {
            return null;
        }
        foreach (var language in All)
        {
            if (language.RawValue() == raw)
            {
                return language;
            }
        }
        return null;
    }

    /// <summary>Whether this language puts a distinct form between one and many (only Russian).</summary>
    public static bool UsesFewCountForm(this AppLanguage language) => language == AppLanguage.Ru;

    /// <summary>The language's own name, shown in its own script.</summary>
    public static string DisplayName(this AppLanguage language) => language switch
    {
        AppLanguage.EnUS => "English (US)",
        AppLanguage.PtBR => "Português (Brasil)",
        AppLanguage.Tr => "Türkçe",
        AppLanguage.Ru => "Русский",
        AppLanguage.Es => "Español",
        AppLanguage.De => "Deutsch",
        AppLanguage.Fr => "Français",
        AppLanguage.It => "Italiano",
        AppLanguage.Ja => "日本語",
        AppLanguage.Ko => "한국어",
        AppLanguage.ZhHans => "简体中文",
        AppLanguage.ZhHK => "繁體中文（香港）",
        AppLanguage.ZhTW => "繁體中文（台灣）",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
    };

    /// <summary>The language matching the user's UI culture, with upstream's prefix rules.</summary>
    public static AppLanguage SystemDefault(string? preferredCulture = null)
    {
        var preferred = preferredCulture ?? CultureInfo.CurrentUICulture.Name;
        if (string.IsNullOrEmpty(preferred))
        {
            preferred = "en";
        }
        var lowered = preferred.ToLowerInvariant();

        if (lowered.StartsWith("zh-hk") || lowered.StartsWith("zh-hant-hk"))
        {
            return AppLanguage.ZhHK;
        }
        if (lowered.StartsWith("zh-tw") || lowered.StartsWith("zh-hant-tw") || lowered.StartsWith("zh-hant"))
        {
            return AppLanguage.ZhTW;
        }

        (string Prefix, AppLanguage Language)[] matches =
        [
            ("pt", AppLanguage.PtBR), ("tr", AppLanguage.Tr), ("ru", AppLanguage.Ru), ("es", AppLanguage.Es),
            ("de", AppLanguage.De), ("fr", AppLanguage.Fr), ("it", AppLanguage.It), ("ja", AppLanguage.Ja),
            ("ko", AppLanguage.Ko), ("zh", AppLanguage.ZhHans),
        ];
        foreach (var (prefix, language) in matches)
        {
            if (lowered.StartsWith(prefix))
            {
                return language;
            }
        }
        return AppLanguage.EnUS;
    }
}
