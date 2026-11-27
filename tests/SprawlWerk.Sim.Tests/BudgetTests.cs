using SprawlWerk.Sim.Content;

namespace SprawlWerk.Sim.Tests;

public class BudgetTests
{
    // Same layout as GrowthTests: a road along the top, a plant, then R, C and I zones.
    private static Simulation Town(ContentSet? content = null, ulong seed = 1)
    {
        var sim = new Simulation(12, 3, startingFunds: 1_000_000, content: content ?? TestContent.Load(), seed: seed);
        sim.Enqueue(new PlaceCommand(0, 0, 12, 1, TileKind.Road));
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 1, 4, 2, TileKind.Residential));
        sim.Enqueue(new PlaceCommand(5, 1, 3, 2, TileKind.Commercial));
        sim.Enqueue(new PlaceCommand(8, 1, 4, 2, TileKind.Industrial));
        return sim;
    }

    private static void RunTo(Simulation sim, long tick)
    {
        while (sim.Tick < tick) sim.Step();
    }

    // SPW-C-ECON-01: income is the sum of the taxes, and funds change by income minus expenses.
    [Fact]
    public void At_a_month_start_funds_change_by_taxes_minus_upkeep()
    {
        var sim = Town();
        sim.Enqueue(new SetTaxCommand(ZoneKind.Residential, 10));
        sim.Enqueue(new SetTaxCommand(ZoneKind.Commercial, 5));
        sim.Enqueue(new SetTaxCommand(ZoneKind.Industrial, 14));
        RunTo(sim, 3 * CityDate.TicksPerMonth); // the next step is a month start
        int residents = sim.Residents, commercial = sim.CommercialJobs, industrial = sim.IndustrialJobs;
        Assert.True(residents > 0 && commercial > 0 && industrial > 0);
        long before = sim.Funds;

        sim.Step();

        long rTax = residents * 1L * 10 / 7, cTax = commercial * 2L * 5 / 7, iTax = industrial * 2L * 14 / 7;
        long upkeep = 12 * 1 + 100; // 12 road tiles at 1, one plant at 100
        var budget = Assert.NotNull(sim.LastBudget);
        Assert.Equal((rTax, cTax, iTax), (budget.ResidentialTax, budget.CommercialTax, budget.IndustrialTax));
        Assert.Equal(rTax + cTax + iTax, budget.Income);
        Assert.Equal((12L, 0L, 100L), (budget.RoadUpkeep, budget.PowerLineUpkeep, budget.PlantUpkeep));
        Assert.Equal(upkeep, budget.Expenses);
        Assert.Equal(before + rTax + cTax + iTax - upkeep, sim.Funds);
    }

    [Fact]
    public void At_the_neutral_rate_each_unit_pays_its_tax_per_unit()
    {
        var sim = Town();
        RunTo(sim, 2 * CityDate.TicksPerMonth);
        int residents = sim.Residents, industrial = sim.IndustrialJobs;
        sim.Step();
        var budget = Assert.NotNull(sim.LastBudget);
        Assert.Equal(residents, budget.ResidentialTax);
        Assert.Equal(2L * industrial, budget.IndustrialTax);
    }

    [Fact]
    public void Nothing_is_settled_before_the_first_month_has_passed()
    {
        var sim = Town();
        sim.Step(); // tick 0
        Assert.Null(sim.LastBudget);
        long funds = sim.Funds;
        RunTo(sim, CityDate.TicksPerMonth);
        Assert.Equal(funds, sim.Funds);
        sim.Step();
        Assert.NotNull(sim.LastBudget);
    }

    // SPW-C-ECON-02: upkeep is per tile, so twice the road costs twice as much.
    [Fact]
    public void Twenty_road_tiles_cost_twice_the_upkeep_of_ten()
    {
        long RoadUpkeep(int tiles)
        {
            var sim = new Simulation(30, 3, content: TestContent.Load());
            sim.Enqueue(new PlaceCommand(0, 0, tiles, 1, TileKind.Road));
            RunTo(sim, CityDate.TicksPerMonth + 1);
            return Assert.NotNull(sim.LastBudget).RoadUpkeep;
        }
        Assert.Equal(10, RoadUpkeep(10));
        Assert.Equal(2 * RoadUpkeep(10), RoadUpkeep(20));
    }

    [Fact]
    public void A_road_carrying_a_line_pays_for_both_and_upkeep_comes_from_content()
    {
        var content = TestContent.With("road.sim.json", "upkeep", 3);
        var sim = new Simulation(10, 3, content: content);
        sim.Enqueue(new PlaceCommand(0, 0, 4, 1, TileKind.Road));
        sim.Enqueue(new PlaceCommand(2, 0, 1, 3, TileKind.PowerLine)); // crosses at (2, 0)
        RunTo(sim, CityDate.TicksPerMonth + 1);
        var budget = Assert.NotNull(sim.LastBudget);
        Assert.Equal(4 * 3, budget.RoadUpkeep);
        Assert.Equal(3 * 1, budget.PowerLineUpkeep);
        Assert.Equal(3 + 1, sim.UpkeepOf(TileKind.RoadWithLine));
    }

    [Fact]
    public void Without_content_upkeep_is_zero()
    {
        var sim = new Simulation(10, 3, startingFunds: 5_000);
        sim.Enqueue(new PlaceCommand(0, 0, 10, 1, TileKind.Road));
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(1, 1, 5, 1, TileKind.PowerLine));
        sim.Step();
        long funds = sim.Funds;
        RunTo(sim, CityDate.TicksPerMonth + 1);
        Assert.Equal(0, Assert.NotNull(sim.LastBudget).Expenses);
        Assert.Equal(funds, sim.Funds);
    }

    [Fact]
    public void Upkeep_can_take_the_city_into_debt()
    {
        var sim = new Simulation(10, 3, startingFunds: 4_000 + 15 * 10, content: TestContent.Load());
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        sim.Enqueue(new PlaceCommand(0, 0, 10, 1, TileKind.Road));
        RunTo(sim, CityDate.TicksPerMonth + 1);
        Assert.Equal(-110, sim.Funds);

        // In debt, a command with a price is refused, a free one still goes through.
        sim.Enqueue(new PlaceCommand(5, 2, TileKind.Road));
        sim.Enqueue(new SetTaxCommand(ZoneKind.Residential, 9));
        sim.Step();
        Assert.Equal(Refusal.InsufficientFunds, sim.LastOutcomes[0].Result.Refusal);
        Assert.True(sim.LastOutcomes[1].Result.Accepted);
        Assert.Equal(9, sim.Taxes.Residential);
    }

    [Fact]
    public void Every_zone_starts_at_the_neutral_rate()
    {
        var sim = new Simulation(4, 4);
        Assert.Equal(TaxRates.All(GrowthRules.Default.NeutralTax), sim.Taxes);
        Assert.Equal(7, sim.Taxes.Commercial);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    public void A_tax_rate_in_range_is_accepted_free_and_changes_nothing_else(int rate)
    {
        var sim = new Simulation(4, 4, startingFunds: 100);
        sim.Step();
        long revision = sim.MapRevision;
        sim.Enqueue(new SetTaxCommand(ZoneKind.Industrial, rate));
        sim.Step();
        var result = sim.LastOutcomes.Single().Result;
        Assert.True(result.Accepted);
        Assert.Equal(0, result.Cost);
        Assert.Equal(100, sim.Funds);
        Assert.Equal(new TaxRates(7, 7, rate), sim.Taxes);
        Assert.Equal(revision, sim.MapRevision);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public void A_tax_rate_out_of_range_is_refused(int rate)
    {
        var sim = new Simulation(4, 4);
        Assert.Equal(Refusal.OutOfRange, sim.Quote(new SetTaxCommand(ZoneKind.Residential, rate)).Refusal);
        sim.Enqueue(new SetTaxCommand(ZoneKind.Residential, rate));
        sim.Step();
        Assert.Equal(Refusal.OutOfRange, sim.LastOutcomes.Single().Result.Refusal);
        Assert.Equal(7, sim.Taxes.Residential);
    }

    [Fact]
    public void Higher_tax_lowers_demand_and_lower_tax_raises_it()
    {
        Demand DemandWith(int rate)
        {
            var sim = Town();
            sim.Enqueue(new SetTaxCommand(ZoneKind.Residential, rate));
            sim.Enqueue(new SetTaxCommand(ZoneKind.Commercial, rate));
            sim.Step(); // tick 0 is a month start
            return sim.Demand;
        }
        // At neutral: R 1000 and C 0 (GrowthTests); each point is TaxWeight (50).
        Assert.Equal(new Demand(1000, 0, 1000), DemandWith(7));
        Assert.Equal(new Demand(1000 - 13 * 50, -13 * 50, 1000), DemandWith(20));
        Assert.Equal(new Demand(1000, 7 * 50, 1000), DemandWith(0)); // R stays clamped at 1000
    }

    [Fact]
    public void A_high_tax_slows_growth()
    {
        int ResidentsAfter(int rate)
        {
            var sim = Town();
            sim.Enqueue(new SetTaxCommand(ZoneKind.Residential, rate));
            RunTo(sim, 3 * CityDate.TicksPerMonth);
            return sim.Residents;
        }
        Assert.True(ResidentsAfter(20) < ResidentsAfter(7));
    }

    [Fact]
    public void Taxes_and_the_last_budget_survive_save_and_load()
    {
        var content = TestContent.Load();
        var sim = Town(content);
        sim.Enqueue(new SetTaxCommand(ZoneKind.Residential, 12));
        sim.Enqueue(new SetTaxCommand(ZoneKind.Industrial, 3));
        RunTo(sim, 2 * CityDate.TicksPerMonth + 5);

        var loaded = SaveGame.Deserialize(SaveGame.Serialize(sim), content: content);
        Assert.Equal(new TaxRates(12, 7, 3), loaded.Taxes);
        Assert.Equal(sim.LastBudget, loaded.LastBudget);
        Assert.Equal(sim.Funds, loaded.Funds);
    }

    [Fact]
    public void A_loaded_city_keeps_its_budget_exactly_as_the_saved_one_would_have()
    {
        var content = TestContent.Load();
        var original = Town(content);
        original.Enqueue(new SetTaxCommand(ZoneKind.Commercial, 15));
        RunTo(original, CityDate.TicksPerMonth + 40);
        var loaded = SaveGame.Deserialize(SaveGame.Serialize(original), content: content);

        foreach (var sim in new[] { original, loaded })
        {
            sim.Enqueue(new SetTaxCommand(ZoneKind.Residential, 2));
            RunTo(sim, 6 * CityDate.TicksPerMonth + 3);
        }
        Assert.Equal(original.Funds, loaded.Funds);
        Assert.Equal(original.LastBudget, loaded.LastBudget);
        Assert.Equal(original.Taxes, loaded.Taxes);
        Assert.Equal(original.Demand, loaded.Demand);
        Assert.Equal(original.Residents, loaded.Residents);
    }

    [Fact]
    public void A_city_in_debt_saves_and_loads()
    {
        var sim = new Simulation(10, 3, startingFunds: 4_000, content: TestContent.Load());
        sim.Enqueue(new PlaceCommand(0, 1, TileKind.PowerPlant));
        RunTo(sim, CityDate.TicksPerMonth + 1);
        Assert.Equal(-100, sim.Funds);
        Assert.Equal(-100, SaveGame.Deserialize(SaveGame.Serialize(sim), content: TestContent.Load()).Funds);
    }

    [Fact]
    public void An_older_save_without_budget_fields_loads_with_defaults()
    {
        const string json = "{\"Version\":1,\"Width\":2,\"Height\":1,\"Tick\":0,\"Funds\":10,\"Kinds\":[\"..\"],\"Heights\":[[0,0]]}";
        var sim = SaveGame.Deserialize(json);
        Assert.Equal(TaxRates.All(7), sim.Taxes);
        Assert.Null(sim.LastBudget);
        Assert.Equal(10, sim.Funds);
    }

    [Theory]
    [InlineData("[7,7]")]
    [InlineData("[7,7,21]")]
    [InlineData("[-1,7,7]")]
    public void A_save_with_bad_tax_rates_is_refused(string taxes)
    {
        string json = "{\"Version\":1,\"Width\":2,\"Height\":1,\"Tick\":0,\"Funds\":10,\"Kinds\":[\"..\"],\"Heights\":[[0,0]],\"Taxes\":" + taxes + "}";
        Assert.Throws<SaveFormatException>(() => SaveGame.Deserialize(json));
    }
}
