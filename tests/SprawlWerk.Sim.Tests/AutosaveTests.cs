using SprawlWerk.Sim;

namespace SprawlWerk.Sim.Tests;

public class AutosaveTests
{
    private const long Month = (long)CityDate.TicksPerDay * CityDate.DaysPerMonth;

    [Fact]
    public void The_autosave_slot_is_a_listable_save_file()
    {
        Assert.Equal("autosave", SaveNames.FromFileName(Autosave.FileName));
    }

    [Fact]
    public void Autosave_is_due_when_a_step_crosses_into_a_new_month()
    {
        Assert.False(Autosave.IsDue(0, 1));
        Assert.False(Autosave.IsDue(Month - 2, Month - 1));
        Assert.True(Autosave.IsDue(Month - 1, Month));
        Assert.False(Autosave.IsDue(Month, Month + 1));
        Assert.True(Autosave.IsDue(Month * 12 - 1, Month * 12)); // into a new year
    }

    [Fact]
    public void Autosave_is_not_due_when_nothing_moved_or_time_went_backwards()
    {
        Assert.False(Autosave.IsDue(Month, Month));
        Assert.False(Autosave.IsDue(Month * 3, Month)); // e.g. a load of an older city
    }

    [Fact]
    public void Autosave_fires_once_per_month_over_a_running_city()
    {
        var sim = new Simulation(4, 4);
        int due = 0;
        for (int i = 0; i < Month * 5; i++)
        {
            long before = sim.Tick;
            sim.Step();
            if (Autosave.IsDue(before, sim.Tick)) due++;
        }
        Assert.Equal(5, due); // the boundaries at months 1 to 5; the run ends exactly on the last one
    }

    [Fact]
    public void Continue_picks_the_newest_readable_save_of_any_kind()
    {
        var pick = Autosave.ContinueTarget(new[]
        {
            new Autosave.Candidate("old.json", 100, true),
            new Autosave.Candidate("autosave.json", 300, true),
            new Autosave.Candidate("mine.json", 200, true),
        });
        Assert.Equal("autosave.json", pick);

        pick = Autosave.ContinueTarget(new[]
        {
            new Autosave.Candidate("autosave.json", 300, true),
            new Autosave.Candidate("mine.json", 400, true),
        });
        Assert.Equal("mine.json", pick);
    }

    [Fact]
    public void Continue_skips_damaged_and_newer_version_saves()
    {
        var pick = Autosave.ContinueTarget(new[]
        {
            new Autosave.Candidate("broken.json", 900, false),
            new Autosave.Candidate("good.json", 100, true),
        });
        Assert.Equal("good.json", pick);
    }

    [Fact]
    public void Continue_has_nothing_to_open_when_no_save_is_readable()
    {
        Assert.Null(Autosave.ContinueTarget(Array.Empty<Autosave.Candidate>()));
        Assert.Null(Autosave.ContinueTarget(new[] { new Autosave.Candidate("broken.json", 5, false) }));
    }

    [Fact]
    public void Continue_ties_go_to_the_name_that_sorts_first_whatever_the_order()
    {
        var a = new Autosave.Candidate("a.json", 50, true);
        var b = new Autosave.Candidate("b.json", 50, true);
        Assert.Equal("a.json", Autosave.ContinueTarget(new[] { a, b }));
        Assert.Equal("a.json", Autosave.ContinueTarget(new[] { b, a }));
    }
}
