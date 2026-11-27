using SprawlWerk.Sim;
using SprawlWerk.Sim.Content;

namespace SprawlWerk;

/// <summary>
/// Player-facing text in one place, so views never hold string literals and
/// a translation table can replace this later. Money is always <c>$</c>.
/// </summary>
public static class Strings
{
    public const string GameTitle = "SprawlWerk";
    public const string MenuNew = "New city";
    public const string MenuResume = "Resume";
    public const string MenuQuit = "Quit";
    public const string MenuPaused = "Paused";
    public const string Saved = "Saved. The city is safely in a drawer.";
    public const string Loaded = "Loaded. Everyone is back where you left them.";
    public const string SaveFailed = "Could not write the save";
    public const string NoSave = "No quick save yet. F5 makes one.";
    public static string LoadFailed(string why) => $"Could not load: {why}";
    public const string MenuLoad = "Load";
    public const string MenuContinue = "Continue";
    public static string AutosaveFailed(string why) => $"Autosave failed: {why}";
    public const string MenuSave = "Save";
    public const string MenuBack = "Back";
    public const string MenuDelete = "Delete";
    public const string MenuYes = "Yes";
    public const string MenuNo = "No";
    public const string SaveNameHint = "Save name";
    public const string NoSavesYet = "No saves yet";
    public const string SaveMissing = "That save is gone";
    public const string DeleteFailed = "Could not delete the save";
    public const string PickASave = "Pick a save first";
    public const string BadSaveName = "Type a name using letters or numbers";
    public static string Overwrite(string name) => $"Overwrite \"{name}\"?";
    public static string ConfirmDelete(string name) => $"Delete \"{name}\"?";
    public static string SavedAs(string name) => $"Saved as \"{name}\"";
    public static string SaveLine(string name, string date, long tick) => $"{name}  -  {date}  -  {tick} ticks";
    public static string SaveUnreadable(string name, string why) => $"{name}  -  unreadable: {why}";
    public const string VolumeMusic = "Music";
    public const string VolumeEffects = "Effects";
    public const string VolumeAmbience = "Ambience";
    public static string LoadedWithChanges(int count) => $"Loaded. {count} buildings from missing content were removed.";

    // --- refusals, as toasts ---
    public static string NeedsFunds(long cost) => $"Not enough funds. That costs {Money(cost)}.";
    public const string AlreadyBuilt = "That's already built.";
    public const string OffTheMap = "That's off the edge of the map. The surveyors won't go there.";
    public const string OutOfRange = "That rate is off the dial.";
    public const string InDebt = "The treasury is in the red. The city accountant has taken up stress-knitting.";
    public static string PowerShort(int zones) =>
        zones == 1 ? "A zone is in the dark. The grid needs another power plant." : $"{zones:N0} zones are in the dark. The grid needs another power plant.";

    // --- numbers ---
    private static readonly string[] MonthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>"Apr 12 Y3": the calendar has no epoch, so years count from the founding.</summary>
    public static string Date(CityDate date) => $"{MonthNames[(date.Month - 1) % 12]} {date.Day:00} Y{date.Year}";

    /// <summary>"Apr Y3", for save slots.</summary>
    public static string MonthYear(CityDate date) => $"{MonthNames[(date.Month - 1) % 12]} Y{date.Year}";

    public static string Money(long amount) => amount < 0 ? $"-${-amount:N0}" : $"${amount:N0}";
    public static string Signed(long amount) => amount < 0 ? $"-${-amount:N0}" : $"+${amount:N0}";
    public static string Count(long n) => $"{n:N0}";
    public const string NoData = "--";

    // --- top bar ---
    public const string ReadoutDate = "Date";
    public const string ReadoutTreasury = "Treasury";
    public const string ReadoutTreasuryLow = "Treasury · low";
    public const string ReadoutTreasuryDebt = "Treasury · in debt";
    public const string ReadoutPerMonth = "Per month";
    public const string ReadoutPopulation = "Population";
    public const string MenuTip = "Menu (Esc)";
    public const string BudgetButton = "Budget";
    public const string SaveButton = "Save";

    // --- speeds ---
    public static readonly string[] SpeedNames = ["Pause", "Snail", "Fox", "Falcon"];
    public static readonly string[] SpeedTips = ["Pause (Space)", "Snail: slow", "Fox: normal", "Falcon: fast"];

    // --- tools ---
    public const string QueryName = "Query";
    public const string PowerGroup = "Power";
    public const string ZonesGroup = "Zones";
    public const string Free = "Free";

    public static string ToolName(TileKind kind) => kind switch
    {
        TileKind.Residential => "Residential",
        TileKind.Commercial => "Commercial",
        TileKind.Industrial => "Industrial",
        TileKind.Road => "Road",
        TileKind.PowerLine => "Power line",
        TileKind.PowerPlant => "Power plant",
        TileKind.RoadWithLine => "Road with power line",
        TileKind.Empty => "Bulldoze",
        _ => kind.ToString(),
    };

    public static string TileName(TileKind kind) => kind == TileKind.Empty ? "Empty land" : ToolName(kind);

    /// <summary>A tool's price as the palette shows it: per tile ("$15/t") or each ("$4,000").</summary>
    public static string ToolCost(long price, bool perTile) => perTile ? $"{Money(price)}/t" : Money(price);

    public static string ToolTip(string key, string name) => key.Length == 0 ? name : $"{name} ({key})";

    public const string QueryHint = "Click any tile to inspect it. Drag to pan.";

    public static string ToolHint(TileKind kind, int roadReach, long crossing) => kind switch
    {
        TileKind.Residential => "Drag to zone homes. They move in with power, a road and demand.",
        TileKind.Commercial => "Drag to zone shops and offices. They follow the residents.",
        TileKind.Industrial => "Drag to zone industry. Factories want workers nearby.",
        TileKind.Road => $"Drag to lay road. Zones need one within {roadReach} tiles.",
        TileKind.PowerLine => crossing > 0 ? $"Drag to string line. Crossing a road adds {Money(crossing)}." : "Drag to string line between plants and zones.",
        TileKind.PowerPlant => "Click to build. Wire it to zones with lines or zoned lots.",
        _ => "Drag to clear anything built or zoned.",
    };

    // --- budget window ---
    public const string BudgetTitle = "Budget";
    public const string SimPaused = "SIM PAUSED";
    public const string TaxColumn = "Tax rates · revenue";
    public const string UpkeepColumn = "Monthly upkeep";
    public const string BudgetRoads = "Roads";
    public const string BudgetPowerLines = "Power lines";
    public const string BudgetPlants = "Power plants";
    public const string BudgetZones = "Zones";
    public const string IncomeWell = "Income / mo";
    public const string UpkeepWell = "Upkeep / mo";
    public const string NetWell = "Net / mo";
    public const string TreasuryWell = "Treasury";
    public const string Done = "Done";
    public const string DemandSuffering = " · demand is suffering";
    public static string TaxRate(int rate) => $"{rate}%";
    public static string TaxNote(long perMonth, long units, ZoneKind zone) => zone switch
    {
        ZoneKind.Residential => $"{Money(perMonth)}/mo from {units:N0} residents",
        ZoneKind.Commercial => $"{Money(perMonth)}/mo from {units:N0} shop & office jobs",
        _ => $"{Money(perMonth)}/mo from {units:N0} factory jobs",
    };
    public static string UpkeepTiles(long count, long each) => $"{count:N0} tiles × {Money(each)}";
    public static string UpkeepSpans(long count, long each) => $"{count:N0} spans × {Money(each)}";
    public static string UpkeepEach(long count, long each) => $"{count:N0} × {Money(each)}";
    public static string BudgetHint(int neutral) =>
        $"Taxes above {neutral}% start to suppress demand for that zone. Upkeep is charged on the 1st of each month.";

    public static string ZoneName(ZoneKind zone) => zone switch
    {
        ZoneKind.Residential => "Residential",
        ZoneKind.Commercial => "Commercial",
        _ => "Industrial",
    };

    // --- save window ---
    public const string SaveTitle = "Save / Load";
    public const string AutosaveSlot = "Autosave";
    public static string Slot(int n) => $"Slot {n}";
    public const string EmptySlot = "Nothing here yet";
    public const string SaveHere = "Save here";
    public const string LoadSlot = "Load";
    public const string AutosaveNote = "Autosave runs at the start of every month.";
    public static string SlotDetail(CityDate date, long funds, string saved) => $"{MonthYear(date)} · {Money(funds)} · saved {saved}";
    public static string SlotUnreadable(string why) => $"Unreadable: {why}";
    public static string SavedTo(string slot) => $"Saved to {slot}.";

    // --- query card ---
    public static string TileAt(int x, int y) => $"tile {x},{y}";
    public static string AbandonedTitle(ZoneKind zone) => $"Abandoned {ZoneName(zone).ToLowerInvariant()}";
    public static string ZoneLot(ZoneKind zone) => $"{ZoneName(zone)} lot";
    public const string EmptyLot = "empty lot";
    public const string ReadyToGrow = "Ready to grow";
    public const string ReadyToGrowSub = "Something will move in soon.";
    public static string NotGrowing(string why) => $"Not growing: {why}";
    public static string AtRisk(string why) => $"At risk: {why}";
    public static string AbandonedOver(string why) => $"Abandoned: left over {why}";
    public const string AbandonedRecovering = "Abandoned";
    public const string FixEveryRedLine = "Fix every red line and people move back in.";
    public const string TenantsReturning = "Conditions fixed. Tenants are returning.";
    public const string NothingToBuild = "nothing to build";
    public const string BadMonths = "a run of bad months";
    public static string Why(DeclineReason reason) => reason switch
    {
        DeclineReason.NoPower => "no power",
        DeclineReason.NoRoad => "no road",
        DeclineReason.LowDemand => "no demand",
        _ => "",
    };

    public const string CheckPower = "Power";
    public const string PowerOk = "Connected";
    public const string PowerBad = "No power";
    public const string PowerFix = "String a power line to a plant, or zone next to a powered lot.";
    public const string CheckRoad = "Road access";
    public static string RoadOk(int reach) => $"Within {reach} tiles";
    public const string RoadBad = "None nearby";
    public static string RoadFix(int reach) => $"Lay a road within {reach} tiles.";
    public static string CheckDemand(ZoneKind zone) => $"{ZoneName(zone)} demand";
    public static string DemandValue(int demand) => demand > 0 ? $"+{demand / 10}" : $"{demand / 10}";
    public static string DemandFix(ZoneKind zone) => zone switch
    {
        ZoneKind.Residential => "More jobs bring more residents. Or lower the residential tax.",
        ZoneKind.Commercial => "Shops follow residents. Or lower the commercial tax.",
        _ => "Factories follow workers. Or lower the industrial tax.",
    };

    public static string Occupants(ZoneKind zone, int n) => zone == ZoneKind.Residential ? $"Residents {n:N0}" : $"Jobs {n:N0}";
    public static string OccupantsWas(ZoneKind zone, int n, int was) => $"{Occupants(zone, n)} · was {was:N0}";
    public static string RoadNote(long upkeep) => upkeep > 0 ? $"Upkeep {Money(upkeep)}/mo per tile." : "Carries traffic to the zones beside it.";
    public const string LineNote = "Carries power between plants and zones.";
    public const string RoadLineNote = "A road with a power line strung over it.";
    public static string PlantNote(long demand, long supply) => $"The grid uses {demand:N0} of {supply:N0} power.";
    public const string EmptyNote = "Open land. Zone it, or lay a road.";

    /// <summary>A content id as a name: <c>base:house_small</c> becomes "House small".</summary>
    public static string BuildingName(string id)
    {
        var name = id[(id.IndexOf(':') + 1)..].Replace('_', ' ');
        return name.Length == 0 ? id : char.ToUpperInvariant(name[0]) + name[1..];
    }
}
