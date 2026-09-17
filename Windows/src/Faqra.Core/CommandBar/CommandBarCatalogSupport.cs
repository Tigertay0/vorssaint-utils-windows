// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the pure parts of Sources/Vorssaint/Services/CommandBar/CommandBarCatalog.swift (toggleEntries
// 205-268, answerEntry and openURLEntry 1478-1527) and CommandBarService.suggestionRows (1153-1258). The
// rows' actions live in the app layer; this decides which rows exist and in what order.

using System.Globalization;
using Faqra.Core.Defaults;
using Faqra.Core.Features;
using Faqra.Core.Localization;

namespace Faqra.Core.CommandBar;

/// <summary>A generated on/off row for a feature with exactly one switch.</summary>
public sealed record CommandBarToggle(CommandBarEntry Entry, AppFeature Feature, string Key, bool IsOn);

/// <summary>The calculator, unit or date answer that leads the list; Enter copies <paramref name="Value"/>.</summary>
public sealed record CommandBarAnswer(CommandBarEntry Entry, string Value);

/// <summary>A run of rows under an optional heading.</summary>
public sealed record CommandBarSection(string? Title, IReadOnlyList<CommandBarEntry> Entries);

public static class CommandBarCatalogSupport
{
    /// <summary>At most this many suggestions on an empty bar (upstream's 7 minus pins; Stage 1 has no pins).</summary>
    public const int SuggestionLimit = 7;

    /// <summary>Each browse group on an empty bar stops here.</summary>
    public const int BrowseGroupLimit = 12;

    /// <summary>
    /// One row per installed feature with exactly one enable key. Two keys are two different switches,
    /// and which one a single row would flip is a guess (CommandBarCatalog.swift:258-259).
    /// </summary>
    public static IReadOnlyList<CommandBarToggle> Toggles(IEnumerable<AppFeature> installed, ISettingsStore store,
        CommandBarStrings bar, FeatureHubStrings hub)
    {
        var toggles = new List<CommandBarToggle>();
        foreach (var feature in installed)
        {
            if (feature.EnabledKeys() is not [var key])
            {
                continue;
            }
            var isOn = store.Bool(key);
            var name = hub.FeatureTitles[feature];
            var entry = new CommandBarEntry(
                $"toggle.{feature.RawValue()}",
                string.Format(isOn ? bar.TurnOffFormat : bar.TurnOnFormat, name),
                hub.GroupTitles[feature.Group()],
                name,
                CommandBarEntryKind.Toggle,
                CommandBarSource.Actions);
            toggles.Add(new CommandBarToggle(entry, feature, key, isOn));
        }
        return toggles;
    }

    /// <summary>A sum, then a conversion, then a date: the stricter parsers first, so "3" alone never becomes a date.</summary>
    public static CommandBarAnswer? Answer(string query, DateTimeOffset now, TimeZoneInfo zone, CultureInfo culture, CommandBarStrings bar)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }
        var numbers = culture.NumberFormat;
        if (CommandBarMath.Evaluate(query, numbers.NumberDecimalSeparator, numbers.NumberGroupSeparator, culture) is { } math)
        {
            return Make("math.result", math.Formatted, bar.CopyHint);
        }
        if (CommandBarUnits.Convert(query, numbers.NumberDecimalSeparator, numbers.NumberGroupSeparator, culture) is { } units)
        {
            return Make("units.result", units.Formatted, bar.CopyHint);
        }
        if (CommandBarDates.Evaluate(query, now, zone, culture) is { } date)
        {
            return Make("date.result", date.Formatted, date.Detail);
        }
        return null;

        static CommandBarAnswer Make(string id, string value, string subtitle) =>
            new(new CommandBarEntry(id, value, subtitle, string.Empty, CommandBarEntryKind.Answer, CommandBarSource.Calculator, CountsUsage: false), value);
    }

    /// <summary>The whole trimmed query as an http or https address, or null.</summary>
    public static Uri? TypedUrl(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0 || trimmed.Any(char.IsWhiteSpace))
        {
            return null;
        }
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && uri.Host.Contains('.')
            ? uri
            : null;
    }

    /// <summary>
    /// The empty bar: suggestions (most used, then curated), then everything else grouped by subtitle.
    /// Apps and windows never appear here; hundreds of them would bury the commands, and they are what
    /// search is for (CommandBarService.swift:1153-1239).
    /// </summary>
    public static IReadOnlyList<CommandBarSection> Home(IReadOnlyList<CommandBarEntry> pool,
        IReadOnlyDictionary<string, CommandBarUse> usage, IReadOnlyList<string> curated, CommandBarStrings bar)
    {
        var browsable = pool.Where(e => e.Kind is not (CommandBarEntryKind.App or CommandBarEntryKind.Window)).ToList();
        var byId = browsable.GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());
        var sections = new List<CommandBarSection>();

        var suggestionIds = CommandBarUsage.SuggestionIds(usage, byId.Keys.ToList(), curated, SuggestionLimit);
        if (suggestionIds.Count > 0)
        {
            sections.Add(new CommandBarSection(bar.SuggestionsLabel, suggestionIds.Select(id => byId[id]).ToList()));
        }
        var shown = suggestionIds.ToHashSet(StringComparer.Ordinal);
        foreach (var group in browsable.Where(e => !shown.Contains(e.Id)).GroupBy(e => e.Subtitle))
        {
            sections.Add(new CommandBarSection(group.Key, group.Take(BrowseGroupLimit).ToList()));
        }
        return sections;
    }
}
