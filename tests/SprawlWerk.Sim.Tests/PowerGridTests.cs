using SprawlWerk.Sim;

namespace SprawlWerk.Sim.Tests;

public class PowerGridTests
{
    [Fact]
    public void Plant_powers_itself()
    {
        var map = new CityMap(5, 5);
        map[2, 2].Kind = TileKind.PowerPlant;

        Assert.Equal(1, PowerGrid.Recompute(map));
        Assert.True(map[2, 2].Powered);
    }

    [Fact]
    public void Power_flows_through_lines_into_connected_zones()
    {
        var map = new CityMap(10, 1);
        map[0, 0].Kind = TileKind.PowerPlant;
        for (int x = 1; x <= 4; x++) map[x, 0].Kind = TileKind.PowerLine;
        map[5, 0].Kind = TileKind.Residential;
        map[6, 0].Kind = TileKind.Residential; // zones conduct to each other

        PowerGrid.Recompute(map);

        Assert.True(map[5, 0].Powered);
        Assert.True(map[6, 0].Powered);
        Assert.False(map[7, 0].Powered); // empty land
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

        PowerGrid.Recompute(map);

        Assert.True(map[1, 0].Powered);
        Assert.False(map[3, 0].Powered);
        Assert.False(map[4, 0].Powered);
    }

    [Fact]
    public void Roads_do_not_carry_power()
    {
        var map = new CityMap(3, 1);
        map[0, 0].Kind = TileKind.PowerPlant;
        map[1, 0].Kind = TileKind.Road;
        map[2, 0].Kind = TileKind.Industrial;

        PowerGrid.Recompute(map);

        Assert.False(map[2, 0].Powered);
    }

    [Fact]
    public void Diagonal_tiles_are_not_connected()
    {
        var map = new CityMap(2, 2);
        map[0, 0].Kind = TileKind.PowerPlant;
        map[1, 1].Kind = TileKind.Residential;

        PowerGrid.Recompute(map);

        Assert.False(map[1, 1].Powered);
    }
}
