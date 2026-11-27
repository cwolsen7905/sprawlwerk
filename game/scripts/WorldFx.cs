using System.Collections.Generic;
using Godot;

namespace SprawlWerk;

/// <summary>
/// Settings and timing for the 3D view's motion: buildings rising and sinking,
/// placed tiles popping in, dust puffs and smoke. All of it is view-side only:
/// it reads what the simulation already decided and never feeds anything back,
/// and it takes no randomness from the simulation.
///
/// <see cref="ReduceMotion"/> is the one switch a settings menu can flip: with
/// it on, changes appear at once with no rise, pop or puff. Developer flag:
/// <c>-- --reduce-motion</c>. Smoke has its own switch (<see cref="Smoke"/>)
/// because it also tells the player which industry is working.
/// </summary>
public static class WorldFx
{
    /// <summary>Skip rises, sinks, pops and dust puffs; changes appear instantly.</summary>
    public static bool ReduceMotion { get; set; } = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--reduce-motion") >= 0;

    /// <summary>Chimneys and working industry give off smoke.</summary>
    public static bool Smoke { get; set; } = true;

    public const float RiseSeconds = 0.55f;
    public const float SinkSeconds = 0.45f;
    public const float PopSeconds = 0.3f;

    /// <summary>At most this many tiles move at once; further changes in a burst just appear.</summary>
    public const int MaxAnimations = 96;

    /// <summary>Changes arriving together start this far apart, so a burst ripples instead of jumping...</summary>
    public const float StaggerStep = 0.035f;

    /// <summary>...but the whole burst always starts within this long.</summary>
    public const float MaxStagger = 0.6f;

    /// <summary>Overshoot past 1 then settle (Penner's back ease-out); <paramref name="k"/> sets how far.</summary>
    public static float EaseOutBack(float t, float k = 1.70158f)
    {
        float u = t - 1f;
        return 1f + (k + 1f) * u * u * u + k * u * u;
    }

    public static float EaseInCubic(float t) => t * t * t;
}

/// <summary>How a tile's models move while they change.</summary>
public enum Motion
{
    /// <summary>A building or plant grows up out of its lot.</summary>
    Rise,
    /// <summary>A removed building or plant shrinks back into the ground (drawn from a ghost copy).</summary>
    Sink,
    /// <summary>A road, power line or zone lot laid by the player bounces in.</summary>
    Pop,
}

/// <summary>
/// One tile's change in motion. <see cref="Slots"/> are the MultiMesh instances
/// it moves, with their resting transforms; the view fills them in each time it
/// rebuilds its MultiMeshes, then the animation only rewrites those few instances.
/// </summary>
public sealed class TileAnimation
{
    public required Vector2I Tile { get; init; }
    public required Motion Motion { get; init; }
    public double Start { get; set; }
    public required Vector3 Pivot { get; init; }

    /// <summary>The models of a removed building, drawn only while it sinks.</summary>
    public List<(string Model, Transform3D Xf)>? Ghost { get; init; }

    /// <summary>Dust at the base when it starts, this wide (0 for none).</summary>
    public float PuffSize { get; init; }

    /// <summary>A pop also moves the tile's zone lot.</summary>
    public bool IncludesLot { get; init; }

    public bool Puffed { get; set; }

    public List<(MultiMesh Mesh, int Index, Transform3D Resting)> Slots { get; } = new();

    public float Duration => Motion switch
    {
        Motion.Rise => WorldFx.RiseSeconds,
        Motion.Sink => WorldFx.SinkSeconds,
        _ => WorldFx.PopSeconds,
    };

    public bool Started(double now) => now >= Start;

    public bool Done(double now) => now >= Start + Duration;

    /// <summary>The motion at time <paramref name="now"/>, applied on top of a resting transform.</summary>
    public Transform3D Offset(double now)
    {
        float t = Mathf.Clamp((float)((now - Start) / Duration), 0f, 1f);
        Vector3 scale;
        switch (Motion)
        {
            case Motion.Rise:
            {
                if (!Started(now)) { scale = new Vector3(0.001f, 0.001f, 0.001f); break; }
                float up = WorldFx.EaseOutBack(t, 1.3f);
                float wide = 0.7f + 0.3f * WorldFx.EaseOutBack(Mathf.Min(1f, t * 1.4f), 1.0f);
                scale = new Vector3(wide, Mathf.Max(up, 0.001f), wide);
                break;
            }
            case Motion.Sink:
            {
                float down = 1f - WorldFx.EaseInCubic(t);
                float wide = 1f - 0.25f * t;
                scale = new Vector3(wide, Mathf.Max(down, 0.001f), wide);
                break;
            }
            default:
            {
                if (!Started(now)) { scale = new Vector3(0.001f, 0.001f, 0.001f); break; }
                float s = 0.55f + 0.45f * WorldFx.EaseOutBack(t, 2.2f);
                scale = new Vector3(s, s, s);
                break;
            }
        }
        // Scale about the tile's base point, so things grow from the ground up.
        return new Transform3D(Basis.Identity, Pivot)
             * new Transform3D(Basis.FromScale(scale), Vector3.Zero)
             * new Transform3D(Basis.Identity, -Pivot);
    }

    /// <summary>Write the current motion into every slot.</summary>
    public void Apply(double now)
    {
        var offset = Offset(now);
        foreach (var (mesh, index, resting) in Slots)
            if (index < mesh.InstanceCount) mesh.SetInstanceTransform(index, offset * resting);
    }

    /// <summary>Put every slot back at rest.</summary>
    public void Settle()
    {
        foreach (var (mesh, index, resting) in Slots)
            if (index < mesh.InstanceCount) mesh.SetInstanceTransform(index, resting);
    }
}
