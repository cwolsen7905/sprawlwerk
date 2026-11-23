using SprawlWerk.Sim;

namespace SprawlWerk.Sim.Tests;

public class PowerCapacityTests
{
    // One plant, a 1-wide corridor of zones: (0,0) plant, zones at x = 1..n.
    private static (CityMap Map, PowerGrid Grid) Corridor(int zones, PowerRules rules)
    {
        var map = new CityMap(zones + 1, 1);
        map[0, 0].Kind = TileKind.PowerPlant;
        for (int x = 1; x <= zones; x++) map[x, 0].Kind = TileKind.Residential;
        var grid = new PowerGrid(map.Width, map.Height, rules);
        grid.Recompute(map);
        return (map, grid);
    }

    [Fact]
    public void Zones_within_capacity_are_all_powered()
    {
        var (_, grid) = Corridor(3, new PowerRules(PlantCapacity: 3));
        Assert.Equal(3, grid.PoweredZones);
        Assert.Equal(0, grid.ZonesWithoutSupply);
    }

    [Fact]
    public void Over_capacity_the_nearest_zones_are_powered_first()
    {
        var (_, grid) = Corridor(5, new PowerRules(PlantCapacity: 2));
        Assert.True(grid.IsPowered(1, 0));
        Assert.True(grid.IsPowered(2, 0));
        Assert.False(grid.IsPowered(3, 0));
        Assert.False(grid.IsPowered(5, 0));
        Assert.Equal(3, grid.ZonesWithoutSupply);
        Assert.Equal(2, grid.Supply);
        Assert.Equal(5, grid.Demand);
    }

    [Fact]
    public void Demand_per_zone_scales_what_a_plant_can_serve()
    {
        var (_, grid) = Corridor(4, new PowerRules(PlantCapacity: 6, DemandPerZone: 3));
        Assert.Equal(2, grid.PoweredZones);
    }

    [Fact]
    public void Lines_and_plants_stay_live_even_when_zones_go_dark()
    {
        var map = new CityMap(4, 1);
        map[0, 0].Kind = TileKind.PowerPlant;
        map[1, 0].Kind = TileKind.PowerLine;
        map[2, 0].Kind = TileKind.Residential;
        map[3, 0].Kind = TileKind.Residential;
        var grid = new PowerGrid(4, 1, new PowerRules(PlantCapacity: 0));
        grid.Recompute(map);

        Assert.True(grid.IsPowered(0, 0));
        Assert.True(grid.IsPowered(1, 0));
        Assert.False(grid.IsPowered(2, 0));
        Assert.Equal(2, grid.PoweredCount);
    }

    [Fact]
    public void A_second_plant_adds_capacity()
    {
        var map = new CityMap(6, 1);
        map[0, 0].Kind = TileKind.PowerPlant;
        for (int x = 1; x <= 4; x++) map[x, 0].Kind = TileKind.Residential;
        map[5, 0].Kind = TileKind.PowerPlant;
        var grid = new PowerGrid(6, 1, new PowerRules(PlantCapacity: 2));
        grid.Recompute(map);

        Assert.Equal(4, grid.PoweredZones);
        Assert.Equal(4, grid.Supply);
    }

    [Fact]
    public void Each_network_has_its_own_supply()
    {
        // Plant A feeds 3 zones with capacity 1; plant B (separate) feeds 1 zone.
        var map = new CityMap(9, 1);
        map[0, 0].Kind = TileKind.PowerPlant;
        for (int x = 1; x <= 3; x++) map[x, 0].Kind = TileKind.Residential;
        // (4,0) empty gap
        map[5, 0].Kind = TileKind.PowerPlant;
        map[6, 0].Kind = TileKind.Residential;
        var grid = new PowerGrid(9, 1, new PowerRules(PlantCapacity: 1));
        grid.Recompute(map);

        Assert.True(grid.IsPowered(1, 0));
        Assert.False(grid.IsPowered(2, 0)); // A is spent; B's spare capacity does not cross the gap
        Assert.True(grid.IsPowered(6, 0));
    }

    [Fact]
    public void Ties_in_distance_go_to_the_lower_tile_index()
    {
        // Plant in the middle, one zone each side at equal distance, capacity for one.
        var map = new CityMap(3, 1);
        map[1, 0].Kind = TileKind.PowerPlant;
        map[0, 0].Kind = TileKind.Residential;
        map[2, 0].Kind = TileKind.Residential;
        var grid = new PowerGrid(3, 1, new PowerRules(PlantCapacity: 1));
        grid.Recompute(map);

        Assert.True(grid.IsPowered(0, 0));
        Assert.False(grid.IsPowered(2, 0));
    }

    [Fact]
    public void The_simulation_reports_a_shortfall_after_building_beyond_capacity()
    {
        var sim = new Simulation(8, 1, startingFunds: 100_000, power: new PowerRules(PlantCapacity: 3));
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 0, 5, 1, TileKind.Residential));
        sim.Step();

        Assert.Equal(3, sim.Power.PoweredZones);
        Assert.Equal(2, sim.Power.ZonesWithoutSupply);
    }
}

public class PowerCrossingTests
{
    [Fact]
    public void A_line_over_a_road_makes_a_crossing_that_carries_both()
    {
        var sim = new Simulation(5, 1, startingFunds: 100_000);
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 0, TileKind.Road));
        sim.Enqueue(new PlaceCommand(1, 0, TileKind.PowerLine));
        sim.Enqueue(new PlaceCommand(2, 0, TileKind.Residential));
        sim.Step();

        Assert.Equal(TileKind.RoadWithLine, sim.Map[1, 0].Kind);
        Assert.True(sim.Power.IsPowered(2, 0));
    }

    [Fact]
    public void A_road_over_a_line_also_makes_a_crossing()
    {
        var sim = new Simulation(3, 1, startingFunds: 100_000);
        sim.Enqueue(new PlaceCommand(1, 0, TileKind.PowerLine));
        sim.Enqueue(new PlaceCommand(1, 0, TileKind.Road));
        sim.Step();
        Assert.Equal(TileKind.RoadWithLine, sim.Map[1, 0].Kind);
    }

    [Fact]
    public void A_crossing_costs_the_line_plus_the_crossing_fee()
    {
        var sim = new Simulation(3, 1, startingFunds: 100_000);
        sim.Enqueue(new PlaceCommand(1, 0, TileKind.Road));
        sim.Step();

        var quote = sim.Quote(new PlaceCommand(1, 0, TileKind.PowerLine));
        Assert.True(quote.Accepted);
        Assert.Equal(sim.Prices.Build(TileKind.PowerLine) + sim.Prices.LineCrossing, quote.Cost);
    }

    [Fact]
    public void Repeating_a_line_on_a_crossing_changes_nothing()
    {
        var sim = new Simulation(3, 1, startingFunds: 100_000);
        sim.Enqueue(new PlaceCommand(1, 0, TileKind.Road));
        sim.Enqueue(new PlaceCommand(1, 0, TileKind.PowerLine));
        sim.Step();

        Assert.Equal(Refusal.NothingToChange, sim.Quote(new PlaceCommand(1, 0, TileKind.PowerLine)).Refusal);
        Assert.Equal(Refusal.NothingToChange, sim.Quote(new PlaceCommand(1, 0, TileKind.Road)).Refusal);
    }

    [Fact]
    public void Bulldozing_a_crossing_clears_both()
    {
        var sim = new Simulation(3, 1, startingFunds: 100_000);
        sim.Enqueue(new PlaceCommand(1, 0, TileKind.Road));
        sim.Enqueue(new PlaceCommand(1, 0, TileKind.PowerLine));
        sim.Enqueue(new BulldozeCommand(1, 0));
        sim.Step();
        Assert.Equal(TileKind.Empty, sim.Map[1, 0].Kind);
    }

    [Fact]
    public void A_plain_road_still_blocks_power()
    {
        var sim = new Simulation(3, 1, startingFunds: 100_000);
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 0, TileKind.Road));
        sim.Enqueue(new PlaceCommand(2, 0, TileKind.Residential));
        sim.Step();
        Assert.False(sim.Power.IsPowered(2, 0));
    }
}
