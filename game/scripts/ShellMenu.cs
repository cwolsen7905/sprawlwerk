using System.Linq;
using Godot;
using SprawlWerk.Sim;

namespace SprawlWerk;

/// <summary>
/// The minimal game shell over the debug view: a title menu (Continue when a save exists,
/// New city, Load, Quit) and a pause overlay (Resume, Save, Quit); Load and Save open the
/// <see cref="SaveBrowser"/>. Escape cancels a drag first; otherwise
/// it opens or closes the pause overlay. While a menu is up the city view
/// is inactive, so no time passes and clicks do not reach the map.
/// The pause overlay also holds the volume sliders (<see cref="AudioDirector"/>).
/// Built in code so the scene stays small; a settings screen comes later.
/// </summary>
public partial class ShellMenu : CanvasLayer
{
    private CityView _city = null!;
    private Control _title = null!;
    private Control _pause = null!;
    private SaveBrowser _browser = null!;
    private Control _returnTo = null!;
    private Button _continue = null!;
    private Label _titleStatus = null!;
    private bool _playing;

    public override void _Ready()
    {
        _city = GetNode<CityView>("../CityView");
        _title = BuildPanel(Strings.GameTitle,
            (Strings.MenuContinue, ContinueNow),
            (Strings.MenuNew, StartNew),
            (Strings.MenuLoad, () => OpenBrowser(SaveBrowser.BrowserMode.Load, _title)),
            (Strings.MenuQuit, Quit));
        _pause = BuildPanel(Strings.MenuPaused,
            (Strings.MenuResume, Resume),
            (Strings.MenuSave, () => OpenBrowser(SaveBrowser.BrowserMode.Save, _pause)),
            (Strings.MenuQuit, Quit));
        if (AudioDirector.Instance is { } audio && _pause.FindChild("Box", true, false) is Control pauseBox)
        {
            var volume = audio.BuildVolumeControls();
            pauseBox.AddChild(volume);
            pauseBox.MoveChild(volume, pauseBox.GetChildCount() - 2); // above the status line
        }
        _continue = _title.FindChild(Strings.MenuContinue, true, false) as Button ?? throw new System.InvalidOperationException("no Continue button");
        _titleStatus = _title.FindChild("Status", true, false) as Label ?? throw new System.InvalidOperationException("no status label");
        _browser = new SaveBrowser(_city);
        _browser.Closed += () => ShowPanel(_returnTo);
        _browser.Loaded += () => { _playing = true; ShowPanel(null); };
        AddChild(_title);
        AddChild(_pause);
        AddChild(_browser);
        _returnTo = _title;
        ShowPanel(_title);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        if (_browser.Visible)
        {
            _browser.Back();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (!_playing) return;

        if (_pause.Visible) Resume();
        else if (!_city.CancelDrag()) ShowPanel(_pause);
        GetViewport().SetInputAsHandled();
    }

    // Continue opens the newest readable save of any kind; it is hidden when there is none.
    private void RefreshContinue()
    {
        _titleStatus.Text = "";
        _continue.Visible = ContinueTarget() is not null;
    }

    private static string? ContinueTarget() => Autosave.ContinueTarget(
        SaveStore.List().Select(e => new Autosave.Candidate(e.FileName, e.Modified, e.Info is not null)));

    private void ContinueNow()
    {
        if (ContinueTarget() is not { } file) { RefreshContinue(); return; }

        var json = SaveStore.Read(file, out var problem);
        if (json is null) { RefreshContinue(); _titleStatus.Text = problem ?? Strings.SaveMissing; return; }

        try
        {
            _city.LoadFromJson(json);
        }
        catch (SaveFormatException e)
        {
            _titleStatus.Text = Strings.LoadFailed(e.Message);
            return;
        }

        _playing = true;
        ShowPanel(null);
    }

    private void StartNew()
    {
        _city.NewCity();
        _playing = true;
        ShowPanel(null);
    }

    private void OpenBrowser(SaveBrowser.BrowserMode mode, Control from)
    {
        _returnTo = from;
        _title.Visible = false;
        _pause.Visible = false;
        _browser.Open(mode);
    }

    private void Resume() => ShowPanel(null);

    private void Quit() => GetTree().Quit();

    // Only one panel at a time; none means the game is running.
    private void ShowPanel(Control? panel)
    {
        bool pauseWasOpen = _pause.Visible;
        if (AudioDirector.Instance is { } audio)
        {
            if (panel == _pause && !pauseWasOpen) audio.Play(Sfx.UiOpen);
            else if (panel is null && pauseWasOpen) audio.Play(Sfx.UiClose);
            audio.SetCityAmbience(panel is null);
        }
        _title.Visible = panel == _title;
        _pause.Visible = panel == _pause;
        _city.Active = panel is null;
        if (panel == _title) RefreshContinue();
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

        var box = new VBoxContainer { Name = "Box", CustomMinimumSize = new Vector2(220, 0) };
        box.AddThemeConstantOverride("separation", 10);
        center.AddChild(box);

        box.AddChild(new Label { Text = heading, HorizontalAlignment = HorizontalAlignment.Center });
        foreach (var (text, onPress) in buttons)
        {
            var button = new Button { Text = text, Name = text };
            var action = onPress;
            button.Pressed += () => action();
            box.AddChild(button);
        }
        box.AddChild(new Label
        {
            Name = "Status",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color("e86f5c"),
        });
        return root;
    }
}
