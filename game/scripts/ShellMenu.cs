using Godot;

namespace SprawlWerk;

/// <summary>
/// The minimal game shell over the debug view: a title menu (New city, Quit)
/// and a pause overlay (Resume, Quit). Escape cancels a drag first; otherwise
/// it opens or closes the pause overlay. While a menu is up the city view
/// is inactive, so no time passes and clicks do not reach the map.
/// Built in code so the scene stays small; a settings screen comes later.
/// </summary>
public partial class ShellMenu : CanvasLayer
{
    private CityView _city = null!;
    private Control _title = null!;
    private Control _pause = null!;
    private bool _playing;

    public override void _Ready()
    {
        _city = GetNode<CityView>("../CityView");
        _title = BuildPanel(Strings.GameTitle, (Strings.MenuNew, StartNew), (Strings.MenuQuit, Quit));
        _pause = BuildPanel(Strings.MenuPaused, (Strings.MenuResume, Resume), (Strings.MenuQuit, Quit));
        AddChild(_title);
        AddChild(_pause);
        ShowPanel(_title);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        if (!_playing) return;

        if (_pause.Visible) Resume();
        else if (!_city.CancelDrag()) ShowPanel(_pause);
        GetViewport().SetInputAsHandled();
    }

    private void StartNew()
    {
        _city.NewCity();
        _playing = true;
        ShowPanel(null);
    }

    private void Resume() => ShowPanel(null);

    private void Quit() => GetTree().Quit();

    // Only one panel at a time; none means the game is running.
    private void ShowPanel(Control? panel)
    {
        _title.Visible = panel == _title;
        _pause.Visible = panel == _pause;
        _city.Active = panel is null;
    }

    private Control BuildPanel(string heading, params (string Text, System.Action OnPress)[] buttons)
    {
        var root = new Control { Visible = false };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var box = new VBoxContainer { CustomMinimumSize = new Vector2(220, 0) };
        box.AddThemeConstantOverride("separation", 10);
        center.AddChild(box);

        box.AddChild(new Label { Text = heading, HorizontalAlignment = HorizontalAlignment.Center });
        foreach (var (text, onPress) in buttons)
        {
            var button = new Button { Text = text };
            var action = onPress;
            button.Pressed += () => action();
            box.AddChild(button);
        }
        return root;
    }
}
