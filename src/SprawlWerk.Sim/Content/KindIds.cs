namespace SprawlWerk.Sim.Content;

/// <summary>
/// Which base content type each <see cref="TileKind"/> stands for, while the
/// map still stores kinds (ADR 0011 moves it to type handles later). Kinds
/// with no type of their own (empty land, a road carrying a line) map to null.
/// </summary>
public static class KindIds
{
    public static string? IdFor(TileKind kind) => kind switch
    {
        TileKind.Residential => "base:residential",
        TileKind.Commercial => "base:commercial",
        TileKind.Industrial => "base:industrial",
        TileKind.Road => "base:road",
        TileKind.PowerLine => "base:power_line",
        TileKind.PowerPlant => "base:power_plant",
        _ => null,
    };

    /// <summary>The type for a kind, or an error naming the id the content lacks.</summary>
    public static BuildingType TypeFor(ContentSet content, TileKind kind)
    {
        var id = IdFor(kind) ?? throw new ArgumentException($"{kind} has no content type", nameof(kind));
        return content.TryFind(id, out var handle)
            ? content[handle]
            : throw new InvalidOperationException($"the loaded content has no '{id}', which the game needs for {kind}");
    }
}
