namespace SprawlWerk.Sim;

/// <summary>
/// Decline (ADR 0012): each building carries stress, stored and saved. At each
/// month start a building without power or road access has a bad month, and
/// when a zone's demand is low enough some of its buildings are drawn to have
/// one too. A bad month adds a point, a good month takes one away. At
/// <see cref="GrowthRules.AbandonAt"/> a building is abandoned (it keeps its lot
/// and power use but houses and employs no one); back at 0 it is lived in again;
/// at <see cref="GrowthRules.ClearAt"/> it is cleared and the zone may grow anew.
/// </summary>
public sealed partial class Simulation
{
    private byte[] _stress = [];
    private bool[] _abandoned = [];

    private void EnsureDeclineArrays()
    {
        int n = Map.Width * Map.Height;
        if (_stress.Length == n) return;
        _stress = new byte[n];
        _abandoned = new bool[n];
    }

    /// <summary>Bad months a building has piled up (0 when well).</summary>
    public int Stress(int x, int y)
    {
        EnsureDeclineArrays();
        return Map.InBounds(x, y) && !Map[x, y].Building.IsNone ? _stress[y * Map.Width + x] : 0;
    }

    /// <summary>True for a building nobody lives or works in any more.</summary>
    public bool IsAbandoned(int x, int y)
    {
        EnsureDeclineArrays();
        return Map.InBounds(x, y) && !Map[x, y].Building.IsNone && _abandoned[y * Map.Width + x];
    }

    /// <summary>A building has just grown here: it starts well.</summary>
    private void ResetStress(int index)
    {
        EnsureDeclineArrays();
        _stress[index] = 0;
        _abandoned[index] = false;
    }

    /// <summary>Restore one building's stress from a save.</summary>
    internal void RestoreStress(int x, int y, int stress, bool abandoned)
    {
        EnsureDeclineArrays();
        int i = y * Map.Width + x;
        _stress[i] = (byte)Math.Clamp(stress, 0, Growth.ClearAt);
        _abandoned[i] = abandoned;
    }

    /// <summary>Buildings with stress or abandoned, in index order, for the save.</summary>
    internal IEnumerable<(int X, int Y, int Stress, bool Abandoned)> StressedBuildings()
    {
        EnsureDeclineArrays();
        for (int i = 0; i < _stress.Length; i++)
        {
            int x = i % Map.Width, y = i / Map.Width;
            if (Map[x, y].Building.IsNone || (_stress[i] == 0 && !_abandoned[i])) continue;
            yield return (x, y, _stress[i], _abandoned[i]);
        }
    }

    /// <summary>One month of decline: whether a building was cleared, and whether any building changed how it looks.</summary>
    private (bool Cleared, bool Changed) DeclinePass()
    {
        EnsureDeclineArrays();
        var g = Growth;
        int n = _stress.Length;
        var bad = new bool[n];

        // Buildings cut off from power or road.
        for (int i = 0; i < n; i++)
        {
            int x = i % Map.Width, y = i / Map.Width;
            if (Map[x, y].Building.IsNone)
            {
                _stress[i] = 0; // tiles rezoned or cleared since last month
                _abandoned[i] = false;
                continue;
            }
            bad[i] = !Power.IsPowered(x, y) || !Roads.HasAccess(x, y);
        }

        // A shrinking market: some of a zone's buildings, drawn in index order.
        foreach (var zone in Zones)
        {
            int demand = Demand.For(zone);
            if (demand >= g.DeclineDemand) continue;
            var kind = KindOf(zone);
            var pool = new List<int>();
            for (int i = 0; i < n; i++)
                if (!bad[i] && Map[i % Map.Width, i / Map.Width] is { Building.IsNone: false } t && t.Kind == kind) pool.Add(i);
            int count = (int)Math.Min(pool.Count, ((long)-demand * g.MaxDeclinePerMonth + 999) / 1000);
            for (int k = 0; k < count; k++)
            {
                int pick = Random.Next(pool.Count);
                bad[pool[pick]] = true;
                pool.RemoveAt(pick);
            }
        }

        bool cleared = false, changed = false;
        for (int i = 0; i < n; i++)
        {
            ref var tile = ref Map[i % Map.Width, i / Map.Width];
            if (tile.Building.IsNone) continue;
            bool wasAbandoned = _abandoned[i];
            if (bad[i]) _stress[i] = (byte)Math.Min(_stress[i] + 1, g.ClearAt);
            else if (_stress[i] > 0) _stress[i]--;

            if (_stress[i] >= g.ClearAt)
            {
                tile.Building = default;
                _stress[i] = 0;
                _abandoned[i] = false;
                cleared = true;
            }
            else if (_stress[i] >= g.AbandonAt) _abandoned[i] = true;
            else if (_stress[i] == 0) _abandoned[i] = false;
            changed |= wasAbandoned != _abandoned[i];
        }
        return (cleared, changed || cleared);
    }
}
