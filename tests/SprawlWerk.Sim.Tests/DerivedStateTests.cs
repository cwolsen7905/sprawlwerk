using System.Runtime.InteropServices;
using SprawlWerk.Sim;

namespace SprawlWerk.Sim.Tests;

/// <summary>
/// Stored state (what is on each tile) is all a save needs; derived state
/// (power, later water and traffic) is rebuilt from it.
/// </summary>
public class DerivedStateTests
{
    [Fact]
    public void Power_rebuilt_from_a_copy_of_the_map_matches_the_running_city()
    {
        var sim = new Simulation(12, 6);
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 0, 5, 1, TileKind.PowerLine));
        sim.Enqueue(new PlaceCommand(6, 0, 3, 3, TileKind.Residential));
        sim.Enqueue(new PlaceCommand(9, 4, 2, 2, TileKind.Industrial)); // unpowered
        sim.Step();
        sim.Enqueue(new BulldozeCommand(3, 0)); // cut the line, then
        sim.Step();
        sim.Enqueue(new PlaceCommand(3, 0, TileKind.PowerLine)); // mend it
        sim.Step();

        // "Save": copy only the stored tiles. "Load": a fresh grid over the copy.
        var copy = new CityMap(sim.Map.Width, sim.Map.Height);
        for (int y = 0; y < copy.Height; y++)
        for (int x = 0; x < copy.Width; x++)
            copy[x, y] = sim.Map[x, y];
        var rebuilt = new PowerGrid(copy.Width, copy.Height);
        rebuilt.Recompute(copy);

        Assert.Equal(sim.PoweredTiles, rebuilt.PoweredCount);
        for (int y = 0; y < copy.Height; y++)
        for (int x = 0; x < copy.Width; x++)
            Assert.Equal(sim.Power.IsPowered(x, y), rebuilt.IsPowered(x, y));
    }

    [Fact]
    public void Recompute_forgets_earlier_results()
    {
        var map = new CityMap(3, 1);
        map[0, 0].Kind = TileKind.PowerPlant;
        map[1, 0].Kind = TileKind.Residential;
        var grid = new PowerGrid(3, 1);
        grid.Recompute(map);
        Assert.True(grid.IsPowered(1, 0));

        map[0, 0].Kind = TileKind.Empty;
        grid.Recompute(map);

        Assert.False(grid.IsPowered(1, 0));
        Assert.Equal(0, grid.PoweredCount);
    }

    [Fact]
    public void A_tile_holds_only_stored_fields()
    {
        // Height and Kind, one byte each. A derived flag added here again would grow it.
        Assert.Equal(2, Marshal.SizeOf<Tile>());
        var fields = typeof(Tile).GetFields().Select(f => f.Name).Order();
        Assert.Equal(new[] { "Height", "Kind" }, fields);
    }

    [Fact]
    public void A_grid_refuses_a_map_of_another_size()
    {
        var grid = new PowerGrid(4, 4);
        Assert.Throws<ArgumentException>(() => grid.Recompute(new CityMap(5, 4)));
    }

    [Fact]
    public void Asking_about_a_tile_off_the_grid_throws()
    {
        var grid = new PowerGrid(2, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.IsPowered(2, 0));
    }
}
