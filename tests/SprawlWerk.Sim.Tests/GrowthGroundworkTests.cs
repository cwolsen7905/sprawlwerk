using SprawlWerk.Sim.Content;

namespace SprawlWerk.Sim.Tests;

public class CityRandomTests
{
    [Fact]
    public void The_same_seed_gives_the_same_sequence()
    {
        var a = new CityRandom(42);
        var b = new CityRandom(42);
        for (int i = 0; i < 100; i++) Assert.Equal(a.Next(1000), b.Next(1000));
    }

    [Fact]
    public void Different_seeds_give_different_sequences()
    {
        var a = new CityRandom(1);
        var b = new CityRandom(2);
        Assert.NotEqual(Enumerable.Range(0, 10).Select(_ => a.Next(1000)), Enumerable.Range(0, 10).Select(_ => b.Next(1000)));
    }

    // Pinned values: the sequence must not change between platforms or versions,
    // or saved cities would grow differently after an update.
    [Fact]
    public void The_sequence_is_pinned()
    {
        var r = new CityRandom(0);
        Assert.Equal(0xE220A8397B1DCDAFUL, r.NextULong());
        Assert.Equal(0x6E789E6AA1B965F4UL, r.NextULong());
    }

    [Fact]
    public void Restoring_the_state_resumes_the_sequence()
    {
        var a = new CityRandom(7);
        a.Next(10);
        var b = new CityRandom { State = a.State };
        Assert.Equal(a.Next(1_000_000), b.Next(1_000_000));
    }

    [Fact]
    public void Next_stays_in_range_and_reaches_every_value()
    {
        var r = new CityRandom(3);
        var seen = new HashSet<int>();
        for (int i = 0; i < 1000; i++)
        {
            int v = r.Next(7);
            Assert.InRange(v, 0, 6);
            seen.Add(v);
        }
        Assert.Equal(7, seen.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => r.Next(0));
    }
}

public class GrowthGroundworkTests
{
    [Fact]
    public void A_month_is_120_ticks_and_starts_at_tick_0()
    {
        Assert.Equal(120, CityDate.TicksPerMonth);
        Assert.True(CityDate.IsMonthStart(0));
        Assert.False(CityDate.IsMonthStart(119));
        Assert.True(CityDate.IsMonthStart(120));
        Assert.Equal(2, CityDate.FromTick(120).Month);
    }

    [Fact]
    public void Road_access_reaches_three_steps_over_any_tile_and_no_further()
    {
        var sim = new Simulation(10, 1);
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.Road));
        sim.Enqueue(new PlaceCommand(1, 0, TileKind.PowerPlant)); // not a road, still crossed
        sim.Step();
        Assert.Equal(0, sim.Roads.Distance(0, 0));
        Assert.Equal(3, sim.Roads.Distance(3, 0));
        Assert.True(sim.Roads.HasAccess(3, 0));
        Assert.False(sim.Roads.HasAccess(4, 0));
        Assert.Equal(-1, sim.Roads.Distance(4, 0));
    }

    [Fact]
    public void A_road_carrying_a_line_gives_access_and_a_bare_line_does_not()
    {
        var sim = new Simulation(5, 5);
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.PowerLine));
        sim.Step();
        Assert.False(sim.Roads.HasAccess(0, 0));
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.Road));
        sim.Step();
        Assert.Equal(TileKind.RoadWithLine, sim.Map[0, 0].Kind);
        Assert.True(sim.Roads.HasAccess(1, 1));
    }

    [Fact]
    public void Access_follows_the_road_limit_in_the_rules()
    {
        var sim = new Simulation(6, 1, growth: GrowthRules.Default with { RoadAccess = 1 });
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.Road));
        sim.Step();
        Assert.True(sim.Roads.HasAccess(1, 0));
        Assert.False(sim.Roads.HasAccess(2, 0));
    }

    private static (Simulation Sim, ContentSet Content) CityWithContent()
    {
        var content = TestContent.Load();
        return (new Simulation(6, 2, content: content), content);
    }

    [Fact]
    public void Residents_and_jobs_count_each_building_once_by_its_type()
    {
        var (sim, content) = CityWithContent();
        sim.Enqueue(new PlaceCommand(0, 0, 3, 1, TileKind.Residential));
        sim.Enqueue(new PlaceCommand(0, 1, 2, 1, TileKind.Commercial));
        sim.Enqueue(new PlaceCommand(3, 1, TileKind.Industrial));
        sim.Step();
        // Stand-in for growth (next PR): put buildings on the zones directly.
        sim.Map[0, 0].Building = content.Find("base:house");
        sim.Map[1, 0].Building = content.Find("base:house_small");
        sim.Map[0, 1].Building = content.Find("base:shop");
        sim.Map[3, 1].Building = content.Find("base:depot");
        sim.Enqueue(new PlaceCommand(5, 0, TileKind.Road)); // any map change triggers a recount
        sim.Step();
        Assert.Equal(8 + 4, sim.Residents);
        Assert.Equal(6, sim.CommercialJobs);
        Assert.Equal(10, sim.IndustrialJobs);
    }

    [Fact]
    public void Rezoning_or_bulldozing_a_tile_removes_its_building()
    {
        var (sim, content) = CityWithContent();
        sim.Enqueue(new PlaceCommand(0, 0, 2, 1, TileKind.Residential));
        sim.Step();
        sim.Map[0, 0].Building = content.Find("base:house");
        sim.Map[1, 0].Building = content.Find("base:house");
        sim.Enqueue(new BulldozeCommand(0, 0));
        sim.Enqueue(new PlaceCommand(1, 0, TileKind.Commercial));
        sim.Step();
        Assert.True(sim.Map[0, 0].Building.IsNone);
        Assert.True(sim.Map[1, 0].Building.IsNone);
        Assert.Equal(0, sim.Residents);
    }

    [Fact]
    public void Zoning_a_tile_again_with_the_same_zone_keeps_its_building()
    {
        var (sim, content) = CityWithContent();
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.Residential));
        sim.Step();
        sim.Map[0, 0].Building = content.Find("base:house");
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.Residential));
        sim.Step();
        Assert.Equal(Refusal.NothingToChange, sim.LastOutcomes[0].Result.Refusal);
        Assert.False(sim.Map[0, 0].Building.IsNone);
    }

    [Fact]
    public void The_random_state_survives_a_save()
    {
        var sim = new Simulation(4, 4, seed: 99);
        sim.Random.Next(100);
        var loaded = SaveGame.Deserialize(SaveGame.Serialize(sim));
        Assert.Equal(sim.Random.State, loaded.Random.State);
    }

    [Fact]
    public void An_older_save_without_random_state_starts_from_the_default_seed()
    {
        var json = SaveGame.Serialize(new Simulation(2, 2, seed: 5));
        var old = System.Text.RegularExpressions.Regex.Replace(json, @",\s*""Random"":\s*\d+", "");
        Assert.DoesNotContain("Random", old);
        Assert.Equal(CityRandom.DefaultSeed, SaveGame.Deserialize(old).Random.State);
    }

    [Fact]
    public void A_grown_building_type_must_be_free_and_without_upkeep()
    {
        var errors = ContentLoader.Load([new PackSource(
            new SourceFile("pack.json", """{ "schemaVersion": 1, "id": "base", "version": "1.0.0", "license": "CC0-1.0" }"""),
            [new SourceFile("villa.sim.json", """{ "schemaVersion": 1, "id": "villa", "role": "building", "zone": "residential", "cost": 500, "upkeep": 5 }""")])]).Errors;
        Assert.Contains(errors, e => e.JsonPath == "$.cost");
        Assert.Contains(errors, e => e.JsonPath == "$.upkeep");
    }

    [Fact]
    public void The_base_pack_has_two_low_density_buildings_for_each_zone()
    {
        var content = TestContent.Load();
        foreach (var zone in Enum.GetValues<ZoneKind>())
        {
            var grown = content.Types.Where(t => t.Role == TypeRole.Building && t.Zone == zone).ToList();
            Assert.Equal(2, grown.Count);
            Assert.All(grown, t => Assert.Equal(Density.Low, t.Density));
            Assert.All(grown, t => Assert.Equal((1, 1), (t.Width, t.Height)));
        }
    }
}
