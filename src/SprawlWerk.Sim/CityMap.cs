namespace SprawlWerk.Sim;

/// <summary>A fixed-size grid of tiles, stored row-major in one array.</summary>
public sealed class CityMap
{
    private readonly Tile[] _tiles;

    public int Width { get; }
    public int Height { get; }

    public CityMap(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
        _tiles = new Tile[width * height];
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>Direct access by reference, so callers can mutate in place.</summary>
    public ref Tile this[int x, int y]
    {
        get
        {
            if (!InBounds(x, y))
                throw new ArgumentOutOfRangeException($"({x},{y}) is outside a {Width}x{Height} map");
            return ref _tiles[y * Width + x];
        }
    }

    /// <summary>The four edge-adjacent neighbours that are on the map.</summary>
    public IEnumerable<(int X, int Y)> Neighbours(int x, int y)
    {
        if (InBounds(x + 1, y)) yield return (x + 1, y);
        if (InBounds(x - 1, y)) yield return (x - 1, y);
        if (InBounds(x, y + 1)) yield return (x, y + 1);
        if (InBounds(x, y - 1)) yield return (x, y - 1);
    }
}
