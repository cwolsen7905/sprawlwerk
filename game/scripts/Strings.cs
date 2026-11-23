namespace SprawlWerk;

/// <summary>
/// Player-facing text in one place, so views never hold string literals and
/// a translation table can replace this later.
/// </summary>
public static class Strings
{
    public const string ToolHelp = "1 Res  2 Com  3 Ind  4 Road  5 Line  6 Plant  0 Bulldoze";
    public const string TimeHelp = "Space pause  - slower  = faster";
    public const string Paused = "PAUSED";
    public const string AlreadyBuilt = "already built";
    public const string OffTheMap = "off the map";

    public static string Hud(long funds, string tool, string date, int powered, string speed) =>
        $"Funds: ${funds:N0}    Tool: {tool}    {date}    Speed: {speed}    Powered tiles: {powered}";

    public static string NeedsFunds(long cost) => $"needs ${cost:N0}, not enough funds";
    public static string Cost(long cost) => $"Here: ${cost:N0}";
    public static string Here(string why) => $"Here: {why}";
    public static string Refused(string why) => $"Refused: {why}";
}
