namespace SprawlWerk.Sim;

/// <summary>
/// How far each tile is from the nearest road, in four-neighbour steps over
/// any tile, up to a limit. Derived state like <see cref="PowerGrid"/>: rebuilt
/// from the map when it changes, never saved.
/// </summary>
public sealed class RoadAccess
{
    private readonly int[] _distance;

    public int Width { get; }
    public int Height { get; }

    /// <summary>Furthest a tile may be from a road and still have access.</summary>
    public int MaxDistance { get; }

    public RoadAccess(int width, int height, int maxDistance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxDistance);
        Width = width;
        Height = height;
        MaxDistance = maxDistance;
        _distance = new int[width * height];
        Array.Fill(_distance, -1);
    }

    public static bool CarriesRoad(TileKind kind) => kind is TileKind.Road or TileKind.RoadWithLine;

    /// <summary>Steps to the nearest road, or -1 when it is further than <see cref="MaxDistance"/>.</summary>
    public int Distance(int x, int y) => _distance[Index(x, y)];

    public bool HasAccess(int x, int y) => _distance[Index(x, y)] >= 0;

    private int Index(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            throw new ArgumentOutOfRangeException($"({x},{y}) is outside a {Width}x{Height} grid");
        return y * Width + x;
    }

    /// <summary>Breadth-first from every road tile at once, in index order.</summary>
    public void Recompute(CityMap map)
    {
        if (map.Width != Width || map.Height != Height)
            throw new ArgumentException($"map is {map.Width}x{map.Height}, grid is {Width}x{Height}", nameof(map));
        Array.Fill(_distance, -1);
        var queue = new Queue<int>();
        for (int i = 0; i < _distance.Length; i++)
        {
            if (!CarriesRoad(map[i % Width, i / Width].Kind)) continue;
            _distance[i] = 0;
            queue.Enqueue(i);
        }
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            if (_distance[cur] == MaxDistance) continue;
            foreach (var (nx, ny) in map.Neighbours(cur % Width, cur / Width))
            {
                int ni = ny * Width + nx;
                if (_distance[ni] >= 0) continue;
                _distance[ni] = _distance[cur] + 1;
                queue.Enqueue(ni);
            }
        }
    }
}
