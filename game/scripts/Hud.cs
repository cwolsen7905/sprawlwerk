using System.Collections.Generic;
using Godot;
using SprawlWerk.Sim;

namespace SprawlWerk;

/// <summary>
/// The in-game interface over the city: a build toolbar on the left (one
/// button per tool with its colour, price and key), a top bar (funds, date,
/// power, pause and speed, Menu) and a status line at the bottom (the tile
/// under the cursor, the cost of what the tool would do, notices and
/// refusals). It reads <see cref="CityController"/> every frame and changes only
/// its tool, pause and speed; building still goes through commands. Hidden on
/// the title menu.
/// </summary>
public partial class Hud : CanvasLayer
{
    private static readonly (TileKind Kind, string Key)[] Tools =
    [
        (TileKind.Residential, "1"),
        (TileKind.Commercial, "2"),
        (TileKind.Industrial, "3"),
        (TileKind.Road, "4"),
        (TileKind.PowerLine, "5"),
        (TileKind.PowerPlant, "6"),
        (TileKind.Empty, "0"),
    ];

    private static readonly Color Warning = new("f2c46b");
    private static readonly Color Problem = new("e86f5c");
    private static readonly Color Good = new("8fd18a");
    private static readonly Color Dim = new(1, 1, 1, 0.6f);

    private CityController _city = null!;
    private ShellMenu _shell = null!;
    private readonly List<Control> _panels = new();
    private readonly Dictionary<TileKind, Button> _toolButtons = new();
    private readonly List<Button> _speedButtons = new();
    private Button _pauseButton = null!;
    private Label _funds = null!;
    private Label _date = null!;
    private Label _power = null!;
    private Label _tile = null!;
    private Label _quote = null!;
    private Label _message = null!;

    public override void _Ready()
    {
        _city = GetNode<CityController>("../City");
        _shell = GetNode<ShellMenu>("../Shell");
        _city.IsOverUi = IsOverPanel;
        BuildTopBar();
        BuildToolbar();
        BuildStatusBar();
    }

    public override void _Process(double delta)
    {
        Visible = _shell.Playing;
        if (!Visible) return;
        var sim = _city.Sim;

        _funds.Text = Strings.Funds(sim.Funds);
        _date.Text = sim.Date.ToString();
        _power.Text = Strings.Power(sim.Power.Demand, sim.Power.Supply);
        _power.Modulate = sim.Power.ZonesWithoutSupply > 0 ? Warning : Colors.White;

        foreach (var (kind, button) in _toolButtons) button.SetPressedNoSignal(kind == _city.Tool);
        _pauseButton.SetPressedNoSignal(_city.Paused);
        for (int i = 0; i < _speedButtons.Count; i++)
            _speedButtons[i].SetPressedNoSignal(!_city.Paused && i == _city.SpeedIndex);

        if (_city.Hover is { } h)
        {
            var kind = sim.Map[h.X, h.Y].Kind;
            bool? powered = sim.Map[h.X, h.Y].IsZone ? sim.Power.IsPowered(h.X, h.Y) : null;
            _tile.Text = Strings.TileStatus(h.X, h.Y, Strings.TileName(kind), powered);
        }
        else
        {
            _tile.Text = "";
        }

        if (_city.PreviewCommand() is { } preview)
        {
            var quote = sim.Quote(preview);
            _quote.Text = quote.Accepted ? Strings.Cost(quote.Cost) : Strings.Here(CityController.Describe(quote));
            _quote.Modulate = quote.Accepted ? Colors.White : new Color("f2a07b");
        }
        else
        {
            _quote.Text = "";
        }

        if (_city.LastRefusal.Length > 0) Show(_message, _city.LastRefusal, Problem);
        else if (sim.Power.ZonesWithoutSupply > 0)
            Show(_message, Strings.PowerShort(sim.Power.ZonesWithoutSupply, sim.Power.Demand, sim.Power.Supply), Warning);
        else if (_city.Notice.Length > 0) Show(_message, _city.Notice, Good);
        else Show(_message, Strings.KeyHelp, Dim);
    }

    private static void Show(Label label, string text, Color color)
    {
        label.Text = text;
        label.Modulate = color;
    }

    // Panels swallow clicks, and the city view asks here so it does not preview under them.
    private bool IsOverPanel(Vector2 screen)
    {
        if (!Visible) return false;
        foreach (var panel in _panels)
            if (panel.Visible && panel.GetGlobalRect().HasPoint(screen)) return true;
        return false;
    }

    private PanelContainer Panel(Control.LayoutPreset preset)
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        AddChild(panel);
        // Presets keep the size a panel had when set, so set again whenever its contents resize it.
        void Place() => panel.SetAnchorsAndOffsetsPreset(preset, Control.LayoutPresetMode.KeepSize, 8);
        Place();
        panel.Resized += Place;
        _panels.Add(panel);
        return panel;
    }

    private void BuildTopBar()
    {
        var panel = Panel(Control.LayoutPreset.TopWide);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 18);
        panel.AddChild(row);

        _funds = new Label();
        _date = new Label();
        _power = new Label();
        row.AddChild(_funds);
        row.AddChild(_date);
        row.AddChild(_power);
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var speeds = new HBoxContainer();
        speeds.AddThemeConstantOverride("separation", 2);
        row.AddChild(speeds);
        _pauseButton = new Button { Text = Strings.PauseButton, ToggleMode = true, FocusMode = Control.FocusModeEnum.None };
        _pauseButton.Pressed += () => _city.Paused = !_city.Paused;
        speeds.AddChild(_pauseButton);
        for (int i = 0; i < CityController.SpeedSteps.Count; i++)
        {
            int index = i;
            var button = new Button { Text = Strings.Speed(CityController.SpeedSteps[i]), ToggleMode = true, FocusMode = Control.FocusModeEnum.None };
            button.Pressed += () => { _city.SpeedIndex = index; _city.Paused = false; };
            speeds.AddChild(button);
            _speedButtons.Add(button);
        }

        var menu = new Button { Text = Strings.MenuButton, FocusMode = Control.FocusModeEnum.None };
        menu.Pressed += _shell.OpenPause;
        row.AddChild(menu);
    }

    private void BuildToolbar()
    {
        var panel = Panel(Control.LayoutPreset.CenterLeft);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        panel.AddChild(column);
        column.AddChild(new Label { Text = Strings.ToolsHeading, HorizontalAlignment = HorizontalAlignment.Center });

        var prices = _city.Sim.Prices;
        foreach (var (kind, key) in Tools)
        {
            bool bulldoze = kind == TileKind.Empty;
            long price = bulldoze ? prices.Demolish : prices.Build(kind);
            string name = Strings.ToolName(kind);
            var button = new Button
            {
                Text = Strings.ToolButton(name, price),
                Icon = Swatch(bulldoze ? new Color("8a6f5a") : Palette.For(kind)),
                ToggleMode = true,
                Alignment = HorizontalAlignment.Left,
                FocusMode = Control.FocusModeEnum.None,
                TooltipText = Strings.ToolTip(key, name, price, perTile: kind != TileKind.PowerPlant),
                CustomMinimumSize = new Vector2(190, 0),
            };
            var tool = kind;
            button.Pressed += () => _city.Tool = tool;
            column.AddChild(button);
            _toolButtons[kind] = button;
        }
    }

    private void BuildStatusBar()
    {
        var panel = Panel(Control.LayoutPreset.BottomWide);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 24);
        panel.AddChild(row);
        _tile = new Label { CustomMinimumSize = new Vector2(260, 0) };
        _quote = new Label { CustomMinimumSize = new Vector2(200, 0) };
        _message = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true };
        row.AddChild(_tile);
        row.AddChild(_quote);
        row.AddChild(_message);
    }

    private static ImageTexture Swatch(Color color)
    {
        var image = Image.CreateEmpty(14, 14, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }
}
