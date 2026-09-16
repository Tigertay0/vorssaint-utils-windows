// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/Core/Defaults.swift (registeredDefaults). Grows per milestone.

using Faqra.Core.Features;

namespace Faqra.Core.Defaults;

public static partial class RegisteredDefaults
{
    private static readonly Lazy<IReadOnlyDictionary<string, object>> AllValue = new(Build);

    /// <summary>Every registered default, keyed by settings key.</summary>
    public static IReadOnlyDictionary<string, object> All => AllValue.Value;

    private static IReadOnlyDictionary<string, object> Build()
    {
        var defaults = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (key, value) in AppFeatures.AvailabilityDefaults)
        {
            defaults[key] = value;
        }
        foreach (var (key, value) in Stage1Defaults)
        {
            defaults[key] = value;
        }
        foreach (var (key, value) in CoreDefaults)
        {
            defaults[key] = value;
        }
        return defaults;
    }

    /// <summary>Defaults for keys owned by the app shell and the island. Later milestones add their own tables.</summary>
    private static readonly IReadOnlyDictionary<string, object> CoreDefaults = new Dictionary<string, object>(StringComparer.Ordinal)
    {
        [DefaultsKey.OnboardingStep] = 0L,
        // Deliberate deviation from upstream (false): the island's hover-at-top behavior is the
        // reason this port exists, so it ships on. Users can still switch it off.
        [DefaultsKey.NotchEnabled] = true,
    };
}
