using SprawlWerk.Sim.Content;

namespace SprawlWerk.Sim;

/// <summary>Tax rate per zone, in whole percent (<see cref="BudgetRules.MinTax"/> to <see cref="BudgetRules.MaxTax"/>).</summary>
public readonly record struct TaxRates(int Residential, int Commercial, int Industrial)
{
    public static TaxRates All(int rate) => new(rate, rate, rate);

    public int For(ZoneKind zone) => zone switch
    {
        ZoneKind.Residential => Residential,
        ZoneKind.Commercial => Commercial,
        _ => Industrial,
    };

    public TaxRates With(ZoneKind zone, int rate) => zone switch
    {
        ZoneKind.Residential => this with { Residential = rate },
        ZoneKind.Commercial => this with { Commercial = rate },
        _ => this with { Industrial = rate },
    };
}

/// <summary>One month's money: taxes collected per zone and upkeep paid per kind of thing built.</summary>
public readonly record struct MonthlyBudget(
    long ResidentialTax, long CommercialTax, long IndustrialTax,
    long RoadUpkeep, long PowerLineUpkeep, long PlantUpkeep, long ZoneUpkeep = 0)
{
    public long Income => ResidentialTax + CommercialTax + IndustrialTax;

    public long Expenses => RoadUpkeep + PowerLineUpkeep + PlantUpkeep + ZoneUpkeep;

    public long Net => Income - Expenses;

    public long TaxFor(ZoneKind zone) => zone switch
    {
        ZoneKind.Residential => ResidentialTax,
        ZoneKind.Commercial => CommercialTax,
        _ => IndustrialTax,
    };
}

/// <summary>A command that changes a city setting rather than the map; it never marks the map changed.</summary>
internal interface ICitySetting
{
    void Apply(Simulation city);
}

/// <summary>Set one zone's tax rate. Free; refused outside <see cref="BudgetRules.MinTax"/>..<see cref="BudgetRules.MaxTax"/>.</summary>
public sealed record SetTaxCommand(ZoneKind Zone, int Rate) : ICommand, ICitySetting
{
    public CommandResult Plan(CityMap map, PriceList prices) =>
        Rate is < BudgetRules.MinTax or > BudgetRules.MaxTax
            ? CommandResult.Refuse(Refusal.OutOfRange)
            : CommandResult.Accept(0, 0);

    /// <summary>Changes nothing on the map; the simulation applies the rate itself.</summary>
    public void Execute(CityMap map) { }

    void ICitySetting.Apply(Simulation city) => city.SetTaxRate(Zone, Rate);
}

/// <summary>
/// The budget (ADR 0012, option C: sprawl costs money through upkeep). At each
/// month start after the first, before demand is updated, the month that just
/// ended is settled: every zone pays tax on its base (residents, commercial
/// jobs, industrial jobs) and every road, line and plant its type's upkeep.
/// Funds may go negative; there is no bankruptcy yet. Integer maths only.
/// </summary>
public sealed partial class Simulation
{
    private TaxRates? _taxes;
    private long[]? _upkeep;

    public BudgetRules BudgetRules { get; } = BudgetRules.Default;

    /// <summary>Tax rate per zone; every zone starts at <see cref="GrowthRules.NeutralTax"/>.</summary>
    public TaxRates Taxes => _taxes ?? TaxRates.All(Growth.NeutralTax);

    /// <summary>The last month settled, or null before the first.</summary>
    public MonthlyBudget? LastBudget { get; private set; }

    internal void SetTaxRate(ZoneKind zone, int rate) => _taxes = Taxes.With(zone, rate);

    /// <summary>Demand a zone loses to its tax rate (gains, below neutral).</summary>
    internal int TaxPull(ZoneKind zone) => (Taxes.For(zone) - Growth.NeutralTax) * Growth.TaxWeight;

    /// <summary>Monthly upkeep of one tile of this kind; a road carrying a line pays both. 0 without content.</summary>
    public long UpkeepOf(TileKind kind)
    {
        if (_upkeep is null)
        {
            _upkeep = new long[Enum.GetValues<TileKind>().Length];
            if (Content is { } content)
            {
                foreach (var k in Enum.GetValues<TileKind>())
                    if (KindIds.IdFor(k) is not null) _upkeep[(int)k] = KindIds.TypeFor(content, k).Upkeep;
                _upkeep[(int)TileKind.RoadWithLine] = _upkeep[(int)TileKind.Road] + _upkeep[(int)TileKind.PowerLine];
            }
        }
        return _upkeep[(int)kind];
    }

    /// <summary>What the city would collect and pay if the month ended now.</summary>
    public MonthlyBudget ProjectBudget()
    {
        long Tax(long units, int perUnit, ZoneKind zone) =>
            units * perUnit * Taxes.For(zone) / Math.Max(1, Growth.NeutralTax);

        long roads = 0, lines = 0, plants = 0, zones = 0;
        for (int y = 0; y < Map.Height; y++)
        for (int x = 0; x < Map.Width; x++)
        {
            var kind = Map[x, y].Kind;
            switch (kind)
            {
                case TileKind.Road: roads += UpkeepOf(kind); break;
                case TileKind.PowerLine: lines += UpkeepOf(kind); break;
                case TileKind.RoadWithLine:
                    roads += UpkeepOf(TileKind.Road);
                    lines += UpkeepOf(TileKind.PowerLine);
                    break;
                case TileKind.PowerPlant: plants += UpkeepOf(kind); break; // one plant per tile for now
                default: zones += UpkeepOf(kind); break; // zones; empty land is 0
            }
        }

        var r = BudgetRules;
        return new MonthlyBudget(
            Tax(Residents, r.TaxPerResident, ZoneKind.Residential),
            Tax(CommercialJobs, r.TaxPerCommercialJob, ZoneKind.Commercial),
            Tax(IndustrialJobs, r.TaxPerIndustrialJob, ZoneKind.Industrial),
            roads, lines, plants, zones);
    }

    /// <summary>Called at each month start, before demand; the first month start has no month behind it.</summary>
    private void SettleMonth()
    {
        if (Tick == 0) return;
        var budget = ProjectBudget();
        Funds += budget.Net;
        LastBudget = budget;
    }

    /// <summary>Budget state from a save; absent values keep their defaults.</summary>
    internal void RestoreBudget(long funds, TaxRates? taxes, MonthlyBudget? lastBudget)
    {
        Funds = funds;
        _taxes = taxes;
        LastBudget = lastBudget;
    }
}
