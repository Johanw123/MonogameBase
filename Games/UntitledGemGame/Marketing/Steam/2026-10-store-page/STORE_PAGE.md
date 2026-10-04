# Store page update (October 2026)

Everything here is recorded with the game's capture mode: real gameplay on staged saves,
nothing composited. Regenerate with `capture.py scenes/*.json` (takes and the PNG stills)
and `python3 make_clips.py` (the description clips).

## Steam's rules (checked 2026-10-04)

- **About This Game media** ([Store Page Extra Asset Management](https://partner.steamgames.com/doc/store/page/assets)):
  PNG, JPG, GIF, WEBP, MP4 or WEBM; animations only play in About This Game and
  announcements; **1170 px wide** recommended (780 px column at 150% DPI), up to 4096 px;
  **animations at most 12 s**; tag the color space **BT.709**. Steam transcodes every upload.
- **Description guide** ([Store Page Written Description](https://partner.steamgames.com/doc/store/page/description)):
  images **under 5 MB** each, **about 15 MB total** for the section; prefer **textless**
  images (text in images has to be re-made per language).
- **Screenshots** ([Store Graphical Assets](https://partner.steamgames.com/doc/store/assets/standard#screenshots)):
  **at least 5**, **1920x1080 or larger, 16:9**; gameplay only (no concept art, marketing
  text or awards); in-game UI is welcome; mark at least 4 as suitable for all ages.

The clips are 1170x658 H.264 MP4, 60 fps, BT.709, no audio (store animations play muted),
7.5-11.4 s, and cross-fade their last 0.4 s into their start so the autoplay loop has no jump.
The screenshots are 3840x2160 PNG, captured losslessly from the same frames.

## Suggested About This Game layout

Keep the current opening line, then one heading, one sentence and one clip per feature.
Upload the clips with the description editor's "Upload Custom Image" and add alt text.

**Collect gems** - `clips/01_collect_gems.mp4`
Click to harvest the gems drifting through the belt. Every gem pays for your next upgrade.

**Spend on upgrades** - `clips/02_upgrade_trees.mp4`
More than 300 upgrades across three trees: upgrades, abilities and prestige.

**Build a fleet** - `clips/03_ship_classes.mp4`
Five ship classes, each hunting gems its own way: Drifters, Seekers, Prospectors, Trove Hunters and Rimrunners.

**Combine abilities** - `clips/04_abilities.mp4`
Genesis Pulse grows rings of gems, Graviton Cascade pulls them home, Drone Swarms sweep the field.

**Command your fleet** - `clips/05_commands.mp4`
Overdrive, Reserve Burst, Collector Swarm, Homebase Magnetizer and Ability Surge, one press away.

**Discover modules** - `clips/06_modules.mp4`
Salvage sealed modules and slot them into your ships. 55 to find.

**Scan deep space** - `clips/07_signals.mp4`
Pick one of three signals per scan. They stack.

**Prestige** - `clips/08_prestige.mp4`
Reset for prestige points and permanent upgrades, then grow faster than before.

**Grow to billions** - `clips/09_late_game.mp4`
From one ship and a handful of gems to a belt full of fleets.

## Screenshots

3840x2160 PNG, in suggested order (Steam shows the first few on the store front and in hovers;
all are gameplay with the real HUD, and all are suitable for all ages):

 1. `screenshots/01_late_game_field.png` (from `takes/d8_prestige_1.20s.png`)
 2. `screenshots/02_genesis_pulse_golden_age.png` (from `takes/d4_abilities_3.30s.png`)
 3. `screenshots/03_graviton_cascade_constellation.png` (from `takes/d4_abilities_5.00s.png`)
 4. `screenshots/04_upgrade_tree.png` (from `takes/d2_upgrades_11.00s.png`)
 5. `screenshots/05_legendary_module_reveal.png` (from `takes/d6_modules_6.90s.png`)
 6. `screenshots/06_deep_space_signals.png` (from `takes/d7_signals_4.60s.png`)
 7. `screenshots/07_ability_tree.png` (from `takes/s2_ability_tree_1.50s.png`)
 8. `screenshots/08_prestige_upgrades.png` (from `takes/s1_meta_1.50s.png`)
 9. `screenshots/09_collector_swarm_command.png` (from `takes/d5_commands_2.50s.png`)
10. `screenshots/10_ship_classes.png` (from `takes/d3_fleet_v2_6.00s.png`)
11. `screenshots/11_early_game_clicking.png` (from `takes/d1_collect_v3_5.00s.png`)
12. `screenshots/12_late_game_fleet.png` (from `takes/d9_scale_11.00s.png`)
13. `screenshots/13_shipyard_module_slots.png` (from `takes/d6_modules_10.70s.png`)
14. `screenshots/14_storm_drones.png` (from `takes/d4_abilities_9.00s.png`)
