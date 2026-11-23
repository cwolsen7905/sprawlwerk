using System.Text.Json;
using System.Text.Json.Serialization;

namespace SprawlWerk.Sim;

/// <summary>A save that cannot be read: malformed, damaged, or from a newer game.</summary>
public sealed class SaveFormatException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Header facts of a save: enough for a save list.</summary>
public readonly record struct SaveInfo(int Version, int Width, int Height, long Tick, long Funds, CityDate Date);

/// <summary>
/// Saves and loads a city as versioned JSON. Only stored state goes in: the
/// terrain and what is built, funds and the tick. Derived state (power) is
/// rebuilt on load, and commands still queued are not saved, so save between
/// ticks, as the game does. Prices and power rules are game configuration,
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
            Kinds = kinds,
            Heights = heights,
        }, Options);
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

    public static Simulation Deserialize(string json, PriceList? prices = null, PowerRules? power = null)
    {
        var data = Upgrade(Parse(json));

        if (data.Width <= 0 || data.Height <= 0) throw new SaveFormatException("the map size is not positive");
        if (data.Tick < 0) throw new SaveFormatException("the tick is negative");
        if (data.Funds < 0) throw new SaveFormatException("the funds are negative");
        if (data.Kinds is null || data.Kinds.Length != data.Height) throw new SaveFormatException("the tile rows do not match the map height");
        if (data.Heights is null || data.Heights.Length != data.Height) throw new SaveFormatException("the height rows do not match the map height");

        var sim = new Simulation(data.Width, data.Height, data.Funds, prices, power);
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

        sim.Restore(data.Tick);
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
