using Godot;
using SprawlWerk.Sim;
using SprawlWerk.Sim.Content;

namespace SprawlWerk;

/// <summary>
/// Colours, kept in one place: the map and world colours here, the interface
/// tokens in <see cref="Ui"/>. Tile colours: The hues come from the Okabe-Ito set,
/// chosen to stay distinguishable under the common kinds of colour-vision
/// deficiency; the zones also differ in lightness, so they hold up in greyscale.
/// </summary>
public static class Palette
{
    public static readonly Color Ground = new("26302a");

    // The 3D world. Sky, sun and grass follow the low-poly style test; the
    // zone hues stay the same as the 2D view's so the two views agree.
    public static readonly Color Grass = new("7da35a");
    public static readonly Color GrassOutside = new("5f8445");
    public static readonly Color GridLine = new("2f4a24");
    public static readonly Color SkyTop = new("6f9fd8");
    public static readonly Color SkyHorizon = new("d9e6f2");
    public static readonly Color Sunlight = new("fff2df");

    /// <summary>A zone's flat lot in the 3D view.</summary>
    public static Color Lot(TileKind kind) => For(kind).Darkened(0.2f);

    /// <summary>Zones without power are drawn dim, in both views: the first piece of feedback.</summary>
    public static Color Unpowered(Color color) => color.Darkened(0.55f);

    /// <summary>The lot of a building nobody lives or works in: grey, whatever its zone.</summary>
    public static readonly Color Abandoned = new("8a8a84");

    public static Color For(TileKind kind) => kind switch
    {
        TileKind.Residential => new Color("009e73"), // bluish green
        TileKind.Commercial => new Color("56b4e9"),  // sky blue
        TileKind.Industrial => new Color("f0e442"),  // yellow
        TileKind.Road => new Color("4a505c"),        // neutral grey
        TileKind.PowerLine => new Color("cc79a7"),   // reddish purple
        TileKind.RoadWithLine => new Color("7f6890"), // road grey tinted with the line purple
        TileKind.PowerPlant => new Color("d55e00"),  // vermillion
        _ => Ground,
    };

    /// <summary>
    /// The interface colour tokens from the UI design (tokens.json): warm paper
    /// panels, ink outlines and shadows, teal for action and selection, a dark
    /// green LCD for numbers. Zone colours always appear with their letter.
    /// <see cref="UiTheme"/> builds the Godot theme from these.
    /// </summary>
    public static class Ui
    {
        public static readonly Color Ink = new("2a2722");
        public static readonly Color Panel = new("efe6cf");
        public static readonly Color PanelHi = new("f6efdc");
        public static readonly Color PanelLo = new("e4d9be");
        public static readonly Color Divider = new("d6caaa");
        public static readonly Color Muted = new("6b6253");
        public static readonly Color Disabled = new("9c907a");
        public static readonly Color ToggleOff = new("cfc3a4");
        public static readonly Color Primary = new("1f6f6b");
        public static readonly Color PrimaryHover = new("25807b");
        public static readonly Color PrimaryPress = new("185955");
        public static readonly Color OnPrimary = new("f6efdc");
        public static readonly Color Accent = new("e07a2e");
        public static readonly Color Danger = new("c8433a");
        public static readonly Color DangerText = new("b23a2f");
        public static readonly Color LcdWell = new("1e2b26");
        public static readonly Color LcdText = new("a8e6b8");
        public static readonly Color LcdTrack = new("2e3d37");
        public static readonly Color LcdLow = new("f2c14e");
        public static readonly Color LcdOff = new("6e8c80");
        public static readonly Color LowLabel = new("8a6510");
        public static readonly Color DebtWell = new("5a1a14");
        public static readonly Color DebtText = new("ffb4a2");
        public static readonly Color OkBg = new("d6ebd9");
        public static readonly Color WarnBg = new("f3d9c8");
        public static readonly Color RiskBg = new("f6e3b4");
        public static readonly Color AbandonedBg = new("4a4038");
        public static readonly Color AbandonedSwatch = new("857d71");
        public static readonly Color Backdrop = new(0x2a / 255f, 0x27 / 255f, 0x22 / 255f, 0.55f);

        public static readonly Color ZoneR = new("009e73");
        public static readonly Color ZoneC = new("0072b2");
        public static readonly Color ZoneI = new("e69f00");
        public static readonly Color ZoneRLcd = new("2fc79a");
        public static readonly Color ZoneCLcd = new("56b4e9");
        public static readonly Color ZoneILcd = new("e69f00");

        /// <summary>Swatches for tools that are not zones: roads grey, power yellow, clearing brown.</summary>
        public static readonly Color RoadSwatch = new("5f5b56");
        public static readonly Color PowerSwatch = new("f2c14e");
        public static readonly Color BulldozeSwatch = new("a0715a");

        public static Color Zone(ZoneKind zone) => zone switch
        {
            ZoneKind.Residential => ZoneR,
            ZoneKind.Commercial => ZoneC,
            _ => ZoneI,
        };

        public static Color ZoneLcd(ZoneKind zone) => zone switch
        {
            ZoneKind.Residential => ZoneRLcd,
            ZoneKind.Commercial => ZoneCLcd,
            _ => ZoneILcd,
        };

        /// <summary>Text on a zone swatch: white on R and C, ink on I.</summary>
        public static Color OnZone(ZoneKind zone) => zone == ZoneKind.Industrial ? Ink : Colors.White;
    }
}
