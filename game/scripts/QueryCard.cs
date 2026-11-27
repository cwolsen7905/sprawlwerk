using System;
using Godot;
using SprawlWerk.Sim;
using SprawlWerk.Sim.Content;
using static SprawlWerk.UiKit;
using Ui = SprawlWerk.Palette.Ui;

namespace SprawlWerk;

/// <summary>
/// The query card (bottom right): what the clicked tile is and, for a zone,
/// why it is or is not doing well. An ink header with a swatch, title and
/// close button; for an empty lot a status strip (ready to grow, or not
/// growing and why) and a checklist (power, road access, demand) with a fix
/// under each failing row; the same for a building at risk; for an abandoned
/// building a dark strip and the occupants it had. A healthy building shows
/// only who lives or works there. Everything comes from one
/// <see cref="TileReport"/> (<see cref="TileInspect"/>); the card is rebuilt
/// only when the report changes.
/// </summary>
public partial class QueryCard : PanelContainer
{
    private readonly Action _close;
    private VBoxContainer _column = null!;
    private TileReport? _shown;

    public QueryCard(Action close) => _close = close;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", UiTheme.Box(Ui.Panel, Ui.Ink, UiTheme.PanelBorder, UiTheme.PanelRadius, UiTheme.PanelShadow));
        CustomMinimumSize = new Vector2(280, 0);
        _column = Column(0);
        AddChild(_column);
    }

    public void Display(TileReport report, Simulation sim)
    {
        if (_shown == report) return;
        _shown = report;
        foreach (var child in _column.GetChildren()) child.QueueFree();
        Build(report, sim);
        ResetSize();
    }

    private void Build(TileReport r, Simulation sim)
    {
        int reach = r.MaxRoadDistance;
        if (r.Zone is not { } zone)
        {
            Header(TileSwatch(r.Kind), Strings.TileName(r.Kind), Strings.TileAt(r.X, r.Y));
            string note = r.Kind switch
            {
                TileKind.Road => Strings.RoadNote(sim.UpkeepOf(TileKind.Road)),
                TileKind.PowerLine => Strings.LineNote,
                TileKind.RoadWithLine => Strings.RoadLineNote,
                TileKind.PowerPlant => Strings.PlantNote(sim.Power.Demand, sim.Power.Supply),
                _ => Strings.EmptyNote,
            };
            Body(Text(note, UiTheme.Small, Ui.Ink));
            return;
        }

        string name = r.BuildingTypeId is { } id ? Strings.BuildingName(id) : Strings.ZoneLot(zone);
        string subtitle = r.BuildingTypeId is null ? $"{Strings.EmptyLot} · {Strings.TileAt(r.X, r.Y)}" : Strings.TileAt(r.X, r.Y);

        switch (r.Status)
        {
            case TileStatus.Healthy:
                Header(Swatch(Ui.Zone(zone), 24, ZoneLetter(zone), onColor: Ui.OnZone(zone)), name, subtitle);
                Body(Text(Strings.Occupants(zone, r.Occupants), UiTheme.Strong));
                return;

            case TileStatus.Abandoned:
                Header(Swatch(Ui.AbandonedSwatch, 24, ZoneLetter(zone)), Strings.AbandonedTitle(zone), subtitle);
                Status(Ui.AbandonedBg, Ui.OnPrimary, "abandoned",
                    r.Reason == DeclineReason.None ? Strings.AbandonedRecovering : Strings.AbandonedOver(Strings.Why(r.Reason)),
                    r.Reason == DeclineReason.None ? Strings.TenantsReturning : Strings.FixEveryRedLine);
                Checklist(r, zone, reach, sim);
                Body(Text(Strings.OccupantsWas(zone, r.Occupants, r.Capacity), UiTheme.Small, Ui.Ink));
                return;

            case TileStatus.AtRisk:
                Header(Swatch(Ui.Zone(zone), 24, ZoneLetter(zone), onColor: Ui.OnZone(zone)), name, subtitle);
                Status(Ui.RiskBg, Ui.Ink, "warning",
                    Strings.AtRisk(r.Reason == DeclineReason.None ? Strings.BadMonths : Strings.Why(r.Reason)), null);
                Checklist(r, zone, reach, sim);
                Body(Text(Strings.Occupants(zone, r.Occupants), UiTheme.Small, Ui.Ink));
                return;

            default: // an empty lot
                Header(Swatch(Ui.Zone(zone), 24, ZoneLetter(zone), onColor: Ui.OnZone(zone)), name, subtitle);
                if (r.Status == TileStatus.EmptyReady)
                    Status(Ui.OkBg, Ui.Ink, "check", Strings.ReadyToGrow, Strings.ReadyToGrowSub);
                else
                    Status(Ui.WarnBg, Ui.Ink, "warning", Strings.NotGrowing(BlockText(r)), null);
                Checklist(r, zone, reach, sim);
                return;
        }
    }

    private static string BlockText(TileReport r) => r.GrowthBlock switch
    {
        GrowthBlock.NoPower => Strings.Why(DeclineReason.NoPower),
        GrowthBlock.NoRoad => Strings.Why(DeclineReason.NoRoad),
        GrowthBlock.NoDemand => Strings.Why(DeclineReason.LowDemand),
        GrowthBlock.NoBuildingType => Strings.NothingToBuild,
        _ => Strings.Why(r.Reason),
    };

    private static string ZoneLetter(ZoneKind zone) => zone switch
    {
        ZoneKind.Residential => "R",
        ZoneKind.Commercial => "C",
        _ => "I",
    };

    private static Control TileSwatch(TileKind kind) => kind switch
    {
        TileKind.Road => Swatch(Ui.RoadSwatch, 24, "="),
        TileKind.PowerLine or TileKind.RoadWithLine => Swatch(Ui.PowerSwatch, 24, "L", onColor: Ui.Ink),
        TileKind.PowerPlant => Swatch(Ui.PowerSwatch, 24, icon: "plant", onColor: Ui.Ink),
        _ => Swatch(Ui.PanelLo, 24, "×", onColor: Ui.Ink),
    };

    private void Header(Control swatch, string title, string subtitle)
    {
        var style = UiTheme.Box(Ui.Ink, Ui.Ink, 0, 0, 0, 10, 8);
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight = UiTheme.PanelRadius - 2;
        var close = new Button
        {
            ThemeTypeVariation = UiTheme.ToolButton,
            Icon = UiIcons.Get("cross"),
            CustomMinimumSize = new Vector2(28, 28),
            FocusMode = FocusModeEnum.None,
            IconAlignment = HorizontalAlignment.Center,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        close.AddThemeColorOverride("icon_normal_color", Ui.OnPrimary);
        close.AddThemeColorOverride("icon_hover_color", Ui.Ink);
        close.AddThemeConstantOverride("icon_max_width", 16);
        close.Pressed += _close;
        swatch.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var titles = Column(0, Text(title, UiTheme.Strong, Ui.OnPrimary), Text(subtitle, UiTheme.Small, Ui.ToggleOff));
        titles.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _column.AddChild(Box(style, Row(10, swatch, titles, close)));
    }

    private void Status(Color bg, Color fg, string icon, string text, string? sub)
    {
        var style = UiTheme.Box(bg, Ui.Ink, 0, 0, 0, 12, 8);
        style.BorderWidthBottom = 1;
        style.BorderColor = Ui.Divider;
        var lines = Column(1, Text(text, UiTheme.Strong, fg));
        if (sub is not null)
        {
            var subLabel = Text(sub, UiTheme.Small, fg);
            subLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            subLabel.CustomMinimumSize = new Vector2(220, 0);
            lines.AddChild(subLabel);
        }
        var mark = UiIcons.Rect(icon, fg, 18);
        mark.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        _column.AddChild(Box(style, Row(8, mark, lines)));
    }

    private void Checklist(TileReport r, ZoneKind zone, int reach, Simulation sim)
    {
        var list = Column(8);
        // An empty lot grows only with demand above zero; a built one declines only when it is low.
        int floor = r.BuildingTypeId is null ? 0 : sim.Growth.DeclineDemand;
        int demand = r.ZoneDemand ?? 0;
        list.AddChild(Check(r.Powered, Strings.CheckPower, r.Powered ? Strings.PowerOk : Strings.PowerBad, Strings.PowerFix));
        list.AddChild(Check(r.HasRoadAccess, Strings.CheckRoad, r.HasRoadAccess ? Strings.RoadOk(reach) : Strings.RoadBad, Strings.RoadFix(reach)));
        list.AddChild(Check(demand > floor, Strings.CheckDemand(zone), Strings.DemandValue(demand), Strings.DemandFix(zone)));
        Body(list);
    }

    private static Control Check(bool ok, string name, string value, string fix)
    {
        var top = Row(6, Text(name, UiTheme.Strong), Spacer(), Text(value, UiTheme.Small, ok ? Ui.Muted : Ui.DangerText));
        var lines = Column(1, top);
        lines.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (!ok)
        {
            var fixLabel = Text(fix, UiTheme.Small);
            fixLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            fixLabel.CustomMinimumSize = new Vector2(200, 0);
            lines.AddChild(fixLabel);
        }
        return Row(8, Mark(ok), lines);
    }

    private void Body(Control content)
    {
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        margin.AddChild(content);
        _column.AddChild(margin);
    }
}
