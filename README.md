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
game/content/base/           the base content pack: building types as JSON data
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

The city is drawn in 3D. Pick a tool from the palette on the left, or press
`Q` query, `B` bulldoze, `R` road, `L` power line, `1` residential, `2`
commercial, `3` industrial (the power plant is in the Power flyout; `4`, `5`,
`6` and `0` still pick road, line, plant and bulldoze). Click to place one
tile; drag to fill a rectangle (zones, bulldoze) or a straight line (road,
power line); the tiles light up and a cost tag follows the cursor (red when
the city cannot pay), and Escape cancels. With the query tool, click a tile
to see why it is or is not growing; zones without power are drawn dim.

Move the camera with `WASD` or the arrow keys, or drag the ground with the
right mouse button (or the left, with the query tool); the wheel or `+` and
`-` zoom; `,` and `.` or a middle-drag rotate (dragging up and down also
tilts). The top bar shows the date, treasury, last month's net and
population, with Budget (tax sliders and upkeep; pauses the city) and Save
(an autosave row and three slots); the logo opens the menu (also `Escape`),
which holds Save and the volume sliders. The bottom bar shows R/C/I demand
and the speeds Pause, Snail, Fox and Falcon (`Space` pauses and resumes).
`F5` saves and `F9` loads one quick slot; Load is on the title menu. `F3`
swaps to the flat 2D debug view and back. The interface scales with the
window height; the 3D view always renders at full resolution.

To skip the title menu while developing: `godot --path game -- --new-city`
(an empty city) or `-- --demo-city` (a small demo town); add `--open-budget`,
`--open-saves`, `--open-zones` or `--inspect=x,y` to start with that open.

## What works today

- Tile map with zones, roads, power lines and plants
- Buildings grow on zones that have power, a road within three tiles and
  demand: houses, shops and factories, drawn as low-poly models. Jobs pull
  residents, residents pull shops, workers pull industry; the top bar shows
  population and R/C/I demand, and the query tool says why a zone is not growing
- Buildings without power or a road, or in a zone whose demand has collapsed,
  struggle: after three bad months they are abandoned (grey, no residents or
  jobs), after nine they are cleared; good months bring them back
- Building types are data: `game/content/base` holds each type's numbers
  (`*.sim.json`) and models (`*.art.json`); change a number there and restart
- A monthly budget: residents and jobs pay tax at a rate per zone (0-20%,
  7% to start); every road, power line and plant tile costs upkeep, so a
  spread-out city pays more. Taxes above 7% lower a zone's demand, below raise
  it. Funds can go into debt (no bankruptcy yet). Numbers are placeholders,
  in the base pack's `upkeep` fields
- Command queue applied on fixed simulation ticks
- Power grid: flood fill from plants through lines and zones; each plant has a
  capacity, and when a network is over it the zones farthest from a plant go dark
- Power lines can cross roads (for a fee); a plain road still blocks power
- A minimal title menu and pause menu
- Versioned JSON saves, written atomically; monthly autosave and Continue on the title menu; a loaded city continues exactly as the saved one would
- A calendar derived from ticks, pause and three speeds
- A 3D view in Godot: low-poly roads that join into bends, junctions and dead
  ends, power poles, a power plant, zones as tinted lots; sun, soft shadows and
  ambient occlusion; a camera that pans, zooms, rotates and tilts; tile picking
  with a live drag preview
- The build toolbar, top and status bars, and a flat 2D debug view (F3)
- Music, city ambience and sound effects, with volume sliders on the pause menu

## Contributing

Early days; issues and ideas are welcome. Run `dotnet test` before
opening a pull request. CI runs the simulation tests on every push.

## License

BSD 3-Clause. See [LICENSE](LICENSE).

Third-party music, sounds and other assets keep their own licences (CC0 or
CC-BY); each file is listed with its source and licence in
[ASSETS.md](ASSETS.md).
