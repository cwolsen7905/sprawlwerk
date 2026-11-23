using SprawlWerk.Sim;

namespace SprawlWerk.Sim.Tests;

public class PowerGridTests
{
    [Fact]
    public void Plant_powers_itself()
    {
        var map = new CityMap(5, 5);
        map[2, 2].Kind = TileKind.PowerPlant;

        var grid = new PowerGrid(map.Width, map.Height);
        Assert.Equal(1, grid.Recompute(map));
        Assert.True(grid.IsPowered(2, 2));
    }

    [Fact]
    public void Power_flows_through_lines_into_connected_zones()
    {
        var map = new CityMap(10, 1);
        map[0, 0].Kind = TileKind.PowerPlant;
        for (int x = 1; x <= 4; x++) map[x, 0].Kind = TileKind.PowerLine;
        map[5, 0].Kind = TileKind.Residential;
        map[6, 0].Kind = TileKind.Residential; // zones conduct to each other

        var grid = new PowerGrid(map.Width, map.Height);

        grid.Recompute(map);

        Assert.True(grid.IsPowered(5, 0));
        Assert.True(grid.IsPowered(6, 0));
        Assert.False(grid.IsPowered(7, 0)); // empty land
    }

    [Fact]
    public void Gap_in_the_line_cuts_power()
    {
        var map = new CityMap(5, 1);
        map[0, 0].Kind = TileKind.PowerPlant;
        map[1, 0].Kind = TileKind.PowerLine;
        // (2,0) left empty
        map[3, 0].Kind = TileKind.PowerLine;
        map[4, 0].Kind = TileKind.Commercial;

        var grid = new PowerGrid(map.Width, map.Height);

        grid.Recompute(map);

        Assert.True(grid.IsPowered(1, 0));
        Assert.False(grid.IsPowered(3, 0));
        Assert.False(grid.IsPowered(4, 0));
    }

    [Fact]
    public void Roads_do_not_carry_power()
    {
        var map = new CityMap(3, 1);
        map[0, 0].Kind = TileKind.PowerPlant;
        map[1, 0].Kind = TileKind.Road;
        map[2, 0].Kind = TileKind.Industrial;

        var grid = new PowerGrid(map.Width, map.Height);

        grid.Recompute(map);

        Assert.False(grid.IsPowered(2, 0));
    }

    [Fact]
    public void Diagonal_tiles_are_not_connected()
    {
        var map = new CityMap(2, 2);
        map[0, 0].Kind = TileKind.PowerPlant;
        map[1, 1].Kind = TileKind.Residential;

        var grid = new PowerGrid(map.Width, map.Height);

        grid.Recompute(map);

        Assert.False(grid.IsPowered(1, 1));
    }
}
