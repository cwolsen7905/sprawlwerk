# Backlog

Where the project stands and what is next, ordered menus first, then
gameplay, then the rest. Sizes: **S** under a day, **M** a few days,
**L** a design in its own right.

Sources checked: open issues on the GitHub repo (none yet) and TODO/FIXME
comments in the code (none), plus the maintainers' own planning notes. The
issue column says "none yet" until an issue is filed; link it here when it is.

Status: `done` (in this branch), `held` (needs a decision, proposal below),
`blocked` (waits on another item), `open`.

## Menus

| # | Item | Size | Issue | Status |
|---|------|------|-------|--------|
| M1 | Strings in a table, not inline in views | S | none yet | done |
| M2 | Title screen and main menu | L | none yet | minimal shell done (New city, Quit); splash and 3D backdrop later |
| M3 | Pause menu, unsaved-changes prompt, settings (video, audio, controls, UI scale) | L | none yet | pause overlay done (Resume, Quit); prompt and settings open |
| M4 | New-city setup and loading screen | M | none yet | blocked (map sizes undecided) |
| M5 | Save browser, Continue, autosave | M | none yet | blocked (needs save/load, G5) |
| M6 | Toolbar replacing the key-only tool picker | M | none yet | held (P1) |
| M7 | Credits and third-party asset list | S | none yet | blocked (no third-party assets yet) |
| M8 | Colour-blind-safe zone and overlay palette | S | none yet | done for tiles (overlays wait on overlays) |

## Gameplay

| # | Item | Size | Issue | Status |
|---|------|------|-------|--------|
| G1 | Calendar (day, month, year) derived from ticks | S | none yet | done |
| G2 | Pause and three speeds | S | none yet | done |
| G3 | Power capacity: plants supply a limited amount, the player is told about shortfalls | M | none yet | done (placeholder numbers: 40 per plant, 1 per zoned tile) |
| G4 | Power lines may cross roads, at a cost | S | none yet | done (crossing tile, extra fee per tile) |
| G5 | Save and load, versioned format | L | none yet | held (P3) |
| G6 | Demand and growth: buildings appear on zones | L | none yet | held (P4) |
| G7 | Budget, taxes, network upkeep | L | none yet | held (P4) |
| G8 | Rectangle and line drag with a cost preview | M | none yet | done |
| G9 | Query tool: click a tile to see what it is and why it is not growing | M | none yet | blocked (needs G6) |
| G10 | Zone densities | M | none yet | blocked (needs G6) |

## The rest

| # | Item | Size | Issue | Status |
|---|------|------|-------|--------|
| R1 | Replace the 2D debug view with a 3D view (terrain chunks, camera, instanced tiles) | L | none yet | open, own design |
| R2 | First audio and volume buses | M | none yet | blocked (asset source undecided) |
| R3 | Road graph and traffic | L | none yet | open |
| R4 | Water network and underground view | L | none yet | open |
| R5 | Desktop export presets and release CI | M | none yet | open |
| R6 | Simulation on a worker thread | M | none yet | open |

## Proposals held for a decision

**P1: menu structure (M2, M3, M6), decided: option (a).** Options: (a) a minimal shell now: title
with New / Quit, a pause overlay with Resume / Quit, on Godot `Control`
nodes in the 2D debug view; (b) wait for the 3D view so menus sit over the
real city, building only the speed and pause controls meanwhile; (c) the
full set (settings, rebinding, UI scale) at once. Recommend (b): menus that
sit over the debug view get rebuilt, and settings storage is worth designing
once.

**P2: power rules (G3, G4), decided: option (a).** Capacity needs a number per plant and a per-zone
demand, which is a balance choice. Options: (a) one flat capacity per plant
and one unit of demand per zoned tile, powered tiles chosen nearest the plant
first; (b) data-defined plant types from the start; (c) keep the free
grid and revisit with growth. Recommend (a) for capacity, shipped together
with the crossing-roads rule, with the numbers kept in `PriceList`-style data
so they are easy to retune.

**P3: save format (G5).** Options: (a) JSON with a version number; (b)
a compact binary format; (c) a command log plus a seed. Recommend (a) now:
readable, diffable, easy to migrate; revisit if saves get large.

**P4: growth and budget (G6, G7).** Growth is a balance philosophy (how
punishing sprawl should be). Options: (a) tick-based growth gated on power
and road access only; (b) full demand model with monthly update; (c) wait
for the calendar and data-defined buildings. Recommend (c): the calendar is
in (G1), buildings as data comes next.
