using System.Text.Json;
using SprawlWerk.Sim;

namespace SprawlWerk.Sim.Tests;

public class SaveGameTests
{
    private static Simulation SampleCity()
    {
        var sim = new Simulation(12, 8, startingFunds: 50_000);
        sim.Map[3, 3].Height = 4;
        sim.Enqueue(new PlaceCommand(0, 0, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 0, 4, 1, TileKind.PowerLine));
        sim.Enqueue(new PlaceCommand(2, 1, 3, 3, TileKind.Residential));
        sim.Enqueue(new PlaceCommand(6, 0, 1, 8, TileKind.Road));
        sim.Enqueue(new PlaceCommand(6, 4, TileKind.PowerLine)); // crossing
        sim.Enqueue(new PlaceCommand(8, 2, 2, 2, TileKind.Commercial));
        sim.Enqueue(new PlaceCommand(8, 6, 2, 1, TileKind.Industrial));
        for (int i = 0; i < 10; i++) sim.Step();
        return sim;
    }

    [Fact]
    public void A_save_round_trips_everything_stored()
    {
        var sim = SampleCity();
        var loaded = SaveGame.Deserialize(SaveGame.Serialize(sim));

        Assert.Equal(sim.Tick, loaded.Tick);
        Assert.Equal(sim.Funds, loaded.Funds);
        Assert.Equal(sim.Map.Width, loaded.Map.Width);
        for (int y = 0; y < sim.Map.Height; y++)
        for (int x = 0; x < sim.Map.Width; x++)
        {
            Assert.Equal(sim.Map[x, y].Kind, loaded.Map[x, y].Kind);
            Assert.Equal(sim.Map[x, y].Height, loaded.Map[x, y].Height);
        }
    }

    [Fact]
    public void Derived_power_is_rebuilt_on_load()
    {
        var sim = SampleCity();
        var loaded = SaveGame.Deserialize(SaveGame.Serialize(sim));

        Assert.Equal(sim.PoweredTiles, loaded.PoweredTiles);
        for (int y = 0; y < sim.Map.Height; y++)
        for (int x = 0; x < sim.Map.Width; x++)
            Assert.Equal(sim.Power.IsPowered(x, y), loaded.Power.IsPowered(x, y));
    }

    [Fact]
    public void A_loaded_city_continues_exactly_as_the_saved_one_would_have()
    {
        var original = SampleCity();
        var loaded = SaveGame.Deserialize(SaveGame.Serialize(original));

        foreach (var sim in new[] { original, loaded })
        {
            sim.Enqueue(new PlaceCommand(7, 0, 1, 8, TileKind.Road));
            sim.Enqueue(new PlaceCommand(9, 0, TileKind.PowerPlant));
            sim.Enqueue(new BulldozeCommand(2, 1, 1, 3));
            for (int i = 0; i < 1000; i++) sim.Step();
        }

        Assert.Equal(SaveGame.Serialize(original), SaveGame.Serialize(loaded));
        Assert.Equal(original.Date, loaded.Date);
    }

    [Fact]
    public void The_save_is_readable_json_with_a_version()
    {
        using var doc = JsonDocument.Parse(SaveGame.Serialize(SampleCity()));
        Assert.Equal(SaveGame.CurrentVersion, doc.RootElement.GetProperty("Version").GetInt32());
        Assert.Equal(8, doc.RootElement.GetProperty("Kinds").GetArrayLength());
    }

    [Fact]
    public void Every_tile_kind_has_a_save_character()
    {
        var sim = new Simulation(8, 1);
        int i = 0;
        foreach (var kind in Enum.GetValues<TileKind>()) sim.Map[i++, 0].Kind = kind;

        var loaded = SaveGame.Deserialize(SaveGame.Serialize(sim));
        i = 0;
        foreach (var kind in Enum.GetValues<TileKind>()) Assert.Equal(kind, loaded.Map[i++, 0].Kind);
    }

    [Fact]
    public void A_save_from_a_newer_game_is_refused_not_misread()
    {
        var json = SaveGame.Serialize(SampleCity()).Replace("\"Version\": 1", "\"Version\": 99");
        var e = Assert.Throws<SaveFormatException>(() => SaveGame.Deserialize(json));
        Assert.Contains("newer", e.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"Version\":1,\"Width\":2,\"Height\":1,\"Tick\":0,\"Funds\":0,\"Kinds\":[\"..\",\"..\"],\"Heights\":[[0,0]]}")]
    [InlineData("{\"Version\":1,\"Width\":2,\"Height\":1,\"Tick\":0,\"Funds\":0,\"Kinds\":[\"?.\"],\"Heights\":[[0,0]]}")]
    [InlineData("{\"Version\":1,\"Width\":2,\"Height\":1,\"Tick\":0,\"Funds\":0,\"Kinds\":[\"...\"],\"Heights\":[[0,0]]}")]
    [InlineData("{\"Version\":1,\"Width\":2,\"Height\":1,\"Tick\":-5,\"Funds\":0,\"Kinds\":[\"..\"],\"Heights\":[[0,0]]}")]
    [InlineData("{\"Version\":1,\"Width\":2,\"Height\":1,\"Tick\":0,\"Funds\":0,\"Kinds\":[\"..\"],\"Heights\":[[0,300]]}")]
    public void Damaged_saves_are_refused_with_a_clear_error(string json)
    {
        Assert.Throws<SaveFormatException>(() => SaveGame.Deserialize(json));
    }
}
