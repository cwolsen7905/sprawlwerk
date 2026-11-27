using System;
using Godot;
using Ui = SprawlWerk.Palette.Ui;

namespace SprawlWerk;

/// <summary>
/// Small builders the HUD and its windows share, so layout code reads as
/// layout: labels in a theme style, padded boxes, swatches, dividers, LCD
/// readouts and the window frame (teal header, scrolling body, ink backdrop).
/// </summary>
public static class UiKit
{
    public static Label Text(string text, string style = "", Color? color = null, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, ThemeTypeVariation = style, HorizontalAlignment = align, MouseFilter = Control.MouseFilterEnum.Ignore };
        if (color is { } c) label.AddThemeColorOverride("font_color", c);
        return label;
    }

    public static Label Upper(string text, string style, Color? color = null)
    {
        var label = Text(text, style, color);
        label.Uppercase = true;
        return label;
    }

    public static PanelContainer Box(StyleBox style, Control? child = null)
    {
        var box = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeStyleboxOverride("panel", style);
        if (child is not null) box.AddChild(child);
        return box;
    }

    public static HBoxContainer Row(int gap, params Control[] children)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", gap);
        foreach (var c in children) row.AddChild(c);
        return row;
    }

    public static VBoxContainer Column(int gap, params Control[] children)
    {
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", gap);
        foreach (var c in children) column.AddChild(c);
        return column;
    }

    public static Control Spacer() => new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };

    /// <summary>A horizontal rule: hairline dividers between rows, 2 px ink under column headings.</summary>
    public static ColorRect Rule(Color color, int thickness = 1) =>
        new() { Color = color, CustomMinimumSize = new Vector2(0, thickness), MouseFilter = Control.MouseFilterEnum.Ignore };

    /// <summary>A colour swatch with an ink border and a letter or icon on it.</summary>
    public static PanelContainer Swatch(Color color, int size, string? letter = null, string? icon = null, Color? onColor = null)
    {
        var box = Box(UiTheme.Box(color, Ui.Ink, 2, 4));
        box.CustomMinimumSize = new Vector2(size, size);
        var fg = onColor ?? Colors.White;
        if (icon is not null)
        {
            var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            center.AddChild(UiIcons.Rect(icon, fg, size * 0.6f));
            box.AddChild(center);
        }
        else if (letter is not null)
        {
            var label = Text(letter, UiTheme.LcdSmall, fg, HorizontalAlignment.Center);
            label.VerticalAlignment = VerticalAlignment.Center;
            box.AddChild(label);
        }
        return box;
    }

    /// <summary>An 18 px pass/fail circle with a check or a cross.</summary>
    public static PanelContainer Mark(bool ok)
    {
        var style = UiTheme.Box(ok ? Ui.Primary : Ui.Danger, Ui.Ink, 0, 9);
        var box = Box(style);
        box.CustomMinimumSize = new Vector2(18, 18);
        box.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.AddChild(UiIcons.Rect(ok ? "check" : "cross", Ui.OnPrimary, 13));
        box.AddChild(center);
        return box;
    }

    /// <summary>A labelled LCD readout: a small overline above a well. Returns the value label and the parts that change with state.</summary>
    public static Readout Readout(string caption, float minWidth, string valueStyle = UiTheme.LcdLabel)
    {
        var label = Upper(caption, UiTheme.Micro);
        var value = Text("", valueStyle, align: HorizontalAlignment.Right);
        var well = Box(UiTheme.WellBox(), value);
        well.CustomMinimumSize = new Vector2(minWidth, 0);
        var column = Column(1, label, well);
        column.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        return new Readout(column, label, well, value);
    }

    /// <summary>
    /// A centred window: ink backdrop at 55%, a panel at most 820 wide with a teal
    /// header (title, an optional tag, a close button) and a scrolling body.
    /// </summary>
    public static WindowFrame Window(string title, Control? tag, Action close)
    {
        var root = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var backdrop = new ColorRect { Color = Ui.Backdrop, MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) close(); };
        root.AddChild(backdrop);

        var panel = new PanelContainer { ThemeTypeVariation = UiTheme.WindowPanel, MouseFilter = Control.MouseFilterEnum.Stop };
        root.AddChild(panel);

        var column = Column(0);
        panel.AddChild(column);

        var head = UiTheme.Box(Ui.Primary, Ui.Ink, 0, 0, 0, 16, 10);
        head.BorderWidthBottom = 2;
        head.CornerRadiusTopLeft = head.CornerRadiusTopRight = UiTheme.PanelRadius - 2;
        var closeButton = new Button
        {
            ThemeTypeVariation = UiTheme.CloseButton,
            Icon = UiIcons.Get("cross"),
            CustomMinimumSize = new Vector2(32, 32),
            FocusMode = Control.FocusModeEnum.None,
            IconAlignment = HorizontalAlignment.Center,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        closeButton.Pressed += close;
        var titleLabel = Text(title, UiTheme.WindowTitle);
        titleLabel.VerticalAlignment = VerticalAlignment.Center;
        var headRow = Row(12, titleLabel);
        if (tag is not null)
        {
            tag.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            headRow.AddChild(tag);
        }
        headRow.AddChild(Spacer());
        headRow.AddChild(closeButton);
        column.AddChild(Box(head, headRow));

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        column.AddChild(scroll);
        var margin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var side in new[] { "left", "right" }) margin.AddThemeConstantOverride($"margin_{side}", 20);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        scroll.AddChild(margin);
        var body = Column(16);
        body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        margin.AddChild(body);

        return new WindowFrame(root, panel, scroll, body);
    }

    /// <summary>A small LCD tag, as on the window header ("SIM PAUSED").</summary>
    public static PanelContainer Tag(string text)
    {
        var style = UiTheme.WellBox(padX: 8);
        style.ContentMarginTop = 1;
        style.BorderWidthTop = 0;
        return Box(style, Text(text, UiTheme.LcdSmall));
    }
}

public sealed record Readout(VBoxContainer Root, Label Caption, PanelContainer Well, Label Value);

/// <summary>A window built by <see cref="UiKit.Window"/>: lay it out with <see cref="Fit"/> whenever the logical size changes.</summary>
public sealed record WindowFrame(Control Root, PanelContainer Panel, ScrollContainer Scroll, VBoxContainer Body)
{
    /// <summary>Width min(820, W - 48), height at most H - 48, centred in a logical screen of this size.</summary>
    public void Fit(Vector2 screen)
    {
        float width = Mathf.Min(820, screen.X - 48);
        var header = (Control)Panel.GetChild(0).GetChild(0);
        var content = (Control)Scroll.GetChild(0);
        float room = screen.Y - 48 - header.GetCombinedMinimumSize().Y - UiTheme.PanelBorder * 2;
        float height = Mathf.Max(0, Mathf.Min(content.GetCombinedMinimumSize().Y, room));
        if (!Mathf.IsEqualApprox(Scroll.CustomMinimumSize.Y, height)) Scroll.CustomMinimumSize = new Vector2(0, height);
        Panel.CustomMinimumSize = new Vector2(width, 0);
        Panel.Size = Panel.GetCombinedMinimumSize();
        Panel.Position = ((screen - Panel.Size) / 2).Floor();
    }
}
