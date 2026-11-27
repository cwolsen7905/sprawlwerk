using System;
using System.Collections.Generic;
using Godot;

namespace SprawlWerk;

/// <summary>Sound effects the game plays; each maps to one file in <c>assets/audio/sfx</c>.</summary>
public enum Sfx
{
    UiClick,
    UiHover,
    UiOpen,
    UiClose,
    Confirm,
    Refused,
    Place,
    PlaceBuilding,
    Bulldoze,
}

/// <summary>
/// All game audio, loaded once as an autoload so it outlives scenes: three
/// buses under Master (Music, Effects, Ambience), a shuffled soundtrack that
/// moves to the next track when one ends, looping city ambience while a city
/// is in play, and a small pool of effect players. Every <see cref="Button"/>
/// that enters the tree gets click and hover sounds, so menus need no audio
/// code. Volumes are kept in <c>user://settings.cfg</c>, outside saves.
/// </summary>
public partial class AudioDirector : Node
{
    public const string MusicBus = "Music";
    public const string EffectsBus = "Effects";
    public const string AmbienceBus = "Ambience";
    public static readonly string[] Buses = [MusicBus, EffectsBus, AmbienceBus];

    private const string SettingsPath = "user://settings.cfg";
    private const string SettingsSection = "audio";
    private const int EffectVoices = 6;

    private static readonly string[] Tracks =
    [
        "res://assets/audio/music/calm_track.ogg",
        "res://assets/audio/music/jazz_brunch.mp3",
        "res://assets/audio/music/wholesome.mp3",
        "res://assets/audio/music/airport_lounge.mp3",
    ];

    private static readonly Dictionary<Sfx, (string Path, float Db)> Effects = new()
    {
        [Sfx.UiClick] = ("res://assets/audio/sfx/ui_click.ogg", -4f),
        [Sfx.UiHover] = ("res://assets/audio/sfx/ui_hover.ogg", -14f),
        [Sfx.UiOpen] = ("res://assets/audio/sfx/ui_open.ogg", -6f),
        [Sfx.UiClose] = ("res://assets/audio/sfx/ui_close.ogg", -6f),
        [Sfx.Confirm] = ("res://assets/audio/sfx/confirm.ogg", -4f),
        [Sfx.Refused] = ("res://assets/audio/sfx/refused.ogg", -4f),
        [Sfx.Place] = ("res://assets/audio/sfx/place.ogg", -4f),
        [Sfx.PlaceBuilding] = ("res://assets/audio/sfx/place_building.ogg", -6f),
        [Sfx.Bulldoze] = ("res://assets/audio/sfx/bulldoze.ogg", -6f),
    };

    /// <summary>The one instance, set when the autoload enters the tree.</summary>
    public static AudioDirector? Instance { get; private set; }

    private readonly Dictionary<Sfx, AudioStream> _effects = new();
    private readonly List<AudioStreamPlayer> _voices = new();
    private readonly RandomNumberGenerator _rng = new();
    private AudioStreamPlayer _music = null!;
    private AudioStreamPlayer _cityAmbience = null!;
    private AudioStreamPlayer _birdAmbience = null!;
    private int[] _order = [];
    private int _next;
    private int _nextVoice;

    public override void _EnterTree()
    {
        Instance = this;
        foreach (var bus in Buses)
        {
            if (AudioServer.GetBusIndex(bus) >= 0) continue;
            AudioServer.AddBus();
            int index = AudioServer.BusCount - 1;
            AudioServer.SetBusName(index, bus);
            AudioServer.SetBusSend(index, "Master");
        }
        LoadVolumes();
        GetTree().NodeAdded += OnNodeAdded;
    }

    public override void _ExitTree()
    {
        GetTree().NodeAdded -= OnNodeAdded;
        // Let go of the streams so nothing is still playing when the engine shuts down.
        if (_music is not null)
        {
            _music.Finished -= PlayNextTrack;
            _music.Stop();
            _music.Stream = null;
        }
        if (Instance == this) Instance = null;
    }

    public override void _Ready()
    {
        _rng.Randomize();
        foreach (var (sfx, (path, _)) in Effects)
            _effects[sfx] = GD.Load<AudioStream>(path);
        for (int i = 0; i < EffectVoices; i++)
        {
            var voice = new AudioStreamPlayer { Bus = EffectsBus };
            AddChild(voice);
            _voices.Add(voice);
        }

        _music = new AudioStreamPlayer { Bus = MusicBus, VolumeDb = -8f };
        _music.Finished += PlayNextTrack;
        AddChild(_music);
        Shuffle();
        PlayNextTrack();

        _cityAmbience = Looping("res://assets/audio/ambience/city.ogg", -10f);
        _birdAmbience = Looping("res://assets/audio/ambience/birds.ogg", -20f);
    }

    /// <summary>Play an effect on the next free voice (or the oldest, if all are busy).</summary>
    public void Play(Sfx sfx)
    {
        if (!_effects.TryGetValue(sfx, out var stream)) return;
        var voice = _voices.Find(v => !v.Playing) ?? _voices[_nextVoice++ % _voices.Count];
        voice.Stream = stream;
        voice.VolumeDb = Effects[sfx].Db;
        voice.Play();
    }

    /// <summary>City ambience plays only while a city is on screen and not behind a menu.</summary>
    public void SetCityAmbience(bool on)
    {
        foreach (var player in new[] { _cityAmbience, _birdAmbience })
        {
            if (on && !player.Playing) player.Play();
            else if (!on && player.Playing) player.Stop();
        }
    }

    /// <summary>Bus volume from 0 (silent) to 1 (full), as shown on the sliders.</summary>
    public static float GetVolume(string bus) => Mathf.DbToLinear(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex(bus)));

    public void SetVolume(string bus, float linear)
    {
        int index = AudioServer.GetBusIndex(bus);
        if (index < 0) return;
        linear = Mathf.Clamp(linear, 0f, 1f);
        AudioServer.SetBusMute(index, linear <= 0.001f);
        AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Mathf.Max(linear, 0.0001f)));
        SaveVolumes();
    }

    // Each pass plays every track once in a new order; the next pass never opens with the track just heard.
    private void Shuffle()
    {
        int last = _order.Length > 0 ? _order[^1] : -1;
        _order = new int[Tracks.Length];
        for (int i = 0; i < _order.Length; i++) _order[i] = i;
        for (int i = _order.Length - 1; i > 0; i--)
        {
            int j = _rng.RandiRange(0, i);
            (_order[i], _order[j]) = (_order[j], _order[i]);
        }
        if (_order.Length > 1 && _order[0] == last) (_order[0], _order[1]) = (_order[1], _order[0]);
        _next = 0;
    }

    private void PlayNextTrack()
    {
        if (_next >= _order.Length) Shuffle();
        _music.Stream = GD.Load<AudioStream>(Tracks[_order[_next++]]);
        _music.Play();
    }

    private AudioStreamPlayer Looping(string path, float db)
    {
        var stream = GD.Load<AudioStream>(path);
        switch (stream)
        {
            case AudioStreamOggVorbis ogg: ogg.Loop = true; break;
            case AudioStreamMP3 mp3: mp3.Loop = true; break;
        }
        var player = new AudioStreamPlayer { Bus = AmbienceBus, Stream = stream, VolumeDb = db };
        AddChild(player);
        return player;
    }

    private void OnNodeAdded(Node node)
    {
        if (node is not BaseButton button) return;
        button.Pressed += () => Play(Sfx.UiClick);
        button.MouseEntered += () => { if (!button.Disabled) Play(Sfx.UiHover); };
    }

    private void LoadVolumes()
    {
        var config = new ConfigFile();
        if (config.Load(SettingsPath) != Error.Ok) return;
        foreach (var bus in Buses)
        {
            var value = config.GetValue(SettingsSection, bus, 1.0f);
            float linear = value.VariantType == Variant.Type.Float ? (float)value.AsDouble() : 1f;
            int index = AudioServer.GetBusIndex(bus);
            linear = Mathf.Clamp(linear, 0f, 1f);
            AudioServer.SetBusMute(index, linear <= 0.001f);
            AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Mathf.Max(linear, 0.0001f)));
        }
    }

    private static void SaveVolumes()
    {
        var config = new ConfigFile();
        config.Load(SettingsPath); // keep other sections; a missing file is fine
        foreach (var bus in Buses)
            config.SetValue(SettingsSection, bus, AudioServer.IsBusMute(AudioServer.GetBusIndex(bus)) ? 0f : GetVolume(bus));
        if (config.Save(SettingsPath) is var error && error != Error.Ok)
            GD.PushWarning($"Could not save audio settings: {error}");
    }

    /// <summary>A labelled slider per bus, for a menu panel.</summary>
    public Control BuildVolumeControls()
    {
        var grid = new GridContainer { Columns = 2, Name = "Volume" };
        grid.AddThemeConstantOverride("h_separation", 12);
        foreach (var (bus, label) in new[] { (MusicBus, Strings.VolumeMusic), (EffectsBus, Strings.VolumeEffects), (AmbienceBus, Strings.VolumeAmbience) })
        {
            grid.AddChild(new Label { Text = label });
            var slider = new HSlider
            {
                MinValue = 0, MaxValue = 1, Step = 0.05,
                Value = AudioServer.IsBusMute(AudioServer.GetBusIndex(bus)) ? 0 : GetVolume(bus),
                CustomMinimumSize = new Vector2(140, 0),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            string target = bus;
            slider.ValueChanged += v => SetVolume(target, (float)v);
            grid.AddChild(slider);
        }
        return grid;
    }
}
