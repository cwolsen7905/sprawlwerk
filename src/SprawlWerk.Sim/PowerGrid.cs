namespace SprawlWerk.Sim;

/// <summary>
/// Power connectivity: a flood fill outward from every power plant through
/// tiles that conduct (zones, power lines, plants). Anything the fill
/// reaches is powered; everything else is not.
///
/// The result is derived state, owned here rather than stored on the tiles:
/// it is rebuilt from the map whenever the map changes, and never saved.
/// </summary>
public sealed class PowerGrid
{
    private readonly bool[] _powered;

    public int Width { get; }
    public int Height { get; }

    /// <summary>Tiles powered after the last <see cref="Recompute"/>.</summary>
    public int PoweredCount { get; private set; }

    public PowerGrid(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        _powered = new bool[width * height];
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

        var queue = new Queue<(int X, int Y)>();
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            bool plant = map[x, y].Kind == TileKind.PowerPlant;
            _powered[y * Width + x] = plant;
            if (plant) queue.Enqueue((x, y));
        }

        int powered = queue.Count;
        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            foreach (var (nx, ny) in map.Neighbours(cx, cy))
            {
                int i = ny * Width + nx;
                if (_powered[i] || !map[nx, ny].ConductsPower) continue;
                _powered[i] = true;
                powered++;
                queue.Enqueue((nx, ny));
            }
        }

        PoweredCount = powered;
        return powered;
    }
}
