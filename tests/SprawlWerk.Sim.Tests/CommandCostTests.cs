using SprawlWerk.Sim;

namespace SprawlWerk.Sim.Tests;

public class CommandCostTests
{
    private static readonly PriceList Prices = new(
        new Dictionary<TileKind, long>
        {
            [TileKind.Residential] = 10,
            [TileKind.Road] = 5,
            [TileKind.PowerPlant] = 1000,
        },
        demolish: 1);

    private static Simulation NewSim(long funds = 10_000, int size = 8) => new(size, size, funds, Prices);

    [Fact]
    public void Placing_charges_the_price_of_each_changed_tile()
    {
        var sim = NewSim();
        sim.Enqueue(new PlaceCommand(0, 0, 3, 2, TileKind.Residential));
        sim.Step();

        var result = Assert.Single(sim.LastOutcomes).Result;
        Assert.True(result.Accepted);
        Assert.Equal(60, result.Cost);
        Assert.Equal(6, result.TilesChanged);
        Assert.Equal(10_000 - 60, sim.Funds);
    }

    [Fact]
    public void Tiles_already_of_that_kind_are_free()
    {
        var sim = NewSim();
        sim.Enqueue(new PlaceCommand(0, 0, 2, 1, TileKind.Residential));
        sim.Step();
        sim.Enqueue(new PlaceCommand(0, 0, 4, 1, TileKind.Residential));
        sim.Step();

        var result = sim.LastOutcomes[0].Result;
        Assert.Equal(20, result.Cost);
        Assert.Equal(2, result.TilesChanged);
    }

    [Fact]
    public void Placing_what_is_already_there_is_refused_and_free()
    {
        var sim = NewSim();
        sim.Enqueue(new PlaceCommand(1, 1, TileKind.Road));
        sim.Step();
        long funds = sim.Funds;

        sim.Enqueue(new PlaceCommand(1, 1, TileKind.Road));
        sim.Step();

        Assert.Equal(Refusal.NothingToChange, sim.LastOutcomes[0].Result.Refusal);
        Assert.Equal(funds, sim.Funds);
    }

    [Fact]
    public void A_command_entirely_off_the_map_is_refused()
    {
        var sim = NewSim(size: 4);
        sim.Enqueue(new PlaceCommand(10, 10, 2, 2, TileKind.Road));
        sim.Step();

        Assert.Equal(Refusal.OutOfBounds, sim.LastOutcomes[0].Result.Refusal);
        Assert.Equal(10_000, sim.Funds);
    }

    [Fact]
    public void Only_tiles_on_the_map_are_charged()
    {
        var sim = NewSim(size: 4);
        sim.Enqueue(new PlaceCommand(2, 2, 5, 5, TileKind.Road)); // 2x2 of it is on the map
        sim.Step();

        Assert.Equal(20, sim.LastOutcomes[0].Result.Cost);
    }

    [Fact]
    public void A_command_the_city_cannot_afford_is_refused_whole()
    {
        var sim = NewSim(funds: 45);
        sim.Enqueue(new PlaceCommand(0, 0, 5, 1, TileKind.Residential)); // 50
        sim.Step();

        var result = sim.LastOutcomes[0].Result;
        Assert.Equal(Refusal.InsufficientFunds, result.Refusal);
        Assert.Equal(50, result.Cost); // what it would have cost
        Assert.Equal(45, sim.Funds);
        for (int x = 0; x < 5; x++) Assert.Equal(TileKind.Empty, sim.Map[x, 0].Kind);
    }

    [Fact]
    public void Commands_in_one_tick_are_paid_in_order()
    {
        var sim = NewSim(funds: 1500);
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.PowerPlant)); // 1000
        sim.Enqueue(new PlaceCommand(1, 0, TileKind.PowerPlant)); // 1000, only 500 left
        sim.Enqueue(new PlaceCommand(2, 0, TileKind.Road));       // 5
        sim.Step();

        Assert.Equal(3, sim.LastOutcomes.Count);
        Assert.True(sim.LastOutcomes[0].Result.Accepted);
        Assert.Equal(Refusal.InsufficientFunds, sim.LastOutcomes[1].Result.Refusal);
        Assert.True(sim.LastOutcomes[2].Result.Accepted);
        Assert.Equal(1500 - 1000 - 5, sim.Funds);
    }

    [Fact]
    public void A_quote_changes_nothing_and_matches_the_charge()
    {
        var sim = NewSim();
        var command = new PlaceCommand(0, 0, 4, 4, TileKind.Residential);

        var quote = sim.Quote(command);
        Assert.Equal(TileKind.Empty, sim.Map[0, 0].Kind);
        Assert.Equal(10_000, sim.Funds);

        sim.Enqueue(command);
        sim.Step();
        Assert.Equal(quote, sim.LastOutcomes[0].Result);
    }

    [Fact]
    public void A_quote_reports_when_the_city_cannot_afford_it()
    {
        var sim = NewSim(funds: 999);
        var quote = sim.Quote(new PlaceCommand(0, 0, TileKind.PowerPlant));

        Assert.Equal(Refusal.InsufficientFunds, quote.Refusal);
        Assert.Equal(1000, quote.Cost);
    }

    [Fact]
    public void Bulldozing_charges_demolition_for_each_built_tile_only()
    {
        var sim = NewSim();
        sim.Enqueue(new PlaceCommand(0, 0, 3, 1, TileKind.Road));
        sim.Step();
        long funds = sim.Funds;

        sim.Enqueue(new BulldozeCommand(0, 0, 5, 1)); // 3 built, 2 already empty
        sim.Step();

        var result = sim.LastOutcomes[0].Result;
        Assert.Equal(3, result.Cost);
        Assert.Equal(3, result.TilesChanged);
        Assert.Equal(funds - 3, sim.Funds);
    }

    [Fact]
    public void Bulldozing_empty_land_is_refused()
    {
        var sim = NewSim();
        sim.Enqueue(new BulldozeCommand(0, 0));
        sim.Step();

        Assert.Equal(Refusal.NothingToChange, sim.LastOutcomes[0].Result.Refusal);
    }

    [Fact]
    public void Outcomes_are_cleared_each_tick()
    {
        var sim = NewSim();
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.Road));
        sim.Step();
        sim.Step();

        Assert.Empty(sim.LastOutcomes);
    }

    [Fact]
    public void A_refused_command_does_not_change_power()
    {
        var sim = new Simulation(3, 1, startingFunds: 1500, Prices);
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.PowerPlant));       // 1000
        sim.Enqueue(new PlaceCommand(1, 0, 2, 1, TileKind.Residential)); // 20, leaves 480
        sim.Step();
        Assert.Equal(3, sim.PoweredTiles);

        // A second plant costs 1000, which the city no longer has.
        sim.Enqueue(new PlaceCommand(2, 0, TileKind.PowerPlant));
        sim.Step();
        Assert.Equal(Refusal.InsufficientFunds, sim.LastOutcomes[0].Result.Refusal);
        Assert.Equal(TileKind.Residential, sim.Map[2, 0].Kind);
        Assert.Equal(3, sim.PoweredTiles);
    }

    [Fact]
    public void Default_prices_cover_every_buildable_kind()
    {
        foreach (var kind in Enum.GetValues<TileKind>())
        {
            if (kind == TileKind.Empty) Assert.Equal(0, PriceList.Default.Build(kind));
            else Assert.True(PriceList.Default.Build(kind) > 0, $"{kind} has no default price");
        }
        Assert.True(PriceList.Default.Demolish > 0);
    }

    [Fact]
    public void Negative_starting_funds_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Simulation(2, 2, startingFunds: -1));
    }
}
