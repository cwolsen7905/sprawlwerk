namespace SprawlWerk.Sim;

/// <summary>
/// What building and clearing cost, per tile. The defaults are placeholders
/// until building types move into data files that mods can change.
/// </summary>
public sealed class PriceList
{
    private readonly long[] _build;

    /// <summary>Cost to clear one tile that is not already empty.</summary>
    public long Demolish { get; }

    /// <summary>Extra cost for each tile where a power line and a road meet.</summary>
    public long LineCrossing { get; }

    public PriceList(IReadOnlyDictionary<TileKind, long> build, long demolish, long lineCrossing = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lineCrossing);
        LineCrossing = lineCrossing;
        ArgumentOutOfRangeException.ThrowIfNegative(demolish);
        _build = new long[Enum.GetValues<TileKind>().Length];
        foreach (var (kind, price) in build)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(price);
            _build[(int)kind] = price;
        }
        Demolish = demolish;
    }

    /// <summary>Cost to make one tile this kind. Empty is free here; clearing costs <see cref="Demolish"/>.</summary>
    public long Build(TileKind kind) => _build[(int)kind];

    public static PriceList Default { get; } = new(
        new Dictionary<TileKind, long>
        {
            [TileKind.Residential] = 20,
            [TileKind.Commercial] = 30,
            [TileKind.Industrial] = 30,
            [TileKind.Road] = 15,
            [TileKind.PowerLine] = 8,
            [TileKind.PowerPlant] = 4000,
            [TileKind.RoadWithLine] = 23, // a road plus a line, when placed outright
        },
        demolish: 2,
        lineCrossing: 12);
}
