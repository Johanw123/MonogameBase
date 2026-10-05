# Beyond the Belt — feature trailer

90 seconds, landscape 16:9, 60 fps. Real capture-mode gameplay with staged progression snapshots, auto-refuel enabled, and understated feature labels. Music: Pleiadeans by Holizna, from the game soundtrack.

| Time | Shot |
| --- | --- |
| 0–8.33 | Mid-game fleet, Genesis Pulse, cascade and collection |
| 8.33–15 | In-game title and original crash landing |
| 15–20 | Manual collecting |
| 20–25 | Real upgrade purchases |
| 25–30 | First harvesters, then all five classes |
| 30–40 | Main Battery Relay, quad lasers and Beam Riders |
| 40–46.67 | Golden Genesis spiral and Graviton Cascade |
| 46.67–55 | Ship system upgrades followed by Kamikaze Wing and Core Drill |
| 55–61.67 | Legendary module reveal and fitting |
| 61.67–66.67 | Signal scan and selection |
| 66.67–75 | Core fracture, shard extraction and laser upgrade |
| 75–80 | Planetary Overload |
| 80–85 | Late-game pull-out |
| 85–90 | Logo over moving gameplay |

No invented progression times, debug endgame, or promotional questions.

## Recreate

The final export is 1920 × 1080 at 60 fps. Gameplay takes use 60 fps; UI takes use 30 fps. All scenes use temporary capture saves and auto-refuel. These are staged snapshots, not measured play times.

Record `scenes/*.json` with the shorts skill capture script, then run `render_trailer.py` for the complete edit. Bump video output versions before re-recording or rendering. `--preview` uses the latest available preview take for each shot.

For this cut the first 40 seconds were rendered separately with `edit_opening.json`, then `finish_trailer.py` rendered the remaining 50 seconds and joined both with one continuous music/game-audio mix. The separate section files contain video only.
