// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Plays the role of UserDefaults.standard plus Defaults.register() in Sources/Vorssaint/Core/Defaults.swift

namespace Faqra.Core.Defaults;

/// <summary>
/// The app's preferences: an in-memory map of explicitly set values over the registered
/// defaults, persisted to one JSON file with a short write debounce. Thread-safe for reads
/// and writes; <see cref="Changed"/> fires synchronously on the setting thread.
/// </summary>
public sealed class DefaultsStore : ISettingsStore, IDisposable
{
    public static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(250);

    private readonly object _gate = new();
    private readonly Dictionary<string, object> _values;
    private readonly IReadOnlyDictionary<string, object> _registered;
    private readonly string? _path;
    private readonly Timer? _saveTimer;
    private bool _dirty;

    private DefaultsStore(string? path, Dictionary<string, object> values, IReadOnlyDictionary<string, object> registered)
    {
        _path = path;
        _values = values;
        _registered = registered;
        if (path is not null)
        {
            _saveTimer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <summary>Loads (or starts fresh) from the given file. Pass null registered defaults for none.</summary>
    public static DefaultsStore Open(string path, IReadOnlyDictionary<string, object>? registered = null) =>
        new(path, JsonSettingsFile.Load(path), registered ?? RegisteredDefaults.All);

    /// <summary>A store that never touches disk, for tests and previews.</summary>
    public static DefaultsStore InMemory(IReadOnlyDictionary<string, object>? registered = null) =>
        new(null, new Dictionary<string, object>(StringComparer.Ordinal), registered ?? RegisteredDefaults.All);

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public IReadOnlyCollection<string> Keys
    {
        get
        {
            lock (_gate)
            {
                return _values.Keys.ToArray();
            }
        }
    }

    public bool Bool(string key) => Object(key) switch
    {
        bool b => b,
        long l => l != 0,
        _ => false,
    };

    public int Int(string key) => Object(key) switch
    {
        long l => l is > int.MaxValue or < int.MinValue ? 0 : (int)l,
        double d => (int)d,
        bool b => b ? 1 : 0,
        _ => 0,
    };

    public double Double(string key) => Object(key) switch
    {
        double d => d,
        long l => l,
        _ => 0,
    };

    public string? String(string key) => Object(key) as string;

    public byte[]? Data(string key) => Object(key) as byte[];

    public IReadOnlyList<string>? StringList(string key) => Object(key) as IReadOnlyList<string>;

    public IReadOnlyDictionary<string, string>? StringMap(string key) => Object(key) as IReadOnlyDictionary<string, string>;

    public IReadOnlyDictionary<string, double>? DoubleMap(string key) => Object(key) as IReadOnlyDictionary<string, double>;

    public object? Object(string key)
    {
        lock (_gate)
        {
            if (_values.TryGetValue(key, out var value))
            {
                return Normalize(value);
            }
        }
        return _registered.TryGetValue(key, out var registered) ? Normalize(registered) : null;
    }

    public bool Contains(string key)
    {
        lock (_gate)
        {
            return _values.ContainsKey(key);
        }
    }

    public void Set(string key, bool value) => Store(key, value);

    public void Set(string key, int value) => Store(key, (long)value);

    public void Set(string key, double value) => Store(key, value);

    public void Set(string key, string? value)
    {
        if (value is null)
        {
            Remove(key);
        }
        else
        {
            Store(key, value);
        }
    }

    public void Set(string key, byte[]? value)
    {
        if (value is null)
        {
            Remove(key);
        }
        else
        {
            Store(key, value);
        }
    }

    public void Set(string key, IReadOnlyList<string>? value) => StoreOrRemove(key, value is null ? null : value.ToArray());

    public void Set(string key, IReadOnlyDictionary<string, string>? value) =>
        StoreOrRemove(key, value is null ? null : new Dictionary<string, string>(value, StringComparer.Ordinal));

    public void Set(string key, IReadOnlyDictionary<string, double>? value) =>
        StoreOrRemove(key, value is null ? null : new Dictionary<string, double>(value, StringComparer.Ordinal));

    private void StoreOrRemove(string key, object? value)
    {
        if (value is null)
        {
            Remove(key);
        }
        else
        {
            Store(key, value);
        }
    }

    public void Remove(string key)
    {
        lock (_gate)
        {
            if (!_values.Remove(key))
            {
                return;
            }
            MarkDirty();
        }
        Changed?.Invoke(this, new SettingsChangedEventArgs(key));
    }

    /// <summary>Writes pending changes now. Called on exit and session end; also by the debounce timer.</summary>
    public void Flush()
    {
        if (_path is null)
        {
            return;
        }
        Dictionary<string, object> snapshot;
        lock (_gate)
        {
            if (!_dirty)
            {
                return;
            }
            _dirty = false;
            snapshot = new Dictionary<string, object>(_values, StringComparer.Ordinal);
        }
        JsonSettingsFile.Save(_path, snapshot);
    }

    private void Store(string key, object value)
    {
        lock (_gate)
        {
            if (_values.TryGetValue(key, out var existing) && ValuesEqual(existing, value))
            {
                return;
            }
            _values[key] = value;
            MarkDirty();
        }
        Changed?.Invoke(this, new SettingsChangedEventArgs(key));
    }

    private void MarkDirty()
    {
        _dirty = true;
        _saveTimer?.Change(SaveDelay, Timeout.InfiniteTimeSpan);
    }

    private static object Normalize(object value) => value is int i ? (long)i : value;

    private static bool ValuesEqual(object a, object b)
    {
        a = Normalize(a);
        b = Normalize(b);
        if (a is byte[] left && b is byte[] right)
        {
            return left.AsSpan().SequenceEqual(right);
        }
        if (a is IReadOnlyList<string> listA && b is IReadOnlyList<string> listB)
        {
            return listA.SequenceEqual(listB, StringComparer.Ordinal);
        }
        if (a is IReadOnlyDictionary<string, string> mapA && b is IReadOnlyDictionary<string, string> mapB)
        {
            return mapA.Count == mapB.Count && mapA.All(pair => mapB.TryGetValue(pair.Key, out var other) && other == pair.Value);
        }
        if (a is IReadOnlyDictionary<string, double> doublesA && b is IReadOnlyDictionary<string, double> doublesB)
        {
            return doublesA.Count == doublesB.Count && doublesA.All(pair => doublesB.TryGetValue(pair.Key, out var other) && other.Equals(pair.Value));
        }
        return Equals(a, b);
    }

    public void Dispose()
    {
        _saveTimer?.Dispose();
        Flush();
    }
}
