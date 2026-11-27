using System.Collections.Generic;
using Godot;
using SprawlWerk.Sim;
using SprawlWerk.Sim.Content;
using static SprawlWerk.UiKit;
using Ui = SprawlWerk.Palette.Ui;

namespace SprawlWerk;

/// <summary>
/// The in-game interface over the city, laid out on the edges so the 3D view
/// fills the rest:
/// <list type="bullet">
/// <item>top bar: the logo (opens the menu), readouts for date, treasury
/// (normal, low under $1,000, in debt), net per month and population, and the
/// Budget and Save buttons;</item>
/// <item>tool palette (left): Query, Bulldoze, Road, a Power flyout (line,
/// plant) and a Zones flyout (R, C, I), each flyout row with its cost;</item>
/// <item>tool hint (bottom left): the tool's name, cost and what it does;</item>
/// <item>bottom bar: R/C/I demand bars and the speed control (Pause, Snail,
/// Fox, Falcon);</item>
/// <item>toasts (top centre, at most three, 4.2 s): refusals, saves, the
/// treasury going into the red, zones going dark;</item>
/// <item>a cost tag by the cursor while placing (red when the city cannot pay);</item>
/// <item>the query card (<see cref="QueryCard"/>) and the Budget and Save / Load
/// windows (<see cref="BudgetWindow"/>, <see cref="SaveWindow"/>).</item>
/// </list>
/// It reads <see cref="CityController"/> every frame and changes only its tool,
/// pause, speed and inspected tile; building and taxes go through commands.
/// Only the interface is scaled (<see cref="UiScale"/>): this layer's transform,
/// with the root sized to the window divided by the scale, so the 3D view
/// keeps its native resolution. Breakpoints use that logical size.
/// Hidden on the title menu. The developer flags <c>--open-budget</c>,
/// <c>--open-saves</c>, <c>--open-zones</c> and <c>--inspect=x,y</c> open those at start (for screenshots).
/// </summary>
public partial class Hud : CanvasLayer
{
    private const int TopBarHeight = 60;
    private const int BottomBarHeight = 66;
    private const int Edge = 12;
    private const float ToastSeconds = 4.2f;
    private const int MaxToasts = 3;

    private sealed record ToolSpec(TileKind? Kind, string Icon, string Key);

    private static readonly ToolSpec Query = new(null, "query", "Q");
    private static readonly ToolSpec Bulldoze = new(TileKind.Empty, "bulldoze", "B");
    private static readonly ToolSpec Road = new(TileKind.Road, "road", "R");
    private static readonly ToolSpec[] PowerTools = [new(TileKind.PowerLine, "line", "L"), new(TileKind.PowerPlant, "plant", "")];
    private static readonly ToolSpec[] ZoneTools = [new(TileKind.Residential, "R", "1"), new(TileKind.Commercial, "C", "2"), new(TileKind.Industrial, "I", "3")];

    private CityController _city = null!;
    private ShellMenu _shell = null!;
    private Control _root = null!;
    private float _scale = 1f;
    private Vector2 _logical;
    private readonly List<Control> _panels = new();

    // top bar
    private Readout _date = null!, _treasury = null!, _perMonth = null!, _population = null!;
    private readonly List<Label> _menuLabels = new();
    private readonly List<Button> _menuButtons = new();

    // tools
    private PanelContainer _palette = null!;
    private readonly List<(Button Button, ToolSpec[] Members, bool Group)> _toolButtons = new();
    private readonly Dictionary<ToolSpec[], PanelContainer> _flyouts = new();
    private readonly Dictionary<ToolSpec[], ToolSpec> _groupChoice = new();
    private readonly List<(Button Row, TileKind Kind)> _flyoutRows = new();
    private Label _hintName = null!, _hintCost = null!, _hintText = null!;
    private PanelContainer _costTag = null!;
    private Label _costText = null!;

    // bottom bar
    private readonly RciBar[] _rci = new RciBar[3];
    private readonly List<Button> _speedButtons = new();

    // toasts and query
    private VBoxContainer _toasts = null!;
    private QueryCard _query = null!;
    private BudgetWindow _budget = null!;
    private SaveWindow _saves = null!;
    private long? _lastFunds;
    private int _lastDark;

    public override void _Ready()
    {
        _city = GetNode<CityController>("../City");
        _shell = GetNode<ShellMenu>("../Shell");
        _city.IsOverUi = IsOverPanel;
        _city.Message += (text, problem) => Toast(text, problem);
        _city.CityReplaced += OnCityReplaced;
        _shell.HandleEscape = HandleEscape;

        // A theme does not pass through a CanvasLayer, so each layer's root control carries it.
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps, Theme = UiTheme.Theme };
        AddChild(_root);

        BuildTopBar();
        BuildBottomBar();
        BuildPalette();
        BuildHint();
        _query = new QueryCard(() => _city.Inspected = null) { Visible = false };
        _root.AddChild(_query);
        _panels.Add(_query);
        _toasts = Column(8);
        _toasts.Alignment = BoxContainer.AlignmentMode.Begin;
        _root.AddChild(_toasts);
        BuildCostTag();
        _budget = new BudgetWindow(_city);
        _root.AddChild(_budget.Root);
        _panels.Add(_budget.Root);
        _saves = new SaveWindow(_city);
        _saves.Loaded += OnCityReplaced;
        _root.AddChild(_saves.Root);
        _panels.Add(_saves.Root);

        GetViewport().SizeChanged += ApplyScale;
        ApplyScale();

        var args = OS.GetCmdlineUserArgs();
        if (System.Array.IndexOf(args, "--open-budget") >= 0) CallDeferred(nameof(OpenBudget));
        if (System.Array.IndexOf(args, "--open-saves") >= 0) CallDeferred(nameof(OpenSaves));
        if (System.Array.IndexOf(args, "--open-zones") >= 0) _flyouts[ZoneTools].Visible = true;
        foreach (var arg in args)
            if (arg.StartsWith("--inspect=") && arg[10..].Split(',') is [var xs, var ys] && int.TryParse(xs, out int ix) && int.TryParse(ys, out int iy))
                CallDeferred(nameof(InspectAt), ix, iy);
    }

    private void InspectAt(int x, int y) => _city.Inspected = new Vector2I(x, y);

    private void OpenBudget() => _budget.Open();
    private void OpenSaves() => _saves.Open();

    // --- scale and layout ---

    private void ApplyScale()
    {
        var window = GetViewport().GetVisibleRect().Size;
        _scale = UiScale.For(window.Y);
        Scale = new Vector2(_scale, _scale);
        _logical = (window / _scale).Floor();
        _root.Size = _logical;

        bool compact = _logical.Y < 760;
        foreach (var (button, _, _) in _toolButtons)
            button.CustomMinimumSize = new Vector2(compact ? 36 : 46, compact ? 36 : 46);
        _palette.ResetSize();

        bool labels = _logical.X >= 1280;
        foreach (var label in _menuLabels) label.Visible = labels;
        foreach (var button in _menuButtons) button.CustomMinimumSize = new Vector2(labels ? 56 : 44, labels ? 52 : 44);
        _perMonth.Root.Visible = _logical.X >= 1000;
    }

    public override void _Process(double delta)
    {
        Visible = _shell.Playing;
        if (!Visible) return;
        var sim = _city.Sim;

        UpdateTopBar(sim);
        UpdateTools(sim);
        UpdateBottomBar(sim);
        UpdateQuery(sim);
        UpdateCostTag(sim);
        _budget.Update(_logical);
        _saves.Update(_logical);
        PlaceFloating();
        WatchCity(sim);
    }

    private void PlaceFloating()
    {
        _palette.Position = new Vector2(Edge, TopBarHeight + 16);
        _hintCard.Position = new Vector2(84, _logical.Y - BottomBarHeight - 12 - _hintCard.Size.Y);
        _query.Position = new Vector2(_logical.X - Edge - _query.Size.X, _logical.Y - BottomBarHeight - 12 - _query.Size.Y);
        _toasts.ResetSize();
        _toasts.Position = new Vector2(Mathf.Floor((_logical.X - _toasts.Size.X) / 2), 122);
        foreach (var (members, flyout) in _flyouts)
        {
            if (!flyout.Visible) continue;
            var button = _toolButtons.Find(t => t.Members == members).Button;
            flyout.ResetSize();
            flyout.Position = new Vector2(_palette.Position.X + _palette.Size.X + 8, button.GetGlobalRect().Position.Y - 8);
        }
    }

    // Panels swallow clicks, and the city view asks here so it does not preview under them.
    private bool IsOverPanel(Vector2 screen)
    {
        if (!Visible) return false;
        var point = screen / _scale;
        foreach (var panel in _panels)
            if (panel.IsVisibleInTree() && panel.GetGlobalRect().HasPoint(point)) return true;
        return false;
    }

    private bool HandleEscape()
    {
        if (_budget.IsOpen) { _budget.Close(); return true; }
        if (_saves.IsOpen) { _saves.Close(); return true; }
        if (CloseFlyouts()) return true;
        if (_city.Inspected is not null) { _city.Inspected = null; return true; }
        if (_city.Tool is not null) { _city.Tool = null; return true; }
        return false;
    }

    private void OnCityReplaced()
    {
        _budget.Reset();
        _lastFunds = null;
        _lastDark = 0;
    }

    // --- top bar ---

    private void BuildTopBar()
    {
        var style = UiTheme.Box(Ui.Panel, Ui.Ink, 0, 0, 0, Edge, 0);
        style.BorderWidthBottom = 2;
        var bar = Box(style);
        bar.MouseFilter = Control.MouseFilterEnum.Stop;
        bar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        bar.CustomMinimumSize = new Vector2(0, TopBarHeight);
        _root.AddChild(bar);
        _panels.Add(bar);

        var row = Row(16);
        bar.AddChild(row);

        var logo = new Button
        {
            Text = Strings.GameTitle,
            ThemeTypeVariation = UiTheme.PrimaryButton,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = Strings.MenuTip,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(0, 42),
        };
        logo.AddThemeFontOverride("font", UiFonts.Title);
        logo.AddThemeFontSizeOverride("font_size", 17);
        logo.Pressed += _shell.OpenPause;
        row.AddChild(logo);
        row.AddChild(new Control { CustomMinimumSize = new Vector2(4, 0) });

        _date = Readout(Strings.ReadoutDate, 124);
        _treasury = Readout(Strings.ReadoutTreasury, 112);
        _perMonth = Readout(Strings.ReadoutPerMonth, 96);
        _population = Readout(Strings.ReadoutPopulation, 96);
        foreach (var r in new[] { _date, _treasury, _perMonth, _population }) row.AddChild(r.Root);

        row.AddChild(Spacer());
        row.AddChild(MenuButton("budget", Strings.BudgetButton, () => { _saves.Close(); _budget.Toggle(); }));
        row.AddChild(MenuButton("save", Strings.SaveButton, () => { _budget.Close(); _saves.Toggle(); }));
    }

    private Button MenuButton(string icon, string label, System.Action onPress)
    {
        var button = new Button
        {
            ThemeTypeVariation = UiTheme.ToolButton,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = label,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        button.AddThemeStyleboxOverride("normal", UiTheme.Box(Ui.PanelHi, Ui.Ink, 2, 6, UiTheme.ControlShadow));
        button.AddThemeStyleboxOverride("hover", UiTheme.Box(Ui.PanelLo, Ui.Ink, 2, 6, UiTheme.ControlShadow));
        button.AddThemeStyleboxOverride("pressed", UiTheme.Pressed(UiTheme.Box(Ui.PanelLo, Ui.Ink, 2, 6, UiTheme.ControlShadow), Ui.PanelLo, UiTheme.ControlShadow));
        button.Pressed += onPress;
        var caption = Upper(label, UiTheme.Micro, Ui.Ink);
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        caption.AddThemeFontSizeOverride("font_size", 9);
        var inner = Column(1, UiIcons.Rect(icon, Ui.Ink, 22), caption);
        inner.Alignment = BoxContainer.AlignmentMode.Center;
        inner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        button.AddChild(inner);
        _menuLabels.Add(caption);
        _menuButtons.Add(button);
        return button;
    }

    private void UpdateTopBar(Simulation sim)
    {
        _date.Value.Text = Strings.Date(sim.Date);
        ShowTreasury(_treasury, sim.Funds, captions: true);
        if (sim.LastBudget is { } last)
        {
            _perMonth.Value.Text = Strings.Signed(last.Net);
            _perMonth.Value.AddThemeColorOverride("font_color", last.Net < 0 ? Ui.DebtText : Ui.LcdText);
            _perMonth.Well.AddThemeStyleboxOverride("panel", UiTheme.WellBox());
        }
        else
        {
            // No month settled yet: the "no data" look.
            _perMonth.Value.Text = Strings.NoData;
            _perMonth.Value.AddThemeColorOverride("font_color", Ui.LcdOff);
            var off = UiTheme.WellBox(Ui.LcdTrack);
            off.BorderWidthTop = 0;
            off.ContentMarginTop = 0;
            _perMonth.Well.AddThemeStyleboxOverride("panel", off);
        }
        _population.Value.Text = Strings.Count(sim.Residents);
    }

    /// <summary>The treasury's three looks: normal, low (under $1,000) and in debt (below $0).</summary>
    public static void ShowTreasury(Readout r, long funds, bool captions, int padX = 10, int padY = 0)
    {
        r.Value.Text = Strings.Money(funds);
        if (funds < 0)
        {
            r.Value.AddThemeColorOverride("font_color", Ui.DebtText);
            r.Well.AddThemeStyleboxOverride("panel", UiTheme.WellBox(Ui.DebtWell, ring: Ui.Danger, padX: padX, padY: padY));
            if (captions) { r.Caption.Text = Strings.ReadoutTreasuryDebt; r.Caption.AddThemeColorOverride("font_color", Ui.DangerText); }
        }
        else
        {
            r.Value.AddThemeColorOverride("font_color", funds < 1000 ? Ui.LcdLow : Ui.LcdText);
            r.Well.AddThemeStyleboxOverride("panel", UiTheme.WellBox(padX: padX, padY: padY));
            if (captions)
            {
                r.Caption.Text = funds < 1000 ? Strings.ReadoutTreasuryLow : Strings.ReadoutTreasury;
                r.Caption.AddThemeColorOverride("font_color", funds < 1000 ? Ui.LowLabel : Ui.Muted);
            }
        }
    }

    // --- tool palette, flyouts and hint ---

    private void BuildPalette()
    {
        _palette = Box(UiTheme.PanelBox(6));
        _palette.MouseFilter = Control.MouseFilterEnum.Stop;
        _root.AddChild(_palette);
        _panels.Add(_palette);
        var column = Column(4);
        _palette.AddChild(column);

        column.AddChild(ToolButton([Query], group: false));
        column.AddChild(ToolButton([Bulldoze], group: false));
        column.AddChild(ToolButton([Road], group: false));
        column.AddChild(ToolButton(PowerTools, group: true));
        column.AddChild(ToolButton(ZoneTools, group: true));
        _groupChoice[PowerTools] = PowerTools[0];
        _groupChoice[ZoneTools] = ZoneTools[0];
        BuildFlyout(PowerTools, Strings.PowerGroup);
        BuildFlyout(ZoneTools, Strings.ZonesGroup);
    }

    private Button ToolButton(ToolSpec[] members, bool group)
    {
        var first = members[0];
        var button = new Button
        {
            ThemeTypeVariation = UiTheme.ToolButton,
            Icon = UiIcons.Get(first.Icon),
            IconAlignment = HorizontalAlignment.Center,
            ToggleMode = true,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = group ? (members == PowerTools ? Strings.PowerGroup : Strings.ZonesGroup) : Strings.ToolTip(first.Key, first.Kind is { } k ? Strings.ToolName(k) : Strings.QueryName),
            CustomMinimumSize = new Vector2(46, 46),
        };
        if (group)
        {
            button.AddChild(new CornerMark());
            button.Pressed += () => ToggleFlyout(members);
        }
        else
        {
            button.Pressed += () => { CloseFlyouts(); _city.Tool = first.Kind; };
        }
        _toolButtons.Add((button, members, group));
        return button;
    }

    private void BuildFlyout(ToolSpec[] members, string title)
    {
        var flyout = Box(UiTheme.PanelBox(8));
        flyout.MouseFilter = Control.MouseFilterEnum.Stop;
        flyout.Visible = false;
        var column = Column(4, Upper(title, UiTheme.Overline));
        flyout.AddChild(column);
        var prices = _city.Sim.Prices;
        foreach (var spec in members)
        {
            var kind = spec.Kind!.Value;
            var row = new Button
            {
                ThemeTypeVariation = UiTheme.ToolButton,
                ToggleMode = true,
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(224, 40),
                TooltipText = Strings.ToolTip(spec.Key, Strings.ToolName(kind)),
            };
            var swatch = ToolSwatch(spec);
            swatch.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            var name = Text(Strings.ToolName(kind), UiTheme.Strong);
            var cost = Text(Strings.ToolCost(prices.Build(kind), perTile: kind != TileKind.PowerPlant), UiTheme.LcdSmall, Ui.Primary);
            var key = Text(spec.Key, UiTheme.LcdSmall, Ui.Muted);
            key.CustomMinimumSize = new Vector2(12, 0);
            var content = Row(10, swatch, name, Spacer(), cost, key);
            var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 6);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddChild(content);
            row.AddChild(margin);
            row.Pressed += () =>
            {
                _groupChoice[members] = spec;
                _city.Tool = kind;
                CloseFlyouts();
            };
            column.AddChild(row);
            _flyoutRows.Add((row, kind));
            // The name and cost go cream on the selected row.
            row.Toggled += on =>
            {
                name.AddThemeColorOverride("font_color", on ? Ui.OnPrimary : Ui.Ink);
                cost.AddThemeColorOverride("font_color", on ? Ui.OnPrimary : Ui.Primary);
                key.AddThemeColorOverride("font_color", on ? Ui.ToggleOff : Ui.Muted);
            };
        }
        _root.AddChild(flyout);
        _panels.Add(flyout);
        _flyouts[members] = flyout;
    }

    private static PanelContainer ToolSwatch(ToolSpec spec) => spec.Kind switch
    {
        TileKind.Residential => Swatch(Ui.ZoneR, 28, icon: "R"),
        TileKind.Commercial => Swatch(Ui.ZoneC, 28, icon: "C"),
        TileKind.Industrial => Swatch(Ui.ZoneI, 28, icon: "I", onColor: Ui.Ink),
        _ => Swatch(Ui.PowerSwatch, 28, icon: spec.Icon, onColor: Ui.Ink),
    };

    private void ToggleFlyout(ToolSpec[] members)
    {
        bool open = !_flyouts[members].Visible;
        CloseFlyouts();
        _flyouts[members].Visible = open;
    }

    private bool CloseFlyouts()
    {
        bool any = false;
        foreach (var flyout in _flyouts.Values)
        {
            any |= flyout.Visible;
            flyout.Visible = false;
        }
        return any;
    }

    private PanelContainer _hintCard = null!;

    private void BuildHint()
    {
        _hintCard = new PanelContainer { ThemeTypeVariation = UiTheme.CardPanel, MouseFilter = Control.MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(260, 0) };
        _hintName = Text("", UiTheme.Heading);
        _hintCost = Text("", UiTheme.LcdLabel, Ui.Primary, HorizontalAlignment.Right);
        _hintText = Text("", UiTheme.Small, Ui.Ink);
        _hintText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hintText.CustomMinimumSize = new Vector2(236, 0);
        _hintCard.AddChild(Column(2, Row(8, _hintName, Spacer(), _hintCost), _hintText));
        _root.AddChild(_hintCard);
        _panels.Add(_hintCard);
    }

    private void UpdateTools(Simulation sim)
    {
        var tool = _city.Tool;
        foreach (var (button, members, group) in _toolButtons)
        {
            bool selected = System.Array.Exists(members, m => m.Kind == tool);
            if (group && selected) _groupChoice[members] = System.Array.Find(members, m => m.Kind == tool)!;
            if (group) button.Icon = UiIcons.Get(_groupChoice[members].Icon);
            button.SetPressedNoSignal(selected || (group && _flyouts[members].Visible));
        }
        foreach (var (row, rowKind) in _flyoutRows)
            if (row.ButtonPressed != (rowKind == tool)) row.ButtonPressed = rowKind == tool;

        string name, cost, text;
        if (tool is not { } kind)
        {
            name = Strings.QueryName;
            cost = Strings.Free;
            text = Strings.QueryHint;
        }
        else
        {
            name = Strings.ToolName(kind);
            long price = kind == TileKind.Empty ? sim.Prices.Demolish : sim.Prices.Build(kind);
            cost = Strings.ToolCost(price, perTile: kind != TileKind.PowerPlant);
            text = Strings.ToolHint(kind, sim.Growth.RoadAccess, sim.Prices.LineCrossing);
        }
        if (_hintName.Text != name || _hintText.Text != text)
        {
            _hintName.Text = name;
            _hintCost.Text = cost;
            _hintText.Text = text;
            _hintCard.ResetSize();
        }
    }

    // --- cost tag by the cursor ---

    private void BuildCostTag()
    {
        _costText = Text("", UiTheme.LcdSmall);
        _costTag = Box(UiTheme.WellBox(padX: 6), _costText);
        _costTag.Visible = false;
        _root.AddChild(_costTag);
    }

    private void UpdateCostTag(Simulation sim)
    {
        var preview = _city.Active && !_city.PointerOverUi() ? _city.PreviewCommand() : null;
        if (preview is null) { _costTag.Visible = false; return; }
        var quote = sim.Quote(preview);
        bool short_ = quote.Refusal == Refusal.InsufficientFunds;
        if (!quote.Accepted && !short_) { _costTag.Visible = false; return; }
        _costTag.Visible = true;
        _costText.Text = Strings.Money(quote.Cost);
        _costText.AddThemeColorOverride("font_color", short_ ? Ui.DebtText : Ui.LcdText);
        _costTag.AddThemeStyleboxOverride("panel", short_ ? UiTheme.WellBox(Ui.DebtWell, ring: Ui.Danger, padX: 6) : UiTheme.WellBox(padX: 6));
        _costTag.ResetSize();
        _costTag.Position = (GetViewport().GetMousePosition() / _scale + new Vector2(18, 14)).Floor();
    }

    // --- bottom bar ---

    private void BuildBottomBar()
    {
        var style = UiTheme.Box(Ui.Panel, Ui.Ink, 0, 0, 0, Edge, 0);
        style.BorderWidthTop = 2;
        var bar = Box(style);
        bar.MouseFilter = Control.MouseFilterEnum.Stop;
        bar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        bar.CustomMinimumSize = new Vector2(0, BottomBarHeight);
        bar.GrowVertical = Control.GrowDirection.Begin;
        _root.AddChild(bar);
        _panels.Add(bar);

        var row = Row(16);
        bar.AddChild(row);

        // R/C/I demand on the LCD.
        var bars = Row(10);
        var zones = new[] { (ZoneKind.Residential, "R"), (ZoneKind.Commercial, "C"), (ZoneKind.Industrial, "I") };
        for (int i = 0; i < 3; i++)
        {
            var (zone, letter) = zones[i];
            _rci[i] = new RciBar { Color = Ui.ZoneLcd(zone) };
            var label = Text(letter, UiTheme.LcdSmall, Ui.ZoneLcd(zone), HorizontalAlignment.Center);
            label.AddThemeConstantOverride("line_spacing", 0);
            bars.AddChild(Column(0, _rci[i], label));
        }
        var well = UiTheme.WellBox(padX: 10, padY: 3);
        well.BorderWidthTop = 2;
        var rciBox = Box(well, bars);
        rciBox.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        rciBox.TooltipText = "R C I demand";
        row.AddChild(rciBox);

        // Pause / Snail / Fox / Falcon.
        var group = Box(UiTheme.Box(Ui.Ink, Ui.Ink, 2, 6, UiTheme.ControlShadow));
        group.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        var cells = Row(0);
        group.AddChild(cells);
        string[] icons = ["pause", "snail", "fox", "falcon"];
        for (int i = 0; i < icons.Length; i++)
        {
            int index = i;
            var cell = new Button
            {
                ThemeTypeVariation = UiTheme.SegmentButton,
                Text = Strings.SpeedNames[i],
                Icon = UiIcons.Get(icons[i]),
                IconAlignment = HorizontalAlignment.Center,
                VerticalIconAlignment = VerticalAlignment.Top,
                ToggleMode = true,
                FocusMode = Control.FocusModeEnum.None,
                TooltipText = Strings.SpeedTips[i],
                CustomMinimumSize = new Vector2(62, 42),
            };
            UiTheme.StyleSegment(cell, first: i == 0, last: i == icons.Length - 1);
            cell.Pressed += () =>
            {
                if (index == 0) _city.Paused = true;
                else { _city.SpeedIndex = index - 1; _city.Paused = false; }
            };
            cells.AddChild(cell);
            _speedButtons.Add(cell);
        }
        row.AddChild(group);
    }

    private void UpdateBottomBar(Simulation sim)
    {
        _rci[0].Demand = sim.Demand.Residential;
        _rci[1].Demand = sim.Demand.Commercial;
        _rci[2].Demand = sim.Demand.Industrial;
        for (int i = 0; i < _speedButtons.Count; i++)
            _speedButtons[i].SetPressedNoSignal(i == 0 ? _city.Paused : !_city.Paused && i - 1 == _city.SpeedIndex);
    }

    // --- query card ---

    private void UpdateQuery(Simulation sim)
    {
        if (_city.Inspected is { } at && sim.Map.InBounds(at.X, at.Y))
        {
            _query.Visible = true;
            _query.Display(sim.Inspect(at.X, at.Y), sim);
        }
        else
        {
            _query.Visible = false;
        }
    }

    // --- toasts ---

    private void WatchCity(Simulation sim)
    {
        if (_lastFunds is { } before && before >= 0 && sim.Funds < 0) Toast(Strings.InDebt, problem: true);
        _lastFunds = sim.Funds;
        int dark = sim.Power.ZonesWithoutSupply;
        if (dark > 0 && _lastDark == 0) Toast(Strings.PowerShort(dark), problem: true);
        _lastDark = dark;
    }

    /// <summary>Show a toast for 4.2 seconds; with three up, the oldest goes.</summary>
    public void Toast(string text, bool problem)
    {
        while (_toasts.GetChildCount() >= MaxToasts)
        {
            var oldest = _toasts.GetChild(0);
            _toasts.RemoveChild(oldest);
            oldest.QueueFree();
        }
        var icon = Swatch(problem ? Ui.Accent : Ui.Primary, 28, icon: problem ? "warning" : "check", onColor: Ui.OnPrimary);
        icon.AddThemeStyleboxOverride("panel", UiTheme.Box(problem ? Ui.Accent : Ui.Primary, Ui.Ink, 2, 14));
        icon.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        var label = Text(text, UiTheme.Strong);
        label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        var style = UiTheme.Box(Ui.PanelHi, Ui.Ink, 2, 22, UiTheme.PanelShadow, 8, 6);
        style.ContentMarginRight = 16;
        var toast = Box(style, Row(10, icon, label));
        toast.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        _toasts.AddChild(toast);
        var tween = toast.CreateTween();
        tween.TweenInterval(ToastSeconds - 0.3f);
        tween.TweenProperty(toast, "modulate:a", 0f, 0.3f);
        tween.TweenCallback(Callable.From(toast.QueueFree));
    }
}

/// <summary>One demand bar on the LCD: a 14 x 30 track with a zero line; the bar grows up or down from it, 15 px at full demand.</summary>
public partial class RciBar : Control
{
    private int _demand;

    public Color Color { get; set; }

    public int Demand
    {
        get => _demand;
        set { if (_demand != value) { _demand = value; QueueRedraw(); } }
    }

    public RciBar()
    {
        CustomMinimumSize = new Vector2(14, 30);
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Palette.Ui.LcdTrack);
        float mid = Size.Y / 2;
        float height = Mathf.Clamp(Mathf.Abs(_demand) / 1000f, 0, 1) * 15f;
        if (height > 0)
            DrawRect(_demand > 0 ? new Rect2(0, mid - height, Size.X, height) : new Rect2(0, mid, Size.X, height), Color);
        DrawLine(new Vector2(0, mid), new Vector2(Size.X, mid), Palette.Ui.LcdText.Darkened(0.35f), 1);
    }
}

/// <summary>The small corner triangle that marks a tool button with a flyout.</summary>
public partial class CornerMark : Control
{
    public CornerMark()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _Draw()
    {
        var parent = GetParent<Button>();
        var color = parent.ButtonPressed ? Palette.Ui.OnPrimary : Palette.Ui.Ink;
        var s = Size;
        DrawColoredPolygon([new Vector2(s.X - 4, s.Y - 10), new Vector2(s.X - 4, s.Y - 4), new Vector2(s.X - 10, s.Y - 4)], color);
    }

    public override void _Process(double delta) => QueueRedraw();
}
