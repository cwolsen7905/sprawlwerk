using SprawlWerk.Sim;

namespace SprawlWerk.Sim.Tests;

public class DragTests
{
    [Fact]
    public void Zones_fill_a_rectangle_whichever_corner_the_drag_starts_from()
    {
        Assert.Equal(new PlaceCommand(2, 3, 4, 3, TileKind.Residential),
            Drag.Command(TileKind.Residential, 5, 5, 2, 3));
    }

    [Fact]
    public void A_click_is_a_single_tile()
    {
        Assert.Equal(new PlaceCommand(4, 4, 1, 1, TileKind.Commercial),
            Drag.Command(TileKind.Commercial, 4, 4, 4, 4));
    }

    [Fact]
    public void Roads_and_lines_run_straight_along_the_longer_axis()
    {
        Assert.Equal(new PlaceCommand(1, 2, 6, 1, TileKind.Road),
            Drag.Command(TileKind.Road, 1, 2, 6, 4));
        Assert.Equal(new PlaceCommand(3, 1, 1, 5, TileKind.PowerLine),
            Drag.Command(TileKind.PowerLine, 3, 5, 4, 1));
    }

    [Fact]
    public void Plants_are_placed_at_the_drag_end_only()
    {
        Assert.Equal(new PlaceCommand(7, 7, 1, 1, TileKind.PowerPlant),
            Drag.Command(TileKind.PowerPlant, 1, 1, 7, 7));
    }

    [Fact]
    public void Bulldoze_clears_a_rectangle()
    {
        Assert.Equal(new BulldozeCommand(1, 1, 3, 2), Drag.Command(TileKind.Empty, 3, 2, 1, 1));
    }

    [Fact]
    public void The_dragged_command_quotes_the_real_cost()
    {
        var sim = new Simulation(8, 8);
        var quote = sim.Quote(Drag.Command(TileKind.Residential, 0, 0, 2, 1));
        Assert.True(quote.Accepted);
        Assert.Equal(6, quote.TilesChanged);
        Assert.Equal(6 * sim.Prices.Build(TileKind.Residential), quote.Cost);
    }
}
