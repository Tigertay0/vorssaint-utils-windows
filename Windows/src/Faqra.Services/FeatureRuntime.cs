// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors Sources/Vorssaint/App/FeatureRuntime.swift

using Faqra.Core.Defaults;
using Faqra.Core.Features;

namespace Faqra.Services;

/// <summary>
/// Bridges the pure feature catalog to the live services. Every binding is a closure, so merely
/// mentioning a feature never starts its service: a service only comes to life when its binding
/// runs, and <see cref="SyncAtLaunch"/> skips unavailable features entirely. Switched off in the
/// hub means nothing loads and nothing runs after the next launch. UI thread only.
/// </summary>
public sealed class FeatureRuntime
{
    private readonly ISettingsStore _store;
    private readonly Dictionary<AppFeature, Action> _bindings;
    private readonly HashSet<AppFeature> _loadedThisSession;

    public FeatureRuntime(ISettingsStore store, IReadOnlyDictionary<AppFeature, Action>? bindings = null)
    {
        _store = store;
        _bindings = bindings is null
            ? new Dictionary<AppFeature, Action>()
            : new Dictionary<AppFeature, Action>(bindings);
        _loadedThisSession = AppFeatures.All.Where(IsAvailable).ToHashSet();
    }

    /// <summary>Bumped on every availability change; views re-read the catalog when it moves.</summary>
    public int Revision { get; private set; }

    public event EventHandler? RevisionChanged;

    /// <summary>Raised when a restart would unload something; the hub shows its banner from this.</summary>
    public bool NeedsRestartToUnload => _loadedThisSession.Any(feature => !IsAvailable(feature));

    public bool IsAvailable(AppFeature feature) => _store.Bool(feature.AvailabilityKey());

    public int AvailableCount => AppFeatures.All.Count(IsAvailable);

    /// <summary>
    /// How many features this PC can end up with. Counting the whole catalog instead would leave
    /// Install all forever one short of its own disabled condition.
    /// </summary>
    public int InstallableCount => AppFeatures.All.Count(feature => feature.IsSupported() || IsAvailable(feature));

    /// <summary>
    /// The one gate every install passes, whichever surface asks. A feature Windows cannot run never
    /// installs. Uninstalls are never refused and an existing install is never revoked.
    /// </summary>
    public bool MayFlip(AppFeature feature, bool available) =>
        IsAvailable(feature) != available && (!available || feature.IsSupported());

    /// <summary>Flipping availability runs the feature's binding immediately: off tears down, on restores.</summary>
    public void SetAvailable(AppFeature feature, bool available)
    {
        if (!MayFlip(feature, available))
        {
            return;
        }
        _store.Set(feature.AvailabilityKey(), available);
        if (available)
        {
            _loadedThisSession.Add(feature);
        }
        RunBinding(feature);
        FinishAvailabilityChange();
    }

    /// <summary>Applies a hub preset: its features become the installed set, everything else uninstalls.</summary>
    public void Apply(FeaturePreset preset) => ReplaceAvailable(preset.Features(), preset.EnableKeys());

    /// <summary>Replaces the installed set (hub preset or first-run picker). Nothing is deleted.</summary>
    public void ReplaceAvailable(IReadOnlySet<AppFeature> selected, IReadOnlyList<string>? enabling = null)
    {
        foreach (var key in enabling ?? [])
        {
            _store.Set(key, true);
        }
        foreach (var feature in AppFeatures.All)
        {
            var joins = selected.Contains(feature);
            if (!MayFlip(feature, joins))
            {
                continue;
            }
            _store.Set(feature.AvailabilityKey(), joins);
            if (joins)
            {
                _loadedThisSession.Add(feature);
            }
            RunBinding(feature);
        }
        // Features that stayed installed still need a sync: their enable keys may have just flipped on.
        // Syncs are idempotent, so repeating one costs nothing.
        foreach (var feature in selected.Where(IsAvailable))
        {
            RunBinding(feature);
        }
        FinishAvailabilityChange();
    }

    /// <summary>Bulk install or uninstall for the hub's "all" buttons: one revision bump.</summary>
    public void SetAllAvailable(bool available)
    {
        var changed = false;
        foreach (var feature in AppFeatures.All.Where(f => MayFlip(f, available)))
        {
            _store.Set(feature.AvailabilityKey(), available);
            if (available)
            {
                _loadedThisSession.Add(feature);
            }
            RunBinding(feature);
            changed = true;
        }
        if (changed)
        {
            FinishAvailabilityChange();
        }
    }

    /// <summary>Launch path: only available features get their binding run, so nothing else starts.</summary>
    public void SyncAtLaunch()
    {
        foreach (var feature in AppFeatures.All.Where(IsAvailable))
        {
            RunBinding(feature);
        }
    }

    /// <summary>Re-syncs a set of features; skips unavailable ones so their services never start.</summary>
    public void Sync(IEnumerable<AppFeature> features)
    {
        foreach (var feature in features.Where(IsAvailable))
        {
            RunBinding(feature);
        }
    }

    private void RunBinding(AppFeature feature)
    {
        if (_bindings.TryGetValue(feature, out var binding))
        {
            binding();
        }
    }

    private void FinishAvailabilityChange()
    {
        Revision++;
        RevisionChanged?.Invoke(this, EventArgs.Empty);
    }
}
