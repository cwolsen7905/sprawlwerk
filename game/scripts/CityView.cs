using Godot;
using SprawlWerk.Sim;

namespace SprawlWerk;

/// <summary>
/// The 2D debug view, shown instead of the 3D world while F3 is on: the map as
/// flat isometric diamonds (immediate-mode drawing, no art), so rules can be
/// checked tile by tile. Everything that is not drawing (time, tools, drags,
/// saves) is in <see cref="CityController"/>; this view only draws it and
/// reports the tile under the pointer (and, with the query tool, the one clicked).
///
/// Camera: WASD or the arrow keys pan, right- or middle-drag pans, the wheel zooms toward the cursor.
/// </summary>
public partial class CityView : Node2D
{
    private const float TileW = 48f;
    private const float TileH = 24f;
    private const float MinZoom = 0.5f;
    private const float MaxZoom = 3f;
    private const float PanSpeed = 900f; // screen pixels per second

    private CityController _city = null!;
    private Camera2D _camera = null!;

    public override void _Ready()
    {
        _city = GetNode<CityController>("../City");
        _camera = new Camera2D();
        AddChild(_camera);
        _city.CityReplaced += CenterCamera;
        Visible = false;
        CenterCamera();
    }

    public override void _Process(double delta)
    {
        bool shown = _city.DebugView;
        if (Visible != shown)
        {
            Visible = shown;
            if (shown) _camera.MakeCurrent();
        }
        if (!shown) return;

        if (_city.Active) PanWithKeys(delta);
        _city.Hover = _city.Active && !_city.PointerOverUi() ? ScreenToTile(GetLocalMousePosition()) : null;
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_city.DebugView || !_city.Active) return;
        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                ZoomAt(GetViewport().GetMousePosition(), 1.15f);
                break;

            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                ZoomAt(GetViewport().GetMousePosition(), 1f / 1.15f);
                break;

            case InputEventMouseMotion motion when (motion.ButtonMask & (MouseButtonMask.Right | MouseButtonMask.Middle)) != 0:
                MoveCamera(-motion.Relative / _camera.Zoom);
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                if (mb.Pressed && _city.Tool is null && !_city.PointerOverUi()) _city.Inspected = _city.Hover;
                if (mb.Pressed) _city.BeginDrag();
                else _city.FinishDrag(ScreenToTile(GetLocalMousePosition()));
                break;
        }
    }

    public override void _Draw()
    {
        var map = _city.Sim.Map;
        DrawRect(new Rect2(_camera.Position - GetViewportRect().Size / _camera.Zoom, GetViewportRect().Size * 2 / _camera.Zoom), Palette.Ground.Darkened(0.4f));

        // Back-to-front: increasing x + y draws far tiles first.
        for (int sum = 0; sum <= map.Width + map.Height - 2; sum++)
        for (int x = 0; x < map.Width; x++)
        {
            int y = sum - x;
            if (y < 0 || y >= map.Height) continue;
            DrawTile(x, y, map[x, y]);
        }

        if (CityController.AreaOf(_city.PreviewCommand()) is { } area) DrawArea(area);
    }

    private void DrawTile(int x, int y, Tile tile)
    {
        var color = Palette.For(tile.Kind);
        if (tile.IsZone && !_city.Sim.Power.IsPowered(x, y)) color = Palette.Unpowered(color);

        DrawColoredPolygon(Diamond(x, y), color);
        DrawPolyline(Diamond(x, y, close: true), new Color(0, 0, 0, 0.25f), 1f);

        // A grown building: a smaller, darker diamond on its lot.
        if (!tile.Building.IsNone)
        {
            var c = Diamond(x, y);
            var mid = (c[0] + c[2]) / 2;
            var inner = new Vector2[c.Length];
            for (int i = 0; i < c.Length; i++) inner[i] = mid + (c[i] - mid) * 0.55f;
            DrawColoredPolygon(inner, _city.Sim.IsAbandoned(x, y) ? Palette.Abandoned : color.Darkened(0.45f));
        }
    }

    private void DrawArea(Rect2I area)
    {
        for (int ty = area.Position.Y; ty < area.End.Y; ty++)
        for (int tx = area.Position.X; tx < area.End.X; tx++)
        {
            if (!_city.Sim.Map.InBounds(tx, ty)) continue;
            DrawPolyline(Diamond(tx, ty, close: true), Colors.White, 1.5f);
        }
    }

    // --- isometric helpers ---

    private static Vector2 TileOrigin(int x, int y) =>
        new((x - y) * TileW / 2f, (x + y) * TileH / 2f);

    private static Vector2[] Diamond(int x, int y, bool close = false)
    {
        var o = TileOrigin(x, y);
        var pts = new[]
        {
            o,
            o + new Vector2(TileW / 2f, TileH / 2f),
            o + new Vector2(0, TileH),
            o + new Vector2(-TileW / 2f, TileH / 2f),
        };
        return close ? [.. pts, o] : pts;
    }

    private Vector2I? ScreenToTile(Vector2 local)
    {
        float a = local.X / (TileW / 2f);
        float b = local.Y / (TileH / 2f);
        int x = Mathf.FloorToInt((b + a) / 2f);
        int y = Mathf.FloorToInt((b - a) / 2f);
        return _city.Sim.Map.InBounds(x, y) ? new Vector2I(x, y) : null;
    }

    // --- camera ---

    private void CenterCamera()
    {
        if (_camera is null) return;
        _camera.Zoom = Vector2.One;
        _camera.Position = new Vector2(0, _city.Sim.Map.Height * TileH / 2f);
    }

    private void PanWithKeys(double delta)
    {
        var dir = Vector2.Zero;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) dir.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) dir.X += 1;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) dir.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) dir.Y += 1;
        if (dir != Vector2.Zero) MoveCamera(dir.Normalized() * PanSpeed * (float)delta / _camera.Zoom.X);
    }

    // Keep the map's middle reachable: the camera may not wander past its edges.
    private void MoveCamera(Vector2 by)
    {
        var map = _city.Sim.Map;
        float halfW = (map.Width + map.Height) * TileW / 4f;
        var p = _camera.Position + by;
        _camera.Position = new Vector2(
            Mathf.Clamp(p.X, -halfW, halfW),
            Mathf.Clamp(p.Y, 0, (map.Width + map.Height) * TileH / 2f));
    }

    // Zoom so the point under the cursor stays put.
    private void ZoomAt(Vector2 screen, float factor)
    {
        float old = _camera.Zoom.X;
        float zoom = Mathf.Clamp(old * factor, MinZoom, MaxZoom);
        var offset = screen - GetViewportRect().Size / 2f;
        var world = _camera.Position + offset / old;
        _camera.Zoom = new Vector2(zoom, zoom);
        _camera.Position = world - offset / zoom;
    }
}
