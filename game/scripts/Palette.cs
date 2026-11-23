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

    public static Color For(TileKind kind) => kind switch
    {
        TileKind.Residential => new Color("009e73"), // bluish green
        TileKind.Commercial => new Color("56b4e9"),  // sky blue
        TileKind.Industrial => new Color("f0e442"),  // yellow
        TileKind.Road => new Color("4a505c"),        // neutral grey
        TileKind.PowerLine => new Color("cc79a7"),   // reddish purple
        TileKind.PowerPlant => new Color("d55e00"),  // vermillion
        _ => Ground,
    };
}
