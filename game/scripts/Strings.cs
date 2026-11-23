namespace SprawlWerk;

/// <summary>
/// Player-facing text in one place, so views never hold string literals and
/// a translation table can replace this later.
/// </summary>
public static class Strings
{
    public const string ToolHelp = "1 Res  2 Com  3 Ind  4 Road  5 Line  6 Plant  0 Bulldoze";
    public const string TimeHelp = "Space pause  - slower  = faster";
    public const string GameTitle = "SprawlWerk";
    public const string MenuNew = "New city";
    public const string MenuResume = "Resume";
    public const string MenuQuit = "Quit";
    public const string MenuPaused = "Paused";
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
