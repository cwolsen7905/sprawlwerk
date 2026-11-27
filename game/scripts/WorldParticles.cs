using System.Collections.Generic;
using Godot;

namespace SprawlWerk;

/// <summary>
/// Dust puffs and smoke for the 3D view, all made in code: a camera-facing quad
/// with a soft radial gradient, coloured and faded by a ramp. Puffs come from a
/// small pool of one-shot emitters (a burst bigger than the pool reuses the
/// oldest); smoke is one gentle looping emitter per source tile, started and
/// stopped as the sources change. Particles run on the GPU, and their
/// randomness is the engine's, never the simulation's.
/// </summary>
public partial class WorldParticles : Node3D
{
    private const int PuffVoices = 16;
    private const float PuffLifetime = 0.9f;
    private const float SmokeLifetime = 3.6f;

    private static readonly Color Dust = new(0.68f, 0.60f, 0.47f);
    private static readonly Color SmokeColor = new(0.78f, 0.78f, 0.80f);

    private readonly List<(GpuParticles3D Emitter, ParticleProcessMaterial Process)> _puffs = new();
    private readonly Dictionary<Vector2I, (GpuParticles3D Emitter, Vector3 At)> _smoke = new();
    private readonly Dictionary<float, ParticleProcessMaterial> _smokeBySize = new();
    private QuadMesh _dustQuad = null!;
    private QuadMesh _smokeQuad = null!;
    private int _nextPuff;

    public override void _Ready()
    {
        var soft = SoftDot();
        _dustQuad = new QuadMesh { Size = Vector2.One, Material = ParticleMaterial(soft) };
        _smokeQuad = new QuadMesh { Size = Vector2.One, Material = ParticleMaterial(soft) };
        for (int i = 0; i < PuffVoices; i++)
        {
            var process = PuffProcess();
            var puff = new GpuParticles3D
            {
                Emitting = false,
                OneShot = true,
                Amount = 18,
                Lifetime = PuffLifetime,
                Explosiveness = 0.92f,
                ProcessMaterial = process,
                DrawPass1 = _dustQuad,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityAabb = new Aabb(new Vector3(-2, -1, -2), new Vector3(4, 3, 4)),
            };
            AddChild(puff);
            _puffs.Add((puff, process));
        }
    }

    /// <summary>A burst of dust on the ground at <paramref name="at"/>, about <paramref name="size"/> tiles across.</summary>
    public void Puff(Vector3 at, float size)
    {
        // A free voice if there is one; else the next in turn, whose puff is nearly faded anyway.
        int pick = _nextPuff;
        for (int i = 0; i < _puffs.Count; i++)
        {
            int k = (_nextPuff + i) % _puffs.Count;
            if (!_puffs[k].Emitter.Emitting) { pick = k; break; }
        }
        _nextPuff = (pick + 1) % _puffs.Count;

        var (emitter, process) = _puffs[pick];
        emitter.Position = at;
        process.EmissionSphereRadius = 0.28f * size;
        process.InitialVelocityMin = 0.35f * size;
        process.InitialVelocityMax = 0.8f * size;
        process.ScaleMin = 0.3f * size;
        process.ScaleMax = 0.5f * size;
        emitter.Restart();
    }

    /// <summary>
    /// Smoke from exactly these tiles, each at its point. New sources start
    /// already flowing; gone ones stop emitting and let their last puffs drift off.
    /// </summary>
    public void SetSmoke(IReadOnlyDictionary<Vector2I, (Vector3 At, float Size)> sources)
    {
        var gone = new List<Vector2I>();
        foreach (var (tile, (emitter, at)) in _smoke)
            if (!sources.TryGetValue(tile, out var s) || s.At != at) { Retire(emitter); gone.Add(tile); }
        foreach (var tile in gone) _smoke.Remove(tile);

        foreach (var (tile, (at, size)) in sources)
        {
            if (_smoke.ContainsKey(tile)) continue;
            var emitter = new GpuParticles3D
            {
                Amount = 7,
                Lifetime = SmokeLifetime,
                Preprocess = SmokeLifetime,
                Randomness = 0.3f,
                ProcessMaterial = SmokeProcess(size),
                DrawPass1 = _smokeQuad,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityAabb = new Aabb(new Vector3(-2, -0.5f, -2), new Vector3(4, 4, 4)),
                Position = at,
            };
            AddChild(emitter);
            _smoke[tile] = (emitter, at);
        }
    }

    /// <summary>Drop all smoke at once (a different city).</summary>
    public void ClearSmoke()
    {
        foreach (var (emitter, _) in _smoke.Values) emitter.QueueFree();
        _smoke.Clear();
    }

    private void Retire(GpuParticles3D emitter)
    {
        emitter.Emitting = false;
        GetTree().CreateTimer(SmokeLifetime + 0.2).Timeout += emitter.QueueFree;
    }

    private static ParticleProcessMaterial PuffProcess() => new()
    {
        EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
        EmissionSphereRadius = 0.28f,
        Direction = Vector3.Up,
        Spread = 75f,
        Flatness = 0.6f,
        Gravity = new Vector3(0, -0.35f, 0),
        DampingMin = 1.4f,
        DampingMax = 2.2f,
        ScaleCurve = Ramp((0f, 0.5f), (0.35f, 1f), (1f, 1f)),
        ColorRamp = Fade(Dust, 0.9f),
    };

    // One process material per smoke size; emitters of a size share it.
    private ParticleProcessMaterial SmokeProcess(float size)
    {
        if (_smokeBySize.TryGetValue(size, out var cached)) return cached;
        var m = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.03f * size,
            Direction = Vector3.Up,
            Spread = 12f,
            InitialVelocityMin = 0.16f * size,
            InitialVelocityMax = 0.26f * size,
            // A light breeze, the same way everywhere.
            Gravity = new Vector3(0.05f, 0.03f, 0.02f),
            DampingMin = 0.02f,
            DampingMax = 0.05f,
            ScaleMin = 0.22f * size,
            ScaleMax = 0.32f * size,
            ScaleCurve = Ramp((0f, 0.35f), (1f, 1f)),
            ColorRamp = Fade(SmokeColor, 0.42f),
        };
        _smokeBySize[size] = m;
        return m;
    }

    // White in the middle fading to clear at the rim; tinted per particle by the colour ramp.
    private static GradientTexture2D SoftDot()
    {
        var g = new Gradient
        {
            Offsets = [0f, 0.45f, 1f],
            Colors = [new Color(1, 1, 1, 1), new Color(1, 1, 1, 0.55f), new Color(1, 1, 1, 0)],
        };
        return new GradientTexture2D
        {
            Gradient = g,
            Width = 64,
            Height = 64,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
        };
    }

    private static StandardMaterial3D ParticleMaterial(Texture2D texture) => new()
    {
        AlbedoTexture = texture,
        VertexColorUseAsAlbedo = true,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    private static GradientTexture1D Fade(Color c, float peak) => new()
    {
        Gradient = new Gradient
        {
            Offsets = [0f, 0.2f, 1f],
            Colors = [new Color(c, 0f), new Color(c, peak), new Color(c, 0f)],
        },
    };

    private static CurveTexture Ramp(params (float X, float Y)[] points)
    {
        var curve = new Curve();
        foreach (var (x, y) in points) curve.AddPoint(new Vector2(x, y));
        return new CurveTexture { Curve = curve };
    }
}
