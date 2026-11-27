using System.Collections.Generic;
using Godot;
using SprawlWerk.Sim;

namespace SprawlWerk;

/// <summary>
/// The city in 3D: sky and sun, a grass ground with a faint tile grid, roads,
/// power lines and plants as low-poly models (chosen by <see cref="TileModels"/>),
/// and zones as flat lots tinted from <see cref="Palette"/> (dimmed without power).
/// Every copy of a model is one instance in a <see cref="MultiMeshInstance3D"/>,
/// so the whole city costs a few draw calls.
///
/// The map is rebuilt only when <see cref="CityController.MapVersion"/> moves,
/// never every frame. The pointer is turned into a tile by casting a ray from the
/// camera to the ground; the hovered tile and the drag preview are drawn as
/// translucent tiles (white, or red when the simulation would refuse).
/// Shown unless the 2D debug view is on (F3).
/// </summary>
public partial class CityWorld : Node3D
{
    private const float LotInset = 0.06f;
    private const float LotHeight = 0.015f;
    private const float PreviewHeight = 0.05f;

    private static readonly Color PreviewOk = new(1, 1, 1, 0.38f);
    private static readonly Color PreviewRefused = new(0.91f, 0.33f, 0.27f, 0.45f);

    private CityController _city = null!;
    private CameraRig _rig = null!;
    private Node3D _scenery = null!;
    private MeshInstance3D _ground = null!;
    private MultiMeshInstance3D _lots = null!;
    private MultiMeshInstance3D _preview = null!;
    private readonly Dictionary<string, Model> _models = new();
    private readonly Dictionary<(string Model, int Part), MultiMeshInstance3D> _instances = new();
    private int _builtVersion = -1;
    private Vector2I _groundSize;
    private Rect2I? _previewArea;
    private bool _previewRefused;

    /// <summary>A model file's meshes, with each one's transform inside the model and the model's bounds.</summary>
    private sealed record Model(List<(Mesh Mesh, Transform3D Local)> Parts, Aabb Bounds);

    public override void _Ready()
    {
        _city = GetNode<CityController>("../City");
        _rig = new CameraRig { PointerOverUi = _city.PointerOverUi };
        AddChild(_rig);
        _scenery = new Node3D { Name = "Scenery" };
        AddChild(_scenery);

        BuildEnvironment();
        _ground = new MeshInstance3D { Name = "Ground", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_ground);
        _lots = NewMultiMesh(LotMesh(), LotMaterial(), useColors: true, shadows: false);
        _preview = NewMultiMesh(LotMesh(), PreviewMaterial(), useColors: true, shadows: false);

        foreach (var path in TileModels.All) LoadModel(path);

        _city.CityReplaced += () => _rig.Frame(_city.Sim.Map.Width, _city.Sim.Map.Height);
        _rig.Frame(_city.Sim.Map.Width, _city.Sim.Map.Height);
    }

    public override void _Process(double delta)
    {
        bool shown = !_city.DebugView;
        if (Visible != shown)
        {
            Visible = shown;
            _rig.Camera.Current = shown;
        }
        _rig.Enabled = shown && _city.Active;
        if (!shown) return;

        if (_builtVersion != _city.MapVersion) Rebuild();

        if (_city.Active)
        {
            var mouse = GetViewport().GetMousePosition();
            _city.Hover = _city.PointerOverUi() ? null : TileAt(mouse);
        }
        else
        {
            _city.Hover = null;
        }
        UpdatePreview();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_city.DebugView || !_city.Active) return;
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mb) return;

        if (mb.Pressed)
        {
            if (!_city.PointerOverUi()) _city.BeginDrag();
        }
        else
        {
            _city.FinishDrag(TileAt(mb.Position));
        }
    }

    private Vector2I? TileAt(Vector2 screen)
    {
        if (_rig.GroundAt(screen) is not { } p) return null;
        int x = Mathf.FloorToInt(p.X), y = Mathf.FloorToInt(p.Z);
        return _city.Sim.Map.InBounds(x, y) ? new Vector2I(x, y) : null;
    }

    // --- building the city ---

    private void Rebuild()
    {
        _builtVersion = _city.MapVersion;
        var sim = _city.Sim;
        var map = sim.Map;

        BuildGround(map.Width, map.Height);

        var transforms = new Dictionary<string, List<Transform3D>>();
        var lots = new List<(Transform3D, Color)>();
        var placements = new List<ModelPlacement>();
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var tile = map[x, y];
            var centre = new Vector3(x + 0.5f, 0, y + 0.5f);
            if (TileModels.IsLot(tile.Kind))
            {
                var color = Palette.Lot(tile.Kind);
                if (!sim.Power.IsPowered(x, y)) color = Palette.Unpowered(color);
                lots.Add((new Transform3D(Basis.Identity, centre + new Vector3(0, LotHeight, 0)), color));
                continue;
            }

            placements.Clear();
            TileModels.For(map, x, y, placements);
            foreach (var p in placements)
            {
                if (!transforms.TryGetValue(p.Model, out var list)) transforms[p.Model] = list = new List<Transform3D>();
                list.Add(Placed(p, centre));
            }
        }

        // Models no longer on the map keep their (emptied) instance nodes for next time.
        foreach (var (key, mmi) in _instances)
            if (!transforms.ContainsKey(key.Model)) mmi.Multimesh.InstanceCount = 0;

        foreach (var (path, list) in transforms)
        {
            if (LoadModel(path) is not { } model) continue;
            for (int part = 0; part < model.Parts.Count; part++)
            {
                if (!_instances.TryGetValue((path, part), out var mmi))
                {
                    mmi = NewMultiMesh(model.Parts[part].Mesh, null, useColors: false, shadows: true);
                    _instances[(path, part)] = mmi;
                }
                var mm = mmi.Multimesh;
                mm.InstanceCount = list.Count;
                for (int i = 0; i < list.Count; i++) mm.SetInstanceTransform(i, list[i] * model.Parts[part].Local);
            }
        }

        _lots.Multimesh.InstanceCount = lots.Count;
        for (int i = 0; i < lots.Count; i++)
        {
            _lots.Multimesh.SetInstanceTransform(i, lots[i].Item1);
            _lots.Multimesh.SetInstanceColor(i, lots[i].Item2);
        }

        _previewArea = null; // quotes may have changed with the map
    }

    // Model space to world space for one placement on a tile.
    private Transform3D Placed(ModelPlacement p, Vector3 centre)
    {
        float scale = p.Scale;
        var shift = Vector3.Zero;
        if (p.Fit > 0 && LoadModel(p.Model) is { } model)
        {
            var b = model.Bounds;
            float span = Mathf.Max(b.Size.X, b.Size.Z);
            if (span > p.Fit) scale = p.Fit / span;
            var c = b.GetCenter();
            shift = new Vector3(-c.X, -b.Position.Y, -c.Z);
        }
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(p.Rotation)) * Basis.FromScale(new Vector3(scale * p.Stretch, scale, scale));
        return new Transform3D(basis, centre + p.Offset) * new Transform3D(Basis.Identity, shift);
    }

    private Model? LoadModel(string path)
    {
        if (_models.TryGetValue(path, out var cached)) return cached;
        if (GD.Load<PackedScene>(path) is not { } scene)
        {
            GD.PushWarning($"Missing model {path}");
            return null;
        }

        var root = scene.Instantiate<Node3D>();
        var parts = new List<(Mesh, Transform3D)>();
        var bounds = new Aabb();
        bool first = true;
        Collect(root, Transform3D.Identity, isRoot: true);
        root.Free();

        void Collect(Node node, Transform3D parent, bool isRoot)
        {
            var xf = parent;
            if (node is Node3D n3 && !isRoot) xf = parent * n3.Transform;
            if (node is MeshInstance3D { Mesh: { } mesh })
            {
                parts.Add((mesh, xf));
                var box = xf * mesh.GetAabb();
                bounds = first ? box : bounds.Merge(box);
                first = false;
            }
            foreach (var child in node.GetChildren()) Collect(child, xf, isRoot: false);
        }

        var model = new Model(parts, bounds);
        _models[path] = model;
        return model;
    }

    private MultiMeshInstance3D NewMultiMesh(Mesh mesh, Material? material, bool useColors, bool shadows)
    {
        var mmi = new MultiMeshInstance3D
        {
            Multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = useColors,
                Mesh = mesh,
            },
            MaterialOverride = material,
            CastShadow = shadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _scenery.AddChild(mmi);
        return mmi;
    }

    // --- hover and drag preview ---

    private void UpdatePreview()
    {
        var command = _city.PreviewCommand();
        var area = CityController.AreaOf(command);
        bool refused = command is not null && !_city.Sim.Quote(command).Accepted;
        if (area == _previewArea && refused == _previewRefused) return;
        _previewArea = area;
        _previewRefused = refused;

        var mm = _preview.Multimesh;
        if (area is not { } r) { mm.InstanceCount = 0; return; }

        var map = _city.Sim.Map;
        var cells = new List<Vector2I>();
        for (int y = r.Position.Y; y < r.End.Y; y++)
        for (int x = r.Position.X; x < r.End.X; x++)
            if (map.InBounds(x, y)) cells.Add(new Vector2I(x, y));

        mm.InstanceCount = cells.Count;
        var color = refused ? PreviewRefused : PreviewOk;
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3(c.X + 0.5f, PreviewHeight, c.Y + 0.5f)));
            mm.SetInstanceColor(i, color);
        }
    }

    // --- materials and scenery ---

    private static PlaneMesh LotMesh() => new() { Size = new Vector2(1f - LotInset * 2, 1f - LotInset * 2) };

    private static StandardMaterial3D LotMaterial() => new()
    {
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        Roughness = 0.95f,
    };

    private static StandardMaterial3D PreviewMaterial() => new()
    {
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        NoDepthTest = true,
        RenderPriority = 1,
    };

    private const string GroundShader = """
        shader_type spatial;
        render_mode diffuse_lambert, specular_disabled;
        uniform vec3 grass : source_color;
        uniform vec3 outside : source_color;
        uniform vec3 line_color : source_color;
        uniform vec2 map_size;
        varying vec3 world_pos;
        void vertex() { world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
        void fragment() {
            vec2 p = world_pos.xz;
            vec2 d = abs(fract(p - 0.5) - 0.5) / fwidth(p);
            float line = 1.0 - min(min(d.x, d.y), 1.0);
            float inside = step(0.0, p.x) * step(0.0, p.y) * step(p.x, map_size.x) * step(p.y, map_size.y);
            vec3 base = mix(outside, grass, inside);
            ALBEDO = mix(base, line_color, line * 0.35 * inside);
            ROUGHNESS = 1.0;
        }
        """;

    private void BuildGround(int width, int height)
    {
        if (_groundSize == new Vector2I(width, height)) return;
        _groundSize = new Vector2I(width, height);
        const float margin = 40f;
        _ground.Mesh = new PlaneMesh { Size = new Vector2(width + margin * 2, height + margin * 2) };
        _ground.Position = new Vector3(width / 2f, 0, height / 2f);
        var material = new ShaderMaterial { Shader = new Shader { Code = GroundShader } };
        material.SetShaderParameter("grass", Palette.Grass);
        material.SetShaderParameter("outside", Palette.GrassOutside);
        material.SetShaderParameter("line_color", Palette.GridLine);
        material.SetShaderParameter("map_size", new Vector2(width, height));
        _ground.MaterialOverride = material;
    }

    private void BuildEnvironment()
    {
        var sky = new Sky
        {
            SkyMaterial = new ProceduralSkyMaterial
            {
                SkyTopColor = Palette.SkyTop,
                SkyHorizonColor = Palette.SkyHorizon,
                GroundHorizonColor = Palette.SkyHorizon,
            },
        };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = sky,
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.45f,
            TonemapMode = Godot.Environment.ToneMapper.Aces,
            TonemapExposure = 1.0f,
            AdjustmentEnabled = true,
            AdjustmentSaturation = 1.05f,
            AdjustmentContrast = 1.08f,
            SsaoEnabled = true,
            SsaoRadius = 0.6f,
            SsaoIntensity = 1.6f,
            FogEnabled = true,
            FogLightColor = Palette.SkyHorizon,
            FogDensity = 0.0015f,
        };
        AddChild(new WorldEnvironment { Environment = env });

        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50, -35, 0),
            LightEnergy = 1.25f,
            LightColor = Palette.Sunlight,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 80f,
            ShadowBlur = 1.5f,
        };
        AddChild(sun);
    }
}
