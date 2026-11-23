# SprawlWerk

An open-source city builder: a modern re-imagining of the classic isometric
city sims of the 90s. Zone it, power it, pipe it, and keep up with the
machinery of a growing city.

**Website:** https://sprawlwerk.org · **Status:** early prototype

A [BrainChurts](https://www.brainchurts.com) project.

## Layout

```
src/SprawlWerk.Sim/          the city simulation: plain C#, no engine dependency
tests/SprawlWerk.Sim.Tests/  xUnit tests for the simulation
game/                        Godot 4.7 (.NET) project: rendering, input, UI
SprawlWerk.sln               solution tying the three together
```

The simulation knows nothing about Godot. The game reads simulation state
to draw it and sends player actions in as commands (`ICommand`). That split
keeps the core testable without an engine and makes replays, save files
and other front ends possible later.

## Requirements

- [Godot 4.7, .NET build](https://godotengine.org/download)
- [.NET 8 SDK](https://dotnet.microsoft.com/download) or newer

On macOS: `brew install --cask dotnet-sdk godot-mono`

## Running

**Tests (no Godot needed):**

```bash
dotnet test
```

**The game:** open `game/project.godot` in Godot (.NET) and press Play (F5).

Current controls: `1` residential, `2` commercial, `3` industrial, `4` road,
`5` power line, `6` power plant, `0` bulldoze. Click to place one tile; drag to fill a rectangle (zones, bulldoze) or a
straight line (road, power line); the cost shows while you drag, and Escape
cancels.
Zones without power are drawn dim. `Escape` opens the pause menu; `F5` saves and `F9` loads one quick slot; `Space` pauses; `-` and `=` set the
speed (1x, 2x, 4x).

## What works today

- Tile map with zones, roads, power lines and plants
- Command queue applied on fixed simulation ticks
- Power grid: flood fill from plants through lines and zones; each plant has a
  capacity, and when a network is over it the zones farthest from a plant go dark
- Power lines can cross roads (for a fee); a plain road still blocks power
- A minimal title menu and pause menu
- Versioned JSON saves; a loaded city continues exactly as the saved one would
- A calendar derived from ticks, pause and three speeds
- Isometric debug view in Godot with tile picking

## Contributing

Early days; issues and ideas are welcome. Planned work is in
[docs/backlog.md](docs/backlog.md). Run `dotnet test` before opening
a pull request. CI runs the simulation tests on every push.

## License

BSD 3-Clause. See [LICENSE](LICENSE).
