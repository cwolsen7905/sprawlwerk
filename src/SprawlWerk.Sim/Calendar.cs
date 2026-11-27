namespace SprawlWerk.Sim;

/// <summary>
/// A date derived from the tick count. Nothing is stored: the same tick
/// always gives the same date. Placeholder lengths: 4 ticks a day, 30 days a
/// month, 12 months a year.
/// </summary>
public readonly record struct CityDate(int Year, int Month, int Day)
{
    public const int TicksPerDay = 4;
    public const int DaysPerMonth = 30;
    public const int MonthsPerYear = 12;
    public const int TicksPerMonth = TicksPerDay * DaysPerMonth;

    /// <summary>True on the first tick of a month, tick 0 included; monthly passes run then.</summary>
    public static bool IsMonthStart(long tick) => tick >= 0 && tick % TicksPerMonth == 0;

    /// <summary>The date at a tick count. Year, month and day all start at 1.</summary>
    public static CityDate FromTick(long tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        long days = tick / TicksPerDay;
        long months = days / DaysPerMonth;
        return new CityDate(
            Year: (int)(months / MonthsPerYear) + 1,
            Month: (int)(months % MonthsPerYear) + 1,
            Day: (int)(days % DaysPerMonth) + 1);
    }

    public override string ToString() => $"Y{Year} M{Month:00} D{Day:00}";
}
