using SprawlWerk.Sim.Content;

namespace SprawlWerk.Sim;

/// <summary>
/// The whole city and its clock. Advances in fixed ticks, independent of
/// frame rate: the renderer can draw at 144 fps or 30, and the city still
/// evolves identically.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>Funds a new city starts with unless told otherwise (a placeholder).</summary>
    public const long DefaultStartingFunds = 20_000;

    private readonly Queue<ICommand> _pending = new();
    private readonly List<CommandOutcome> _lastOutcomes = new();
    private bool _networksDirty = true;

    public CityMap Map { get; }

    public PriceList Prices { get; }

    /// <summary>Money the city has; negative is debt. Commands it cannot afford are refused whole.</summary>
    public long Funds { get; private set; }

    /// <summary>Ticks elapsed since the city was founded.</summary>
    public long Tick { get; private set; }

    /// <summary>The city's date, derived from <see cref="Tick"/>.</summary>
    public CityDate Date => CityDate.FromTick(Tick);

    /// <summary>Which tiles have power; derived from <see cref="Map"/>, rebuilt when it changes.</summary>
    public PowerGrid Power { get; }

    public int PoweredTiles => Power.PoweredCount;

    /// <summary>Every command applied or refused in the last <see cref="Step"/>, in order.</summary>
    public IReadOnlyList<CommandOutcome> LastOutcomes => _lastOutcomes;

    /// <summary>
    /// The content packs this city runs on, or null for the built-in placeholder
    /// numbers. With content, prices and power rules come from its types unless
    /// passed in explicitly.
    /// </summary>
    public ContentSet? Content { get; }

    /// <summary>Numbers for demand, growth and decline (ADR 0012).</summary>
    public GrowthRules Growth { get; }

    /// <summary>The city's only source of chance; its state is saved.</summary>
    public CityRandom Random { get; }

    /// <summary>How far each tile is from a road; derived, rebuilt with the networks.</summary>
    public RoadAccess Roads { get; }

    /// <summary>People living in buildings that are lived in (abandonment comes with decline).</summary>
    public int Residents { get; private set; }

    public int CommercialJobs { get; private set; }

    public int IndustrialJobs { get; private set; }

    /// <summary>
    /// Goes up every time something a view draws may have changed: the map, by
    /// a command or by the city itself (growth), or the networks recomputed
    /// from it. A view can redraw only when it moves. Not saved.
    /// </summary>
    public long MapRevision { get; private set; }

    /// <summary>What a load could not keep (e.g. buildings from a pack that is not loaded); empty otherwise.</summary>
    public IReadOnlyList<string> LoadReport { get; internal set; } = [];

    public Simulation(int width, int height, long startingFunds = DefaultStartingFunds, PriceList? prices = null,
        PowerRules? power = null, ContentSet? content = null, GrowthRules? growth = null, ulong seed = CityRandom.DefaultSeed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startingFunds);
        Content = content;
        Map = new CityMap(width, height);
        Power = new PowerGrid(width, height, power ?? (content is null ? null : PowerRules.FromContent(content)));
        Funds = startingFunds;
        Prices = prices ?? (content is null ? PriceList.Default : PriceList.FromContent(content));
        Growth = growth ?? GrowthRules.Default;
        Random = new CityRandom(seed);
        Roads = new RoadAccess(width, height, Growth.RoadAccess);
    }

    /// <summary>Set the clock and rebuild derived state after a load; the map is already filled in.</summary>
    internal void Restore(long tick, ulong randomState, Demand? demand)
    {
        Tick = tick;
        Random.State = randomState;
        if (demand is { } d) { Demand = d; _demandStarted = true; }
        RecomputeNetworks();
        Recount();
    }

    private void RecomputeNetworks()
    {
        Power.Recompute(Map, Content);
        Roads.Recompute(Map);
        _networksDirty = false;
    }

    /// <summary>
    /// Residents and jobs: each tile's building counts its type's numbers once
    /// (every grown building is one tile until multi-tile lots).
    /// </summary>
    private void Recount()
    {
        int residents = 0, commercial = 0, industrial = 0;
        if (Content is { } content)
        {
            for (int y = 0; y < Map.Height; y++)
            for (int x = 0; x < Map.Width; x++)
            {
                var handle = Map[x, y].Building;
                if (handle.IsNone || IsAbandoned(x, y)) continue;
                var type = content[handle];
                residents += type.Residents;
                if (type.Zone == ZoneKind.Commercial) commercial += type.Jobs;
                else if (type.Zone == ZoneKind.Industrial) industrial += type.Jobs;
            }
        }
        (Residents, CommercialJobs, IndustrialJobs) = (residents, commercial, industrial);
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
        if (plan.Accepted && plan.Cost > 0 && plan.Cost > Funds) // a free command goes through even in debt
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
            if (result.Accepted && command is ICitySetting setting)
            {
                setting.Apply(this);
                Funds -= result.Cost;
            }
            else if (result.Accepted)
            {
                command.Execute(Map);
                Funds -= result.Cost;
                MapRevision++;
                _networksDirty = true;
            }
            _lastOutcomes.Add(new CommandOutcome(command, result));
        }

        // Order within a tick (ADR 0012): commands; networks if the map changed;
        // then, as they arrive, the monthly passes (decline, economy, demand) and
        // the growth pass, each followed by a recount.
        // Networks only change when the map does, so skip the flood fills otherwise.
        if (_networksDirty)
        {
            RecomputeNetworks();
            Recount();
            MapRevision++;
        }

        if (CityDate.IsMonthStart(Tick))
        {
            var (cleared, changed) = DeclinePass();
            if (cleared) _networksDirty = true; // cleared buildings use less power
            if (changed) MapRevision++;
            Recount();
            SettleMonth(); // after decline, so abandoned buildings pay no tax
            UpdateDemand();
        }

        if (Tick % Growth.GrowthEvery == Growth.GrowthOffset && GrowthPass())
        {
            _networksDirty = true; // new buildings draw power; recompute next tick
            MapRevision++;
            Recount();
        }

        Tick++;
    }
}
