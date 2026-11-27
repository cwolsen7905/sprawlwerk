using SprawlWerk.Sim.Content;

namespace SprawlWerk.Sim.Tests;

public class DeclineTests
{
    private const int Month = CityDate.TicksPerMonth;

    // A road, a plant, and a residential block that grows full within two months.
    private static Simulation GrownTown(ContentSet content, GrowthRules? rules = null)
    {
        var sim = new Simulation(8, 3, startingFunds: 1_000_000, content: content, growth: rules);
        sim.Enqueue(new PlaceCommand(0, 0, 8, 1, TileKind.Road));
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 1, 3, 2, TileKind.Residential));
        // Stop mid-month, so a change made next is not on a month start.
        for (int i = 0; i < 3 * Month + 10; i++) sim.Step();
        return sim;
    }

    private static List<(int X, int Y)> Built(Simulation sim)
    {
        var list = new List<(int, int)>();
        for (int y = 0; y < sim.Map.Height; y++)
        for (int x = 0; x < sim.Map.Width; x++)
            if (!sim.Map[x, y].Building.IsNone) list.Add((x, y));
        return list;
    }

    // Steps until just after the next month start has run.
    private static void NextMonth(Simulation sim)
    {
        do sim.Step(); while (!CityDate.IsMonthStart(sim.Tick - 1));
    }

    // SPW-C-ZONE-03, decline half: cut power, abandoned by month 3, cleared by month 9.
    [Fact]
    public void Without_power_buildings_are_abandoned_after_three_months_and_cleared_after_nine()
    {
        var sim = GrownTown(TestContent.Load());
        var built = Built(sim);
        Assert.NotEmpty(built);
        Assert.True(sim.Residents > 0);

        sim.Enqueue(new BulldozeCommand(0, 1)); // the plant
        sim.Step();
        for (int m = 1; m <= 2; m++) NextMonth(sim);
        Assert.All(built, b => Assert.False(sim.IsAbandoned(b.X, b.Y)));
        Assert.True(sim.Residents > 0);

        NextMonth(sim); // third bad month
        Assert.All(built, b => Assert.True(sim.IsAbandoned(b.X, b.Y)));
        Assert.Equal(0, sim.Residents);

        for (int m = 4; m <= 8; m++) NextMonth(sim);
        Assert.All(built, b => Assert.False(sim.Map[b.X, b.Y].Building.IsNone));
        NextMonth(sim); // ninth
        Assert.All(built, b => Assert.True(sim.Map[b.X, b.Y].Building.IsNone));
        Assert.Equal(TileKind.Residential, sim.Map[built[0].X, built[0].Y].Kind);
    }

    [Fact]
    public void Power_back_in_time_brings_people_back()
    {
        var sim = GrownTown(TestContent.Load());
        var built = Built(sim);
        sim.Enqueue(new BulldozeCommand(0, 1));
        sim.Step();
        for (int m = 1; m <= 3; m++) NextMonth(sim);
        Assert.Equal(0, sim.Residents);

        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Step();
        for (int m = 1; m <= 2; m++) NextMonth(sim);
        Assert.Equal(0, sim.Residents); // stress 3 -> 1: still empty
        NextMonth(sim); // stress 0
        Assert.All(built, b => Assert.False(sim.IsAbandoned(b.X, b.Y)));
        Assert.True(sim.Residents > 0);
    }

    [Fact]
    public void A_shrinking_market_gives_some_buildings_a_bad_month()
    {
        // No bootstrap: residents with no jobs anywhere push residential demand to -1000.
        var content = TestContent.Load();
        var rules = GrowthRules.Default with { BaseResidents = 0, ExportJobs = 0 };
        var sim = new Simulation(8, 3, startingFunds: 1_000_000, content: content, growth: rules);
        sim.Enqueue(new PlaceCommand(0, 0, 8, 1, TileKind.Road));
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 1, 7, 2, TileKind.Residential));
        sim.Step();
        var house = content.Find("base:house");
        for (int y = 1; y <= 2; y++)
        for (int x = 1; x < 8; x++) sim.Map[x, y].Building = house;
        sim.Enqueue(new PlaceCommand(0, 2, TileKind.Road)); // a change, so counts refresh
        while (sim.Tick <= Month) sim.Step(); // month 2's start sets demand from these residents
        int demand = sim.Demand.Residential; // smoothed halfway towards -1000
        Assert.Equal(-500, demand);

        NextMonth(sim);
        int stressed = Built(sim).Count(b => sim.Stress(b.X, b.Y) == 1);
        Assert.Equal((-demand * rules.MaxDeclinePerMonth + 999) / 1000, stressed); // 2 of 14
    }

    [Fact]
    public void Healthy_buildings_have_no_stress()
    {
        var sim = GrownTown(TestContent.Load());
        Assert.All(Built(sim), b => Assert.Equal(0, sim.Stress(b.X, b.Y)));
    }

    [Fact]
    public void A_building_grown_on_a_cleared_lot_starts_well()
    {
        var sim = GrownTown(TestContent.Load());
        var (x, y) = Built(sim)[0];
        sim.Enqueue(new BulldozeCommand(0, 1));
        sim.Step();
        for (int m = 1; m <= 9; m++) NextMonth(sim);
        Assert.True(sim.Map[x, y].Building.IsNone);
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Step();
        for (int m = 1; m <= 2; m++) NextMonth(sim);
        Assert.False(sim.Map[x, y].Building.IsNone);
        Assert.Equal(0, sim.Stress(x, y));
        Assert.False(sim.IsAbandoned(x, y));
    }

    private static string Fingerprint(Simulation sim)
    {
        var sb = new System.Text.StringBuilder();
        for (int y = 0; y < sim.Map.Height; y++)
        for (int x = 0; x < sim.Map.Width; x++)
            sb.Append(sim.Map[x, y].Building.Value).Append(':').Append(sim.Stress(x, y)).Append(sim.IsAbandoned(x, y) ? 'A' : '.').Append(' ');
        return sb.Append(sim.Residents).Append(sim.Demand).Append(sim.Random.State).ToString();
    }

    [Fact]
    public void Decline_survives_a_save_and_continues_exactly()
    {
        var content = TestContent.Load();
        var sim = GrownTown(content);
        sim.Enqueue(new BulldozeCommand(0, 1));
        sim.Step();
        for (int m = 1; m <= 4; m++) NextMonth(sim);
        var loaded = SaveGame.Deserialize(SaveGame.Serialize(sim), content: content);
        Assert.Equal(Fingerprint(sim), Fingerprint(loaded));
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        loaded.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        for (int i = 0; i < 6 * Month; i++) { sim.Step(); loaded.Step(); }
        Assert.Equal(Fingerprint(sim), Fingerprint(loaded));
    }
}
