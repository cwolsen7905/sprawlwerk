using Godot;
using SprawlWerk.Sim;

namespace SprawlWerk;

/// <summary>
/// First playable view: draws the simulation's map as isometric tiles and
/// turns mouse and keyboard input into simulation commands. Deliberately
/// simple (immediate-mode drawing, no art) so the simulation can grow
/// before the rendering does.
///
/// Controls: 1 residential, 2 commercial, 3 industrial, 4 road,
/// 5 power line, 6 power plant, 0 bulldoze. Left-click places one tile; drag to fill
/// a rectangle (zones, bulldoze) or a straight line (road, power line), with the
/// total cost shown while dragging. Escape cancels a drag (see <see cref="CancelDrag"/>).
/// Space pauses, - and = change the speed (1x, 2x, 4x). Commands are still
/// accepted while paused and apply when time resumes.
/// F5 quick-saves and F9 quick-loads one slot (a stand-in until there is a save browser).
/// While <see cref="Active"/> is false (a menu is up) time stands still and input is ignored.
/// The HUD shows funds, what the hovered tile would cost, and the last refusal.
/// </summary>
public partial class CityView : Node2D
{
    private const int MapSize = 32;
    private const float TileW = 48f;
    private const float TileH = 24f;
    private const double SecondsPerTick = 0.25;

    private Simulation _sim = null!;
    private TileKind _tool = TileKind.Residential;
    private static readonly int[] Speeds = [1, 2, 4];

    private double _tickAccumulator;
    private bool _paused;
    private int _speedIndex;
    private Vector2I? _dragStart;
    private Vector2I? _hover;
    private string _lastRefusal = "";
    private string _notice = "";

    /// <summary>False while a menu is open: no time passes and the view ignores input.</summary>
    public bool Active { get; set; } = true;

    /// <summary>Abandon a drag in progress; true if there was one.</summary>
    public bool CancelDrag()
    {
        bool was = _dragStart is not null;
        _dragStart = null;
        return was;
    }

    private const string QuickSavePath = "user://quicksave.json";

    private void QuickSave()
    {
        using var file = Godot.FileAccess.Open(QuickSavePath, Godot.FileAccess.ModeFlags.Write);
        if (file is null) { _lastRefusal = Strings.SaveFailed; return; }
        file.StoreString(SaveGame.Serialize(_sim));
        _lastRefusal = "";
        _notice = Strings.Saved;
    }

    private void QuickLoad()
    {
        if (!Godot.FileAccess.FileExists(QuickSavePath)) { _lastRefusal = Strings.NoSave; return; }
        try
        {
            _sim = SaveGame.Deserialize(Godot.FileAccess.GetFileAsString(QuickSavePath));
        }
        catch (SaveFormatException e)
        {
            _lastRefusal = Strings.LoadFailed(e.Message);
            return;
        }
        _tickAccumulator = 0;
        _dragStart = null;
        _lastRefusal = "";
        _notice = Strings.Loaded;
    }

    /// <summary>Start over with an empty city.</summary>
    public void NewCity()
    {
        _sim = new Simulation(MapSize, MapSize);
        _sim.Step();
        _tickAccumulator = 0;
        _paused = false;
        _dragStart = null;
        _lastRefusal = "";
    }

    public override void _Ready()
    {
        _sim = new Simulation(MapSize, MapSize);
        SeedDemoCity();
        _sim.Step();
        CenterOnScreen();
        GetViewport().SizeChanged += CenterOnScreen;
    }

    public override void _Process(double delta)
    {
        // Fixed-rate simulation ticks, independent of frame rate.
        // Speed only changes how fast time is fed in; each tick is the same.
        if (Active && !_paused) _tickAccumulator += delta * Speeds[_speedIndex];
        while (_tickAccumulator >= SecondsPerTick)
        {
            _sim.Step();
            _tickAccumulator -= SecondsPerTick;
            NoteRefusals();
        }

        var hover = ScreenToTile(GetLocalMousePosition());
        if (hover != _hover)
        {
            _hover = hover;
        }

        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Active) return;
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                if (mb.Pressed)
                    _dragStart = ScreenToTile(GetLocalMousePosition());
                else if (_dragStart is { } start)
                {
                    // Released off the map: use the last tile the cursor was over.
                    if ((ScreenToTile(GetLocalMousePosition()) ?? _hover) is { } end) Place(start, end);
                    _dragStart = null;
                }
                break;

            case InputEventKey { Pressed: true, Echo: false } key:
                if (key.Keycode == Key.F5) QuickSave();
                else if (key.Keycode == Key.F9) QuickLoad();
                else if (key.Keycode == Key.Space) _paused = !_paused;
                else if (key.Keycode == Key.Minus) _speedIndex = Mathf.Max(0, _speedIndex - 1);
                else if (key.Keycode == Key.Equal) _speedIndex = Mathf.Min(Speeds.Length - 1, _speedIndex + 1);

                _tool = key.Keycode switch
                {
                    Key.Key1 => TileKind.Residential,
                    Key.Key2 => TileKind.Commercial,
                    Key.Key3 => TileKind.Industrial,
                    Key.Key4 => TileKind.Road,
                    Key.Key5 => TileKind.PowerLine,
                    Key.Key6 => TileKind.PowerPlant,
                    Key.Key0 => TileKind.Empty,
                    _ => _tool,
                };
                break;
        }
    }

    public override void _Draw()
    {
        var map = _sim.Map;

        // Back-to-front: increasing x + y draws far tiles first.
        for (int sum = 0; sum <= map.Width + map.Height - 2; sum++)
        for (int x = 0; x < map.Width; x++)
        {
            int y = sum - x;
            if (y < 0 || y >= map.Height) continue;
            DrawTile(x, y, map[x, y]);
        }

        var preview = PreviewCommand();
        if (preview is PlaceCommand pc) DrawArea(pc.X, pc.Y, pc.Width, pc.Height);
        else if (preview is BulldozeCommand bc) DrawArea(bc.X, bc.Y, bc.Width, bc.Height);

        var hudOrigin = -Position + new Vector2(16, 28);
        DrawString(ThemeDB.FallbackFont, hudOrigin,
            Strings.Hud(_sim.Funds, _tool.ToString(), _sim.Date.ToString(), _sim.PoweredTiles,
                _paused ? Strings.Paused : $"{Speeds[_speedIndex]}x"),
            HorizontalAlignment.Left, -1, 16, Colors.White);
        DrawString(ThemeDB.FallbackFont, hudOrigin + new Vector2(0, 22),
            $"{Strings.ToolHelp}    {Strings.TimeHelp}",
            HorizontalAlignment.Left, -1, 14, new Color(1, 1, 1, 0.6f));

        if (preview is not null)
        {
            var quote = _sim.Quote(preview);
            var (text, color) = quote.Accepted
                ? (Strings.Cost(quote.Cost), Colors.White)
                : (Strings.Here(Describe(quote)), new Color("f2a07b"));
            DrawString(ThemeDB.FallbackFont, hudOrigin + new Vector2(0, 44),
                text, HorizontalAlignment.Left, -1, 14, color);
        }

        if (_notice.Length > 0)
            DrawString(ThemeDB.FallbackFont, hudOrigin + new Vector2(0, 110),
                _notice, HorizontalAlignment.Left, -1, 14, new Color("8fd18a"));

        if (_sim.Power.ZonesWithoutSupply > 0)
            DrawString(ThemeDB.FallbackFont, hudOrigin + new Vector2(0, 88),
                Strings.PowerShort(_sim.Power.ZonesWithoutSupply, _sim.Power.Demand, _sim.Power.Supply),
                HorizontalAlignment.Left, -1, 14, new Color("f2c46b"));

        if (_lastRefusal.Length > 0)
            DrawString(ThemeDB.FallbackFont, hudOrigin + new Vector2(0, 66),
                _lastRefusal, HorizontalAlignment.Left, -1, 14, new Color("e86f5c"));
    }

    // Re-sent areas that are already built are not worth showing; only real problems are.
    private void NoteRefusals()
    {
        foreach (var outcome in _sim.LastOutcomes)
        {
            if (outcome.Result.Refusal is Refusal.None or Refusal.NothingToChange) continue;
            _lastRefusal = Strings.Refused(Describe(outcome.Result));
        }
    }

    private static string Describe(CommandResult r) => r.Refusal switch
    {
        Refusal.InsufficientFunds => Strings.NeedsFunds(r.Cost),
        Refusal.NothingToChange => Strings.AlreadyBuilt,
        Refusal.OutOfBounds => Strings.OffTheMap,
        _ => r.Refusal.ToString(),
    };

    private void DrawTile(int x, int y, Tile tile)
    {
        var color = Palette.For(tile.Kind);

        // Zones without power are drawn dim: the first piece of feedback.
        if (tile.IsZone && !_sim.Power.IsPowered(x, y)) color = color.Darkened(0.55f);

        DrawColoredPolygon(Diamond(x, y), color);
        DrawPolyline(Diamond(x, y, close: true), new Color(0, 0, 0, 0.25f), 1f);
    }

    private void Place(Vector2I from, Vector2I to)
    {
        _lastRefusal = "";
        _notice = "";
        _sim.Enqueue(Drag.Command(_tool, from.X, from.Y, to.X, to.Y));
    }

    // What the tool would do now: the whole drag if one is under way, else the hovered tile.
    private ICommand? PreviewCommand()
    {
        if (_hover is not { } h) return null;
        var from = _dragStart ?? h;
        return Drag.Command(_tool, from.X, from.Y, h.X, h.Y);
    }

    private void DrawArea(int x, int y, int w, int h)
    {
        for (int ty = y; ty < y + h; ty++)
        for (int tx = x; tx < x + w; tx++)
        {
            if (!_sim.Map.InBounds(tx, ty)) continue;
            DrawPolyline(Diamond(tx, ty, close: true), Colors.White, 1.5f);
        }
    }

    private void SeedDemoCity()
    {
        _sim.Enqueue(new PlaceCommand(4, 4, TileKind.PowerPlant));
        _sim.Enqueue(new PlaceCommand(5, 4, 6, 1, TileKind.PowerLine));
        _sim.Enqueue(new PlaceCommand(11, 2, 4, 5, TileKind.Residential));
        _sim.Enqueue(new PlaceCommand(15, 0, 1, MapSize, TileKind.Road));
        _sim.Enqueue(new PlaceCommand(16, 2, 4, 4, TileKind.Commercial));
        _sim.Enqueue(new PlaceCommand(22, 10, 5, 4, TileKind.Industrial)); // unpowered on purpose
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
        return _sim.Map.InBounds(x, y) ? new Vector2I(x, y) : null;
    }

    private void CenterOnScreen()
    {
        var size = GetViewportRect().Size;
        float mapPixelHeight = MapSize * TileH;
        Position = new Vector2(size.X / 2f, (size.Y - mapPixelHeight) / 2f);
    }
}
