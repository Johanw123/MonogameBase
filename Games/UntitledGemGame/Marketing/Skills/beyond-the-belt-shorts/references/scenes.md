# Scene files (game capture mode)

One JSON file = one take. The game boots offscreen, stages a save, simulates
`warmup` seconds, then records `duration` seconds at a fixed timestep (slow
encoding never changes the pacing) and exits.

```
dotnet bin/Debug/net10.0/UntitledGemGame.dll --capture scene.json [--overwrite]
dotnet bin/Debug/net10.0/UntitledGemGame.dll --capture-list catalog.json
```
Use `scripts/capture.py` instead of calling these directly (it sets the
offscreen environment, rebuilds when needed, writes game audio and a sheet).

## Example

```json
{
  "output": "../takes/fleet_swarm.mp4",
  "preset": "Late game: fleet",
  "width": 2160, "height": 3840, "fps": 60,
  "warmup": 8, "duration": 10,
  "zoom": 1.6,
  "save": {
    "gems": 2.5e12,
    "upgrades": {"HC1": "max", "AHC1": 3},
    "abilities": {"GS1": 1, "CM1": "max"},
    "equip": ["spawner", "chain"]
  },
  "stats": {"MaxGemCount": 20000, "AutoRefuel": true},
  "actions": [
    {"at": 1.0, "do": "click_gems", "dur": 3, "rate": 3},
    {"at": 4.5, "do": "ability", "id": "chain"},
    {"at": 6.0, "do": "manual", "id": "Collector Swarm"},
    {"at": 8.0, "do": "zoom", "value": 0.6, "dur": 2}
  ]
}
```

Keys are snake_case; unknown keys are an error (typos fail before booting).
Comments (`//`) and trailing commas are allowed (also in edit files).

## Top level

| key | default | meaning |
|---|---|---|
| `output` | required | `.mp4`, relative to the scene file. Writes `<name>.capture.json` beside it. |
| `width`, `height` | 2160, 3840 | Output size. The world always renders at the pixel area of a 4K screen in this aspect (9:16 -> 2160x3840, 1:1 -> 2880x2880), so framing does not depend on output size; smaller outputs are scaled down. |
| `fps` | 60 | 120 for slow motion (play at `speed: 0.5`). |
| `time_scale` | 1 | Time-lapse: the game simulates N real steps per recorded frame (6 = six times speed; label it on screen). `duration` and action times are footage seconds. |
| `warmup` | 6 | Simulated seconds before recording. The landing intro + logo take ~3.5 s; ≥5 s skips them. `0` records the intro (a good hook). |
| `duration` | 10 | Recorded seconds. |
| `preset` | Beginning | A progression preset name or index, or a feature preset name (list below / `--capture-list`). |
| `save` | | Changes to the preset's save before loading (below). |
| `stats` | | Raw stat values by short or property name (`"CameraZoomScale": 2.0`, `"HCE": true`), applied after loading, like the debug stat sliders. Kept on top of later upgrades. |
| `auto_fractures` | true | `false` stops damage from setting off core fractures on its own (weapon showcases deal plenty); the `fracture` action still works. |
| `zoom` | 1 | Multiplies the camera zoom after loading. Gameplay happens in the camera view, so zooming in also makes the play area smaller and denser; zooming out gives more space. |
| `hud` | false | Compose the real HUD over the world. Needs a 16:9 size (renders 3840x2160). For upgrade trees, shipyard, signals. |
| `hud_inset` | false | Keep gems out of the HUD strip even without a HUD. Off: the whole frame is play area. |
| `encoder` | auto | `nvenc` (fast, default when available) or `x264`. |
| `quality` | 16 | CQ/CRF; lower is better. Takes are intermediates, keep it high. |
| `stills` | | Seconds of recorded footage to also save as lossless PNGs beside the take (`<take>_<t>s.png`, full capture size): store screenshots from the same frames the video gets. |
| `benchmark` | false | Measure instead of record: no video, the game runs unthrottled and `<name>.capture.json` gets a `benchmark` block (frame/update/draw times, GC, draw calls, every frame's time). Stills still work; their frames and the two after them are left out of the timings. Used by `benchmark.sh` with the scenes in `tools/benchmark/scenes`. |

## save

| key | meaning |
|---|---|
| `gems`, `earned_this_run`, `ability_points`, `prestige_points` | Currencies (numbers, `1e12` ok). `earned_this_run` defaults to at least `gems` (manual abilities unlock from it). |
| `active_gems` | Gems on the field at load. |
| `upgrades`, `abilities`, `meta` | `{id: level}` per tree; level is a number or `"max"`, `0` removes. `"GS*": "max"` sets every node starting with GS, `"*"` the whole tree (a fully built ability needs its sub-nodes, not just the root). Blocking prerequisites are added at level 1. Ids from `--capture-list` (`upgrades[].id`, `tree`). |
| `equip` | Abilities to equip, by id or name: `spawner`/`GS1` (Genesis Pulse), `chain`/`CM1` (Graviton Cascade), `drones`/`Drones1`, `drill`/`CoreDrill1` (Core Drill), `kamikaze`/`KW1` (Kamikaze Wing, which replaces Drone Swarm; its talents in `abilities` imply the Kamikaze Drones talent). Each must be unlocked in `abilities`. In game every online ship system takes a slot, so systems left out of this list are taken offline (`[]` = none). (Speed1/HBM1 exist in code but have no tree node.) Equip only what the shot is about: built-out abilities fire every 1.5-3 s on their own. |
| `modules` | `"all"` or module names to own (unlocks the shipyard). |
| `fit` | Modules fitted per fleet class, owned for you: `{"harvester": ["Rocket Rack", "Gun Pod"], "advanced": ["Laser Uplink"]}` (up to 4 per class; each module fits once; slots past two add Module Bays ranks). |
| `reveal` | Module names queued as sealed discoveries (inspect them in the shipyard's Discovery tab: legendary reveals take 3.6 s and end on an impact). |
| `signals` | `[{"name": "Gem Value", "rarity": "Legendary", "count": 2}]` (unlocks signals). |
| `core_fractures` | Core fractures already this run: the planet starts cracked and swollen (7% per fracture, up to 8), without its shell. |
| `shell` | The share of the planet's shell left, `1` untouched to `0.1` badly cracked. Each run's planet starts sealed in a dark shell with a violet edge glow; weapon damage cracks it (250 damage in total) until it shatters and the planet shows. Unset, the shell is already gone, so scenes show the planet unless they ask for the shell. It breaks on its own damage even with `auto_fractures: false`. |

## Presets

Progression: Beginning, First upgrades, First prestige, Developing fleet,
Mid game: logistics, Mid game: abilities, Late game: production, Late game:
fleet, Early game: clicking, Mid game: clicking, Late game: clicking, Uber
endgame (everything maxed, deliberately unrealistic). Feature sandboxes:
Abilities: starter / fully upgraded, Shipyard: Drifters / Seekers / Prospectors
/ Trove hunters / Rimrunners, Modules: discovery queue, Signals: pending choice,
Manual collection, Weapons: all unlocked. These are developer snapshots, not play times.

## actions

`at` is seconds of recorded footage (negative = during warmup). Positions are
normalized frame coordinates `[x, y]` (0..1, top left). `target: "gems"` aims at
a weighted gem cluster, `"home"` at the homebase, `"planet"` at the planet. The pointer starts hidden; the
game's own click cursor ring follows it while visible.

| do | fields | effect |
|---|---|---|
| `pointer` | `pos`/`target`/`ui`, `dur` | Show the pointer and glide there (eased). `target`: `gems`, `home`, `planet`, or `shard` (a Core Shard waiting after a `fracture`). |
| | `ui` | With `hud: true`, aim at a named HUD element via the game's own layout: `nav:upgrades/abilities/shipyard/signals`, `discovery`, `inspect`, `reveal_skip`, `reveal_continue`, `reveal_shipyard`, `ship:<class>`, `slot:<0-3>`, `module:<name>`, `scan`, `card:<0-2>`, `command:<0-4|name>` (the manual fleet command buttons; a click fires the command through the game's own input), `node:<id>` (a node of the open upgrade tree, e.g. `node:LZQ1`; glide there, then buy it with `upgrade`). `damage` is the Damage panel's header (top right): a click opens or closes the panel listing the planet's damage by source (closed by default in captures). Works on pointer, click and hold (drag a module = hold on `module:X` + pointer to `slot:N`). The capture draws a mouse cursor in HUD shots. |
| `hide` | | Hide the pointer (no cursor ring). |
| `click` | `pos`/`target` | One left click. |
| `click_gems` | `dur`, `rate` (clicks/s, 3) | A player clicking through gem clusters. |
| `hold` | `pos`/`target`, `dur` | Hold left (sustained harvest when unlocked). |
| `gravity` | `pos`/`target`, `dur` | Cursor gravity well (when unlocked): holds right and left-clicks once, as the game expects; the well lives for its own duration. |
| `ability` | `id` | Fire an equipped automatic ability now (resets its cooldown to 0). |
| `manual` | `id` (name or slot 0-4) | Trigger a manual fleet ability: Overdrive, Planet Cracker, Collector Swarm, Homebase Magnetizer, Ability Surge. Fails if locked or recharging. |
| `upgrade` | `id` | Buy through the real purchase path (cost, sounds, animation). Fails if locked or unaffordable. |
| `level` | `id`, `value` (level or "max"; default +1) | Set a level instantly (debug path, no cost). Good for "and now 3 more ships". |
| `stat` | `id`, `value` | Change a raw stat mid-shot. |
| `zoom` | `value` (multiplier), `dur` | Smooth camera zoom, e.g. 0.6 pulls out to reveal more space. |
| `panel` | `id`: none/upgrades/abilities/meta/shipyard/signals; `value`: tab index for abilities (Ship Systems: 0 Drone Swarm, 1 Graviton Cascade, 2 Genesis Pulse, 3 Core Drill, 4 Kamikaze Wing; 0 opens the Kamikaze Wing when it has replaced Drone Swarm) | Open a HUD window (use with `hud`). Ship Systems need an online system (`save.abilities`), which grants Auxiliary Power. |
| `prestige` | | Start the prestige sequence. |
| `fracture` | | While the planet still has its shell (`save.shell`), shatter it: about 0.9 s of strain as light floods its cracks, then its plates fly apart over 1.6 s; nothing freezes and no shard comes out. Otherwise start a core fracture now: weapons and ships freeze, the planet shakes and pulls the field back in, then erupts, blowing the fleet to the screen edges, and releases a Core Shard (about 4.5 s until everything moves again; click the shard with `target: shard`). |
| `new_run` | | After a prestige: start the next run (what the permanent-upgrade tree's Apply button does). |
| `marker` | `name` | Only logs a named event. |
| `event` | `id`: cannon, rockets or railgun | Fire a main ship weapon at the planet now, through the game's own weapon code: one cannon shot, a rocket salvo or a Railgun round (works even before the weapon is unlocked; its upgrades set the size). An owned railgun first winds up on its turret (0.85 s at its base fire rate, shorter when fire rate is high) and the round lands about 0.1 s after it fires; one that is not owned fires at once. |
| `gems` | `points` [[x, y], ...], `gem` (Blue, DarkBlue, Gold, LightGreen, Lilac, Purple, Red, Teal), `from` ("" in place, "home", "edges"), `order` (left/random/center), `dur` | Real gems at exact frame positions; with `from` each one is launched and stops on its point (~0.4 s), spawns spread over `dur`. |
| `gem_image` | `image` (path from the scene), `gem` (list of types), `colour` ("x": bands left to right; "nearest"), `cols` (70), `width`, `pos`, `alpha` (140), `from`, `order`, `dur` | Any image (e.g. the logo) built from gems: opaque pixels sampled on a dot grid (capture.py expands it into `gems`). |
| `gem_text` | `text` (`\n` lines), `gem` (type or one per line), `pos` (block centre), `width` (0.86), `rows` (dots per line, 11), `gap` (line heights between lines), `from`, `order`, `dur` | Text spelled in gems (expanded to `gems` by capture.py: a heavy font sampled on a dot grid). For a clean frame keep the weapons quiet (`"stats": {"AutoCannon": false, "MaxGemCount": 3000}`, `"save": {"active_gems": 0, "upgrades": {"HU1": 0}}`). Collect it afterwards with `manual: Homebase Magnetizer` (all of it in ~1.3 s; needs `earned_this_run` >= 5e6). |

## Capture log (`<take>.capture.json`)

`events`: `{t, type, ...}` with `action`, `click` (`pos`), `ability` (automatic
activations, id), `sound` (`file`, `volume`, `pitch`, `pan`). `samples` every
0.25 s: `gems`, `gems_per_minute`, `earned_this_run`, `active_gems`. Use the
events for cut points ("chain fired at 4.53 s") and the samples for honest
numbers in text. `scripts/sfx.py` rebuilds the game audio from the sound events
(`capture.py` does this automatically: `<take>.sfx.wav`).

## Limits

- Offscreen rendering is silent; game audio is rebuilt from the log (all
  one-shot sounds; no music from the game).
- Randomness is not seeded: takes differ every run. Record longer and choose.
- HUD clicks are not simulated; use `panel`, `upgrade`, `level` instead.
- The HUD is laid out for 16:9 only.
- Rarely the game hangs on shutdown after a finished take; capture.py stops it
  15 s after "CAPTURE DONE" (the take is complete) and has an overall timeout.
