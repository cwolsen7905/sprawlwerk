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
`5` power line, `6` power plant, `0` bulldoze. Click or drag to place.
Zones without power are drawn dim.

## What works today

- Tile map with zones, roads, power lines and plants
- Command queue applied on fixed simulation ticks
- Power grid: flood fill from plants through lines and zones
- Isometric debug view in Godot with tile picking

## Contributing

Early days; issues and ideas are welcome. Run `dotnet test` before opening
a pull request. CI runs the simulation tests on every push.

## License

TBD.
