using System.Collections.Generic;
using Godot;
using SprawlWerk.Sim;
using SprawlWerk.Sim.Content;
using static SprawlWerk.UiKit;
using Ui = SprawlWerk.Palette.Ui;

namespace SprawlWerk;

/// <summary>
/// The Budget window. Left, a tax rate per zone (a 0-20 slider sent as a
/// <see cref="SetTaxCommand"/>) with what it would bring in a month; right,
/// the month's upkeep for roads, power lines and plants, counted from the map
/// at the city's real prices; below, four readouts (income, upkeep, net,
/// treasury). Every figure is what the month would settle at if it ended now
/// (<see cref="Simulation.ProjectBudget"/>). Opening it pauses the city;
/// closing it restores the speed it had. Tax changes apply on the next tick,
/// so while paused the income shown for a changed rate is worked out here
/// (<see cref="TaxAt"/>).
/// </summary>
public sealed class BudgetWindow
{
    private static readonly ZoneKind[] Zones = [ZoneKind.Residential, ZoneKind.Commercial, ZoneKind.Industrial];

    private readonly CityController _city;
    private readonly WindowFrame _frame;
    private readonly Dictionary<ZoneKind, (HSlider Slider, Label Rate, Label Note, Label Warning)> _taxes = new();
    private readonly Dictionary<string, (Label Detail, Label Amount, Control Row)> _upkeep = new();
    private readonly Readout _income, _expenses, _net, _treasury;
    private bool _wasPaused;
    private bool _syncing;

    public Control Root => _frame.Root;
    public bool IsOpen => _frame.Root.Visible;

    public BudgetWindow(CityController city)
    {
        _city = city;
        _frame = Window(Strings.BudgetTitle, Tag(Strings.SimPaused), Close);
        var body = _frame.Body;

        var columns = Row(32);
        body.AddChild(columns);

        var left = Column(0);
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        left.SizeFlagsStretchRatio = 1.1f;
        left.AddChild(Upper(Strings.TaxColumn, UiTheme.Overline));
        left.AddChild(Gap(6));
        left.AddChild(Rule(Ui.Ink, 2));
        foreach (var zone in Zones) left.AddChild(TaxRow(zone));
        columns.AddChild(left);

        var right = Column(0);
        right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        right.AddChild(Upper(Strings.UpkeepColumn, UiTheme.Overline));
        right.AddChild(Gap(6));
        right.AddChild(Rule(Ui.Ink, 2));
        right.AddChild(UpkeepRow("roads", Strings.BudgetRoads));
        right.AddChild(UpkeepRow("lines", Strings.BudgetPowerLines));
        right.AddChild(UpkeepRow("plants", Strings.BudgetPlants));
        right.AddChild(UpkeepRow("zones", Strings.BudgetZones));
        columns.AddChild(right);

        var wells = Row(12);
        _income = Summary(wells, Strings.IncomeWell);
        _expenses = Summary(wells, Strings.UpkeepWell);
        _net = Summary(wells, Strings.NetWell);
        _treasury = Summary(wells, Strings.TreasuryWell);
        body.AddChild(wells);

        var hint = Text(Strings.BudgetHint(city.Sim.Growth.NeutralTax), UiTheme.Small, Ui.Ink);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hint.CustomMinimumSize = new Vector2(300, 0);
        var done = new Button { Text = Strings.Done, ThemeTypeVariation = UiTheme.PrimaryButton, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(88, 44) };
        done.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        done.Pressed += Close;
        body.AddChild(Row(16, hint, done));
    }

    public void Open()
    {
        if (IsOpen) return;
        _wasPaused = _city.Paused;
        _city.Paused = true;
        _frame.Root.Visible = true;
        AudioDirector.Instance?.Play(Sfx.UiOpen);
    }

    public void Close()
    {
        if (!IsOpen) return;
        _frame.Root.Visible = false;
        _city.Paused = _wasPaused;
        AudioDirector.Instance?.Play(Sfx.UiClose);
    }

    public void Toggle() { if (IsOpen) Close(); else Open(); }

    /// <summary>A different city was put in place: nothing to restore to but running.</summary>
    public void Reset()
    {
        _frame.Root.Visible = false;
        _wasPaused = false;
    }

    public void Update(Vector2 screen)
    {
        if (!IsOpen) return;
        var sim = _city.Sim;
        var projected = sim.ProjectBudget();
        int neutral = sim.Growth.NeutralTax;

        long income = 0;
        _syncing = true;
        foreach (var zone in Zones)
        {
            var (slider, rate, note, warning) = _taxes[zone];
            int shown = _city.TaxShown(zone);
            if ((int)slider.Value != shown) slider.SetValueNoSignal(shown);
            rate.Text = Strings.TaxRate(shown);
            long units = Units(sim, zone);
            long tax = shown == sim.Taxes.For(zone) ? projected.TaxFor(zone) : TaxAt(sim, zone, shown);
            income += tax;
            note.Text = Strings.TaxNote(tax, units, zone);
            warning.Visible = shown > neutral + 2;
        }
        _syncing = false;

        long roads = 0, lines = 0, plants = 0;
        var map = sim.Map;
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            switch (map[x, y].Kind)
            {
                case TileKind.Road: roads++; break;
                case TileKind.PowerLine: lines++; break;
                case TileKind.RoadWithLine: roads++; lines++; break;
                case TileKind.PowerPlant: plants++; break;
            }
        }
        SetUpkeep("roads", Strings.UpkeepTiles(roads, sim.UpkeepOf(TileKind.Road)), projected.RoadUpkeep);
        SetUpkeep("lines", Strings.UpkeepSpans(lines, sim.UpkeepOf(TileKind.PowerLine)), projected.PowerLineUpkeep);
        SetUpkeep("plants", Strings.UpkeepEach(plants, sim.UpkeepOf(TileKind.PowerPlant)), projected.PlantUpkeep);
        SetUpkeep("zones", "", projected.ZoneUpkeep);
        _upkeep["zones"].Row.Visible = projected.ZoneUpkeep != 0; // only content that gives zones upkeep

        long net = income - projected.Expenses;
        _income.Value.Text = Strings.Money(income);
        _expenses.Value.Text = Strings.Money(projected.Expenses);
        _expenses.Value.AddThemeColorOverride("font_color", Ui.DebtText);
        _net.Value.Text = Strings.Signed(net);
        _net.Value.AddThemeColorOverride("font_color", net < 0 ? Ui.DebtText : Ui.LcdText);
        Hud.ShowTreasury(_treasury, sim.Funds, captions: false, padX: 12, padY: 2);

        _frame.Fit(screen);
    }

    /// <summary>
    /// A zone's monthly tax at a rate not yet applied. Mirrors
    /// <see cref="Simulation.ProjectBudget"/>; a sim overload taking the rates
    /// would let this go.
    /// </summary>
    private static long TaxAt(Simulation sim, ZoneKind zone, int rate)
    {
        var r = sim.BudgetRules;
        long perUnit = zone switch
        {
            ZoneKind.Residential => r.TaxPerResident,
            ZoneKind.Commercial => r.TaxPerCommercialJob,
            _ => r.TaxPerIndustrialJob,
        };
        return Units(sim, zone) * perUnit * rate / Mathf.Max(1, sim.Growth.NeutralTax);
    }

    private static long Units(Simulation sim, ZoneKind zone) => zone switch
    {
        ZoneKind.Residential => sim.Residents,
        ZoneKind.Commercial => sim.CommercialJobs,
        _ => sim.IndustrialJobs,
    };

    private Control TaxRow(ZoneKind zone)
    {
        var chip = Swatch(Ui.Zone(zone), 16);
        chip.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        var name = Text(Strings.ZoneName(zone), UiTheme.Strong);
        name.CustomMinimumSize = new Vector2(104, 0);
        var slider = new HSlider
        {
            MinValue = BudgetRules.MinTax,
            MaxValue = BudgetRules.MaxTax,
            Step = 1,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(120, 22),
        };
        var z = zone;
        slider.ValueChanged += v => { if (!_syncing) _city.SetTax(z, (int)v); };
        var rate = Text("", UiTheme.LcdLabel, Ui.Ink, HorizontalAlignment.Right);
        rate.CustomMinimumSize = new Vector2(44, 0);
        var top = Row(10, chip, name, slider, rate);

        var note = Text("", UiTheme.Small, Ui.Ink);
        var warning = Text(Strings.DemandSuffering, UiTheme.Small, Ui.DangerText);
        var indent = new Control { CustomMinimumSize = new Vector2(16 + 10 + 104 + 10, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        var under = Row(0, indent, note, warning);

        _taxes[zone] = (slider, rate, note, warning);
        var row = Column(2, top, under);
        return Padded(row);
    }

    private Control UpkeepRow(string key, string name)
    {
        var detail = Text("", UiTheme.Small);
        var amount = Text("", UiTheme.LcdLabel, Ui.Ink, HorizontalAlignment.Right);
        amount.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        var lines = Column(0, Text(name, UiTheme.Strong), detail);
        var row = Padded(Row(8, lines, Spacer(), amount));
        _upkeep[key] = (detail, amount, row);
        return row;
    }

    private void SetUpkeep(string key, string detail, long amount)
    {
        var (d, a, _) = _upkeep[key];
        d.Text = detail;
        d.Visible = detail.Length > 0;
        a.Text = Strings.Money(amount);
    }

    private static Readout Summary(HBoxContainer row, string caption)
    {
        var readout = Readout(caption, 0, UiTheme.LcdLarge);
        readout.Caption.ThemeTypeVariation = UiTheme.Overline;
        readout.Caption.AddThemeColorOverride("font_color", Ui.Ink);
        readout.Root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        readout.Well.AddThemeStyleboxOverride("panel", UiTheme.WellBox(padX: 12, padY: 2));
        row.AddChild(readout.Root);
        return readout;
    }

    // A row with a hairline under it.
    private static Control Padded(Control content)
    {
        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        margin.AddChild(content);
        return Column(0, margin, Rule(Ui.Divider));
    }

    private static Control Gap(int height) => new() { CustomMinimumSize = new Vector2(0, height), MouseFilter = Control.MouseFilterEnum.Ignore };
}
