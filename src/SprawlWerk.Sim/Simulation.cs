namespace SprawlWerk.Sim;

/// <summary>
/// The whole city and its clock. Advances in fixed ticks, independent of
/// frame rate: the renderer can draw at 144 fps or 30, and the city still
/// evolves identically.
/// </summary>
public sealed class Simulation
{
    /// <summary>Funds a new city starts with unless told otherwise (a placeholder).</summary>
    public const long DefaultStartingFunds = 20_000;

    private readonly Queue<ICommand> _pending = new();
    private readonly List<CommandOutcome> _lastOutcomes = new();
    private bool _networksDirty = true;

    public CityMap Map { get; }

    public PriceList Prices { get; }

    /// <summary>Money the city has. Commands it cannot afford are refused whole.</summary>
    public long Funds { get; private set; }

    /// <summary>Ticks elapsed since the city was founded.</summary>
    public long Tick { get; private set; }

    public int PoweredTiles { get; private set; }

    /// <summary>Every command applied or refused in the last <see cref="Step"/>, in order.</summary>
    public IReadOnlyList<CommandOutcome> LastOutcomes => _lastOutcomes;

    public Simulation(int width, int height, long startingFunds = DefaultStartingFunds, PriceList? prices = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startingFunds);
        Map = new CityMap(width, height);
        Funds = startingFunds;
        Prices = prices ?? PriceList.Default;
    }

    /// <summary>Queue a player action; it is applied at the start of the next tick.</summary>
    public void Enqueue(ICommand command) => _pending.Enqueue(command);

    /// <summary>
    /// What the command would cost if applied now, or why it would be refused,
    /// without changing anything. Commands still queued are not taken into account.
    /// </summary>
    public CommandResult Quote(ICommand command)
    {
        var plan = command.Plan(Map, Prices);
        if (plan.Accepted && plan.Cost > Funds)
            return CommandResult.Refuse(Refusal.InsufficientFunds, plan.Cost);
        return plan;
    }

    /// <summary>Advance the city by one tick.</summary>
    public void Step()
    {
        _lastOutcomes.Clear();
        while (_pending.Count > 0)
        {
            var command = _pending.Dequeue();
            var result = Quote(command);
            if (result.Accepted)
            {
                command.Execute(Map);
                Funds -= result.Cost;
                _networksDirty = true;
            }
            _lastOutcomes.Add(new CommandOutcome(command, result));
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
