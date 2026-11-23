namespace SprawlWerk.Sim;

/// <summary>Placeholder numbers for the power rules, until plants become data-defined types.</summary>
/// <param name="PlantCapacity">Units of power one plant supplies.</param>
/// <param name="DemandPerZone">Units one zoned tile uses.</param>
public sealed record PowerRules(int PlantCapacity = 40, int DemandPerZone = 1)
{
    public static PowerRules Default { get; } = new();
}

/// <summary>
/// Power: connectivity, then capacity.
///
/// Connectivity is a flood fill outward from every plant through tiles that
/// conduct (zones, power lines, plants, roads carrying a line). Each connected
/// network then has a supply, its plants' capacity, and a demand, one unit per
/// zoned tile it reaches. When supply is short, the zones nearest a plant are
/// powered first and the far ones go dark; a tie in distance goes to the lower
/// tile index so the result never depends on traversal order.
///
/// The result is derived state, owned here rather than stored on the tiles:
/// it is rebuilt from the map whenever the map changes, and never saved.
/// </summary>
public sealed class PowerGrid
{
    private readonly bool[] _powered;
    private readonly int[] _component;
    private readonly int[] _distance;

    public int Width { get; }
    public int Height { get; }
    public PowerRules Rules { get; }

    /// <summary>Tiles powered after the last <see cref="Recompute"/> (plants, lines and supplied zones).</summary>
    public int PoweredCount { get; private set; }

    /// <summary>Zoned tiles connected to a plant, whether or not there is power for them.</summary>
    public int ConnectedZones { get; private set; }

    /// <summary>Connected zoned tiles that get power.</summary>
    public int PoweredZones { get; private set; }

    /// <summary>Connected zoned tiles left dark because their network is over capacity.</summary>
    public int ZonesWithoutSupply => ConnectedZones - PoweredZones;

    /// <summary>Total capacity of every plant on the map.</summary>
    public long Supply { get; private set; }

    /// <summary>Total demand of every connected zoned tile.</summary>
    public long Demand => (long)ConnectedZones * Rules.DemandPerZone;

    public PowerGrid(int width, int height, PowerRules? rules = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        Rules = rules ?? PowerRules.Default;
        ArgumentOutOfRangeException.ThrowIfNegative(Rules.PlantCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Rules.DemandPerZone);
        _powered = new bool[width * height];
        _component = new int[width * height];
        _distance = new int[width * height];
    }

    public bool IsPowered(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            throw new ArgumentOutOfRangeException($"({x},{y}) is outside a {Width}x{Height} grid");
        return _powered[y * Width + x];
    }

    /// <summary>Rebuild from the map alone; earlier results play no part.</summary>
    /// <returns>The number of powered tiles.</returns>
    public int Recompute(CityMap map)
    {
        if (map.Width != Width || map.Height != Height)
            throw new ArgumentException($"map is {map.Width}x{map.Height}, grid is {Width}x{Height}", nameof(map));

        int n = Width * Height;
        Array.Fill(_powered, false);
        Array.Fill(_component, -1);
        Array.Fill(_distance, -1);

        // Pass 1: label each network and count its plants.
        var plantsPerNetwork = new List<int>();
        var queue = new Queue<int>();
        long plants = 0;
        for (int i = 0; i < n; i++)
        {
            if (map[i % Width, i / Width].Kind != TileKind.PowerPlant || _component[i] >= 0) continue;
            int id = plantsPerNetwork.Count;
            int count = 0;
            _component[i] = id;
            queue.Enqueue(i);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                if (map[cur % Width, cur / Width].Kind == TileKind.PowerPlant) count++;
                foreach (var (nx, ny) in map.Neighbours(cur % Width, cur / Width))
                {
                    int ni = ny * Width + nx;
                    if (_component[ni] >= 0 || !map[nx, ny].ConductsPower) continue;
                    _component[ni] = id;
                    queue.Enqueue(ni);
                }
            }
            plantsPerNetwork.Add(count);
            plants += count;
        }
        Supply = plants * Rules.PlantCapacity;

        // Pass 2: distance from the nearest plant, over the same conducting tiles.
        for (int i = 0; i < n; i++)
        {
            if (map[i % Width, i / Width].Kind != TileKind.PowerPlant) continue;
            _distance[i] = 0;
            queue.Enqueue(i);
        }
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            foreach (var (nx, ny) in map.Neighbours(cur % Width, cur / Width))
            {
                int ni = ny * Width + nx;
                if (_distance[ni] >= 0 || !map[nx, ny].ConductsPower) continue;
                _distance[ni] = _distance[cur] + 1;
                queue.Enqueue(ni);
            }
        }

        // Plants, lines and the like are always live; zones draw on the supply.
        int powered = 0;
        var zones = new List<(int Network, int Distance, int Index)>();
        for (int i = 0; i < n; i++)
        {
            if (_component[i] < 0) continue;
            if (map[i % Width, i / Width].IsZone) zones.Add((_component[i], _distance[i], i));
            else { _powered[i] = true; powered++; }
        }

        zones.Sort();
        var remaining = new long[plantsPerNetwork.Count];
        for (int id = 0; id < remaining.Length; id++)
            remaining[id] = (long)plantsPerNetwork[id] * Rules.PlantCapacity;

        int poweredZones = 0;
        foreach (var (network, _, index) in zones)
        {
            if (remaining[network] < Rules.DemandPerZone) continue;
            remaining[network] -= Rules.DemandPerZone;
            _powered[index] = true;
            poweredZones++;
        }

        ConnectedZones = zones.Count;
        PoweredZones = poweredZones;
        PoweredCount = powered + poweredZones;
        return PoweredCount;
    }
}
