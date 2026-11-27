using SprawlWerk.Sim.Content;

namespace SprawlWerk.Sim;

/// <summary>
/// What building and clearing cost, per tile. <see cref="FromContent"/> takes
/// build prices from the loaded content types; <see cref="Default"/> holds the
/// same placeholder numbers in code. Clearing and the line crossing fee are not
/// content yet (crossings become data with the network rules, ADR 0011).
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

    /// <summary>Default demolish price per tile, until it is content.</summary>
    public const long DefaultDemolish = 2;

    /// <summary>Default fee where a line crosses a road, until crossings are content.</summary>
    public const long DefaultLineCrossing = 12;

    /// <summary>
    /// Build prices from content: each kind costs its type's <c>cost</c>; a road
    /// carrying a line placed outright costs a road plus a line.
    /// </summary>
    public static PriceList FromContent(ContentSet content, long demolish = DefaultDemolish, long lineCrossing = DefaultLineCrossing)
    {
        var build = new Dictionary<TileKind, long>();
        foreach (var kind in Enum.GetValues<TileKind>())
            if (KindIds.IdFor(kind) is not null) build[kind] = KindIds.TypeFor(content, kind).Cost;
        build[TileKind.RoadWithLine] = build[TileKind.Road] + build[TileKind.PowerLine];
        return new PriceList(build, demolish, lineCrossing);
    }

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
        demolish: DefaultDemolish,
        lineCrossing: DefaultLineCrossing);
}
