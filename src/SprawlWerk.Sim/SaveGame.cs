using System.Text.Json;
using System.Text.Json.Serialization;

namespace SprawlWerk.Sim;

/// <summary>A save that cannot be read: malformed, damaged, or from a newer game.</summary>
public sealed class SaveFormatException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Header facts of a save: enough for a save list.</summary>
public readonly record struct SaveInfo(int Version, int Width, int Height, long Tick, long Funds, CityDate Date);

/// <summary>
/// Saves and loads a city as versioned JSON. Only stored state goes in: the
/// terrain and what is built, funds (negative is debt), tax rates, last
/// month's budget and the tick. Derived state (power) is rebuilt on load,
/// and commands still queued are not saved, so save between ticks, as the
/// game does. Prices and power rules are game configuration,
/// not city state, and are supplied when loading.
///
/// Kinds are written as one character per tile so a save stays readable and
/// survives the enum being reordered. To change the format, bump
/// <see cref="CurrentVersion"/> and add a step to <see cref="Upgrade"/>; a
/// newer game must keep loading every older version.
/// </summary>
public static class SaveGame
{
    public const int CurrentVersion = 1;

    private const string KindChars = ".RCI=-P+";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private sealed class Data
    {
        public int Version { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long Tick { get; set; }
        public long Funds { get; set; }

        /// <summary>One string per row; see <see cref="KindChars"/> for the legend.</summary>
        public string[]? Kinds { get; set; }

        /// <summary>One array per row of terrain heights.</summary>
        public int[][]? Heights { get; set; }

        /// <summary>The city's random generator state; absent in older saves, which start from the default seed.</summary>
        public ulong? Random { get; set; }

        /// <summary>Smoothed demand for R, C and I; absent in older saves, which recompute it at the next month.</summary>
        public int[]? Demand { get; set; }

        /// <summary>Grown buildings by tile and content type id; absent in older saves.</summary>
        public List<BuildingEntry>? Buildings { get; set; }

        /// <summary>Buildings in decline: tile, bad months, abandoned. Absent in older saves (all well).</summary>
        public List<StressEntry>? Stress { get; set; }

        /// <summary>Tax rates for R, C and I in percent; absent in older saves, which start at the neutral rate.</summary>
        public int[]? Taxes { get; set; }

        /// <summary>The last month settled; absent in older saves and before the first month ends.</summary>
        public BudgetEntry? LastBudget { get; set; }
    }

    private sealed class StressEntry
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Months { get; set; }
        public bool Abandoned { get; set; }
    }

    private sealed class BudgetEntry
    {
        public long ResidentialTax { get; set; }
        public long CommercialTax { get; set; }
        public long IndustrialTax { get; set; }
        public long RoadUpkeep { get; set; }
        public long PowerLineUpkeep { get; set; }
        public long PlantUpkeep { get; set; }
        public long ZoneUpkeep { get; set; }
    }

    private sealed class BuildingEntry
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string Type { get; set; } = "";
    }

    private static char CharFor(TileKind kind) => KindChars[(int)kind];

    public static string Serialize(Simulation sim)
    {
        var map = sim.Map;
        var kinds = new string[map.Height];
        var heights = new int[map.Height][];
        for (int y = 0; y < map.Height; y++)
        {
            var row = new char[map.Width];
            heights[y] = new int[map.Width];
            for (int x = 0; x < map.Width; x++)
            {
                row[x] = CharFor(map[x, y].Kind);
                heights[y][x] = map[x, y].Height;
            }
            kinds[y] = new string(row);
        }

        return JsonSerializer.Serialize(new Data
        {
            Version = CurrentVersion,
            Width = map.Width,
            Height = map.Height,
            Tick = sim.Tick,
            Funds = sim.Funds,
            Random = sim.Random.State,
            Demand = sim.DemandStarted ? [sim.Demand.Residential, sim.Demand.Commercial, sim.Demand.Industrial] : null,
            Buildings = BuildingsOf(sim),
            Stress = sim.StressedBuildings().Select(s => new StressEntry { X = s.X, Y = s.Y, Months = s.Stress, Abandoned = s.Abandoned }).ToList(),
            Taxes = [sim.Taxes.Residential, sim.Taxes.Commercial, sim.Taxes.Industrial],
            LastBudget = sim.LastBudget is { } b ? new BudgetEntry
            {
                ResidentialTax = b.ResidentialTax, CommercialTax = b.CommercialTax, IndustrialTax = b.IndustrialTax,
                RoadUpkeep = b.RoadUpkeep, PowerLineUpkeep = b.PowerLineUpkeep, PlantUpkeep = b.PlantUpkeep,
                ZoneUpkeep = b.ZoneUpkeep,
            } : null,
            Kinds = kinds,
            Heights = heights,
        }, Options);
    }

    private static List<BuildingEntry> BuildingsOf(Simulation sim)
    {
        var list = new List<BuildingEntry>();
        if (sim.Content is not { } content) return list;
        for (int y = 0; y < sim.Map.Height; y++)
        for (int x = 0; x < sim.Map.Width; x++)
            if (!sim.Map[x, y].Building.IsNone)
                list.Add(new BuildingEntry { X = x, Y = y, Type = content[sim.Map[x, y].Building].Id });
        return list;
    }

    /// <summary>What a save says about itself, read without building the city.</summary>
    public static SaveInfo ReadInfo(string json)
    {
        var data = Upgrade(Parse(json));
        if (data.Width <= 0 || data.Height <= 0) throw new SaveFormatException("the map size is not positive");
        if (data.Tick < 0) throw new SaveFormatException("the tick is negative");
        return new SaveInfo(data.Version, data.Width, data.Height, data.Tick, data.Funds, CityDate.FromTick(data.Tick));
    }

    private static Data Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Data>(json, Options)
                ?? throw new SaveFormatException("the save is empty");
        }
        catch (JsonException e)
        {
            throw new SaveFormatException($"the save is not valid JSON: {e.Message}", e);
        }
    }

    public static Simulation Deserialize(string json, PriceList? prices = null, PowerRules? power = null, Content.ContentSet? content = null)
    {
        var data = Upgrade(Parse(json));

        if (data.Width <= 0 || data.Height <= 0) throw new SaveFormatException("the map size is not positive");
        if (data.Tick < 0) throw new SaveFormatException("the tick is negative");
        if (data.Kinds is null || data.Kinds.Length != data.Height) throw new SaveFormatException("the tile rows do not match the map height");
        if (data.Heights is null || data.Heights.Length != data.Height) throw new SaveFormatException("the height rows do not match the map height");

        var sim = new Simulation(data.Width, data.Height, 0, prices, power, content);
        for (int y = 0; y < data.Height; y++)
        {
            if (data.Kinds[y] is not { } row || row.Length != data.Width)
                throw new SaveFormatException($"tile row {y} does not match the map width");
            if (data.Heights[y] is not { } hs || hs.Length != data.Width)
                throw new SaveFormatException($"height row {y} does not match the map width");

            for (int x = 0; x < data.Width; x++)
            {
                int kind = KindChars.IndexOf(row[x]);
                if (kind < 0) throw new SaveFormatException($"unknown tile '{row[x]}' at ({x},{y})");
                if (hs[x] is < 0 or > byte.MaxValue) throw new SaveFormatException($"height out of range at ({x},{y})");
                sim.Map[x, y].Kind = (TileKind)kind;
                sim.Map[x, y].Height = (byte)hs[x];
            }
        }

        var report = new List<string>();
        foreach (var b in data.Buildings ?? [])
        {
            if (!sim.Map.InBounds(b.X, b.Y)) throw new SaveFormatException($"building at ({b.X},{b.Y}) is off the map");
            if (!sim.Map[b.X, b.Y].IsZone) throw new SaveFormatException($"building at ({b.X},{b.Y}) is not on a zone");
            // A type whose pack is not loaded: the zone stays and will grow again.
            if (content is null || !content.TryFind(b.Type, out var handle))
                report.Add($"'{b.Type}' at ({b.X},{b.Y}) is not in the loaded content; the zone will grow again");
            else
                sim.Map[b.X, b.Y].Building = handle;
        }
        foreach (var s in data.Stress ?? [])
        {
            if (!sim.Map.InBounds(s.X, s.Y)) throw new SaveFormatException($"stress at ({s.X},{s.Y}) is off the map");
            if (s.Months < 0) throw new SaveFormatException($"stress at ({s.X},{s.Y}) is negative");
            if (!sim.Map[s.X, s.Y].Building.IsNone) sim.RestoreStress(s.X, s.Y, s.Months, s.Abandoned);
        }
        Demand? demand = data.Demand is { Length: 3 } d ? new Demand(d[0], d[1], d[2]) : null;
        if (data.Demand is not null && demand is null) throw new SaveFormatException("demand must have three values");
        TaxRates? taxes = data.Taxes switch
        {
            null => null,
            { Length: 3 } t when t.All(r => r is >= BudgetRules.MinTax and <= BudgetRules.MaxTax) => new TaxRates(t[0], t[1], t[2]),
            _ => throw new SaveFormatException($"taxes must be three rates from {BudgetRules.MinTax} to {BudgetRules.MaxTax}"),
        };
        MonthlyBudget? lastBudget = data.LastBudget is { } lb
            ? new MonthlyBudget(lb.ResidentialTax, lb.CommercialTax, lb.IndustrialTax, lb.RoadUpkeep, lb.PowerLineUpkeep, lb.PlantUpkeep, lb.ZoneUpkeep)
            : null;
        sim.RestoreBudget(data.Funds, taxes, lastBudget); // funds may be negative (debt)
        sim.Restore(data.Tick, data.Random ?? CityRandom.DefaultSeed, demand);
        sim.LoadReport = report;
        return sim;
    }

    // One step per version, oldest first. Version 1 is current, so nothing to do yet.
    private static Data Upgrade(Data data)
    {
        if (data.Version < 1) throw new SaveFormatException("the save has no valid version");
        if (data.Version > CurrentVersion)
            throw new SaveFormatException($"the save is from a newer game (version {data.Version}); this one reads up to {CurrentVersion}");
        return data;
    }
}
