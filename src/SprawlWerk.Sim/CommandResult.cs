namespace SprawlWerk.Sim;

/// <summary>Why the simulation turned a command down.</summary>
public enum Refusal : byte
{
    /// <summary>Not refused.</summary>
    None,

    /// <summary>No tile of the command is on the map.</summary>
    OutOfBounds,

    /// <summary>Every tile is already what the command would make it.</summary>
    NothingToChange,

    /// <summary>The city cannot pay the whole cost.</summary>
    InsufficientFunds,

    /// <summary>A setting outside its allowed range (a tax rate above the maximum).</summary>
    OutOfRange,
}

/// <summary>
/// What a command did, or would do: accepted with a cost and the number of
/// tiles it changes, or refused with a reason. A refused command changes
/// nothing and costs nothing.
/// </summary>
public readonly record struct CommandResult(Refusal Refusal, long Cost, int TilesChanged)
{
    public bool Accepted => Refusal == Refusal.None;

    public static CommandResult Accept(long cost, int tilesChanged) => new(Refusal.None, cost, tilesChanged);

    /// <param name="cost">What it would have cost, when known (for "needs $X").</param>
    public static CommandResult Refuse(Refusal reason, long cost = 0) => new(reason, cost, 0);
}

/// <summary>A command and what happened to it in a tick.</summary>
public readonly record struct CommandOutcome(ICommand Command, CommandResult Result);
