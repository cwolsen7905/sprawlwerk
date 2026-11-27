using SprawlWerk.Sim.Content;

namespace SprawlWerk.Sim.Tests;

public class InspectTests
{
    private const int Month = CityDate.TicksPerMonth;

    // A road along the top, a plant, and a residential block that grows full.
    private static Simulation GrownTown(ContentSet? content = null, GrowthRules? rules = null)
    {
        var sim = new Simulation(8, 3, startingFunds: 1_000_000, content: content ?? TestContent.Load(), growth: rules);
        sim.Enqueue(new PlaceCommand(0, 0, 8, 1, TileKind.Road));
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 1, 3, 2, TileKind.Residential));
        for (int i = 0; i < 3 * Month + 10; i++) sim.Step();
        return sim;
    }

    private static void NextMonth(Simulation sim)
    {
        do sim.Step(); while (!CityDate.IsMonthStart(sim.Tick - 1));
    }

    private static (int X, int Y) FirstBuilt(Simulation sim)
    {
        for (int y = 0; y < sim.Map.Height; y++)
        for (int x = 0; x < sim.Map.Width; x++)
            if (!sim.Map[x, y].Building.IsNone) return (x, y);
        throw new InvalidOperationException("nothing grew");
    }

    [Fact]
    public void A_road_is_not_a_zone()
    {
        var sim = GrownTown();
        var r = sim.Inspect(5, 0);
        Assert.Equal(TileStatus.NotAZone, r.Status);
        Assert.Equal(TileKind.Road, r.Kind);
        Assert.Null(r.Zone);
        Assert.Null(r.ZoneDemand);
        Assert.Null(r.BuildingTypeId);
        Assert.Equal(GrowthBlock.NotAZone, r.GrowthBlock);
        Assert.Equal(0, r.RoadDistance);
        Assert.Equal(DeclineReason.None, r.Reason);
    }

    [Fact]
    public void Off_the_map_throws()
    {
        var sim = new Simulation(4, 4);
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.Inspect(4, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.Inspect(-1, 0));
    }

    [Fact]
    public void An_unpowered_empty_lot_is_blocked_by_power()
    {
        var sim = new Simulation(6, 3, content: TestContent.Load());
        sim.Enqueue(new PlaceCommand(0, 0, 6, 1, TileKind.Road));
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.Residential));
        sim.Step();

        var r = sim.Inspect(0, 1);
        Assert.Equal(TileStatus.EmptyBlocked, r.Status);
        Assert.Equal(GrowthBlock.NoPower, r.GrowthBlock);
        Assert.False(r.Powered);
        Assert.True(r.HasRoadAccess);
        Assert.Equal(1, r.RoadDistance);
        Assert.Equal(ZoneKind.Residential, r.Zone);
        Assert.Equal(sim.Demand.Residential, r.ZoneDemand);
        Assert.Equal(0, r.Occupants);
        Assert.Equal(0, r.Capacity);
    }

    [Fact]
    public void A_powered_lot_far_from_a_road_is_blocked_by_road()
    {
        var sim = new Simulation(8, 1, content: TestContent.Load());
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 0, TileKind.Residential));
        sim.Step();

        var r = sim.Inspect(1, 0);
        Assert.Equal(TileStatus.EmptyBlocked, r.Status);
        Assert.Equal(GrowthBlock.NoRoad, r.GrowthBlock);
        Assert.True(r.Powered);
        Assert.False(r.HasRoadAccess);
        Assert.Equal(-1, r.RoadDistance);
        Assert.Equal(sim.Growth.RoadAccess, r.MaxRoadDistance);
    }

    [Fact]
    public void A_lot_with_power_and_road_but_no_demand_is_blocked_by_demand()
    {
        var rules = GrowthRules.Default with { BaseResidents = 0, ExportJobs = 0 };
        var sim = new Simulation(4, 2, content: TestContent.Load(), growth: rules);
        sim.Enqueue(new PlaceCommand(0, 0, 4, 1, TileKind.Road));
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 1, TileKind.Residential));
        sim.Step();

        var r = sim.Inspect(1, 1);
        Assert.Equal(TileStatus.EmptyBlocked, r.Status);
        Assert.Equal(GrowthBlock.NoDemand, r.GrowthBlock);
        Assert.Equal(0, r.ZoneDemand);
    }

    [Fact]
    public void A_lot_passing_every_check_is_ready()
    {
        var sim = new Simulation(4, 2, content: TestContent.Load());
        sim.Enqueue(new PlaceCommand(0, 0, 4, 1, TileKind.Road));
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 1, TileKind.Residential));
        sim.Step();

        var r = sim.Inspect(1, 1);
        Assert.Equal(TileStatus.EmptyReady, r.Status);
        Assert.Equal(GrowthBlock.None, r.GrowthBlock);
        Assert.True(r.ZoneDemand > 0);
    }

    [Fact]
    public void A_lived_in_building_passing_every_check_is_healthy()
    {
        var sim = GrownTown();
        var (x, y) = FirstBuilt(sim);
        var r = sim.Inspect(x, y);
        var type = sim.Content![sim.Map[x, y].Building];

        Assert.Equal(TileStatus.Healthy, r.Status);
        Assert.Equal(DeclineReason.None, r.Reason);
        Assert.Equal(GrowthBlock.Built, r.GrowthBlock);
        Assert.Equal(type.Id, r.BuildingTypeId);
        Assert.Equal(type.Residents, r.Occupants);
        Assert.Equal(type.Residents, r.Capacity);
        Assert.Equal(0, r.Stress);
    }

    [Fact]
    public void A_building_without_power_is_at_risk_for_power_and_piles_up_stress()
    {
        var sim = GrownTown();
        var (x, y) = FirstBuilt(sim);
        sim.Enqueue(new BulldozeCommand(0, 1)); // the plant
        sim.Step();

        var r = sim.Inspect(x, y);
        Assert.Equal(TileStatus.AtRisk, r.Status);
        Assert.Equal(DeclineReason.NoPower, r.Reason);
        Assert.Equal(0, r.Stress);
        Assert.Equal(r.Capacity, r.Occupants);

        NextMonth(sim);
        Assert.Equal(1, sim.Inspect(x, y).Stress);
    }

    [Fact]
    public void Power_is_reported_before_road_like_decline_checks_them()
    {
        var sim = GrownTown();
        var (x, y) = FirstBuilt(sim);
        sim.Enqueue(new BulldozeCommand(0, 1));          // the plant
        sim.Enqueue(new BulldozeCommand(0, 0, 8, 1));    // every road
        sim.Step();

        var r = sim.Inspect(x, y);
        Assert.False(r.Powered);
        Assert.False(r.HasRoadAccess);
        Assert.Equal(DeclineReason.NoPower, r.Reason);
    }

    [Fact]
    public void A_building_cut_off_from_roads_is_at_risk_for_road()
    {
        var sim = GrownTown();
        var (x, y) = FirstBuilt(sim);
        sim.Enqueue(new BulldozeCommand(0, 0, 8, 1));
        sim.Step();

        var r = sim.Inspect(x, y);
        Assert.True(r.Powered);
        Assert.Equal(TileStatus.AtRisk, r.Status);
        Assert.Equal(DeclineReason.NoRoad, r.Reason);
    }

    // Decline draws buildings only once demand is below DeclineDemand: -500 is a risk at -250, not at -1000.
    [Theory]
    [InlineData(-250, TileStatus.AtRisk, DeclineReason.LowDemand)]
    [InlineData(-1000, TileStatus.Healthy, DeclineReason.None)]
    public void A_building_in_a_shrinking_zone_is_at_risk_for_demand(int declineDemand, TileStatus status, DeclineReason reason)
    {
        var content = TestContent.Load();
        var rules = GrowthRules.Default with { BaseResidents = 0, ExportJobs = 0, DeclineDemand = declineDemand };
        var sim = new Simulation(8, 3, startingFunds: 1_000_000, content: content, growth: rules);
        sim.Enqueue(new PlaceCommand(0, 0, 8, 1, TileKind.Road));
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 1, 7, 2, TileKind.Residential));
        sim.Step();
        var house = content.Find("base:house");
        for (int y = 1; y <= 2; y++)
        for (int x = 1; x < 8; x++) sim.Map[x, y].Building = house;
        sim.Enqueue(new PlaceCommand(0, 2, TileKind.Road)); // a change, so counts refresh
        while (sim.Tick <= Month) sim.Step();
        Assert.Equal(-500, sim.Demand.Residential);

        var r = sim.Inspect(4, 1);
        Assert.Equal(status, r.Status);
        Assert.Equal(reason, r.Reason);
        Assert.Equal(-500, r.ZoneDemand);
    }

    [Fact]
    public void An_abandoned_house_reports_no_one_and_what_it_held()
    {
        var sim = GrownTown();
        var (x, y) = FirstBuilt(sim);
        var type = sim.Content![sim.Map[x, y].Building];
        sim.Enqueue(new BulldozeCommand(0, 1));
        sim.Step();
        for (int m = 0; m < 3; m++) NextMonth(sim);

        var r = sim.Inspect(x, y);
        Assert.Equal(TileStatus.Abandoned, r.Status);
        Assert.Equal(DeclineReason.NoPower, r.Reason);
        Assert.Equal(3, r.Stress);
        Assert.Equal(0, r.Occupants);
        Assert.Equal(type.Residents, r.Capacity);
        Assert.True(r.Capacity > 0);
    }

    [Fact]
    public void An_abandoned_building_whose_problem_is_fixed_is_recovering()
    {
        var sim = GrownTown();
        var (x, y) = FirstBuilt(sim);
        sim.Enqueue(new BulldozeCommand(0, 1));
        sim.Step();
        for (int m = 0; m < 3; m++) NextMonth(sim);
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Step();

        var r = sim.Inspect(x, y);
        Assert.Equal(TileStatus.Abandoned, r.Status);
        Assert.Equal(DeclineReason.None, r.Reason);
        Assert.Equal(3, r.Stress);

        NextMonth(sim);
        Assert.Equal(2, sim.Inspect(x, y).Stress);
    }

    [Theory]
    [InlineData(TileKind.Commercial, "base:shop", 6)]
    [InlineData(TileKind.Industrial, "base:depot", 10)]
    public void Commercial_and_industrial_buildings_report_jobs(TileKind kind, string typeId, int jobs)
    {
        var content = TestContent.Load();
        var sim = new Simulation(4, 2, startingFunds: 1_000_000, content: content);
        sim.Enqueue(new PlaceCommand(0, 0, 4, 1, TileKind.Road));
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 1, kind));
        sim.Step();
        sim.Map[1, 1].Building = content.Find(typeId);

        var r = sim.Inspect(1, 1);
        Assert.Equal(typeId, r.BuildingTypeId);
        Assert.Equal(jobs, r.Capacity);
        Assert.Equal(jobs, r.Occupants);
    }

    [Fact]
    public void Inspecting_changes_nothing()
    {
        var sim = GrownTown();
        long funds = sim.Funds, revision = sim.MapRevision, tick = sim.Tick;
        ulong random = sim.Random.State;
        var tiles = Snapshot(sim);

        for (int y = 0; y < sim.Map.Height; y++)
        for (int x = 0; x < sim.Map.Width; x++) sim.Inspect(x, y);

        Assert.Equal(funds, sim.Funds);
        Assert.Equal(revision, sim.MapRevision);
        Assert.Equal(tick, sim.Tick);
        Assert.Equal(random, sim.Random.State);
        Assert.Equal(tiles, Snapshot(sim));
    }

    [Fact]
    public void Inspecting_every_tick_does_not_change_how_the_city_grows()
    {
        Simulation Run(bool inspect)
        {
            var sim = new Simulation(10, 6, startingFunds: 1_000_000, content: TestContent.Load());
            sim.Enqueue(new PlaceCommand(0, 0, 10, 1, TileKind.Road));
            sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
            sim.Enqueue(new PlaceCommand(1, 1, 9, 5, TileKind.Residential));
            for (int i = 0; i < 6 * Month; i++)
            {
                if (i == 3 * Month) sim.Enqueue(new BulldozeCommand(0, 1));
                sim.Step();
                if (inspect)
                    for (int y = 0; y < sim.Map.Height; y++)
                    for (int x = 0; x < sim.Map.Width; x++) sim.Inspect(x, y);
            }
            return sim;
        }

        var plain = Run(false);
        var inspected = Run(true);
        Assert.Equal(plain.Random.State, inspected.Random.State);
        Assert.Equal(Snapshot(plain), Snapshot(inspected));
        Assert.Equal(plain.Residents, inspected.Residents);
        Assert.Equal(plain.Funds, inspected.Funds);
    }

    private static List<(TileKind, ushort, int, bool)> Snapshot(Simulation sim)
    {
        var list = new List<(TileKind, ushort, int, bool)>();
        for (int y = 0; y < sim.Map.Height; y++)
        for (int x = 0; x < sim.Map.Width; x++)
            list.Add((sim.Map[x, y].Kind, sim.Map[x, y].Building.Value, sim.Stress(x, y), sim.IsAbandoned(x, y)));
        return list;
    }
}
