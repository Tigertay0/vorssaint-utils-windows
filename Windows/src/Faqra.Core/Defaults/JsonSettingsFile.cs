// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Faqra.Core.Defaults;

/// <summary>
/// Reads and writes the settings file. Values are JSON scalars; binary data is wrapped as
/// <c>{"$data":"base64"}</c> so it round-trips distinctly from strings. Writes go to a temp
/// file first and replace the target atomically, keeping the previous file as <c>.bak</c>.
/// A corrupt file is set aside as <c>settings.corrupt-&lt;timestamp&gt;.json</c> and the app
/// starts from registered defaults.
/// </summary>
public static class JsonSettingsFile
{
    private const string DataMarker = "$data";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static Dictionary<string, object> Load(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, object>(StringComparer.Ordinal);
        }
        try
        {
            var text = File.ReadAllText(path);
            var root = JsonNode.Parse(text) as JsonObject
                ?? throw new JsonException("settings root is not an object");
            return Decode(root);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            SetAsideCorruptFile(path);
            return new Dictionary<string, object>(StringComparer.Ordinal);
        }
    }

    public static void Save(string path, IReadOnlyDictionary<string, object> values)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var temp = path + ".tmp";
        var backup = path + ".bak";
        File.WriteAllText(temp, Encode(values).ToJsonString(WriteOptions));
        if (File.Exists(path))
        {
            File.Replace(temp, path, backup, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temp, path);
        }
    }

    public static JsonObject Encode(IReadOnlyDictionary<string, object> values)
    {
        var root = new JsonObject();
        foreach (var (key, value) in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            root[key] = value switch
            {
                bool b => JsonValue.Create(b),
                int i => JsonValue.Create((long)i),
                long l => JsonValue.Create(l),
                double d => JsonValue.Create(d),
                string s => JsonValue.Create(s),
                byte[] bytes => new JsonObject { [DataMarker] = Convert.ToBase64String(bytes) },
                IReadOnlyList<string> list => new JsonArray(list.Select(item => (JsonNode?)JsonValue.Create(item)).ToArray()),
                IReadOnlyDictionary<string, string> map => EncodeMap(map, item => JsonValue.Create(item)),
                IReadOnlyDictionary<string, double> doubles => EncodeMap(doubles, item => JsonValue.Create(item)),
                _ => throw new NotSupportedException($"settings value of type {value.GetType().Name} for key '{key}'"),
            };
        }
        return root;
    }

    public static Dictionary<string, object> Decode(JsonObject root)
    {
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (key, node) in root)
        {
            if (Decode(node) is { } value)
            {
                values[key] = value;
            }
        }
        return values;
    }

    private static JsonObject EncodeMap<T>(IReadOnlyDictionary<string, T> map, Func<T, JsonNode?> encode)
    {
        var node = new JsonObject();
        foreach (var (key, value) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            node[key] = encode(value);
        }
        return node;
    }

    private static object? Decode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj when obj.TryGetPropertyValue(DataMarker, out var encoded) && encoded is JsonValue encodedValue:
                try
                {
                    return Convert.FromBase64String(encodedValue.GetValue<string>());
                }
                catch (FormatException)
                {
                    return null;
                }
            case JsonObject obj:
                return DecodeMap(obj);
            case JsonArray array:
                var items = new List<string>(array.Count);
                foreach (var item in array)
                {
                    if (item is JsonValue itemValue && itemValue.TryGetValue<string>(out var text))
                    {
                        items.Add(text);
                    }
                }
                return items.Count == array.Count ? items : null;
            case JsonValue value:
                if (value.TryGetValue<bool>(out var b))
                {
                    return b;
                }
                if (value.TryGetValue<long>(out var l))
                {
                    return l;
                }
                if (value.TryGetValue<double>(out var d))
                {
                    return d;
                }
                if (value.TryGetValue<string>(out var s))
                {
                    return s;
                }
                return null;
            default:
                return null;
        }
    }

    /// <summary>A JSON object is a string map when every value is a string, a double map when every value is a number.</summary>
    private static object? DecodeMap(JsonObject obj)
    {
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        var doubles = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (key, node) in obj)
        {
            if (node is not JsonValue value)
            {
                return null;
            }
            if (value.TryGetValue<string>(out var s))
            {
                strings[key] = s;
            }
            else if (value.TryGetValue<double>(out var d))
            {
                doubles[key] = d;
            }
            else
            {
                return null;
            }
        }
        if (strings.Count == obj.Count)
        {
            return strings;
        }
        return doubles.Count == obj.Count ? doubles : null;
    }

    private static void SetAsideCorruptFile(string path)
    {
        try
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var directory = Path.GetDirectoryName(path) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(path);
            File.Move(path, Path.Combine(directory, $"{name}.corrupt-{stamp}.json"), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leave it in place; the next save overwrites it.
        }
    }
}
