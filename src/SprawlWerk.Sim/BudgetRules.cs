namespace SprawlWerk.Sim;

/// <summary>
/// Placeholder numbers for taxes (ADR 0012: sprawl costs money through upkeep;
/// upkeep itself is each type's <c>upkeep</c> in content). The neutral rate,
/// which is also every zone's starting rate, and how strongly taxes push
/// demand live in <see cref="GrowthRules"/>.
/// </summary>
public sealed record BudgetRules
{
    public static BudgetRules Default { get; } = new();

    /// <summary>Lowest and highest tax rate, in whole percent.</summary>
    public const int MinTax = 0;

    public const int MaxTax = 20;

    /// <summary>What a resident pays a month at the neutral rate.</summary>
    public int TaxPerResident { get; init; } = 1;

    /// <summary>What a commercial job pays a month at the neutral rate.</summary>
    public int TaxPerCommercialJob { get; init; } = 2;

    /// <summary>What an industrial job pays a month at the neutral rate.</summary>
    public int TaxPerIndustrialJob { get; init; } = 2;
}
