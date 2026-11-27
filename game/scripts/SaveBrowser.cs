using System;
using System.Collections.Generic;
using Godot;
using SprawlWerk.Sim;

namespace SprawlWerk;

/// <summary>
/// A plain list of the files in the saves folder, used two ways: to load one
/// (from the title menu) or to save under a typed name (from the pause overlay).
/// Overwriting and deleting ask first. Problems (a refused name, a damaged
/// save, a save from a newer game) show as text under the list; the city is
/// never touched by a load that fails. All file I/O is in <see cref="SaveStore"/>;
/// naming and header reading are in the simulation library, with tests.
/// </summary>
public partial class SaveBrowser : Control
{
    public enum BrowserMode { Load, Save }

    private static readonly Color ErrorColor = Palette.Ui.DangerText;
    private static readonly Color OkColor = Palette.Ui.Primary;

    private readonly CityController _city;
    private readonly List<SaveEntry> _entries = new();
    private BrowserMode _mode;
    private Label _heading = null!;
    private ItemList _list = null!;
    private LineEdit _name = null!;
    private Label _status = null!;
    private Button _primary = null!;
    private Button _delete = null!;
    private Control _buttons = null!;
    private Control _confirm = null!;
    private Label _confirmText = null!;
    private Action? _confirmed;

    /// <summary>The player left the browser without loading.</summary>
    public event Action? Closed;

    /// <summary>A save was loaded into the city.</summary>
    public event Action? Loaded;

    public SaveBrowser(CityController city) => _city = city;

    public bool Confirming => _confirm.Visible;

    public override void _Ready()
    {
        Visible = false;
        Theme = UiTheme.Theme;
        SetAnchorsPreset(LayoutPreset.FullRect);

        var dim = new ColorRect { Color = Palette.Ui.Backdrop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var box = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        box.AddThemeConstantOverride("separation", 10);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UiTheme.PanelBox(pad: 18));
        center.AddChild(panel);
        panel.AddChild(box);

        _heading = new Label { HorizontalAlignment = HorizontalAlignment.Center, ThemeTypeVariation = UiTheme.Heading };
        box.AddChild(_heading);

        _list = new ItemList { CustomMinimumSize = new Vector2(0, 220) };
        _list.ItemSelected += OnSelected;
        box.AddChild(_list);

        _name = new LineEdit { PlaceholderText = Strings.SaveNameHint, MaxLength = SaveNames.MaxLength };
        _name.TextSubmitted += _ => OnPrimary();
        box.AddChild(_name);

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        box.AddChild(_status);

        var confirm = new HBoxContainer();
        _confirmText = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        confirm.AddChild(_confirmText);
        confirm.AddChild(MakeButton(Strings.MenuYes, () => { var go = _confirmed; HideConfirm(); go?.Invoke(); }));
        confirm.AddChild(MakeButton(Strings.MenuNo, HideConfirm));
        confirm.Visible = false;
        _confirm = confirm;
        box.AddChild(confirm);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 10);
        _primary = MakeButton("", OnPrimary);
        _delete = MakeButton(Strings.MenuDelete, OnDelete);
        buttons.AddChild(_primary);
        buttons.AddChild(_delete);
        buttons.AddChild(MakeButton(Strings.MenuBack, Back));
        _buttons = buttons;
        box.AddChild(buttons);
    }

    public void Open(BrowserMode mode)
    {
        _mode = mode;
        _heading.Text = mode == BrowserMode.Save ? Strings.MenuSave : Strings.MenuLoad;
        _primary.Text = mode == BrowserMode.Save ? Strings.MenuSave : Strings.MenuLoad;
        _name.Visible = mode == BrowserMode.Save;
        _name.Text = mode == BrowserMode.Save ? "city" : "";
        SetStatus("", ok: true);
        HideConfirm();
        Refresh();
        Visible = true;
        if (mode == BrowserMode.Save) _name.GrabFocus();
    }

    /// <summary>Escape or Back: cancel a question if one is up, otherwise leave.</summary>
    public void Back()
    {
        if (Confirming) { HideConfirm(); return; }
        Visible = false;
        Closed?.Invoke();
    }

    private static Button MakeButton(string text, Action onPress)
    {
        var button = new Button { Text = text };
        button.Pressed += () => onPress();
        return button;
    }

    private void Refresh()
    {
        _entries.Clear();
        _entries.AddRange(SaveStore.List());
        _list.Clear();

        if (_entries.Count == 0)
        {
            int i = _list.AddItem(Strings.NoSavesYet);
            _list.SetItemDisabled(i, true);
            return;
        }

        long bias = 0;
        if (Time.GetTimeZoneFromSystem().TryGetValue("bias", out var b)) bias = b.AsInt64() * 60;
        foreach (var e in _entries)
        {
            _list.AddItem(e.Info is { } info
                ? Strings.SaveLine(e.Name, Time.GetDatetimeStringFromUnixTime(e.Modified + bias, true), info.Tick)
                : Strings.SaveUnreadable(e.Name, e.Problem ?? "?"));
        }
    }

    private SaveEntry? Selected =>
        _list.GetSelectedItems() is { Length: > 0 } sel && sel[0] < _entries.Count ? _entries[sel[0]] : null;

    private void OnSelected(long index)
    {
        SetStatus("", ok: true);
        if (_mode == BrowserMode.Save && index < _entries.Count) _name.Text = _entries[(int)index].Name;
    }

    private void OnPrimary() => (_mode == BrowserMode.Save ? (Action)SaveNow : LoadNow)();

    private void LoadNow()
    {
        if (Selected is not { } entry) { SetStatus(Strings.PickASave, ok: false); return; }

        var json = SaveStore.Read(entry.FileName, out var problem);
        if (json is null) { SetStatus(problem ?? Strings.SaveMissing, ok: false); Refresh(); return; }

        try
        {
            _city.LoadFromJson(json);
        }
        catch (SaveFormatException e)
        {
            SetStatus(Strings.LoadFailed(e.Message), ok: false);
            return;
        }

        Visible = false;
        Loaded?.Invoke();
    }

    private void SaveNow()
    {
        if (SaveNames.ToFileName(_name.Text) is not { } fileName)
        {
            SetStatus(Strings.BadSaveName, ok: false);
            return;
        }

        var name = SaveNames.FromFileName(fileName)!;
        if (SaveStore.Exists(fileName)) Ask(Strings.Overwrite(name), () => WriteSave(fileName, name));
        else WriteSave(fileName, name);
    }

    private void WriteSave(string fileName, string name)
    {
        if (SaveStore.Write(fileName, _city.SaveToJson()) is { } problem)
        {
            SetStatus(problem, ok: false);
            return;
        }
        SetStatus(Strings.SavedAs(name), ok: true);
        Refresh();
    }

    private void OnDelete()
    {
        if (Selected is not { } entry) { SetStatus(Strings.PickASave, ok: false); return; }
        Ask(Strings.ConfirmDelete(entry.Name), () =>
        {
            if (SaveStore.Delete(entry.FileName) is { } problem) SetStatus(problem, ok: false);
            else SetStatus("", ok: true);
            Refresh();
        });
    }

    private void Ask(string question, Action onYes)
    {
        _confirmed = onYes;
        _confirmText.Text = question;
        _confirm.Visible = true;
        _buttons.Visible = false;
    }

    private void HideConfirm()
    {
        _confirmed = null;
        _confirm.Visible = false;
        _buttons.Visible = true;
    }

    private void SetStatus(string text, bool ok)
    {
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", ok ? OkColor : ErrorColor);
    }
}
