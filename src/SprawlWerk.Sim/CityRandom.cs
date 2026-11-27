namespace SprawlWerk.Sim;

/// <summary>
/// The simulation's only source of chance: SplitMix64, written here so the
/// sequence is the same on every platform and .NET version (System.Random
/// promises neither). Seeded when a city is founded; its state is saved, so a
/// loaded city draws exactly what the saved one would have.
/// </summary>
public sealed class CityRandom
{
    /// <summary>Seed for a new city until new-city setup lets the player choose one.</summary>
    public const ulong DefaultSeed = 0x5350_5241_574C_0001; // "SPRAWL" and a 1

    public CityRandom(ulong seed = DefaultSeed) => State = seed;

    /// <summary>The whole state; setting it resumes a sequence exactly.</summary>
    public ulong State { get; set; }

    public ulong NextULong()
    {
        ulong z = State += 0x9E37_79B9_7F4A_7C15;
        z = (z ^ (z >> 30)) * 0xBF58_476D_1CE4_E5B9;
        z = (z ^ (z >> 27)) * 0x94D0_49BB_1331_11EB;
        return z ^ (z >> 31);
    }

    /// <summary>A whole number from 0 up to, not including, <paramref name="bound"/>, without modulo bias.</summary>
    public int Next(int bound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);
        ulong b = (ulong)bound;
        ulong limit = ulong.MaxValue - ulong.MaxValue % b; // values at or above this would skew the result
        ulong value;
        do value = NextULong(); while (value >= limit);
        return (int)(value % b);
    }
}
