using SprawlWerk.Sim;

namespace SprawlWerk.Sim.Tests;

public class SimulationTests
{
    [Fact]
    public void Commands_apply_on_the_next_step_not_immediately()
    {
        var sim = new Simulation(8, 8);
        sim.Enqueue(new PlaceCommand(1, 1, TileKind.Residential));

        Assert.Equal(TileKind.Empty, sim.Map[1, 1].Kind);
        sim.Step();
        Assert.Equal(TileKind.Residential, sim.Map[1, 1].Kind);
    }

    [Fact]
    public void Rectangle_zoning_fills_every_tile_and_clips_to_the_map()
    {
        var sim = new Simulation(4, 4);
        sim.Enqueue(new PlaceCommand(2, 2, 5, 5, TileKind.Commercial));
        sim.Step();

        for (int y = 2; y < 4; y++)
        for (int x = 2; x < 4; x++)
            Assert.Equal(TileKind.Commercial, sim.Map[x, y].Kind);
        Assert.Equal(TileKind.Empty, sim.Map[1, 1].Kind);
    }

    [Fact]
    public void Power_updates_after_building_and_bulldozing()
    {
        var sim = new Simulation(6, 1);
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 0, 4, 1, TileKind.PowerLine));
        sim.Enqueue(new PlaceCommand(5, 0, TileKind.Residential));
        sim.Step();
        Assert.True(sim.Power.IsPowered(5, 0));
        Assert.Equal(6, sim.PoweredTiles);

        sim.Enqueue(new BulldozeCommand(3, 0));
        sim.Step();
        Assert.False(sim.Power.IsPowered(5, 0));
        Assert.Equal(3, sim.PoweredTiles);
    }

    [Fact]
    public void Tick_counts_steps()
    {
        var sim = new Simulation(2, 2);
        for (int i = 0; i < 10; i++) sim.Step();
        Assert.Equal(10, sim.Tick);
    }
}
