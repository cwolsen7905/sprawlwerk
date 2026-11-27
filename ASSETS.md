# Third-party assets

Every file under `game/assets/` is listed here with its source and licence.
`tools/check-assets.sh` fails CI when a file is missing from this list or
its licence is not one of: `CC0-1.0`, `CC-BY-3.0`, `CC-BY-4.0`, `OFL-1.1`
(SIL Open Font License, for fonts only).
The credits screen is built from this list.

Files were renamed on the way in; the original name is in the Notes column.
A row whose path ends in `/` covers every file in that directory; model kits
are listed that way, with the kit's own licence file kept alongside.

## Models

Only the pieces the game uses are copied, with their original names and the
kit's shared texture (`Textures/colormap.png`).

| File | Title | Author | Source | Licence | Notes |
|---|---|---|---|---|---|
| `game/assets/models/kenney/city-kit-roads/` | City Kit (Roads) | Kenney | https://kenney.nl/assets/city-kit-roads | CC0-1.0 | Road pieces and electricity pole, GLB; `License.txt` included |
| `game/assets/models/kenney/city-kit-industrial/` | City Kit (Industrial) | Kenney | https://kenney.nl/assets/city-kit-industrial | CC0-1.0 | `building-c` and `chimney-large` (power plant); `building-a`, `b`, `d`, `e`, `f`, `g` (grown industry), GLB; `License.txt` included |
| `game/assets/models/kenney/city-kit-suburban/` | City Kit (Suburban) | Kenney | https://kenney.nl/assets/city-kit-suburban | CC0-1.0 | `building-type-a` to `f` (grown houses), GLB; `License.txt` included |
| `game/assets/models/kenney/city-kit-commercial/` | City Kit (Commercial) | Kenney | https://kenney.nl/assets/city-kit-commercial | CC0-1.0 | `building-a` to `f` (grown shops), GLB; `License.txt` included |

## Interface

| File | Title | Author | Source | Licence | Notes |
|---|---|---|---|---|---|
| `game/assets/ui/icons/` | SprawlWerk UI icons | SprawlWerk contributors (made with Claude Design) | Drawn for this project | CC0-1.0 | 31 SVGs, 20 × 20, stroked in currentColor; kept as delivered, with their embedded provenance metadata |

## Sound effects

| File | Title | Author | Source | Licence | Notes |
|---|---|---|---|---|---|
| `game/assets/audio/sfx/ui_click.ogg` | Interface Sounds | Kenney | https://kenney.nl/assets/interface-sounds | CC0-1.0 | `click_002.ogg` |
| `game/assets/audio/sfx/ui_open.ogg` | Interface Sounds | Kenney | https://kenney.nl/assets/interface-sounds | CC0-1.0 | `open_001.ogg` |
| `game/assets/audio/sfx/ui_close.ogg` | Interface Sounds | Kenney | https://kenney.nl/assets/interface-sounds | CC0-1.0 | `close_001.ogg` |
| `game/assets/audio/sfx/confirm.ogg` | Interface Sounds | Kenney | https://kenney.nl/assets/interface-sounds | CC0-1.0 | `confirmation_001.ogg` |
| `game/assets/audio/sfx/refused.ogg` | Interface Sounds | Kenney | https://kenney.nl/assets/interface-sounds | CC0-1.0 | `error_004.ogg` |
| `game/assets/audio/sfx/place.ogg` | Interface Sounds | Kenney | https://kenney.nl/assets/interface-sounds | CC0-1.0 | `drop_002.ogg` |
| `game/assets/audio/sfx/ui_hover.ogg` | UI Audio | Kenney | https://kenney.nl/assets/ui-audio | CC0-1.0 | `rollover2.ogg` |
| `game/assets/audio/sfx/place_building.ogg` | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0-1.0 | `impactMetal_heavy_000.ogg` |
| `game/assets/audio/sfx/bulldoze.ogg` | building collapse / demolition | greyfeather | https://freesound.org/people/greyfeather/sounds/712918/ | CC0-1.0 | Freesound high-quality OGG preview |

## Ambience

| File | Title | Author | Source | Licence | Notes |
|---|---|---|---|---|---|
| `game/assets/audio/ambience/city.ogg` | Distant, quiet, consistent city ambience | danner | https://freesound.org/people/danner/sounds/426894/ | CC0-1.0 | Freesound high-quality OGG preview |
| `game/assets/audio/ambience/birds.ogg` | Spring Birds Loop | hargissssound | https://freesound.org/people/hargissssound/sounds/345852/ | CC0-1.0 | Freesound high-quality OGG preview |

## Music

| File | Title | Author | Source | Licence | Notes |
|---|---|---|---|---|---|
| `game/assets/audio/music/calm_track.ogg` | Calm Track | pmiller | https://opengameart.org/content/calm-track | CC0-1.0 | `calm_track-loop.ogg` |
| `game/assets/audio/music/jazz_brunch.mp3` | Jazz Brunch | Kevin MacLeod (incompetech.com) | https://incompetech.com/music/royalty-free/ | CC-BY-4.0 | Credit required, see below |
| `game/assets/audio/music/wholesome.mp3` | Wholesome | Kevin MacLeod (incompetech.com) | https://incompetech.com/music/royalty-free/ | CC-BY-4.0 | Credit required, see below |
| `game/assets/audio/music/airport_lounge.mp3` | Airport Lounge | Kevin MacLeod (incompetech.com) | https://incompetech.com/music/royalty-free/ | CC-BY-4.0 | Credit required, see below |

## Required credits

CC-BY tracks, credited as the author asks:

```
Jazz Brunch Kevin MacLeod (incompetech.com)
Licensed under Creative Commons: By Attribution 4.0
https://creativecommons.org/licenses/by/4.0/

Wholesome Kevin MacLeod (incompetech.com)
Licensed under Creative Commons: By Attribution 4.0
https://creativecommons.org/licenses/by/4.0/

Airport Lounge Kevin MacLeod (incompetech.com)
Licensed under Creative Commons: By Attribution 4.0
https://creativecommons.org/licenses/by/4.0/
```

CC0 needs no credit; we thank Kenney (kenney.nl), greyfeather, danner,
hargissssound and pmiller anyway.
