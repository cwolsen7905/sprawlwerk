using System.Collections.Generic;
using Godot;
using SprawlWerk.Sim;
using SprawlWerk.Sim.Content;

namespace SprawlWerk;

/// <summary>
/// The in-game interface over the city: a build toolbar on the left (one
/// button per tool with its colour, price and key), a top bar (funds and last
/// month's net, date, power, pause and speed, Budget, Menu), a budget panel
/// (last month's taxes and upkeep, tax rates with - and + buttons) and a
/// status line at the bottom (the tile under the cursor, the cost of what the
/// tool would do, notices and refusals). It reads <see cref="CityController"/>
/// every frame and changes only its tool, pause and speed; building and tax
/// changes still go through commands. Hidden on the title menu. The developer
/// flag <c>--open-budget</c> opens the budget panel at start (for screenshots).
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
    private Label _net = null!;
    private Control _budget = null!;
    private Label _budgetNote = null!;
    private readonly Dictionary<string, Label> _budgetValues = new();
    private readonly Dictionary<ZoneKind, Label> _taxLabels = new();
    private Label _date = null!;
    private Label _power = null!;
    private Label _people = null!;
    private Label _demand = null!;
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
        BuildBudgetPanel();
        _budget.Visible = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--open-budget") >= 0;
    }

    public override void _Process(double delta)
    {
        Visible = _shell.Playing;
        if (!Visible) return;
        var sim = _city.Sim;

        _funds.Text = Strings.Funds(sim.Funds);
        _funds.Modulate = sim.Funds < 0 ? Problem : Colors.White;
        _net.Text = sim.LastBudget is { } last ? Strings.MonthlyNet(last.Net) : "";
        _net.Modulate = sim.LastBudget is { Net: < 0 } ? Problem : Good;
        if (_budget.Visible) UpdateBudgetPanel(sim);
        _date.Text = sim.Date.ToString();
        _power.Text = Strings.Power(sim.Power.Demand, sim.Power.Supply);
        _power.Modulate = sim.Power.ZonesWithoutSupply > 0 ? Warning : Colors.White;
        _people.Text = Strings.People(sim.Residents, sim.CommercialJobs + sim.IndustrialJobs);
        _demand.Text = Strings.DemandBars(sim.Demand.Residential, sim.Demand.Commercial, sim.Demand.Industrial);

        foreach (var (kind, button) in _toolButtons) button.SetPressedNoSignal(kind == _city.Tool);
        _pauseButton.SetPressedNoSignal(_city.Paused);
        for (int i = 0; i < _speedButtons.Count; i++)
            _speedButtons[i].SetPressedNoSignal(!_city.Paused && i == _city.SpeedIndex);

        if (_city.Hover is { } h)
        {
            var tile = sim.Map[h.X, h.Y];
            bool? powered = tile.IsZone ? sim.Power.IsPowered(h.X, h.Y) : null;
            string name = Strings.TileName(tile.Kind);
            if (tile.IsZone && sim.Content is { } content)
                name = tile.Building.IsNone
                    ? Strings.ZoneWaiting(name, Strings.WhyNot(sim.WhyNotGrowing(h.X, h.Y)))
                    : Strings.Building(Strings.BuildingName(content[tile.Building].Id), sim.Stress(h.X, h.Y), sim.IsAbandoned(h.X, h.Y));
            _tile.Text = Strings.TileStatus(h.X, h.Y, name, powered);
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
        _net = new Label();
        _date = new Label();
        _power = new Label();
        _people = new Label();
        _demand = new Label();
        var money = new HBoxContainer();
        money.AddThemeConstantOverride("separation", 6);
        money.AddChild(_funds);
        money.AddChild(_net);
        row.AddChild(money);
        row.AddChild(_date);
        row.AddChild(_power);
        row.AddChild(_people);
        row.AddChild(_demand);
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

        var budget = new Button { Text = Strings.BudgetButton, FocusMode = Control.FocusModeEnum.None };
        budget.Pressed += () => _budget.Visible = !_budget.Visible;
        row.AddChild(budget);

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

    // Last month's money on the right of the screen, and the three tax rates.
    private void BuildBudgetPanel()
    {
        var panel = Panel(Control.LayoutPreset.CenterRight);
        _budget = panel;
        panel.VisibilityChanged += () => { if (panel.Visible) panel.ResetSize(); };
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(230, 0) };
        column.AddThemeConstantOverride("separation", 4);
        panel.AddChild(column);
        column.AddChild(new Label { Text = Strings.BudgetHeading, HorizontalAlignment = HorizontalAlignment.Center });
        _budgetNote = new Label { Text = Strings.BudgetNotYet, Modulate = Dim, HorizontalAlignment = HorizontalAlignment.Center };
        column.AddChild(_budgetNote);

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 16);
        column.AddChild(grid);
        void Line(string key, string name, Color? color = null)
        {
            var label = new Label { Text = name, Modulate = color ?? Colors.White, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            grid.AddChild(label);
            var value = new Label { HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(80, 0), Modulate = color ?? Colors.White };
            grid.AddChild(value);
            value.VisibilityChanged += () => label.Visible = value.Visible;
            _budgetValues[key] = value;
        }
        Line("taxHeading", Strings.BudgetIncome, Dim);
        foreach (var zone in Zones) Line(zone.ToString(), Strings.ZoneName(zone));
        Line("income", Strings.BudgetTotal, Dim);
        Line("upkeepHeading", Strings.BudgetUpkeep, Dim);
        Line("roads", Strings.BudgetRoads);
        Line("lines", Strings.BudgetPowerLines);
        Line("plants", Strings.BudgetPlants);
        Line("zones", Strings.BudgetZones);
        Line("expenses", Strings.BudgetTotal, Dim);
        Line("net", Strings.BudgetNet);

        column.AddChild(new HSeparator());
        column.AddChild(new Label { Text = Strings.TaxHeading, HorizontalAlignment = HorizontalAlignment.Center });
        var taxes = new GridContainer { Columns = 4, TooltipText = Strings.TaxTip(_city.Sim.Growth.NeutralTax) };
        taxes.AddThemeConstantOverride("h_separation", 8);
        column.AddChild(taxes);
        foreach (var zone in Zones)
        {
            var z = zone;
            taxes.AddChild(new Label { Text = Strings.ZoneName(zone), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            var down = new Button { Text = Strings.TaxDown, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(28, 0) };
            down.Pressed += () => _city.ChangeTax(z, -1);
            taxes.AddChild(down);
            var rate = new Label { HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(40, 0) };
            taxes.AddChild(rate);
            _taxLabels[zone] = rate;
            var up = new Button { Text = Strings.TaxUp, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(28, 0) };
            up.Pressed += () => _city.ChangeTax(z, +1);
            taxes.AddChild(up);
        }
    }

    private static readonly ZoneKind[] Zones = [ZoneKind.Residential, ZoneKind.Commercial, ZoneKind.Industrial];

    private void UpdateBudgetPanel(Simulation sim)
    {
        var b = sim.LastBudget ?? default;
        bool noteWas = _budgetNote.Visible, zonesWere = _budgetValues["zones"].Visible;
        foreach (var zone in Zones) _budgetValues[zone.ToString()].Text = Strings.Money(b.TaxFor(zone));
        _budgetValues["taxHeading"].Text = "";
        _budgetValues["upkeepHeading"].Text = "";
        _budgetValues["income"].Text = Strings.Money(b.Income);
        _budgetValues["roads"].Text = Strings.Money(b.RoadUpkeep);
        _budgetValues["lines"].Text = Strings.Money(b.PowerLineUpkeep);
        _budgetValues["plants"].Text = Strings.Money(b.PlantUpkeep);
        _budgetValues["zones"].Text = Strings.Money(b.ZoneUpkeep);
        _budgetValues["zones"].Visible = b.ZoneUpkeep != 0; // only content that gives zones upkeep
        _budgetNote.Visible = sim.LastBudget is null;
        // A panel keeps its size when its contents shrink; shrink it to fit again.
        if (noteWas != _budgetNote.Visible || zonesWere != _budgetValues["zones"].Visible) _budget.ResetSize();
        _budgetValues["expenses"].Text = Strings.Money(b.Expenses);
        _budgetValues["net"].Text = Strings.Signed(b.Net);
        _budgetValues["net"].Modulate = b.Net < 0 ? Problem : Good;
        foreach (var zone in Zones)
        {
            int rate = _city.TaxShown(zone);
            _taxLabels[zone].Text = Strings.TaxRate(rate);
            _taxLabels[zone].Modulate = rate > sim.Growth.NeutralTax ? Warning : rate < sim.Growth.NeutralTax ? Good : Colors.White;
        }
    }

    private static ImageTexture Swatch(Color color)
    {
        var image = Image.CreateEmpty(14, 14, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }
}
