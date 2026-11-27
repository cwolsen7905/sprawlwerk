namespace SprawlWerk.Sim.Tests;

/// <summary><see cref="Simulation.Quote"/> is the placement ghost's preview: it must match what Step charges and change nothing.</summary>
public class QuoteTests
{
    private const int Month = CityDate.TicksPerMonth;

    // A small grown town, so bulldozing has buildings and lines to clear.
    private static Simulation Town(long funds = 1_000_000)
    {
        var sim = new Simulation(8, 4, startingFunds: funds, content: TestContent.Load());
        sim.Enqueue(new PlaceCommand(0, 0, 8, 1, TileKind.Road));
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 1, 3, 2, TileKind.Residential));
        sim.Enqueue(new PlaceCommand(4, 3, 4, 1, TileKind.PowerLine));
        for (int i = 0; i < 2 * Month + 10; i++) sim.Step();
        return sim;
    }

    private static readonly string[] Names = ["zone", "road-drag", "line-over-road", "bulldoze", "plant", "nothing", "offmap", "tax", "bad-tax"];

    public static TheoryData<string> Commands() => new(Names);

    private static ICommand Make(string name) => name switch
    {
        "zone" => Drag.Command(TileKind.Commercial, 4, 1, 7, 2),
        "road-drag" => Drag.Command(TileKind.Road, 0, 3, 3, 2), // straight along x
        "line-over-road" => new PlaceCommand(0, 0, 8, 1, TileKind.PowerLine),
        "bulldoze" => Drag.Command(TileKind.Empty, 0, 0, 3, 2),
        "plant" => new PlaceCommand(7, 2, TileKind.PowerPlant),
        "nothing" => new PlaceCommand(0, 0, 8, 1, TileKind.Road),
        "offmap" => new BulldozeCommand(20, 20, 2, 2),
        "tax" => new SetTaxCommand(Content.ZoneKind.Commercial, 9),
        _ => new SetTaxCommand(Content.ZoneKind.Commercial, BudgetRules.MaxTax + 1),
    };

    [Theory]
    [MemberData(nameof(Commands))]
    public void A_quote_matches_what_the_step_charges_and_refuses(string name)
    {
        var sim = Town();
        var command = Make(name);
        var quote = sim.Quote(command);
        long before = sim.Funds;

        sim.Enqueue(command);
        sim.Step();
        var outcome = sim.LastOutcomes[0].Result;
        Assert.Equal(quote, outcome);
        Assert.False(CityDate.IsMonthStart(sim.Tick - 1)); // no month settled in this step
        Assert.Equal(quote.Accepted ? before - quote.Cost : before, sim.Funds);
    }

    [Fact]
    public void Quotes_cover_the_expected_refusals()
    {
        var sim = Town();
        Assert.Equal(Refusal.NothingToChange, sim.Quote(Make("nothing")).Refusal);
        Assert.Equal(Refusal.OutOfBounds, sim.Quote(Make("offmap")).Refusal);
        Assert.Equal(Refusal.OutOfRange, sim.Quote(Make("bad-tax")).Refusal);
        Assert.True(sim.Quote(Make("bulldoze")).Accepted);
    }

    [Fact]
    public void A_quote_the_city_cannot_afford_is_refused_with_its_cost_like_the_step()
    {
        var sim = Town();
        var plant = Make("plant");
        long cost = sim.Quote(plant).Cost;
        var poor = Town(funds: 0); // a city with no money: even its own setup was refused
        var quote = poor.Quote(plant);
        Assert.Equal(Refusal.InsufficientFunds, quote.Refusal);
        Assert.Equal(cost, quote.Cost);

        poor.Enqueue(plant);
        poor.Step();
        Assert.Equal(quote, poor.LastOutcomes[0].Result);
    }

    [Fact]
    public void Quoting_changes_nothing()
    {
        var sim = Town();
        long funds = sim.Funds, revision = sim.MapRevision, tick = sim.Tick;
        ulong random = sim.Random.State;
        var tiles = Snapshot(sim);

        foreach (var name in Names) sim.Quote(Make(name));

        Assert.Equal(funds, sim.Funds);
        Assert.Equal(revision, sim.MapRevision);
        Assert.Equal(tick, sim.Tick);
        Assert.Equal(random, sim.Random.State);
        Assert.Equal(sim.Growth.NeutralTax, sim.Taxes.Commercial);
        Assert.Equal(tiles, Snapshot(sim));
    }

    [Fact]
    public void Quoting_every_tick_does_not_change_how_the_city_grows()
    {
        Simulation Run(bool quote)
        {
            var sim = Town();
            for (int i = 0; i < 4 * Month; i++)
            {
                if (quote) foreach (var name in Names) sim.Quote(Make(name));
                sim.Step();
            }
            return sim;
        }

        var plain = Run(false);
        var quoted = Run(true);
        Assert.Equal(plain.Random.State, quoted.Random.State);
        Assert.Equal(plain.Funds, quoted.Funds);
        Assert.Equal(Snapshot(plain), Snapshot(quoted));
    }

    private static List<(TileKind, ushort)> Snapshot(Simulation sim)
    {
        var list = new List<(TileKind, ushort)>();
        for (int y = 0; y < sim.Map.Height; y++)
        for (int x = 0; x < sim.Map.Width; x++) list.Add((sim.Map[x, y].Kind, sim.Map[x, y].Building.Value));
        return list;
    }
}
