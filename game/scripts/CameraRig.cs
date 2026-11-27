using Godot;

namespace SprawlWerk;

/// <summary>
/// The 3D view's camera: a pivot on the ground that pans, a boom whose length
/// is the zoom and whose angle is the pitch, and a free yaw. The field of view
/// narrows as the camera pulls back, so the whole city reads almost like the
/// classic isometric view while the street level keeps some perspective.
///
/// Input (only while <see cref="Enabled"/>): WASD or the arrow keys pan relative
/// to the view, right-drag drags the ground (left-drag too while
/// <see cref="LeftDragPans"/> says so: the query tool), the wheel zooms toward
/// the cursor, + and - zoom at the middle of the view, comma and period or
/// middle-drag rotate (Q is the query tool), middle-drag up and down also tilts.
/// The pivot stays on the map and the pitch stays between a low angle and straight down.
/// </summary>
public partial class CameraRig : Node3D
{
    private const float MinDistance = 4f;
    private const float MaxDistance = 70f;
    private const float MinPitch = 20f;
    private const float MaxPitch = 85f;
    private const float NearFov = 55f;
    private const float FarFov = 28f;
    private const float KeyPanSpeed = 0.9f;  // boom lengths per second
    private const float KeyTurnSpeed = 90f;  // degrees per second
    private const float ZoomStep = 1.15f;

    private Camera3D _camera = null!;
    private Vector3 _pivot;
    private float _yaw = 45f;
    private float _pitch = 45f;
    private float _distance = 30f;
    private Vector2 _mapSize = new(32, 32);

    /// <summary>False while a menu is up or the 2D debug view is shown.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Set by the owner: true when the mouse is over the HUD, so the wheel is left alone.</summary>
    public System.Func<bool>? PointerOverUi { get; set; }

    /// <summary>Set by the owner: true when a left-drag should pan the view (the query tool builds nothing).</summary>
    public System.Func<bool>? LeftDragPans { get; set; }

    public Camera3D Camera => _camera;

    public override void _Ready()
    {
        _camera = new Camera3D { Near = 0.1f, Far = 400f };
        AddChild(_camera);
        Place();
    }

    /// <summary>Look at the middle of a map of this many tiles from the default height.</summary>
    public void Frame(int width, int height)
    {
        _mapSize = new Vector2(width, height);
        _pivot = new Vector3(width / 2f, 0, height / 2f);
        _yaw = 45f;
        _pitch = 45f;
        _distance = Mathf.Max(width, height) * 0.9f;
        Place();
    }

    /// <summary>Where the ray through a screen point meets the ground (y = 0), if it does.</summary>
    public Vector3? GroundAt(Vector2 screen)
    {
        var origin = _camera.ProjectRayOrigin(screen);
        var dir = _camera.ProjectRayNormal(screen);
        if (dir.Y > -1e-4f) return null;
        float t = -origin.Y / dir.Y;
        return origin + dir * t;
    }

    public override void _Process(double delta)
    {
        if (!Enabled) return;
        float dt = (float)delta;

        var move = Vector2.Zero;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) move.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) move.X += 1;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) move.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) move.Y += 1;
        if (move != Vector2.Zero) Pan(move.Normalized() * _distance * KeyPanSpeed * dt);

        float turn = 0;
        if (Input.IsKeyPressed(Key.Comma)) turn += 1;
        if (Input.IsKeyPressed(Key.Period)) turn -= 1;
        if (turn != 0) { _yaw += turn * KeyTurnSpeed * dt; Place(); }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Enabled) return;
        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } mb when !(PointerOverUi?.Invoke() ?? false):
                ZoomAt(mb.Position, 1f / ZoomStep);
                break;

            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } mb when !(PointerOverUi?.Invoke() ?? false):
                ZoomAt(mb.Position, ZoomStep);
                break;

            case InputEventMouseMotion motion when (motion.ButtonMask & MouseButtonMask.Right) != 0
                                                   || ((motion.ButtonMask & MouseButtonMask.Left) != 0 && (LeftDragPans?.Invoke() ?? false)):
                DragGround(motion.Position - motion.Relative, motion.Position);
                break;

            case InputEventKey { Pressed: true, Keycode: Key.Equal or Key.Plus or Key.KpAdd }:
                ZoomAt(GetViewport().GetVisibleRect().Size / 2, 1f / ZoomStep);
                break;

            case InputEventKey { Pressed: true, Keycode: Key.Minus or Key.KpSubtract }:
                ZoomAt(GetViewport().GetVisibleRect().Size / 2, ZoomStep);
                break;

            case InputEventMouseMotion motion when (motion.ButtonMask & MouseButtonMask.Middle) != 0:
                _yaw -= motion.Relative.X * 0.3f;
                _pitch = Mathf.Clamp(_pitch + motion.Relative.Y * 0.2f, MinPitch, MaxPitch);
                Place();
                break;
        }
    }

    // Screen-relative pan: x to the right of the view, y toward the viewer.
    private void Pan(Vector2 by)
    {
        var right = new Vector3(1, 0, 0).Rotated(Vector3.Up, Mathf.DegToRad(_yaw));
        var back = new Vector3(0, 0, 1).Rotated(Vector3.Up, Mathf.DegToRad(_yaw));
        _pivot += right * by.X + back * by.Y;
        Place();
    }

    // The ground point under the cursor follows the cursor.
    private void DragGround(Vector2 from, Vector2 to)
    {
        if (GroundAt(from) is not { } a || GroundAt(to) is not { } b) return;
        _pivot += a - b;
        Place();
    }

    // Zoom so the ground point under the cursor stays under it.
    private void ZoomAt(Vector2 screen, float factor)
    {
        var before = GroundAt(screen);
        _distance = Mathf.Clamp(_distance * factor, MinDistance, MaxDistance);
        Place();
        if (before is { } a && GroundAt(screen) is { } b)
        {
            _pivot += a - b;
            Place();
        }
    }

    private void Place()
    {
        _pivot = new Vector3(Mathf.Clamp(_pivot.X, 0, _mapSize.X), 0, Mathf.Clamp(_pivot.Z, 0, _mapSize.Y));
        if (_camera is null) return;

        float t = (_distance - MinDistance) / (MaxDistance - MinDistance);
        _camera.Fov = Mathf.Lerp(NearFov, FarFov, Mathf.Sqrt(Mathf.Clamp(t, 0, 1)));

        float p = Mathf.DegToRad(_pitch), y = Mathf.DegToRad(_yaw);
        var offset = new Vector3(Mathf.Sin(y) * Mathf.Cos(p), Mathf.Sin(p), Mathf.Cos(y) * Mathf.Cos(p)) * _distance;
        _camera.Position = _pivot + offset;
        _camera.LookAt(_pivot, Vector3.Up);
    }
}
