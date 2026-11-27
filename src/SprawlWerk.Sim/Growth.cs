using SprawlWerk.Sim.Content;

namespace SprawlWerk.Sim;

/// <summary>How much each zone wants to grow, from -1000 (shrinking) to +1000.</summary>
public readonly record struct Demand(int Residential, int Commercial, int Industrial)
{
    public int For(ZoneKind zone) => zone switch
    {
        ZoneKind.Residential => Residential,
        ZoneKind.Commercial => Commercial,
        _ => Industrial,
    };
}

/// <summary>Why a tile is not growing a building, checked in this order.</summary>
public enum GrowthBlock : byte
{
    /// <summary>It could grow on the next growth pass.</summary>
    None,

    /// <summary>Not a zone tile.</summary>
    NotAZone,

    /// <summary>A building is already there.</summary>
    Built,

    NoPower,

    NoRoad,

    /// <summary>Its zone's demand is zero or less.</summary>
    NoDemand,

    /// <summary>The loaded content has no building that fits this zone.</summary>
    NoBuildingType,
}

/// <summary>
/// Demand and growth (ADR 0012, option C: forgiving growth). Integer maths,
/// index order and <see cref="CityRandom"/> only, so the same city and
/// commands always grow the same way.
/// </summary>
public sealed partial class Simulation
{
    private bool _demandStarted;

    private static readonly ZoneKind[] Zones = [ZoneKind.Residential, ZoneKind.Commercial, ZoneKind.Industrial];

    /// <summary>Each zone's demand, recomputed at every month start and smoothed.</summary>
    public Demand Demand { get; private set; }

    /// <summary>False until the first month start has computed demand.</summary>
    internal bool DemandStarted => _demandStarted;

    internal static TileKind KindOf(ZoneKind zone) => zone switch
    {
        ZoneKind.Residential => TileKind.Residential,
        ZoneKind.Commercial => TileKind.Commercial,
        _ => TileKind.Industrial,
    };

    private static ZoneKind? ZoneOf(TileKind kind) => kind switch
    {
        TileKind.Residential => ZoneKind.Residential,
        TileKind.Commercial => ZoneKind.Commercial,
        TileKind.Industrial => ZoneKind.Industrial,
        _ => null,
    };

    /// <summary>
    /// Jobs pull residents, residents pull shops, workers pull industry; an
    /// empty city wants some residents and some industry to start (G-1).
    /// Each zone's tax rate above neutral then takes <see cref="GrowthRules.TaxWeight"/>
    /// a point off its demand (below neutral, adds it).
    /// </summary>
    private void UpdateDemand()
    {
        var g = Growth;
        long workers = (long)Residents * g.LabourShare / 100;
        long targetR = Math.Max(g.BaseResidents, (long)(CommercialJobs + IndustrialJobs) * 100 / Math.Max(1, g.LabourShare));
        long targetC = (long)Residents * g.CommercialPer100 / 100;
        long targetI = workers * g.IndustrialShare / 100 + g.ExportJobs;

        int Raw(long target, long actual, ZoneKind zone) => (int)Math.Clamp(
            Math.Clamp((target - actual) * 1000 / Math.Max(target, g.MinScale), -1000, 1000) - TaxPull(zone), -1000, 1000);

        var raw = new Demand(
            Raw(targetR, Residents, ZoneKind.Residential),
            Raw(targetC, CommercialJobs, ZoneKind.Commercial),
            Raw(targetI, IndustrialJobs, ZoneKind.Industrial));
        if (!_demandStarted)
        {
            Demand = raw;
            _demandStarted = true;
            return;
        }
        var d = Demand;
        Demand = new Demand(
            d.Residential + (raw.Residential - d.Residential) / 2,
            d.Commercial + (raw.Commercial - d.Commercial) / 2,
            d.Industrial + (raw.Industrial - d.Industrial) / 2);
    }

    /// <summary>Why this tile is not growing, or <see cref="GrowthBlock.None"/> if it may grow next pass.</summary>
    public GrowthBlock WhyNotGrowing(int x, int y)
    {
        var tile = Map[x, y];
        if (ZoneOf(tile.Kind) is not { } zone) return GrowthBlock.NotAZone;
        if (!tile.Building.IsNone) return GrowthBlock.Built;
        if (!Power.IsPowered(x, y)) return GrowthBlock.NoPower;
        if (!Roads.HasAccess(x, y)) return GrowthBlock.NoRoad;
        if (Demand.For(zone) <= 0) return GrowthBlock.NoDemand;
        if (BuildingTypesFor(zone).Count == 0) return GrowthBlock.NoBuildingType;
        return GrowthBlock.None;
    }

    /// <summary>Types that can grow on a zone in the demo: 1×1, low density, in id order.</summary>
    private List<TypeHandle> BuildingTypesFor(ZoneKind zone)
    {
        var fits = new List<TypeHandle>();
        if (Content is not { } content) return fits;
        for (int i = 0; i < content.Types.Count; i++)
        {
            var t = content.Types[i];
            if (t.Role == TypeRole.Building && t.Zone == zone && (t.Density ?? Density.Low) == Density.Low
                && t.Width == 1 && t.Height == 1)
                fits.Add(new TypeHandle((ushort)(i + 1)));
        }
        return fits;
    }

    /// <summary>One growth pass over every zone; true if anything grew.</summary>
    private bool GrowthPass()
    {
        if (Content is null) return false;
        bool grew = false;
        foreach (var zone in Zones)
        {
            int demand = Demand.For(zone);
            if (demand <= 0) continue;
            var types = BuildingTypesFor(zone);
            if (types.Count == 0) continue;

            var candidates = new List<(int X, int Y, int Weight)>();
            long totalWeight = 0;
            var kind = KindOf(zone);
            for (int y = 0; y < Map.Height; y++)
            for (int x = 0; x < Map.Width; x++)
            {
                var tile = Map[x, y];
                if (tile.Kind != kind || !tile.Building.IsNone || !Power.IsPowered(x, y) || !Roads.HasAccess(x, y)) continue;
                int built = 0;
                foreach (var (nx, ny) in Map.Neighbours(x, y))
                    if (!Map[nx, ny].Building.IsNone) built++;
                int weight = 1 + Growth.AdjacencyWeight * built;
                candidates.Add((x, y, weight));
                totalWeight += weight;
            }

            int wanted = (int)Math.Min(candidates.Count, ((long)demand * Growth.MaxGrowPerPass + 999) / 1000);
            for (int n = 0; n < wanted; n++)
            {
                int roll = Random.Next((int)Math.Min(totalWeight, int.MaxValue));
                int pick = 0;
                while (roll >= candidates[pick].Weight) roll -= candidates[pick++].Weight;
                var (px, py, w) = candidates[pick];
                candidates.RemoveAt(pick);
                totalWeight -= w;
                Map[px, py].Building = types[Random.Next(types.Count)];
                ResetStress(py * Map.Width + px);
                grew = true;
            }
        }
        return grew;
    }
}
