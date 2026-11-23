namespace SprawlWerk.Sim;

/// <summary>
/// Turns a drag between two tiles into one command: a filled rectangle for
/// zones and bulldozing, a straight line for roads and power lines, a single
/// tile for plants. The view uses it for the live preview and for the command
/// it finally sends, so the previewed cost is the real cost.
/// </summary>
public static class Drag
{
    /// <param name="kind">The tool; <see cref="TileKind.Empty"/> means bulldoze.</param>
    public static ICommand Command(TileKind kind, int x0, int y0, int x1, int y1)
    {
        switch (kind)
        {
            case TileKind.PowerPlant:
                return new PlaceCommand(x1, y1, kind);
            case TileKind.Road or TileKind.PowerLine:
                // Straight along whichever axis the drag covers more.
                if (Math.Abs(x1 - x0) >= Math.Abs(y1 - y0)) y1 = y0; else x1 = x0;
                break;
        }

        int x = Math.Min(x0, x1), y = Math.Min(y0, y1);
        int w = Math.Abs(x1 - x0) + 1, h = Math.Abs(y1 - y0) + 1;
        return kind == TileKind.Empty
            ? new BulldozeCommand(x, y, w, h)
            : new PlaceCommand(x, y, w, h, kind);
    }
}
