using System.Text.Json;

namespace SprawlWerk.Sim.Content;

/// <summary>
/// Reads one JSON object strictly. Each getter names a key it knows; once all
/// are read, <see cref="RejectUnknown"/> reports every other key, with a
/// "did you mean" when one is close. Problems are collected, not thrown, so a
/// modder sees all of them at once.
/// </summary>
internal sealed class JsonFields
{
    private readonly JsonElement _object;
    private readonly string _file;
    private readonly string _path;
    private readonly List<ContentError> _errors;
    private readonly HashSet<string> _known = new(StringComparer.Ordinal);

    public JsonFields(JsonElement element, string file, string path, List<ContentError> errors)
    {
        _object = element;
        _file = file;
        _path = path;
        _errors = errors;
    }

    public int ErrorCount => _errors.Count;

    public void Error(string key, string message) => _errors.Add(new ContentError(_file, PathOf(key), message));

    private string PathOf(string key) => $"{_path}.{key}";

    public bool Has(string key)
    {
        _known.Add(key);
        return _object.TryGetProperty(key, out _);
    }

    private bool TryGet(string key, bool required, out JsonElement value)
    {
        _known.Add(key);
        if (_object.TryGetProperty(key, out value) && value.ValueKind != JsonValueKind.Null) return true;
        if (required) Error(key, "is required");
        return false;
    }

    public string? String(string key, bool required = false)
    {
        if (!TryGet(key, required, out var v)) return null;
        if (v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s) return s;
        Error(key, "must be a non-empty string");
        return null;
    }

    public long? Integer(string key, long min, long max, bool required = false)
    {
        if (!TryGet(key, required, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long n))
        {
            if (n >= min && n <= max) return n;
            Error(key, $"must be from {min} to {max}, not {n}");
            return null;
        }
        Error(key, "must be a whole number");
        return null;
    }

    public T? Choice<T>(string key, IReadOnlyDictionary<string, T> choices, bool required = false) where T : struct
    {
        var s = String(key, required);
        if (s is null) return null;
        if (choices.TryGetValue(s, out var value)) return value;
        var names = choices.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Error(key, $"'{s}' is not one of {string.Join(", ", names)}{Suggest(s, names)}");
        return null;
    }

    public IReadOnlyList<string>? Strings(string key)
    {
        if (!TryGet(key, false, out var v)) return null;
        if (v.ValueKind != JsonValueKind.Array)
        {
            Error(key, "must be a list of strings");
            return null;
        }
        var list = new List<string>();
        int i = 0;
        foreach (var item in v.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } s) list.Add(s);
            else _errors.Add(new ContentError(_file, $"{PathOf(key)}[{i}]", "must be a non-empty string"));
            i++;
        }
        return list;
    }

    /// <summary>A pair of whole numbers, <c>[a, b]</c>, each within the range.</summary>
    public (int A, int B)? Pair(string key, int min, int max)
    {
        if (!TryGet(key, false, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Array && v.GetArrayLength() == 2
            && v[0].TryGetInt32Strict(out int a) && v[1].TryGetInt32Strict(out int b))
        {
            if (a >= min && a <= max && b >= min && b <= max) return (a, b);
            Error(key, $"each must be from {min} to {max}");
            return null;
        }
        Error(key, "must be two whole numbers, like [2, 2]");
        return null;
    }

    /// <summary>Reports keys no getter asked for, and keys given twice.</summary>
    public void RejectUnknown()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var known = _known.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        foreach (var property in _object.EnumerateObject())
        {
            if (!seen.Add(property.Name))
                Error(property.Name, "is given more than once");
            else if (!_known.Contains(property.Name))
                Error(property.Name, $"is not a known key{Suggest(property.Name, known)}");
        }
    }

    internal static string Suggest(string given, IEnumerable<string> candidates)
    {
        string? best = null;
        int bestDistance = int.MaxValue;
        foreach (var candidate in candidates)
        {
            int d = Distance(given.ToLowerInvariant(), candidate.ToLowerInvariant());
            if (d < bestDistance) (best, bestDistance) = (candidate, d);
        }
        return best is not null && bestDistance <= Math.Max(1, given.Length / 3) ? $" (did you mean '{best}'?)" : "";
    }

    /// <summary>Levenshtein distance; inputs are short keys and ids.</summary>
    private static int Distance(string a, string b)
    {
        var row = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) row[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            int diagonal = row[0];
            row[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int above = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                diagonal = above;
            }
        }
        return row[b.Length];
    }
}

internal static class JsonElementExtensions
{
    /// <summary>An integral JSON number that fits an int; 2.0 and "2" do not count.</summary>
    public static bool TryGetInt32Strict(this JsonElement element, out int value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }
}
