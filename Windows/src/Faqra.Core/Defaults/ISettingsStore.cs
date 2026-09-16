// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of UserDefaults.standard as used throughout Sources/Vorssaint

namespace Faqra.Core.Defaults;

public sealed class SettingsChangedEventArgs(string key) : EventArgs
{
    public string Key { get; } = key;
}

/// <summary>
/// Key/value preferences with registered defaults, mirroring UserDefaults semantics: a getter
/// returns the explicitly set value, else the registered default, else the type's zero value.
/// </summary>
public interface ISettingsStore
{
    bool Bool(string key);

    int Int(string key);

    double Double(string key);

    string? String(string key);

    byte[]? Data(string key);

    IReadOnlyList<string>? StringList(string key);

    IReadOnlyDictionary<string, string>? StringMap(string key);

    IReadOnlyDictionary<string, double>? DoubleMap(string key);

    /// <summary>
    /// The raw stored or registered value (bool, long, double, string, byte[],
    /// IReadOnlyList&lt;string&gt;, IReadOnlyDictionary&lt;string,string&gt; or
    /// IReadOnlyDictionary&lt;string,double&gt;), or null.
    /// </summary>
    object? Object(string key);

    /// <summary>True when the key has been explicitly set, regardless of registered defaults.</summary>
    bool Contains(string key);

    void Set(string key, bool value);

    void Set(string key, int value);

    void Set(string key, double value);

    void Set(string key, string? value);

    void Set(string key, byte[]? value);

    void Set(string key, IReadOnlyList<string>? value);

    void Set(string key, IReadOnlyDictionary<string, string>? value);

    void Set(string key, IReadOnlyDictionary<string, double>? value);

    void Remove(string key);

    /// <summary>Every explicitly set key.</summary>
    IReadOnlyCollection<string> Keys { get; }

    event EventHandler<SettingsChangedEventArgs>? Changed;
}
