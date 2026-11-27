namespace SprawlWerk.Sim.Content;

/// <summary>What a type is for: land the player zones, a network that links tiles, or a building.</summary>
public enum TypeRole : byte
{
    Zone,
    Network,
    Building,
}

/// <summary>The three zones. A zone type names one; a building that grows on a zone names it too.</summary>
public enum ZoneKind : byte
{
    Residential,
    Commercial,
    Industrial,
}

/// <summary>Networks the simulation knows how to run.</summary>
public enum NetworkKind : byte
{
    Road,
    Power,
}

public enum Density : byte
{
    Low,
    Medium,
    High,
}

/// <summary>
/// A small number standing for one type in a loaded <see cref="ContentSet"/>.
/// Handles follow the set's id order, so the same packs always give the same
/// handles. 0 is no type (an empty tile).
/// </summary>
public readonly record struct TypeHandle(ushort Value)
{
    public static TypeHandle None => default;

    public bool IsNone => Value == 0;
}

/// <summary>
/// The simulation's half of one type from a content pack (<c>name.sim.json</c>):
/// numbers and rules only. Models and icons live in the art file, which the
/// simulation never reads.
/// </summary>
public sealed record BuildingType
{
    /// <summary>Namespaced id, <c>pack:name</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The pack that supplied this definition (the overriding pack, if overridden).</summary>
    public required string Pack { get; init; }

    public required TypeRole Role { get; init; }

    /// <summary>The zone a zone type is, or a building grows on; null otherwise.</summary>
    public ZoneKind? Zone { get; init; }

    /// <summary>The network a network type carries; null otherwise.</summary>
    public NetworkKind? Network { get; init; }

    public Density? Density { get; init; }

    public int Width { get; init; } = 1;

    public int Height { get; init; } = 1;

    /// <summary>Price to place: per tile for zones and networks, per building for buildings.</summary>
    public long Cost { get; init; }

    /// <summary>Monthly upkeep, on the same basis as <see cref="Cost"/>.</summary>
    public long Upkeep { get; init; }

    public int Residents { get; init; }

    public int Jobs { get; init; }

    /// <summary>Power the type supplies to its network.</summary>
    public int PowerSupply { get; init; }

    /// <summary>Power the type needs to count as powered.</summary>
    public int PowerUse { get; init; }

    /// <summary>Type to use when a save names this one and its pack is missing.</summary>
    public string? Fallback { get; init; }
}
