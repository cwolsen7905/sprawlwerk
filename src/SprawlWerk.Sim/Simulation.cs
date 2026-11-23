namespace SprawlWerk.Sim;

/// <summary>
/// The whole city and its clock. Advances in fixed ticks, independent of
/// frame rate: the renderer can draw at 144 fps or 30, and the city still
/// evolves identically.
/// </summary>
public sealed class Simulation
{
    private readonly Queue<ICommand> _pending = new();
    private bool _networksDirty = true;

    public CityMap Map { get; }

    /// <summary>Ticks elapsed since the city was founded.</summary>
    public long Tick { get; private set; }

    public int PoweredTiles { get; private set; }

    public Simulation(int width, int height)
    {
        Map = new CityMap(width, height);
    }

    /// <summary>Queue a player action; it is applied at the start of the next tick.</summary>
    public void Enqueue(ICommand command) => _pending.Enqueue(command);

    /// <summary>Advance the city by one tick.</summary>
    public void Step()
    {
        while (_pending.Count > 0)
        {
            if (_pending.Dequeue().Apply(Map))
                _networksDirty = true;
        }

        // Networks only change when the map does, so skip the flood fill otherwise.
        if (_networksDirty)
        {
            PoweredTiles = PowerGrid.Recompute(Map);
            _networksDirty = false;
        }

        Tick++;
    }
}
