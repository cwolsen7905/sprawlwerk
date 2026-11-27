using SprawlWerk.Sim;

namespace SprawlWerk;

/// <summary>
/// Player-facing text in one place, so views never hold string literals and
/// a translation table can replace this later.
/// </summary>
public static class Strings
{
    public const string KeyHelp = "WASD/arrows or right-drag pan   wheel zoom   Q/E or middle-drag rotate   Space pause   - / = speed   F5 save   F9 load   F3 2D view   Esc menu";
    public const string GameTitle = "SprawlWerk";
    public const string MenuNew = "New city";
    public const string MenuResume = "Resume";
    public const string MenuQuit = "Quit";
    public const string MenuPaused = "Paused";
    public const string Saved = "Saved";
    public const string Loaded = "Loaded";
    public const string SaveFailed = "Could not write the save";
    public const string NoSave = "No saved city yet (F5 saves)";
    public static string LoadFailed(string why) => $"Could not load: {why}";
    public const string MenuLoad = "Load";
    public const string MenuContinue = "Continue";
    public const string Autosaved = "Autosaved";
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
    public const string Paused = "PAUSED";
    public const string VolumeMusic = "Music";
    public const string VolumeEffects = "Effects";
    public const string VolumeAmbience = "Ambience";
    public const string AlreadyBuilt = "already built";
    public const string OffTheMap = "off the map";

    public const string MenuButton = "Menu";
    public const string PauseButton = "||";
    public const string ToolsHeading = "Build";
    public static string Speed(int multiplier) => $"{multiplier}x";
    public static string Funds(long funds) => $"${funds:N0}";
    public static string Power(long demand, long supply) => $"Power {demand} / {supply}";
    public static string People(int residents, int jobs) => $"Residents {residents:N0}   Jobs {jobs:N0}";
    public static string DemandBars(int r, int c, int i) => $"Demand  R {Bar(r)}  C {Bar(c)}  I {Bar(i)}";

    // -1000..1000 as a five-step bar: below zero shows how far it is shrinking.
    private static string Bar(int demand) => demand switch
    {
        <= -500 => "--",
        < 0 => "-",
        0 => "0",
        < 250 => "+",
        < 500 => "++",
        < 750 => "+++",
        _ => "++++",
    };
    public static string LoadedWithChanges(int count) => $"Loaded ({count} buildings from missing content were removed)";

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

    public static string ToolButton(string name, long price) => $"{name}   ${price:N0}";

    public static string ToolTip(string key, string name, long price, bool perTile) =>
        perTile ? $"{name} ({key}): ${price:N0} per tile" : $"{name} ({key}): ${price:N0}";

    /// <summary>A content id as a name: <c>base:house_small</c> becomes "House small".</summary>
    public static string BuildingName(string id)
    {
        var name = id[(id.IndexOf(':') + 1)..].Replace('_', ' ');
        return name.Length == 0 ? id : char.ToUpperInvariant(name[0]) + name[1..];
    }

    public static string ZoneWaiting(string zone, string why) => why.Length == 0 ? zone : $"{zone}, {why}";

    public static string WhyNot(GrowthBlock block) => block switch
    {
        GrowthBlock.NoPower => "no power",
        GrowthBlock.NoRoad => "no road within reach",
        GrowthBlock.NoDemand => "no demand",
        GrowthBlock.NoBuildingType => "nothing to build",
        GrowthBlock.None => "ready to grow",
        _ => "",
    };

    public static string TileStatus(int x, int y, string kind, bool? powered) => powered switch
    {
        true => $"({x}, {y})  {kind}, powered",
        false => $"({x}, {y})  {kind}, no power",
        null => $"({x}, {y})  {kind}",
    };

    public static string PowerShort(int zones, long demand, long supply) =>
        $"Power short: {zones} zones dark (demand {demand}, supply {supply}); build another plant";

    public static string NeedsFunds(long cost) => $"needs ${cost:N0}, not enough funds";
    public static string Cost(long cost) => $"Here: ${cost:N0}";
    public static string Here(string why) => $"Here: {why}";
    public static string Refused(string why) => $"Refused: {why}";
}
