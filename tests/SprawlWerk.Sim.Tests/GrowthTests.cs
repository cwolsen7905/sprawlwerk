using SprawlWerk.Sim.Content;

namespace SprawlWerk.Sim.Tests;

public class GrowthTests
{
    // A road along the top; below it a plant, then R, C and I zones in a row,
    // all within reach of the road and conducting power from the plant.
    private static Simulation Town(ContentSet? content = null, ulong seed = 1, bool road = true, bool plant = true)
    {
        var sim = new Simulation(12, 3, startingFunds: 1_000_000, content: content ?? TestContent.Load(), seed: seed);
        if (road) sim.Enqueue(new PlaceCommand(0, 0, 12, 1, TileKind.Road));
        if (plant) sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 1, 4, 2, TileKind.Residential));
        sim.Enqueue(new PlaceCommand(5, 1, 3, 2, TileKind.Commercial));
        sim.Enqueue(new PlaceCommand(8, 1, 4, 2, TileKind.Industrial));
        return sim;
    }

    private static void Run(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++) sim.Step();
    }

    private static int Buildings(Simulation sim, TileKind kind)
    {
        int n = 0;
        for (int y = 0; y < sim.Map.Height; y++)
        for (int x = 0; x < sim.Map.Width; x++)
            if (sim.Map[x, y].Kind == kind && !sim.Map[x, y].Building.IsNone) n++;
        return n;
    }

    [Fact]
    public void An_empty_city_wants_residents_and_industry_but_no_shops_yet()
    {
        var sim = Town();
        sim.Step(); // tick 0 is a month start
        Assert.Equal(new Demand(1000, 0, 1000), sim.Demand);
    }

    // SPW-C-ZONE-03 (growth half): powered, road-connected zones with demand grow.
    [Fact]
    public void Powered_zones_by_a_road_grow_houses_and_workshops_within_a_month()
    {
        var sim = Town();
        Run(sim, CityDate.TicksPerMonth);
        Assert.True(Buildings(sim, TileKind.Residential) > 0);
        Assert.True(Buildings(sim, TileKind.Industrial) > 0);
        Assert.Equal(0, Buildings(sim, TileKind.Commercial));
        Assert.True(sim.Residents > 0);
        Assert.True(sim.IndustrialJobs > 0);
    }

    // SPW-C-ZONE-04: residents pull shops.
    [Fact]
    public void Residents_bring_demand_for_shops_and_shops_grow()
    {
        var sim = Town();
        Run(sim, 4 * CityDate.TicksPerMonth);
        Assert.True(sim.Demand.Commercial > 0 || Buildings(sim, TileKind.Commercial) > 0);
        Assert.True(Buildings(sim, TileKind.Commercial) > 0);
    }

    [Fact]
    public void Without_power_nothing_grows_and_the_reason_says_so()
    {
        var sim = Town(plant: false);
        Run(sim, 2 * CityDate.TicksPerMonth);
        Assert.Equal(0, sim.Residents);
        Assert.Equal(GrowthBlock.NoPower, sim.WhyNotGrowing(1, 1));
    }

    // SPW-C-ZONE-05: a zone beyond road access does not grow.
    [Fact]
    public void Without_a_road_nothing_grows_and_the_reason_says_so()
    {
        var sim = Town(road: false);
        Run(sim, 2 * CityDate.TicksPerMonth);
        Assert.Equal(0, sim.Residents);
        Assert.Equal(GrowthBlock.NoRoad, sim.WhyNotGrowing(1, 1));
    }

    [Fact]
    public void A_zone_with_no_demand_waits()
    {
        var sim = Town();
        sim.Step();
        Assert.Equal(GrowthBlock.NoDemand, sim.WhyNotGrowing(5, 1)); // commercial, demand 0
        Assert.Equal(GrowthBlock.None, sim.WhyNotGrowing(1, 1));
        Assert.Equal(GrowthBlock.NotAZone, sim.WhyNotGrowing(0, 0));
    }

    [Fact]
    public void Only_the_road_reach_in_the_rules_counts()
    {
        var sim = new Simulation(1, 6, startingFunds: 100_000, content: TestContent.Load());
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.Road));
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(0, 2, 1, 4, TileKind.Residential));
        Run(sim, CityDate.TicksPerMonth);
        Assert.Equal(GrowthBlock.Built, sim.WhyNotGrowing(0, 3)); // 3 steps from the road
        Assert.Equal(GrowthBlock.NoRoad, sim.WhyNotGrowing(0, 4)); // 4 steps
    }

    // SPW-C-ZONE-06: a building type that exists only in a data file grows.
    [Fact]
    public void A_type_added_only_in_data_grows_on_its_zone()
    {
        var withCottage = ContentLoader.Load([
            TestContent.ReadBasePack(),
            new PackSource(
                new SourceFile("pack.json", """{ "schemaVersion": 1, "id": "cottages", "version": "1.0.0", "license": "CC0-1.0" }"""),
                [new SourceFile("cottage.sim.json", """{ "schemaVersion": 1, "id": "cottage", "role": "building", "zone": "residential", "cost": 0, "residents": 3, "powerUse": 1 }""")]),
        ]).Content!;
        var sim = Town(withCottage);
        Run(sim, 6 * CityDate.TicksPerMonth);
        var cottage = withCottage.Find("cottages:cottage");
        bool found = false;
        for (int x = 0; x < 12; x++)
        for (int y = 0; y < 3; y++)
            found |= sim.Map[x, y].Building == cottage;
        Assert.True(found);
    }

    [Fact]
    public void Without_building_types_for_a_zone_the_reason_says_so()
    {
        var noHouses = TestContent.Load((name, text) => name.StartsWith("house") ? text.Replace("\"residential\"", "\"commercial\"") : text);
        var sim = Town(noHouses);
        sim.Step();
        Assert.Equal(GrowthBlock.NoBuildingType, sim.WhyNotGrowing(1, 1));
    }

    [Fact]
    public void Without_content_nothing_grows()
    {
        var sim = new Simulation(12, 3, startingFunds: 1_000_000);
        sim.Enqueue(new PlaceCommand(0, 0, 12, 1, TileKind.Road));
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 1, 4, 2, TileKind.Residential));
        Run(sim, 2 * CityDate.TicksPerMonth);
        Assert.Equal(0, sim.Residents);
    }

    [Fact]
    public void Buildings_use_their_own_power()
    {
        var sim = Town();
        Run(sim, 4 * CityDate.TicksPerMonth);
        long expected = 0;
        for (int y = 0; y < 3; y++)
        for (int x = 0; x < 12; x++)
        {
            var t = sim.Map[x, y];
            if (!t.IsZone) continue;
            expected += t.Building.IsNone ? 1 : sim.Content![t.Building].PowerUse;
        }
        sim.Enqueue(new PlaceCommand(11, 0, TileKind.Road)); // nothing changes; force a fresh look
        sim.Step();
        Assert.Equal(expected, sim.Power.Demand);
    }

    private static string Fingerprint(Simulation sim)
    {
        var sb = new System.Text.StringBuilder();
        for (int y = 0; y < sim.Map.Height; y++)
        for (int x = 0; x < sim.Map.Width; x++)
            sb.Append((int)sim.Map[x, y].Kind).Append(':').Append(sim.Map[x, y].Building.Value).Append(' ');
        return sb.Append(sim.Demand).Append(sim.Random.State).ToString();
    }

    [Fact]
    public void The_same_seed_and_commands_grow_the_same_city()
    {
        var a = Town(seed: 7);
        var b = Town(seed: 7);
        Run(a, 2000);
        Run(b, 2000);
        Assert.Equal(Fingerprint(a), Fingerprint(b));
    }

    [Fact]
    public void Another_seed_grows_a_different_city()
    {
        var a = Town(seed: 7);
        var b = Town(seed: 8);
        Run(a, 2 * CityDate.TicksPerMonth);
        Run(b, 2 * CityDate.TicksPerMonth);
        Assert.NotEqual(Fingerprint(a), Fingerprint(b));
    }

    // SPW-C-SAVE-02 with growth: a loaded city keeps growing exactly as the saved one.
    [Fact]
    public void A_loaded_city_grows_exactly_as_the_saved_one_would_have()
    {
        var content = TestContent.Load();
        var sim = Town(content, seed: 3);
        Run(sim, 250);
        var loaded = SaveGame.Deserialize(SaveGame.Serialize(sim), content: content);
        Assert.Equal(Fingerprint(sim), Fingerprint(loaded));
        Run(sim, 1000);
        Run(loaded, 1000);
        Assert.Equal(Fingerprint(sim), Fingerprint(loaded));
        Assert.Equal(sim.Residents, loaded.Residents);
    }

    [Fact]
    public void Buildings_from_a_pack_that_is_not_loaded_are_dropped_and_reported()
    {
        var content = TestContent.Load();
        var sim = Town(content);
        Run(sim, CityDate.TicksPerMonth);
        Assert.True(sim.Residents > 0);
        var loaded = SaveGame.Deserialize(SaveGame.Serialize(sim)); // no content
        Assert.NotEmpty(loaded.LoadReport);
        Assert.Equal(0, loaded.Residents);
        Assert.Contains("will grow again", loaded.LoadReport[0]);
    }
}
