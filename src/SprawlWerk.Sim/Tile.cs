using SprawlWerk.Sim.Content;

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
    /// <summary>A road with a power line carried across it; both networks use it.</summary>
    RoadWithLine,
}

/// <summary>
/// One cell of the city grid: only what the player or the terrain decided,
/// the state a save file holds. Anything the simulation works out from it
/// (power, later water, traffic, fields) lives in that system's own arrays,
/// so it is never saved and can always be rebuilt.
/// </summary>
public struct Tile
{
    /// <summary>Terrain height in steps; 0 is sea level.</summary>
    public byte Height;

    public TileKind Kind;

    /// <summary>
    /// The building that has grown on this zone tile, or none. Set only by the
    /// simulation's growth, never by the player; cleared whenever the tile's
    /// kind changes. Refers to a type in the simulation's content.
    /// </summary>
    public TypeHandle Building;

    public readonly bool IsZone =>
        Kind is TileKind.Residential or TileKind.Commercial or TileKind.Industrial;

    /// <summary>Tiles that carry power to their neighbours.</summary>
    public readonly bool ConductsPower =>
        IsZone || Kind is TileKind.PowerLine or TileKind.PowerPlant or TileKind.RoadWithLine;
}
