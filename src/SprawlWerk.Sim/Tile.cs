namespace SprawlWerk.Sim;

/// <summary>What a tile is zoned or built as.</summary>
public enum TileKind : byte
{
    Empty,
    Residential,
    Commercial,
    Industrial,
    Road,
    PowerLine,
    PowerPlant,
}

/// <summary>
/// One cell of the city grid. A small value type so a whole map is one
/// contiguous array: cheap to copy, cheap to iterate, easy to serialize.
/// </summary>
public struct Tile
{
    /// <summary>Terrain height in steps; 0 is sea level.</summary>
    public byte Height;

    public TileKind Kind;

    /// <summary>Set by the power pass each tick.</summary>
    public bool Powered;

    public readonly bool IsZone =>
        Kind is TileKind.Residential or TileKind.Commercial or TileKind.Industrial;

    /// <summary>Tiles that carry power to their neighbours.</summary>
    public readonly bool ConductsPower =>
        IsZone || Kind is TileKind.PowerLine or TileKind.PowerPlant;
}
