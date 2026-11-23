namespace SprawlWerk.Sim;

/// <summary>
/// Everything a player does reaches the simulation as a command. Keeping
/// input as data (not direct mutation from the UI) is what makes replays,
/// undo, save files and, later, multiplayer straightforward.
/// </summary>
public interface ICommand
{
    /// <returns>True if the command changed the map.</returns>
    bool Apply(CityMap map);
}

/// <summary>Set every tile in a rectangle to one kind (zone, road, line, ...).</summary>
public sealed record PlaceCommand(int X, int Y, int Width, int Height, TileKind Kind) : ICommand
{
    public PlaceCommand(int x, int y, TileKind kind) : this(x, y, 1, 1, kind) { }

    public bool Apply(CityMap map)
    {
        bool changed = false;
        for (int y = Y; y < Y + Height; y++)
        for (int x = X; x < X + Width; x++)
        {
            if (!map.InBounds(x, y)) continue;
            ref var tile = ref map[x, y];
            if (tile.Kind == Kind) continue;
            tile.Kind = Kind;
            changed = true;
        }
        return changed;
    }
}

/// <summary>Return a rectangle of tiles to empty land.</summary>
public sealed record BulldozeCommand(int X, int Y, int Width = 1, int Height = 1) : ICommand
{
    public bool Apply(CityMap map) => new PlaceCommand(X, Y, Width, Height, TileKind.Empty).Apply(map);
}
