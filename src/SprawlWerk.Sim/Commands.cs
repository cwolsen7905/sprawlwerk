namespace SprawlWerk.Sim;

/// <summary>
/// Everything a player does reaches the simulation as a command. Keeping
/// input as data (not direct mutation from the UI) is what makes replays,
/// undo, save files and, later, multiplayer straightforward.
///
/// The simulation asks a command for its plan, checks the city can pay,
/// and only then executes it, so a command never handles money itself.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// What this command would do to the map: its cost and how many tiles
    /// change, or why it cannot run. Must not change the map.
    /// </summary>
    CommandResult Plan(CityMap map, PriceList prices);

    /// <summary>Carry the command out. Called only after its plan was accepted and paid for.</summary>
    void Execute(CityMap map);
}

/// <summary>Set every tile in a rectangle to one kind (zone, road, line, ...).</summary>
public sealed record PlaceCommand(int X, int Y, int Width, int Height, TileKind Kind) : ICommand
{
    public PlaceCommand(int x, int y, TileKind kind) : this(x, y, 1, 1, kind) { }

    public CommandResult Plan(CityMap map, PriceList prices)
    {
        int onMap = 0, changed = 0;
        long cost = 0;
        for (int y = Y; y < Y + Height; y++)
        for (int x = X; x < X + Width; x++)
        {
            if (!map.InBounds(x, y)) continue;
            onMap++;
            var current = map[x, y].Kind;
            if (current == Kind) continue;
            changed++;
            cost += Kind == TileKind.Empty ? prices.Demolish : prices.Build(Kind);
        }

        if (onMap == 0) return CommandResult.Refuse(Refusal.OutOfBounds);
        if (changed == 0) return CommandResult.Refuse(Refusal.NothingToChange);
        return CommandResult.Accept(cost, changed);
    }

    public void Execute(CityMap map)
    {
        for (int y = Y; y < Y + Height; y++)
        for (int x = X; x < X + Width; x++)
        {
            if (map.InBounds(x, y)) map[x, y].Kind = Kind;
        }
    }
}

/// <summary>Return a rectangle of tiles to empty land.</summary>
public sealed record BulldozeCommand(int X, int Y, int Width = 1, int Height = 1) : ICommand
{
    private PlaceCommand AsClear => new(X, Y, Width, Height, TileKind.Empty);

    public CommandResult Plan(CityMap map, PriceList prices) => AsClear.Plan(map, prices);

    public void Execute(CityMap map) => AsClear.Execute(map);
}
