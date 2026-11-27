using SprawlWerk.Sim.Content;

namespace SprawlWerk.Sim.Tests;

/// <summary>The base pack the game ships, read from <c>game/content/base</c>.</summary>
public class BasePackTests
{
    private static ContentSet Load() => TestContent.Load();

    [Fact]
    public void The_base_pack_loads_and_declares_a_licence()
    {
        var content = Load();
        var pack = Assert.Single(content.Packs);
        Assert.Equal("base", pack.Id);
        Assert.Equal("BSD-3-Clause", pack.License);
    }

    // Until the simulation reads types from content (ADR 0011), the base pack
    // and the hard-coded placeholders must agree, so switching over changes nothing.
    [Theory]
    [InlineData("base:residential", TileKind.Residential)]
    [InlineData("base:commercial", TileKind.Commercial)]
    [InlineData("base:industrial", TileKind.Industrial)]
    [InlineData("base:road", TileKind.Road)]
    [InlineData("base:power_line", TileKind.PowerLine)]
    [InlineData("base:power_plant", TileKind.PowerPlant)]
    public void Base_prices_match_the_current_price_list(string id, TileKind kind)
    {
        var content = Load();
        Assert.Equal(PriceList.Default.Build(kind), content[content.Find(id)].Cost);
    }

    [Fact]
    public void Base_power_numbers_match_the_current_rules()
    {
        var content = Load();
        Assert.Equal(PowerRules.Default.PlantCapacity, content[content.Find("base:power_plant")].PowerSupply);
        Assert.All(content.Types.Where(t => t.Role == TypeRole.Zone),
            zone => Assert.Equal(PowerRules.Default.DemandPerZone, zone.PowerUse));
    }
}
