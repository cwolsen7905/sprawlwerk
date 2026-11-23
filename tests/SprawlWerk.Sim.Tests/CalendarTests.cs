using SprawlWerk.Sim;

namespace SprawlWerk.Sim.Tests;

public class CalendarTests
{
    [Fact]
    public void A_new_city_starts_on_the_first_day_of_year_one()
    {
        Assert.Equal(new CityDate(1, 1, 1), new Simulation(4, 4).Date);
    }

    [Fact]
    public void Day_rolls_over_after_the_ticks_in_a_day()
    {
        Assert.Equal(new CityDate(1, 1, 1), CityDate.FromTick(CityDate.TicksPerDay - 1));
        Assert.Equal(new CityDate(1, 1, 2), CityDate.FromTick(CityDate.TicksPerDay));
    }

    [Fact]
    public void Month_and_year_roll_over()
    {
        long month = (long)CityDate.TicksPerDay * CityDate.DaysPerMonth;
        Assert.Equal(new CityDate(1, 2, 1), CityDate.FromTick(month));
        Assert.Equal(new CityDate(2, 1, 1), CityDate.FromTick(month * CityDate.MonthsPerYear));
        Assert.Equal(new CityDate(1, 12, 30), CityDate.FromTick(month * CityDate.MonthsPerYear - 1));
    }

    [Fact]
    public void The_date_follows_steps()
    {
        var sim = new Simulation(4, 4);
        for (int i = 0; i < CityDate.TicksPerDay; i++) sim.Step();
        Assert.Equal(new CityDate(1, 1, 2), sim.Date);
    }

    [Fact]
    public void Negative_ticks_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CityDate.FromTick(-1));
    }
}
