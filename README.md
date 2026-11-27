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

Pick a tool from the Build toolbar on the left, or press `1` residential,
`2` commercial, `3` industrial, `4` road, `5` power line, `6` power plant,
`0` bulldoze. Click to place one tile; drag to fill a rectangle (zones,
bulldoze) or a straight line (road, power line); the cost shows in the status
bar while you drag, and Escape cancels. The status bar also names the tile
under the cursor and whether it has power; zones without power are drawn dim.

Move the camera with `WASD` or the arrow keys, or drag with the right or
middle mouse button; the wheel zooms toward the cursor. The top bar shows
funds, date and power demand against supply, with pause and speed buttons
(also `Space`, `-` and `=`) and a Menu button (also `Escape`) for the pause
menu, which holds Save and the volume sliders. `F5` saves and `F9` loads one
quick slot; Load is on the title menu.

To skip the title menu while developing: `godot --path game -- --new-city`.

## What works today

- Tile map with zones, roads, power lines and plants
- Command queue applied on fixed simulation ticks
- Power grid: flood fill from plants through lines and zones; each plant has a
  capacity, and when a network is over it the zones farthest from a plant go dark
- Power lines can cross roads (for a fee); a plain road still blocks power
- A minimal title menu and pause menu
- Versioned JSON saves, written atomically; monthly autosave and Continue on the title menu; a loaded city continues exactly as the saved one would
- A calendar derived from ticks, pause and three speeds
- Isometric debug view in Godot with tile picking, a build toolbar, top and
  status bars, and a camera that pans and zooms
- Music, city ambience and sound effects, with volume sliders on the pause menu

## Contributing

Early days; issues and ideas are welcome. Planned work is in
[docs/backlog.md](docs/backlog.md). Run `dotnet test` before opening
a pull request. CI runs the simulation tests on every push.

## License

BSD 3-Clause. See [LICENSE](LICENSE).

Third-party music, sounds and other assets keep their own licences (CC0 or
CC-BY); each file is listed with its source and licence in
[ASSETS.md](ASSETS.md).
