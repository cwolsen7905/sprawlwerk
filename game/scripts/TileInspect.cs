using SprawlWerk.Sim;
using SprawlWerk.Sim.Content;

// A stand-in for the simulation's tile inspect API (Simulation.Inspect and
// TileReport, coming in the sim library). Same names and shape, built here from
// what the simulation already exposes, so the query card is written against the
// real API. When the library has it, delete this file: the instance method
// takes over from this extension method and the library's types from these.
// Until then the abandon reason is the condition failing now, not the one the
// building actually left over.

namespace SprawlWerk;

/// <summary>Where a tile stands, as the query card shows it.</summary>
public enum TileStatus { NotAZone, EmptyReady, EmptyBlocked, Healthy, AtRisk, Abandoned }

/// <summary>The first failing check for a lot (abandoned with None: conditions are fixed, tenants are returning).</summary>
public enum DeclineReason { None, NoPower, NoRoad, LowDemand }

/// <summary>Everything the query card needs to know about one tile.</summary>
/// <param name="RoadDistance">Steps to the nearest road, -1 when none is within reach.</param>
/// <param name="Occupants">Residents (R) or jobs (C, I); 0 when abandoned.</param>
/// <param name="Capacity">What the building holds when lived in: the "was" figure.</param>
public readonly record struct TileReport(
    int X, int Y, TileKind Kind, ZoneKind? Zone, string? BuildingTypeId,
    bool Powered, bool HasRoadAccess, int RoadDistance, int MaxRoadDistance, int? ZoneDemand,
    GrowthBlock GrowthBlock, TileStatus Status, DeclineReason Reason, int Stress, int Occupants, int Capacity);

public static class TileInspect
{
    public static TileReport Inspect(this Simulation sim, int x, int y)
    {
        var tile = sim.Map[x, y];
        ZoneKind? zone = tile.Kind switch
        {
            TileKind.Residential => ZoneKind.Residential,
            TileKind.Commercial => ZoneKind.Commercial,
            TileKind.Industrial => ZoneKind.Industrial,
            _ => null,
        };
        bool powered = sim.Power.IsPowered(x, y);
        bool road = sim.Roads.HasAccess(x, y);
        int? demand = zone is { } z ? sim.Demand.For(z) : null;
        var block = sim.WhyNotGrowing(x, y);
        bool built = !tile.Building.IsNone;
        var type = built && sim.Content is { } content ? content[tile.Building] : null;
        int stress = built ? sim.Stress(x, y) : 0;
        bool abandoned = built && sim.IsAbandoned(x, y);

        var status = zone is null ? TileStatus.NotAZone
            : !built ? (block == GrowthBlock.None ? TileStatus.EmptyReady : TileStatus.EmptyBlocked)
            : abandoned ? TileStatus.Abandoned
            : stress > 0 ? TileStatus.AtRisk
            : TileStatus.Healthy;

        // Empty lots need demand above zero to grow; built ones decline only when it is low.
        int demandFloor = built ? sim.Growth.DeclineDemand : 0;
        var reason = zone is null ? DeclineReason.None
            : !powered ? DeclineReason.NoPower
            : !road ? DeclineReason.NoRoad
            : demand <= demandFloor ? DeclineReason.LowDemand
            : DeclineReason.None;

        int capacity = type is null ? 0 : zone == ZoneKind.Residential ? type.Residents : type.Jobs;
        return new TileReport(x, y, tile.Kind, zone, type?.Id, powered, road,
            sim.Roads.Distance(x, y), sim.Roads.MaxDistance, demand, block, status, reason, stress,
            abandoned ? 0 : capacity, capacity);
    }
}
