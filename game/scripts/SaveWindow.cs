using System;
using Godot;
using SprawlWerk.Sim;
using static SprawlWerk.UiKit;
using Ui = SprawlWerk.Palette.Ui;

namespace SprawlWerk;

/// <summary>
/// The Save / Load window from the top bar: four rows, the autosave and three
/// slots. Each shows a badge (A, 1, 2, 3), its name and what is in it (date,
/// treasury, when it was saved) or "Nothing here yet", with Save here (not on
/// the autosave) and Load (only when filled). Slots are ordinary files in the
/// saves folder (<c>slot1.json</c> ...), so the title menu's load list sees them
/// too; all file work goes through <see cref="SaveStore"/>. Time keeps running
/// while it is open.
/// </summary>
public sealed class SaveWindow
{
    private const int Slots = 3;

    private readonly CityController _city;
    private readonly WindowFrame _frame;
    private readonly VBoxContainer _rows;
    private readonly Label _status;

    public Control Root => _frame.Root;
    public bool IsOpen => _frame.Root.Visible;

    /// <summary>A slot was loaded into the city.</summary>
    public event Action? Loaded;

    public SaveWindow(CityController city)
    {
        _city = city;
        _frame = Window(Strings.SaveTitle, null, Close);
        _rows = Column(0);
        _frame.Body.AddChild(_rows);
        _status = Text("", UiTheme.Small, Ui.Ink);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _frame.Body.AddChild(_status);
        _frame.Body.AddChild(Text(Strings.AutosaveNote, UiTheme.Small));
    }

    public void Open()
    {
        SetStatus("", problem: false);
        Refresh();
        _frame.Root.Visible = true;
        AudioDirector.Instance?.Play(Sfx.UiOpen);
    }

    public void Close()
    {
        if (!IsOpen) return;
        _frame.Root.Visible = false;
        AudioDirector.Instance?.Play(Sfx.UiClose);
    }

    public void Toggle() { if (IsOpen) Close(); else Open(); }

    public void Update(Vector2 screen)
    {
        if (IsOpen) _frame.Fit(screen);
    }

    private static string SlotFile(int n) => SaveNames.ToFileName($"slot{n}")!;

    private void Refresh()
    {
        foreach (var child in _rows.GetChildren()) child.QueueFree();
        var entries = SaveStore.List();
        _rows.AddChild(SlotRow("A", Strings.AutosaveSlot, Autosave.FileName, entries, canSave: false));
        for (int n = 1; n <= Slots; n++)
            _rows.AddChild(SlotRow(n.ToString(), Strings.Slot(n), SlotFile(n), entries, canSave: true));
    }

    private Control SlotRow(string badge, string name, string file, System.Collections.Generic.List<SaveEntry> entries, bool canSave)
    {
        var entry = entries.Find(e => e.FileName == file);
        var badgeLabel = Text(badge, UiTheme.LcdLarge, align: HorizontalAlignment.Center);
        badgeLabel.VerticalAlignment = VerticalAlignment.Center;
        var well = Box(UiTheme.WellBox(padX: 0), badgeLabel);
        well.CustomMinimumSize = new Vector2(44, 44);
        well.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

        string detail = entry switch
        {
            null => Strings.EmptySlot,
            { Info: { } info } => Strings.SlotDetail(info.Date, info.Funds, Stamp(entry.Modified)),
            _ => Strings.SlotUnreadable(entry.Problem ?? "?"),
        };
        var lines = Column(2, Text(name, UiTheme.Strong), Text(detail, UiTheme.Small));
        lines.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        lines.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

        var row = Row(14, well, lines);
        if (canSave)
        {
            var save = new Button { Text = Strings.SaveHere, FocusMode = Control.FocusModeEnum.None, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            save.Pressed += () => SaveTo(file, name);
            row.AddChild(save);
        }
        if (entry is { Info: not null })
        {
            var load = new Button { Text = Strings.LoadSlot, ThemeTypeVariation = UiTheme.PrimaryButton, FocusMode = Control.FocusModeEnum.None, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            load.Pressed += () => LoadFrom(file);
            row.AddChild(load);
        }

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        margin.AddChild(row);
        return Column(0, margin, Rule(Ui.Divider));
    }

    private static string Stamp(long unix)
    {
        long bias = 0;
        if (Time.GetTimeZoneFromSystem().TryGetValue("bias", out var b)) bias = b.AsInt64() * 60;
        var dt = Time.GetDatetimeDictFromUnixTime(unix + bias);
        return $"{dt["year"]}-{(int)dt["month"]:00}-{(int)dt["day"]:00} {(int)dt["hour"]:00}:{(int)dt["minute"]:00}";
    }

    private void SaveTo(string file, string name)
    {
        if (SaveStore.Write(file, _city.SaveToJson()) is { } problem)
        {
            SetStatus(problem, problem: true);
            return;
        }
        AudioDirector.Instance?.Play(Sfx.Confirm);
        SetStatus(Strings.SavedTo(name), problem: false);
        Refresh();
    }

    private void LoadFrom(string file)
    {
        var json = SaveStore.Read(file, out var problem);
        if (json is null) { SetStatus(problem ?? Strings.SaveMissing, problem: true); Refresh(); return; }
        try
        {
            _city.LoadFromJson(json);
        }
        catch (SaveFormatException e)
        {
            SetStatus(Strings.LoadFailed(e.Message), problem: true);
            return;
        }
        _frame.Root.Visible = false;
        Loaded?.Invoke();
    }

    private void SetStatus(string text, bool problem)
    {
        _status.Text = text;
        _status.Visible = text.Length > 0;
        _status.AddThemeColorOverride("font_color", problem ? Ui.DangerText : Ui.Primary);
    }
}
