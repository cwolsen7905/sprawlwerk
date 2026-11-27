namespace SprawlWerk.Sim;

/// <summary>
/// Placeholder numbers for demand, growth and decline (ADR 0012, option C:
/// forgiving growth). Kept together so balance can change without code
/// changes; they move into the base pack's data with the growth pass.
/// <see cref="SpreadPenalty"/> and <see cref="CommutePenalty"/> are the levers
/// for a harsher setting and stay 0.
/// </summary>
public sealed record GrowthRules
{
    public static GrowthRules Default { get; } = new();

    /// <summary>Furthest a zone tile may be from a road and still grow, in steps.</summary>
    public int RoadAccess { get; init; } = 3;

    /// <summary>Residents an empty city wants before anyone works there.</summary>
    public int BaseResidents { get; init; } = 120;

    /// <summary>Industrial jobs wanted beyond what local workers fill (trade with the region, later).</summary>
    public int ExportJobs { get; init; } = 24;

    /// <summary>Workers per 100 residents.</summary>
    public int LabourShare { get; init; } = 50;

    /// <summary>Commercial jobs wanted per 100 residents.</summary>
    public int CommercialPer100 { get; init; } = 15;

    /// <summary>Industrial jobs wanted per 100 workers.</summary>
    public int IndustrialShare { get; init; } = 60;

    /// <summary>Smallest target used to scale demand, so a few missing jobs already register.</summary>
    public int MinScale { get; init; } = 20;

    /// <summary>Tax rate, in percent, at which taxes neither raise nor lower demand.</summary>
    public int NeutralTax { get; init; } = 7;

    /// <summary>Demand lost per tax point above neutral (gained per point below).</summary>
    public int TaxWeight { get; init; } = 50;

    /// <summary>Ticks between growth passes, and the tick within that cycle when one runs.</summary>
    public int GrowthEvery { get; init; } = 30;

    public int GrowthOffset { get; init; } = 15;

    /// <summary>Most buildings one zone grows in a pass, at full demand.</summary>
    public int MaxGrowPerPass { get; init; } = 4;

    /// <summary>Extra weight for a candidate per edge-adjacent tile holding a building.</summary>
    public int AdjacencyWeight { get; init; } = 2;

    /// <summary>Demand below which buildings of a zone start to decline.</summary>
    public int DeclineDemand { get; init; } = -250;

    /// <summary>Most buildings of one zone given a bad month, at demand -1000.</summary>
    public int MaxDeclinePerMonth { get; init; } = 4;

    /// <summary>Bad months (net) before a building is abandoned, and before it is cleared.</summary>
    public int AbandonAt { get; init; } = 3;

    public int ClearAt { get; init; } = 9;

    /// <summary>Harsher-setting levers; 0 under ADR 0012.</summary>
    public int SpreadPenalty { get; init; }

    public int CommutePenalty { get; init; }
}
