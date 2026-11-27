using SprawlWerk.Sim.Content;

namespace SprawlWerk.Sim;

/// <summary>How a tile is doing, for a query card.</summary>
public enum TileStatus : byte
{
    /// <summary>Not a zone tile (empty land, road, line, plant).</summary>
    NotAZone,

    /// <summary>An empty zone lot that may grow on the next growth pass.</summary>
    EmptyReady,

    /// <summary>An empty zone lot that cannot grow; <see cref="TileReport.GrowthBlock"/> says why.</summary>
    EmptyBlocked,

    /// <summary>A lived-in building passing every decline check.</summary>
    Healthy,

    /// <summary>A lived-in building failing a decline check; <see cref="TileReport.Reason"/> says which.</summary>
    AtRisk,

    /// <summary>A building nobody lives or works in; <see cref="TileReport.Reason"/> is what still keeps it so.</summary>
    Abandoned,
}

/// <summary>The decline check a building fails, in the order decline applies them.</summary>
public enum DeclineReason : byte
{
    /// <summary>Every check passes (an abandoned building with no reason is recovering).</summary>
    None,

    NoPower,

    NoRoad,

    /// <summary>Its zone's demand is below <see cref="GrowthRules.DeclineDemand"/>, so it may be drawn for a bad month.</summary>
    LowDemand,
}

/// <summary>
/// Everything a query card shows about one tile, read from the simulation's
/// current state. Networks are as of their last recompute (the start of the tick).
/// </summary>
/// <param name="Zone">The tile's zone, or null for a tile that is not one.</param>
/// <param name="BuildingTypeId">Id of the building on it, or null.</param>
/// <param name="RoadDistance">Steps to the nearest road, or -1 if none within <paramref name="MaxRoadDistance"/>.</param>
/// <param name="ZoneDemand">Demand for the tile's zone, or null when it is not a zone.</param>
/// <param name="Reason">For a building: the first decline check it fails now; None otherwise.</param>
/// <param name="Stress">Bad months the building has piled up (0 with no building).</param>
/// <param name="Occupants">Residents (residential) or jobs (commercial, industrial) now: 0 when abandoned.</param>
/// <param name="Capacity">Residents or jobs the building type holds when lived in ("was", once abandoned).</param>
public readonly record struct TileReport(
    int X, int Y, TileKind Kind, ZoneKind? Zone, string? BuildingTypeId,
    bool Powered, bool HasRoadAccess, int RoadDistance, int MaxRoadDistance, int? ZoneDemand,
    GrowthBlock GrowthBlock, TileStatus Status, DeclineReason Reason, int Stress,
    int Occupants, int Capacity);

public sealed partial class Simulation
{
    /// <summary>
    /// Explain one tile without changing anything: what is on it, the growth
    /// and decline checks, and who lives or works there.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The tile is off the map.</exception>
    public TileReport Inspect(int x, int y)
    {
        var tile = Map[x, y]; // throws off the map
        var zone = ZoneOf(tile.Kind);
        bool powered = Power.IsPowered(x, y);
        int distance = Roads.Distance(x, y);
        bool access = distance >= 0;
        int? demand = zone is { } z ? Demand.For(z) : null;
        var block = WhyNotGrowing(x, y);

        string? typeId = null;
        int capacity = 0, occupants = 0, stress = 0;
        var reason = DeclineReason.None;
        TileStatus status;
        if (zone is null) status = TileStatus.NotAZone;
        else if (tile.Building.IsNone) status = block == GrowthBlock.None ? TileStatus.EmptyReady : TileStatus.EmptyBlocked;
        else
        {
            if (Content is { } content)
            {
                var type = content[tile.Building];
                typeId = type.Id;
                capacity = zone == ZoneKind.Residential ? type.Residents : type.Jobs;
            }
            reason = DeclineCheck(powered, access, demand!.Value);
            stress = Stress(x, y);
            bool abandoned = IsAbandoned(x, y);
            occupants = abandoned ? 0 : capacity;
            status = abandoned ? TileStatus.Abandoned
                : reason != DeclineReason.None ? TileStatus.AtRisk
                : TileStatus.Healthy;
        }

        return new TileReport(x, y, tile.Kind, zone, typeId, powered, access, distance, Roads.MaxDistance,
            demand, block, status, reason, stress, occupants, capacity);
    }

    /// <summary>The first check <see cref="DeclinePass"/> would hold against a building: power, road, then demand.</summary>
    private DeclineReason DeclineCheck(bool powered, bool roadAccess, int zoneDemand) =>
        !powered ? DeclineReason.NoPower
        : !roadAccess ? DeclineReason.NoRoad
        : zoneDemand < Growth.DeclineDemand ? DeclineReason.LowDemand
        : DeclineReason.None;
}
