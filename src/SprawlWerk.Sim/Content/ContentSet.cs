namespace SprawlWerk.Sim.Content;

/// <summary>A file handed to the loader: where it came from (for messages) and its text.</summary>
public readonly record struct SourceFile(string Path, string Text);

/// <summary>
/// One pack as the caller read it: its <c>pack.json</c> and every
/// <c>*.sim.json</c> type file. The simulation never opens files; the game
/// reads them through the engine, tests pass strings.
/// </summary>
public sealed record PackSource(SourceFile Manifest, IReadOnlyList<SourceFile> Types);

/// <summary>What a pack says about itself in <c>pack.json</c>.</summary>
public sealed record PackInfo(
    string Id,
    Version Version,
    string License,
    IReadOnlyList<string> Authors,
    IReadOnlyList<string> DependsOn,
    string? GameVersion);

/// <summary>A problem in a content file, precise enough for a modder to fix it.</summary>
public sealed record ContentError(string File, string JsonPath, string Message)
{
    public override string ToString() => JsonPath.Length == 0 ? $"{File}: {Message}" : $"{File}: {JsonPath}: {Message}";
}

/// <summary>
/// Every type from every loaded pack, in ordinal id order, each with a stable
/// <see cref="TypeHandle"/>. Built only by <see cref="ContentLoader"/>; read-only.
/// </summary>
public sealed class ContentSet
{
    private readonly BuildingType[] _types;
    private readonly Dictionary<string, TypeHandle> _byId;

    internal ContentSet(IReadOnlyList<PackInfo> packs, IEnumerable<BuildingType> types)
    {
        Packs = packs;
        _types = types.OrderBy(t => t.Id, StringComparer.Ordinal).ToArray();
        if (_types.Length >= ushort.MaxValue)
            throw new InvalidOperationException("too many types for a 16-bit handle");
        _byId = new Dictionary<string, TypeHandle>(StringComparer.Ordinal);
        for (int i = 0; i < _types.Length; i++)
            _byId[_types[i].Id] = new TypeHandle((ushort)(i + 1));
    }

    /// <summary>Packs in the order they were loaded: <c>base</c> first, then by dependency.</summary>
    public IReadOnlyList<PackInfo> Packs { get; }

    /// <summary>Types in handle order (the first has handle 1).</summary>
    public IReadOnlyList<BuildingType> Types => _types;

    public BuildingType this[TypeHandle handle] =>
        handle.IsNone || handle.Value > _types.Length
            ? throw new ArgumentOutOfRangeException(nameof(handle), handle.Value, "no such type")
            : _types[handle.Value - 1];

    public bool TryFind(string id, out TypeHandle handle) => _byId.TryGetValue(id, out handle);

    public TypeHandle Find(string id) =>
        TryFind(id, out var handle) ? handle : throw new KeyNotFoundException($"no content type '{id}'");
}
