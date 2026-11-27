using System;
using System.Collections.Generic;
using Godot;
using SprawlWerk.Sim;
using SprawlWerk.Sim.Content;

namespace SprawlWerk;

/// <summary>
/// Everything about the city in play that is not drawing: it owns the
/// <see cref="Simulation"/>, feeds it fixed ticks at the chosen speed, holds the
/// current tool and the drag in progress, turns drags into commands, saves and
/// loads, and reports what the last commands did (refusals, notices, sounds).
/// The views (<see cref="CityWorld"/> in 3D, <see cref="CityView"/> as the 2D
/// debug view) only draw it and report which tile the pointer is over.
///
/// Keys handled here: Q query, B bulldoze, R road, L power line, 1 2 3 the
/// zones (4, 5, 6 and 0 still pick road, line, plant and bulldoze), Space
/// pauses and resumes at the last speed, F5 quick-saves, F9 quick-loads, F3 swaps to the 2D debug
/// view and back. Commands are still accepted while paused and apply when time
/// resumes. The city autosaves once a month into one slot (see <see cref="Autosave"/>).
/// While <see cref="Active"/> is false (a menu is up) time stands still and input is ignored.
/// Each tick's commands make one sound: the last one's (see <see cref="AudioDirector"/>).
/// Tax changes from the budget window are commands too (<see cref="SetTax"/>).
/// What the player should hear about (refusals, saves, autosaves) is raised as
/// <see cref="Message"/>; the HUD shows it as a toast. The query tool
/// (<see cref="Tool"/> null) builds nothing: a click picks the tile to inspect
/// (<see cref="Inspected"/>).
/// </summary>
public partial class CityController : Node
{
    public const int MapSize = 32;
    private const double SecondsPerTick = 0.25;
    private const string QuickSavePath = "user://quicksave.json";
    private static readonly int[] Speeds = [1, 2, 4];

    private Simulation _sim = null!;
    private TileKind? _tool;
    private double _tickAccumulator;
    private bool _paused;
    private int _speedIndex;
    private Vector2I? _dragStart;
    private readonly Dictionary<ZoneKind, int> _pendingTax = new();

    /// <summary>A different city was put in place (new, loaded): views recentre their cameras.</summary>
    public event Action? CityReplaced;

    /// <summary>Something to tell the player: the text, and true when it is a problem (a refusal, a failed save).</summary>
    public event Action<string, bool>? Message;

    /// <summary>False while a menu is open: no time passes and the views ignore input.</summary>
    public bool Active { get; set; } = true;

    /// <summary>The city being shown. Read-only for views; changes go through commands.</summary>
    public Simulation Sim => _sim;

    /// <summary>
    /// Changes whenever something drawn may have changed: a different city was
    /// put in place, or the simulation's <see cref="Simulation.MapRevision"/>
    /// moved (commands, growth, networks). Views rebuild only when it changes.
    /// </summary>
    public (Simulation City, long Revision) MapVersion => (_sim, _sim.MapRevision);

    /// <summary>True while the 2D debug view is shown instead of the 3D world (F3).</summary>
    public bool DebugView { get; set; }

    /// <summary>The build tool (Empty is bulldoze), or null for the query tool.</summary>
    public TileKind? Tool { get => _tool; set { _tool = value; _dragStart = null; } }

    /// <summary>The tile the query card shows, if any.</summary>
    public Vector2I? Inspected { get; set; }

    public bool Paused { get => _paused; set => _paused = value; }

    public static IReadOnlyList<int> SpeedSteps => Speeds;

    public int SpeedIndex { get => _speedIndex; set => _speedIndex = Mathf.Clamp(value, 0, Speeds.Length - 1); }

    /// <summary>The tile under the pointer, set each frame by the view on screen; null over the HUD or off the map.</summary>
    public Vector2I? Hover { get; set; }

    public bool Dragging => _dragStart is not null;

    /// <summary>Set by the HUD: true when a screen point is over one of its panels.</summary>
    public Func<Vector2, bool>? IsOverUi { get; set; }

    /// <summary>True when the mouse is over a HUD panel, so the views neither pick nor preview under it.</summary>
    public bool PointerOverUi() => IsOverUi?.Invoke(GetViewport().GetMousePosition()) ?? false;

    public override void _Ready()
    {
        // Something to look at behind the title menu; New city replaces it.
        _sim = NewSimulation();
        SeedDemoCity();
        _sim.Step();
    }

    // Every city runs on the loaded content, so buildings grow (ADR 0011, 0012).
    private static Simulation NewSimulation() => new(MapSize, MapSize, content: GameContent.Content);

    public override void _Process(double delta)
    {
        // Fixed-rate simulation ticks, independent of frame rate.
        // Speed only changes how fast time is fed in; each tick is the same.
        if (Active && !_paused) _tickAccumulator += delta * Speeds[_speedIndex];
        while (_tickAccumulator >= SecondsPerTick)
        {
            long before = _sim.Tick;
            _sim.Step();
            _tickAccumulator -= SecondsPerTick;
            if (Autosave.IsDue(before, _sim.Tick)) AutosaveNow();
            NoteOutcomes();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Active || @event is not InputEventKey { Pressed: true, Echo: false } key) return;

        switch (key.Keycode)
        {
            case Key.F3: DebugView = !DebugView; _dragStart = null; return;
            case Key.F5: QuickSave(); return;
            case Key.F9: QuickLoad(); return;
            case Key.Space: _paused = !_paused; return; // the speed is kept, so resuming picks it up
            case Key.Q: Tool = null; return;
        }

        if (ToolForKey(key.Keycode) is { } tool) Tool = tool;
    }

    private static TileKind? ToolForKey(Key key) => key switch
    {
        Key.Key1 => TileKind.Residential,
        Key.Key2 => TileKind.Commercial,
        Key.Key3 => TileKind.Industrial,
        Key.R or Key.Key4 => TileKind.Road,
        Key.L or Key.Key5 => TileKind.PowerLine,
        Key.Key6 => TileKind.PowerPlant,
        Key.B or Key.Key0 => TileKind.Empty,
        _ => null,
    };

    // --- drags and commands ---

    /// <summary>Left button down: the drag starts on the hovered tile (nothing happens off the map). The query tool does not drag.</summary>
    public void BeginDrag() => _dragStart = _tool is null ? null : Hover;

    /// <summary>Left button up: send the drag as one command. Released off the map, the last hovered tile is used.</summary>
    public void FinishDrag(Vector2I? at)
    {
        if (_dragStart is { } start && (at ?? Hover) is { } end) Place(start, end);
        _dragStart = null;
    }

    /// <summary>Abandon a drag in progress; true if there was one.</summary>
    public bool CancelDrag()
    {
        bool was = _dragStart is not null;
        _dragStart = null;
        return was;
    }

    /// <summary>What the tool would do now: the whole drag if one is under way, else the hovered tile.</summary>
    public ICommand? PreviewCommand()
    {
        if (Hover is not { } h || _tool is not { } tool) return null;
        var from = _dragStart ?? h;
        return Drag.Command(tool, from.X, from.Y, h.X, h.Y);
    }

    /// <summary>The rectangle of tiles a place or bulldoze command covers, for previews.</summary>
    public static Rect2I? AreaOf(ICommand? command) => command switch
    {
        PlaceCommand pc => new Rect2I(pc.X, pc.Y, pc.Width, pc.Height),
        BulldozeCommand bc => new Rect2I(bc.X, bc.Y, bc.Width, bc.Height),
        _ => null,
    };

    private void Place(Vector2I from, Vector2I to)
    {
        if (_tool is { } tool) _sim.Enqueue(Drag.Command(tool, from.X, from.Y, to.X, to.Y));
    }

    // --- taxes ---

    /// <summary>The rate to show for a zone: one asked for but not applied yet (paused), else the city's.</summary>
    public int TaxShown(ZoneKind zone) => _pendingTax.TryGetValue(zone, out int rate) ? rate : _sim.Taxes.For(zone);

    /// <summary>Ask for a zone's rate; sent as a command, clamped to the allowed range. Applies on the next tick.</summary>
    public void SetTax(ZoneKind zone, int rate)
    {
        rate = Mathf.Clamp(rate, BudgetRules.MinTax, BudgetRules.MaxTax);
        if (rate == TaxShown(zone)) return;
        _pendingTax[zone] = rate;
        _sim.Enqueue(new SetTaxCommand(zone, rate));
    }

    // Re-sent areas that are already built are not worth showing; only real problems are.
    private void NoteOutcomes()
    {
        Sfx? sound = null;
        foreach (var outcome in _sim.LastOutcomes)
        {
            if (outcome.Command is SetTaxCommand tax && _pendingTax.TryGetValue(tax.Zone, out int asked) && asked == tax.Rate)
                _pendingTax.Remove(tax.Zone);
            if (outcome.Result.Refusal is Refusal.NothingToChange) continue;
            sound = SoundFor(outcome);
            if (outcome.Result.Accepted) continue;
            Message?.Invoke(Describe(outcome.Result), true);
        }
        if (sound is { } s) AudioDirector.Instance?.Play(s);
    }

    private static Sfx SoundFor(CommandOutcome outcome) => outcome switch
    {
        { Result.Accepted: false } => Sfx.Refused,
        { Command: SetTaxCommand } => Sfx.UiClick,
        { Command: BulldozeCommand } => Sfx.Bulldoze,
        { Command: PlaceCommand { Kind: TileKind.Empty } } => Sfx.Bulldoze,
        { Command: PlaceCommand { Kind: TileKind.PowerPlant } } => Sfx.PlaceBuilding,
        _ => Sfx.Place,
    };

    /// <summary>Why a command was refused, as a sentence for a toast.</summary>
    public static string Describe(CommandResult r) => r.Refusal switch
    {
        Refusal.InsufficientFunds => Strings.NeedsFunds(r.Cost),
        Refusal.NothingToChange => Strings.AlreadyBuilt,
        Refusal.OutOfBounds => Strings.OffTheMap,
        Refusal.OutOfRange => Strings.OutOfRange,
        _ => r.Refusal.ToString(),
    };

    // --- whole cities ---

    /// <summary>Start over with an empty city.</summary>
    public void NewCity() => Replace(NewSimulation(), seedDemo: false);

    /// <summary>Start over with the demo layout (developer flag <c>--demo-city</c>, and screenshots).</summary>
    public void DemoCity() => Replace(NewSimulation(), seedDemo: true);

    private void Replace(Simulation sim, bool seedDemo)
    {
        _sim = sim;
        _pendingTax.Clear();
        if (seedDemo) SeedDemoCity();
        _sim.Step();
        _tickAccumulator = 0;
        _paused = false;
        _dragStart = null;
        Inspected = null;
        CityReplaced?.Invoke();
    }

    /// <summary>The city as save text.</summary>
    public string SaveToJson() => SaveGame.Serialize(_sim);

    /// <summary>Replace the city with a saved one.</summary>
    /// <exception cref="SaveFormatException">The save is damaged or from a newer game; the current city is kept.</exception>
    public void LoadFromJson(string json)
    {
        _sim = SaveGame.Deserialize(json, content: GameContent.Content);
        _pendingTax.Clear();
        _tickAccumulator = 0;
        _dragStart = null;
        Inspected = null;
        foreach (var line in _sim.LoadReport) GD.Print($"load: {line}");
        CityReplaced?.Invoke();
        Message?.Invoke(_sim.LoadReport.Count > 0 ? Strings.LoadedWithChanges(_sim.LoadReport.Count) : Strings.Loaded, false);
    }

    // Once a month, into the single autosave slot; a failure is shown and the game carries on.
    private void AutosaveNow()
    {
        if (SaveStore.Write(Autosave.FileName, SaveToJson()) is { } problem)
            Message?.Invoke(Strings.AutosaveFailed(problem), true);
    }

    private void QuickSave()
    {
        if (SaveStore.WriteAtomic(QuickSavePath, SaveToJson()) is { } problem) { Message?.Invoke(problem, true); return; }
        Message?.Invoke(Strings.Saved, false);
        AudioDirector.Instance?.Play(Sfx.Confirm);
    }

    private void QuickLoad()
    {
        if (!Godot.FileAccess.FileExists(QuickSavePath)) { Message?.Invoke(Strings.NoSave, true); return; }
        try
        {
            LoadFromJson(Godot.FileAccess.GetFileAsString(QuickSavePath));
        }
        catch (SaveFormatException e)
        {
            Message?.Invoke(Strings.LoadFailed(e.Message), true);
        }
    }

    // A small town with every kind of tile: straights, bends, junctions and dead ends,
    // lines crossing roads, zones of all three kinds that grow once time runs,
    // and one commercial block left without power to show the difference.
    private void SeedDemoCity()
    {
        _sim.Enqueue(new PlaceCommand(4, 4, TileKind.PowerPlant));
        _sim.Enqueue(new PlaceCommand(5, 4, 6, 1, TileKind.PowerLine));
        _sim.Enqueue(new PlaceCommand(11, 2, 4, 5, TileKind.Residential));
        _sim.Enqueue(new PlaceCommand(15, 0, 1, MapSize, TileKind.Road));
        _sim.Enqueue(new PlaceCommand(16, 2, 4, 4, TileKind.Commercial));
        _sim.Enqueue(new PlaceCommand(22, 10, 5, 4, TileKind.Industrial));
        _sim.Enqueue(new PlaceCommand(5, 7, 10, 1, TileKind.Road));
        _sim.Enqueue(new PlaceCommand(16, 7, 12, 1, TileKind.Road));
        _sim.Enqueue(new PlaceCommand(21, 8, 1, 8, TileKind.Road));
        _sim.Enqueue(new PlaceCommand(10, 8, 1, 4, TileKind.Road));
        _sim.Enqueue(new PlaceCommand(11, 9, 4, 3, TileKind.Residential));
        _sim.Enqueue(new PlaceCommand(16, 9, 4, 3, TileKind.Commercial));
        _sim.Enqueue(new PlaceCommand(8, 5, 1, 5, TileKind.PowerLine)); // crosses the road at (8, 7)
        _sim.Enqueue(new PlaceCommand(7, 10, 3, 3, TileKind.Residential));
        _sim.Enqueue(new PlaceCommand(4, 5, TileKind.PowerPlant)); // a second plant: 80 units
        _sim.Enqueue(new PlaceCommand(15, 3, TileKind.PowerLine)); // across the main road to the shops
        _sim.Enqueue(new PlaceCommand(20, 3, 2, 1, TileKind.PowerLine));
        _sim.Enqueue(new PlaceCommand(21, 3, 1, 4, TileKind.PowerLine)); // down to the industry...
        _sim.Enqueue(new PlaceCommand(21, 7, 1, 4, TileKind.PowerLine)); // ...across the east roads
        // The lower shops (16..19, 9..11) stay unpowered on purpose.
    }
}
