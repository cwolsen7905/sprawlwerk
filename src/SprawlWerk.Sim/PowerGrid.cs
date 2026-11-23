namespace SprawlWerk.Sim;

/// <summary>
/// Power connectivity: a flood fill outward from every power plant through
/// tiles that conduct (zones, power lines, plants). Anything the fill
/// reaches is powered; everything else is not.
/// </summary>
public static class PowerGrid
{
    /// <returns>The number of powered tiles.</returns>
    public static int Recompute(CityMap map)
    {
        var queue = new Queue<(int X, int Y)>();

        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            ref var tile = ref map[x, y];
            tile.Powered = tile.Kind == TileKind.PowerPlant;
            if (tile.Powered) queue.Enqueue((x, y));
        }

        int powered = queue.Count;
        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            foreach (var (nx, ny) in map.Neighbours(cx, cy))
            {
                ref var n = ref map[nx, ny];
                if (n.Powered || !n.ConductsPower) continue;
                n.Powered = true;
                powered++;
                queue.Enqueue((nx, ny));
            }
        }

        return powered;
    }
}
