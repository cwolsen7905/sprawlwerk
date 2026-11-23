using SprawlWerk.Sim;

namespace SprawlWerk.Sim.Tests;

public class SaveNamesTests
{
    [Theory]
    [InlineData("My City", "My City")]
    [InlineData("  spaced   out  ", "spaced out")]
    [InlineData("a/b\\c:d*e", "abcde")]
    [InlineData("../../etc/passwd", "etcpasswd")]
    [InlineData("city-1_final", "city-1_final")]
    [InlineData("tab\tand\nnewline", "tab and newline")]
    public void Typed_names_are_cleaned(string typed, string expected)
    {
        Assert.Equal(expected, SaveNames.Clean(typed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("///...")]
    public void Names_with_nothing_usable_are_refused(string? typed)
    {
        Assert.Null(SaveNames.Clean(typed));
        Assert.Null(SaveNames.ToFileName(typed));
    }

    [Fact]
    public void Long_names_are_cut_to_the_limit()
    {
        var name = SaveNames.Clean(new string('a', 100))!;
        Assert.Equal(SaveNames.MaxLength, name.Length);
    }

    [Fact]
    public void File_names_never_hold_a_path()
    {
        var file = SaveNames.ToFileName("../x")!;
        Assert.Equal("x.json", file);
        Assert.DoesNotContain('/', file);
    }

    [Fact]
    public void File_names_map_back_to_display_names()
    {
        Assert.Equal("My City", SaveNames.FromFileName("My City.json"));
        Assert.Equal("a", SaveNames.FromFileName("a.JSON"));
        Assert.Null(SaveNames.FromFileName("notes.txt"));
        Assert.Null(SaveNames.FromFileName(".json"));
    }

    [Fact]
    public void A_cleaned_name_cleans_to_itself()
    {
        var once = SaveNames.Clean("  Hello,   World! ")!;
        Assert.Equal(once, SaveNames.Clean(once));
    }
}

public class SaveInfoTests
{
    [Fact]
    public void Info_reports_tick_date_and_size_without_loading_the_city()
    {
        var sim = new Simulation(9, 7, startingFunds: 1234);
        for (int i = 0; i < CityDate.TicksPerDay * 3; i++) sim.Step();

        var info = SaveGame.ReadInfo(SaveGame.Serialize(sim));

        Assert.Equal(sim.Tick, info.Tick);
        Assert.Equal(new CityDate(1, 1, 4), info.Date);
        Assert.Equal(9, info.Width);
        Assert.Equal(7, info.Height);
        Assert.Equal(1234, info.Funds);
        Assert.Equal(SaveGame.CurrentVersion, info.Version);
    }

    [Fact]
    public void Info_refuses_newer_and_damaged_saves_like_a_load_does()
    {
        var json = SaveGame.Serialize(new Simulation(2, 2));
        var newer = Assert.Throws<SaveFormatException>(() => SaveGame.ReadInfo(json.Replace("\"Version\": 1", "\"Version\": 2")));
        Assert.Contains("newer", newer.Message);
        Assert.Throws<SaveFormatException>(() => SaveGame.ReadInfo("not json"));
        Assert.Throws<SaveFormatException>(() => SaveGame.ReadInfo("{}"));
    }
}
