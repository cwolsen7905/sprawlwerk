using System.Collections.Generic;
using Godot;
using Ui = SprawlWerk.Palette.Ui;

namespace SprawlWerk;

/// <summary>
/// The one Godot <see cref="Theme"/> for the interface, built in code from the
/// design tokens (<see cref="Palette.Ui"/>, <see cref="UiFonts"/>): paper panels
/// with a 2 px ink border, radius 8 and a hard ink shadow; buttons that drop by
/// their shadow height when pressed and only change colour on hover; LCD wells
/// for numbers; sliders and toggles. Set on the root window, so every control
/// (HUD, menus, save browser) picks it up. Controls choose a look with
/// <see cref="Control.ThemeTypeVariation"/> and the names below.
///
/// Hard shadows: the shadow colour is the border colour (ink), so a shadow
/// of y px is drawn as a bottom border y px thicker, grown y px below the rect
/// (<see cref="StyleBox"/> expand margin). Layout is untouched and the edge stays
/// sharp, which a blurred StyleBoxFlat shadow cannot do.
/// </summary>
public static class UiTheme
{
    public const string PrimaryButton = "PrimaryButton";
    public const string ToolButton = "ToolButton";
    public const string SegmentButton = "SegmentButton";
    public const string CloseButton = "CloseButton";
    public const string HudPanel = "HudPanel";
    public const string BarPanel = "BarPanel";
    public const string WindowPanel = "WindowPanel";
    public const string CardPanel = "CardPanel";
    public const string LcdPanel = "LcdPanel";
    public const string LcdLabel = "LcdLabel";
    public const string LcdSmall = "LcdSmall";
    public const string LcdLarge = "LcdLarge";
    public const string Heading = "Heading";
    public const string WindowTitle = "WindowTitle";
    public const string Wordmark = "Wordmark";
    public const string Strong = "Strong";
    public const string Small = "Small";
    public const string Overline = "Overline";
    public const string Micro = "Micro";
    public const string Danger = "Danger";

    public const int PanelBorder = 2;
    public const int PanelRadius = 8;
    public const int PanelShadow = 4;
    public const int WindowShadow = 6;
    public const int ButtonRadius = 5;
    public const int ControlShadow = 2;
    public const int PrimaryShadow = 3;

    private static Theme? _theme;

    public static Theme Theme => _theme ??= Build();

    // --- style boxes ---

    /// <summary>A flat box with an optional hard shadow below it (see the class notes).</summary>
    public static StyleBoxFlat Box(Color bg, Color? border = null, int borderWidth = PanelBorder, int radius = PanelRadius,
        int shadow = 0, int padX = 0, int padY = 0)
    {
        var box = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border ?? Ui.Ink,
            AntiAliasing = true,
            ContentMarginLeft = padX,
            ContentMarginRight = padX,
            ContentMarginTop = padY,
            ContentMarginBottom = padY,
        };
        box.SetCornerRadiusAll(radius);
        box.SetBorderWidthAll(borderWidth);
        if (shadow > 0)
        {
            box.BorderWidthBottom = borderWidth + shadow;
            box.ExpandMarginBottom = shadow;
        }
        return box;
    }

    /// <summary>The pressed look of a box with a shadow: the face moves down onto where its shadow was.</summary>
    public static StyleBoxFlat Pressed(StyleBoxFlat raised, Color bg, int shadow)
    {
        var box = (StyleBoxFlat)raised.Duplicate();
        box.BgColor = bg;
        box.BorderWidthBottom = box.BorderWidthTop;
        box.ExpandMarginTop = -shadow;
        box.ExpandMarginBottom = shadow;
        box.ContentMarginTop += shadow;
        box.ContentMarginBottom -= shadow;
        return box;
    }

    /// <summary>The panel every HUD piece sits on.</summary>
    public static StyleBoxFlat PanelBox(int pad = 8) => Box(Ui.Panel, Ui.Ink, PanelBorder, PanelRadius, PanelShadow, pad, pad);

    /// <summary>An LCD well; <paramref name="ring"/> draws the 2 px in-debt ring outside it.</summary>
    public static StyleBoxFlat WellBox(Color? bg = null, Color? ring = null, int padX = 10, int padY = 0)
    {
        var box = new StyleBoxFlat
        {
            BgColor = bg ?? Ui.LcdWell,
            AntiAliasing = true,
            ContentMarginLeft = padX,
            ContentMarginRight = padX,
            ContentMarginTop = padY + 2,
            ContentMarginBottom = padY,
        };
        box.SetCornerRadiusAll(3);
        // The inset shadow: a 2 px darker band along the top edge.
        box.BorderWidthTop = 2;
        box.BorderColor = (bg ?? Ui.LcdWell).Darkened(0.5f);
        if (ring is { } r)
        {
            box.BorderColor = r;
            box.SetBorderWidthAll(2);
            box.SetExpandMarginAll(2);
        }
        return box;
    }

    // --- theme ---

    private static Theme Build()
    {
        var t = new Theme
        {
            DefaultFont = UiFonts.Body,
            DefaultFontSize = 14,
        };

        // Labels.
        t.SetColor("font_color", "Label", Ui.Ink);
        Label(t, Heading, UiFonts.Heavy, 16, Ui.Ink);
        Label(t, Strong, UiFonts.Bold, 14, Ui.Ink);
        Label(t, Danger, UiFonts.Bold, 14, Ui.DangerText);
        Label(t, Small, UiFonts.Body, 12, Ui.Muted);
        Label(t, Overline, Spaced(UiFonts.Bold, 1), 11, Ui.Muted);
        Label(t, Micro, Spaced(UiFonts.Heavy, 1), 10, Ui.Muted);
        Label(t, WindowTitle, UiFonts.Title, 19, Ui.OnPrimary);
        Label(t, Wordmark, UiFonts.Title, 17, Ui.OnPrimary);
        Label(t, LcdLabel, UiFonts.Lcd, 24, Ui.LcdText);
        Label(t, LcdSmall, UiFonts.Lcd, 18, Ui.LcdText);
        Label(t, LcdLarge, UiFonts.Lcd, 28, Ui.LcdText);

        // Panels.
        t.SetStylebox("panel", "PanelContainer", PanelBox());
        t.SetTypeVariation(HudPanel, "PanelContainer");
        t.SetStylebox("panel", HudPanel, PanelBox());
        t.SetTypeVariation(CardPanel, "PanelContainer");
        t.SetStylebox("panel", CardPanel, Box(Ui.PanelHi, Ui.Ink, PanelBorder, PanelRadius, PanelShadow, 12, 10));
        t.SetTypeVariation(WindowPanel, "PanelContainer");
        t.SetStylebox("panel", WindowPanel, Box(Ui.Panel, Ui.Ink, PanelBorder, PanelRadius, WindowShadow));
        t.SetTypeVariation(LcdPanel, "PanelContainer");
        t.SetStylebox("panel", LcdPanel, WellBox());
        // Full-width bars: no radius, no shadow, the border only on the edge facing the city.
        t.SetTypeVariation(BarPanel, "PanelContainer");
        t.SetStylebox("panel", BarPanel, Box(Ui.Panel, Ui.Ink, 0, 0, 0, 12, 0));

        // Buttons: secondary by default.
        ButtonStyles(t, "Button", Ui.PanelHi, Ui.PanelLo, Ui.PanelLo, Ui.Ink, PrimaryShadow);
        t.SetTypeVariation(PrimaryButton, "Button");
        ButtonStyles(t, PrimaryButton, Ui.Primary, Ui.PrimaryHover, Ui.PrimaryPress, Ui.OnPrimary, PrimaryShadow);
        foreach (var type in new[] { "Button", PrimaryButton })
        {
            t.SetFont("font", type, UiFonts.Heavy);
            t.SetFontSize("font_size", type, 14);
        }

        // Tool and menu buttons: flat until hovered or selected (a toggle's pressed state is "selected").
        t.SetTypeVariation(ToolButton, "Button");
        var clear = Box(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), 2, 6);
        t.SetStylebox("normal", ToolButton, clear);
        t.SetStylebox("hover", ToolButton, Box(Ui.PanelLo, Ui.PanelLo, 2, 6));
        t.SetStylebox("pressed", ToolButton, Box(Ui.Primary, Ui.Ink, 2, 6));
        t.SetStylebox("hover_pressed", ToolButton, Box(Ui.PrimaryHover, Ui.Ink, 2, 6));
        t.SetStylebox("disabled", ToolButton, clear);
        t.SetStylebox("focus", ToolButton, new StyleBoxEmpty());
        ButtonColors(t, ToolButton, Ui.Ink, Ui.OnPrimary);
        t.SetConstant("icon_max_width", ToolButton, 22);

        // Segmented control cells (the group draws the outer border and shadow).
        t.SetTypeVariation(SegmentButton, "Button");
        t.SetStylebox("normal", SegmentButton, Segment(Ui.PanelHi));
        t.SetStylebox("hover", SegmentButton, Segment(Ui.PanelLo));
        t.SetStylebox("pressed", SegmentButton, Segment(Ui.Primary));
        t.SetStylebox("hover_pressed", SegmentButton, Segment(Ui.PrimaryHover));
        t.SetStylebox("disabled", SegmentButton, Segment(Ui.Panel));
        t.SetStylebox("focus", SegmentButton, new StyleBoxEmpty());
        ButtonColors(t, SegmentButton, Ui.Ink, Ui.OnPrimary);
        t.SetFont("font", SegmentButton, UiFonts.Heavy);
        t.SetFontSize("font_size", SegmentButton, 10);
        t.SetConstant("icon_max_width", SegmentButton, 18);

        // The window close button: 32 px, cream on the teal header.
        t.SetTypeVariation(CloseButton, "Button");
        var close = Box(Ui.PanelHi, Ui.Ink, 2, ButtonRadius, ControlShadow, 6, 4);
        t.SetStylebox("normal", CloseButton, close);
        t.SetStylebox("hover", CloseButton, Box(Ui.PanelLo, Ui.Ink, 2, ButtonRadius, ControlShadow, 6, 4));
        t.SetStylebox("pressed", CloseButton, Pressed(close, Ui.PanelLo, ControlShadow));
        t.SetStylebox("focus", CloseButton, new StyleBoxEmpty());
        ButtonColors(t, CloseButton, Ui.Ink, Ui.Ink);
        t.SetConstant("icon_max_width", CloseButton, 16);

        // Sliders: a 6 px track, teal fill, a 14 x 20 thumb with a 2 px shadow.
        var track = Box(Ui.ToggleOff, Ui.Ink, 2, 3);
        track.ContentMarginTop = track.ContentMarginBottom = 3;
        t.SetStylebox("slider", "HSlider", track);
        var fill = Box(Ui.Primary, Ui.Ink, 2, 3);
        fill.ContentMarginTop = fill.ContentMarginBottom = 3;
        t.SetStylebox("grabber_area", "HSlider", fill);
        var fillHover = (StyleBoxFlat)fill.Duplicate();
        fillHover.BgColor = Ui.PrimaryHover;
        t.SetStylebox("grabber_area_highlight", "HSlider", fillHover);
        t.SetIcon("grabber", "HSlider", Shapes.Thumb(Ui.PanelHi, shadow: true));
        t.SetIcon("grabber_highlight", "HSlider", Shapes.Thumb(Ui.PanelLo, shadow: true));
        t.SetIcon("grabber_disabled", "HSlider", Shapes.Thumb(Ui.Panel, shadow: false));
        t.SetStylebox("focus", "HSlider", new StyleBoxEmpty());

        // Toggles: 44 x 24.
        t.SetIcon("checked", "CheckButton", Shapes.Toggle(on: true));
        t.SetIcon("unchecked", "CheckButton", Shapes.Toggle(on: false));
        t.SetColor("font_color", "CheckButton", Ui.Ink);
        t.SetColor("font_hover_color", "CheckButton", Ui.Ink);
        t.SetColor("font_pressed_color", "CheckButton", Ui.Ink);
        t.SetColor("font_hover_pressed_color", "CheckButton", Ui.Ink);
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus" })
            t.SetStylebox(state, "CheckButton", new StyleBoxEmpty());

        // Inputs and lists (the save browser).
        var input = Box(Ui.PanelHi, Ui.Ink, 2, ButtonRadius, 0, 8, 6);
        t.SetStylebox("normal", "LineEdit", input);
        t.SetStylebox("focus", "LineEdit", Box(new Color(0, 0, 0, 0), Ui.Primary, 2, ButtonRadius));
        t.SetColor("font_color", "LineEdit", Ui.Ink);
        t.SetColor("font_placeholder_color", "LineEdit", Ui.Disabled);
        t.SetColor("caret_color", "LineEdit", Ui.Ink);
        t.SetColor("selection_color", "LineEdit", Ui.PrimaryHover);
        t.SetStylebox("panel", "ItemList", Box(Ui.PanelHi, Ui.Ink, 2, ButtonRadius, 0, 6, 6));
        t.SetStylebox("focus", "ItemList", new StyleBoxEmpty());
        t.SetStylebox("selected", "ItemList", Box(Ui.Primary, Ui.Primary, 0, 3));
        t.SetStylebox("selected_focus", "ItemList", Box(Ui.Primary, Ui.Primary, 0, 3));
        t.SetStylebox("hovered", "ItemList", Box(Ui.PanelLo, Ui.PanelLo, 0, 3));
        t.SetColor("font_color", "ItemList", Ui.Ink);
        t.SetColor("font_hovered_color", "ItemList", Ui.Ink);
        t.SetColor("font_selected_color", "ItemList", Ui.OnPrimary);
        t.SetColor("font_disabled_color", "ItemList", Ui.Disabled);

        // Scroll bars inside windows.
        var grab = Box(Ui.Muted, Ui.Muted, 0, 3);
        grab.ContentMarginLeft = grab.ContentMarginRight = 3;
        t.SetStylebox("grabber", "VScrollBar", grab);
        t.SetStylebox("grabber_highlight", "VScrollBar", Box(Ui.Ink, Ui.Ink, 0, 3));
        t.SetStylebox("grabber_pressed", "VScrollBar", Box(Ui.Ink, Ui.Ink, 0, 3));
        var scroll = Box(Ui.PanelLo, Ui.PanelLo, 0, 3);
        scroll.ContentMarginLeft = scroll.ContentMarginRight = 3;
        t.SetStylebox("scroll", "VScrollBar", scroll);

        // Tooltips and separators.
        t.SetStylebox("panel", "TooltipPanel", Box(Ui.PanelHi, Ui.Ink, 2, ButtonRadius, ControlShadow, 8, 4));
        t.SetColor("font_color", "TooltipLabel", Ui.Ink);
        var line = new StyleBoxLine { Color = Ui.Divider, Thickness = 1 };
        t.SetStylebox("separator", "HSeparator", line);
        t.SetConstant("separation", "HSeparator", 1);
        return t;
    }

    private static void Label(Theme t, string type, Font font, int size, Color color)
    {
        t.SetTypeVariation(type, "Label");
        t.SetFont("font", type, font);
        t.SetFontSize("font_size", type, size);
        t.SetColor("font_color", type, color);
    }

    private static Font Spaced(Font font, int spacing) => new FontVariation { BaseFont = font, SpacingGlyph = spacing };

    private static void ButtonStyles(Theme t, string type, Color bg, Color hover, Color press, Color text, int shadow)
    {
        var normal = Box(bg, Ui.Ink, 2, ButtonRadius, shadow, 18, 9);
        t.SetStylebox("normal", type, normal);
        t.SetStylebox("hover", type, Box(hover, Ui.Ink, 2, ButtonRadius, shadow, 18, 9));
        t.SetStylebox("pressed", type, Pressed(normal, press, shadow));
        t.SetStylebox("hover_pressed", type, Pressed(normal, press, shadow));
        t.SetStylebox("disabled", type, Box(Ui.PanelLo, Ui.Disabled, 2, ButtonRadius, 0, 18, 9));
        t.SetStylebox("focus", type, new StyleBoxEmpty());
        ButtonColors(t, type, text, text);
    }

    private static void ButtonColors(Theme t, string type, Color text, Color pressedText)
    {
        foreach (var (prefix, color) in new[] { ("font", text), ("icon", text) })
        {
            t.SetColor($"{prefix}_color", type, color);
            t.SetColor($"{prefix}_normal_color", type, color);
            t.SetColor($"{prefix}_hover_color", type, color);
            t.SetColor($"{prefix}_focus_color", type, color);
            t.SetColor($"{prefix}_pressed_color", type, pressedText);
            t.SetColor($"{prefix}_hover_pressed_color", type, pressedText);
            t.SetColor($"{prefix}_disabled_color", type, Ui.Disabled);
        }
    }

    private static StyleBoxFlat Segment(Color bg)
    {
        var box = new StyleBoxFlat { BgColor = bg, BorderColor = Ui.Ink, ContentMarginTop = 4, ContentMarginBottom = 3 };
        return box;
    }

    /// <summary>A segment cell's look with its outer corners rounded and a divider on its right (all but the last).</summary>
    public static void StyleSegment(Button button, bool first, bool last)
    {
        foreach (var (state, bg) in new[]
                 {
                     ("normal", Ui.PanelHi), ("hover", Ui.PanelLo), ("pressed", Ui.Primary),
                     ("hover_pressed", Ui.PrimaryHover), ("disabled", Ui.Panel),
                 })
        {
            var box = Segment(bg);
            if (!last) box.BorderWidthRight = 2;
            if (first) box.CornerRadiusTopLeft = box.CornerRadiusBottomLeft = 4;
            if (last) box.CornerRadiusTopRight = box.CornerRadiusBottomRight = 4;
            button.AddThemeStyleboxOverride(state, box);
        }
    }
}

/// <summary>
/// Small shapes the theme needs as textures (slider thumb, toggle), drawn
/// once at 3x and shown at their logical size, so they stay sharp when the
/// interface is scaled up.
/// </summary>
public static class Shapes
{
    private const int Oversample = 3;

    public static Texture2D Thumb(Color face, bool shadow)
    {
        const int w = 14, h = 20, s = UiTheme.ControlShadow;
        return Draw(w, h + s, (x, y) =>
        {
            var sh = shadow ? RoundRect(x, y - s, w, h, 3) : 0f;
            var outer = RoundRect(x, y, w, h, 3);
            var inner = RoundRect(x - 2, y - 2, w - 4, h - 4, 1.5f);
            var c = new Color(Palette.Ui.Ink, Mathf.Max(outer, sh));
            return inner > 0 ? c.Blend(new Color(face, inner)) : c;
        });
    }

    public static Texture2D Toggle(bool on)
    {
        const int w = 44, h = 24;
        var track = on ? Palette.Ui.Primary : Palette.Ui.ToggleOff;
        float knobX = on ? w - 12 : 12;
        return Draw(w, h, (x, y) =>
        {
            var outer = RoundRect(x, y, w, h, h / 2f);
            var inner = RoundRect(x - 2, y - 2, w - 4, h - 4, h / 2f - 2);
            var c = new Color(Palette.Ui.Ink, outer).Blend(new Color(track, inner));
            float d = new Vector2(x - knobX, y - h / 2f).Length();
            float knobEdge = Mathf.Clamp(8.5f - d, 0, 1);
            float knobFace = Mathf.Clamp(7f - d, 0, 1);
            c = c.Blend(new Color(Palette.Ui.Ink, knobEdge)).Blend(new Color(Palette.Ui.PanelHi, knobFace));
            return c;
        });
    }

    // Coverage (0..1) of a w x h rounded rectangle at the origin, at logical point (x, y).
    private static float RoundRect(float x, float y, float w, float h, float r)
    {
        var p = new Vector2(Mathf.Abs(x - w / 2f), Mathf.Abs(y - h / 2f));
        var q = p - new Vector2(w / 2f - r, h / 2f - r);
        float outside = new Vector2(Mathf.Max(q.X, 0), Mathf.Max(q.Y, 0)).Length() + Mathf.Min(Mathf.Max(q.X, q.Y), 0) - r;
        return Mathf.Clamp(0.5f - outside * Oversample, 0, 1);
    }

    private static Texture2D Draw(int w, int h, System.Func<float, float, Color> shade)
    {
        var image = Image.CreateEmpty(w * Oversample, h * Oversample, false, Image.Format.Rgba8);
        for (int py = 0; py < h * Oversample; py++)
        for (int px = 0; px < w * Oversample; px++)
            image.SetPixel(px, py, shade((px + 0.5f) / Oversample, (py + 0.5f) / Oversample));
        image.GenerateMipmaps();
        var texture = ImageTexture.CreateFromImage(image);
        texture.SetSizeOverride(new Vector2I(w, h));
        return texture;
    }
}

/// <summary>
/// The interface icons (<c>res://assets/ui/icons/*.svg</c>, 20 x 20, stroked in
/// currentColor). Godot imports them at 3x with mipmaps; here each is turned
/// white once, keeping its alpha, so a button's icon colour or a modulate tints
/// it to any token colour.
/// </summary>
public static class UiIcons
{
    private const string Folder = "res://assets/ui/icons/";
    private static readonly Dictionary<string, Texture2D?> Cache = new();

    public static Texture2D? Get(string name)
    {
        if (Cache.TryGetValue(name, out var cached)) return cached;
        Texture2D? result = null;
        if (GD.Load<Texture2D>($"{Folder}{name}.svg") is { } source && source.GetImage() is { } image)
        {
            if (image.IsCompressed()) image.Decompress();
            image.ClearMipmaps();
            image.Convert(Image.Format.Rgba8);
            for (int y = 0; y < image.GetHeight(); y++)
            for (int x = 0; x < image.GetWidth(); x++)
                image.SetPixel(x, y, new Color(1, 1, 1, image.GetPixel(x, y).A));
            image.GenerateMipmaps();
            var texture = ImageTexture.CreateFromImage(image);
            texture.SetSizeOverride(new Vector2I(20, 20));
            result = texture;
        }
        else
        {
            GD.PushWarning($"Missing icon {name}");
        }
        Cache[name] = result;
        return result;
    }

    /// <summary>A tinted icon at a logical size, for places that are not buttons.</summary>
    public static TextureRect Rect(string name, Color color, float size = 20) => new()
    {
        Texture = Get(name),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        CustomMinimumSize = new Vector2(size, size),
        SelfModulate = color,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };
}

/// <summary>
/// The interface scale (UI only; the 3D view renders at native resolution):
/// <c>auto = clamp(round(h / 864 * 4) / 4, 1, 3)</c>, then capped so the logical
/// height never drops below 640: 720p gives 1.0, 1080p 1.25, 1440p 1.75, 2160p 2.5.
/// </summary>
public static class UiScale
{
    public const float ReferenceHeight = 864f;
    public const float MinLogicalHeight = 640f;

    /// <param name="windowHeight">Window height in physical pixels.</param>
    /// <param name="userMultiplier">The player's choice (Auto is 1; a Later setting).</param>
    public static float For(float windowHeight, float userMultiplier = 1f)
    {
        float auto = Mathf.Clamp(Mathf.Round(windowHeight / ReferenceHeight * 4f) / 4f, 1f, 3f);
        return Mathf.Min(auto * userMultiplier, windowHeight / MinLogicalHeight);
    }
}
