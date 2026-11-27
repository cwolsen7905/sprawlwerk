using Godot;
using SprawlWerk.Sim;

namespace SprawlWerk;

/// <summary>
/// Tile colours, kept in one place. The hues come from the Okabe-Ito set,
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
}
