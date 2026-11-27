namespace SprawlWerk.Sim;

/// <summary>
/// The rules for autosave and for what "Continue" loads. The game decides
/// when to look and does the file work; the choices live here so they can be
/// tested: autosave once per in-game month, into one slot that is overwritten,
/// and Continue opens the newest readable save of any kind.
/// </summary>
public static class Autosave
{
    /// <summary>The one autosave slot.</summary>
    public const string FileName = "autosave" + SaveNames.Extension;

    private const long TicksPerMonth = (long)CityDate.TicksPerDay * CityDate.DaysPerMonth;

    /// <summary>True if a month boundary lies between the two ticks (the later one inclusive).</summary>
    public static bool IsDue(long previousTick, long currentTick) =>
        currentTick > previousTick && currentTick / TicksPerMonth != previousTick / TicksPerMonth;

    /// <summary>A file in the saves folder, as far as Continue cares.</summary>
    public readonly record struct Candidate(string FileName, long Modified, bool Readable);

    /// <summary>
    /// The file Continue should open: the most recently modified readable save,
    /// whoever wrote it. Damaged or newer-version files are skipped. Ties go to
    /// the name that sorts first so the answer never depends on listing order.
    /// Null when there is nothing to continue.
    /// </summary>
    public static string? ContinueTarget(IEnumerable<Candidate> candidates)
    {
        Candidate? best = null;
        foreach (var c in candidates)
        {
            if (!c.Readable) continue;
            if (best is not { } b
                || c.Modified > b.Modified
                || (c.Modified == b.Modified && string.CompareOrdinal(c.FileName, b.FileName) < 0))
                best = c;
        }
        return best?.FileName;
    }
}
