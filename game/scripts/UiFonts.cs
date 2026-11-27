using Godot;

namespace SprawlWerk;

/// <summary>
/// The interface fonts: Archivo (a variable font, width and weight axes) for
/// all text, VT323 (a pixel font) for numbers and readouts only. Both are SIL
/// OFL 1.1. They are loaded from <c>res://assets/fonts/</c> when present; until
/// they are added, every style falls back to Godot's default font (bolded for
/// the heavy styles) and the readouts to the system monospace font, so the game
/// runs either way. Loaded once, on first use.
/// </summary>
public static class UiFonts
{
    public const string ArchivoPath = "res://assets/fonts/Archivo[wdth,wght].ttf";
    public const string Vt323Path = "res://assets/fonts/VT323-Regular.ttf";

    private static bool _loaded;
    private static Font _title = null!;
    private static Font _heavy = null!;
    private static Font _bold = null!;
    private static Font _body = null!;
    private static Font _lcd = null!;

    /// <summary>Wordmark and window titles: width 125, weight 800.</summary>
    public static Font Title { get { Load(); return _title; } }

    /// <summary>Headings and micro labels: weight 800.</summary>
    public static Font Heavy { get { Load(); return _heavy; } }

    /// <summary>Strong body text and overlines: weight 700.</summary>
    public static Font Bold { get { Load(); return _bold; } }

    /// <summary>Body and small text: weight 500. The theme's default font.</summary>
    public static Font Body { get { Load(); return _body; } }

    /// <summary>Numbers: VT323, crisp at whole sizes (18, 24, 28).</summary>
    public static Font Lcd { get { Load(); return _lcd; } }

    /// <summary>True when the bundled fonts were found (false means fallbacks are in use).</summary>
    public static bool Bundled { get; private set; }

    private static void Load()
    {
        if (_loaded) return;
        _loaded = true;

        if (ResourceLoader.Exists(ArchivoPath) && GD.Load<FontFile>(ArchivoPath) is { } archivo)
        {
            _title = Variation(archivo, width: 125, weight: 800);
            _heavy = Variation(archivo, width: 100, weight: 800);
            _bold = Variation(archivo, width: 100, weight: 700);
            _body = Variation(archivo, width: 100, weight: 500);
            Bundled = true;
        }
        else
        {
            var fallback = ThemeDB.FallbackFont;
            _title = new FontVariation { BaseFont = fallback, VariationEmbolden = 0.9f, SpacingGlyph = 1 };
            _heavy = new FontVariation { BaseFont = fallback, VariationEmbolden = 0.8f };
            _bold = new FontVariation { BaseFont = fallback, VariationEmbolden = 0.5f };
            _body = fallback;
        }

        if (ResourceLoader.Exists(Vt323Path) && GD.Load<FontFile>(Vt323Path) is { } vt)
        {
            // A pixel font: no hinting, no subpixel positioning, or it smears.
            vt.Hinting = TextServer.Hinting.None;
            vt.SubpixelPositioning = TextServer.SubpixelPositioning.Disabled;
            _lcd = vt;
        }
        else
        {
            Bundled = false;
            _lcd = new SystemFont { FontNames = ["Menlo", "Consolas", "DejaVu Sans Mono", "monospace"] };
        }
    }

    private static FontVariation Variation(Font font, int width, int weight) => new()
    {
        BaseFont = font,
        VariationOpentype = new Godot.Collections.Dictionary
        {
            [TextServerManager.GetPrimaryInterface().NameToTag("wdth")] = width,
            [TextServerManager.GetPrimaryInterface().NameToTag("wght")] = weight,
        },
    };
}
