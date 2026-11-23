namespace SprawlWerk;

/// <summary>
/// Player-facing text in one place, so views never hold string literals and
/// a translation table can replace this later.
/// </summary>
public static class Strings
{
    public const string ToolHelp = "1 Res  2 Com  3 Ind  4 Road  5 Line  6 Plant  0 Bulldoze";
    public const string TimeHelp = "Space pause  - slower  = faster  F5 save  F9 load";
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
    public const string AlreadyBuilt = "already built";
    public const string OffTheMap = "off the map";

    public static string Hud(long funds, string tool, string date, int powered, string speed) =>
        $"Funds: ${funds:N0}    Tool: {tool}    {date}    Speed: {speed}    Powered tiles: {powered}";

    public static string PowerShort(int zones, long demand, long supply) =>
        $"Power short: {zones} zones dark (demand {demand}, supply {supply}); build another plant";

    public static string NeedsFunds(long cost) => $"needs ${cost:N0}, not enough funds";
    public static string Cost(long cost) => $"Here: ${cost:N0}";
    public static string Here(string why) => $"Here: {why}";
    public static string Refused(string why) => $"Refused: {why}";
}
