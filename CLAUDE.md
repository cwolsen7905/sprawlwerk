# SprawlWerk

Open-source city builder re-imagining the classic isometric city sims of
the 90s. Website repo: sprawlwerk-web (GitLab). Primary repo: GitHub,
mirrored to gitlab.brainchurts.com/brainchurts/sprawlwerk by
`.github/workflows/mirror-gitlab.yml`.

## Architecture rules

- `src/SprawlWerk.Sim` is engine-agnostic. Never reference Godot from it.
  It must build and test with plain `dotnet test`.
- All player actions go through `ICommand` and are applied in
  `Simulation.Step()`. The game never mutates `CityMap` directly.
- The simulation advances in fixed ticks, independent of frame rate.
- Recompute derived networks (power, later water/traffic) only when the
  map changes; mark dirty instead of recomputing every tick.
- `game/` is rendering, input and UI only. `CityView.cs` is a debug view;
  real rendering (tilesets, terrain height, buildings) replaces it later.

## Conventions

- C# 12, nullable on, file-scoped namespaces, `_camelCase` private fields.
- Every simulation rule gets a test in `tests/SprawlWerk.Sim.Tests`.
- Keep `Godot.NET.Sdk` in `game/SprawlWerk.csproj` matched to the editor
  version (currently 4.7.2).
- Godot workflow: read the whole script before editing; discuss before
  major changes.
- No SimCity/EA/Maxis names, art, data or assets anywhere in the repo.
