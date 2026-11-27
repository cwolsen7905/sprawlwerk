using SprawlWerk.Sim.Content;

namespace SprawlWerk.Sim.Tests;

public class SimulationContentTests
{
    [Fact]
    public void A_city_on_the_base_pack_prices_and_powers_like_the_defaults()
    {
        var sim = new Simulation(8, 8, content: TestContent.Load());
        foreach (var kind in Enum.GetValues<TileKind>())
            Assert.Equal(PriceList.Default.Build(kind), sim.Prices.Build(kind));
        Assert.Equal(PriceList.Default.Demolish, sim.Prices.Demolish);
        Assert.Equal(PriceList.Default.LineCrossing, sim.Prices.LineCrossing);
        Assert.Equal(PowerRules.Default.PlantCapacity, sim.Power.Rules.PlantCapacity);
        Assert.NotNull(sim.Content);
    }

    // SPW-C-MOD-01: a number changed in a data file is the number the game uses.
    [Fact]
    public void A_price_changed_in_the_data_file_is_what_a_road_costs()
    {
        var sim = new Simulation(8, 8, content: TestContent.With("road.sim.json", "cost", 99));
        Assert.Equal(99 * 3, sim.Quote(new PlaceCommand(0, 0, 3, 1, TileKind.Road)).Cost);
        Assert.Equal(99 + 8, sim.Prices.Build(TileKind.RoadWithLine));
    }

    [Fact]
    public void Plant_output_comes_from_the_data_file()
    {
        var sim = new Simulation(8, 1, content: TestContent.With("power_plant.sim.json", "powerSupply", 3));
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 0, 5, 1, TileKind.Residential));
        sim.Step();
        Assert.Equal(3, sim.Power.Supply);
        Assert.Equal(3, sim.Power.PoweredZones);
        Assert.Equal(2, sim.Power.ZonesWithoutSupply);
    }

    [Fact]
    public void Each_zone_kind_uses_its_own_power()
    {
        var sim = new Simulation(8, 1, content: TestContent.With("industrial.sim.json", "powerUse", 5));
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 0, 2, 1, TileKind.Residential));
        sim.Enqueue(new PlaceCommand(3, 0, 2, 1, TileKind.Industrial));
        sim.Step();
        Assert.Equal(2 * 1 + 2 * 5, sim.Power.Demand);
    }

    [Fact]
    public void A_zone_beyond_the_remaining_supply_goes_dark_even_if_a_cheaper_one_follows()
    {
        // 40 supply: two industrial tiles at 15 use 30, then 10 left: the third
        // industrial tile (15) is dark, the residential tile after it (1) is lit.
        var content = TestContent.With("industrial.sim.json", "powerUse", 15);
        var sim = new Simulation(8, 1, content: content);
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 0, 3, 1, TileKind.Industrial));
        sim.Enqueue(new PlaceCommand(4, 0, TileKind.Residential));
        sim.Step();
        Assert.True(sim.Power.IsPowered(2, 0));
        Assert.False(sim.Power.IsPowered(3, 0));
        Assert.True(sim.Power.IsPowered(4, 0));
    }

    [Fact]
    public void Explicit_prices_win_over_content()
    {
        var prices = new PriceList(new Dictionary<TileKind, long> { [TileKind.Road] = 1 }, demolish: 0);
        var sim = new Simulation(4, 4, prices: prices, content: TestContent.Load());
        Assert.Equal(1, sim.Prices.Build(TileKind.Road));
    }

    [Fact]
    public void Content_without_a_type_the_game_needs_is_refused_by_name()
    {
        var result = ContentLoader.Load([new PackSource(
            new SourceFile("pack.json", """{ "schemaVersion": 1, "id": "base", "version": "1.0.0", "license": "CC0-1.0" }"""),
            [])]);
        var error = Assert.Throws<InvalidOperationException>(() => new Simulation(4, 4, content: result.Content));
        Assert.Contains("base:residential", error.Message);
    }

    [Fact]
    public void A_loaded_save_keeps_the_content_it_is_given()
    {
        var content = TestContent.With("road.sim.json", "cost", 50);
        var sim = new Simulation(4, 4, content: content);
        var loaded = SaveGame.Deserialize(SaveGame.Serialize(sim), content: content);
        Assert.Same(content, loaded.Content);
        Assert.Equal(50, loaded.Prices.Build(TileKind.Road));
    }
}
