# Store page refresh — 9 November 2026

Current gameplay from native 4K/60 captures of build `a00bfd91` (9 November):
the Weaponry panel with per-weapon upgrade trees, the Tractor Belt, the
current upgrade, Ship Systems, Shipyard, Signals and talent screens, and the
current weapon, fracture and extraction effects. Progression states and
discoveries are staged for filming; there are no play-time claims. Automatic
refueling is on in every take. No new art or music was generated. Promotional
overlays and Shorts end cards are left out of these store assets. Nothing has
been uploaded or published.

[Review the assets](review.html) · [Source manifest](assets.json) ·
[itch.io versions](../../Itch/2026-11-09-refresh/ITCH_PAGE.md) ·
[Previous refresh (25 October)](../2026-10-25-refresh/STORE_PAGE.md)

## What changed since 25 October

- **New: Weaponry** (`09_weaponry`, `12_weaponry`). Weapon upgrades moved out of
  the main upgrade tree into a Weaponry panel with one tree per weapon, so the
  old "upgrade tree" animation could no longer be filmed. It is replaced by a
  Weaponry clip. The main upgrade tree stays in the gallery as a screenshot.
- **New: Tractor Belt** (`12_tractor_belt`, `13_tractor_belt`), the right-click
  power that replaced the gravity well.
- **Modules** now reveal and fit Thunder God instead of World Eater, which is
  due for a rework. The copy no longer describes World Eater.
- Every other clip and screenshot was recaptured from the same scenes on the
  current build. The prestige scene needed one extra Tier 1 talent, because
  Tier 4 now opens at 10 points.

## Upload formats (checked 25 October 2026, not re-checked)

Steam's [extra-asset documentation](https://partner.steamgames.com/doc/store/page/assets)
supports PNG/JPG/GIF/WEBP/MP4/WEBM, recommends 1170 px width, limits animations
to 12 seconds, and recommends BT.709 video metadata. The clips here are silent
1170×658 H.264 MP4 at 30 fps, with a short loop crossfade and BT.709 tags.

The [description guide](https://partner.steamgames.com/doc/store/page/description)
asks for images below 5 MB and a total section size below 15 MB. All twelve
clips together come to 10.9 MB, and the largest is 2.2 MB.

The [screenshot requirements](https://partner.steamgames.com/doc/store/assets/standard#screenshots)
require at least five gameplay screenshots, minimum 1920×1080, 16:9. Mark at
least four as suitable for all ages. All thirteen screenshots here are native
3840×2160 frames of actual gameplay or the game's own upgrade, weapon, system,
module, signal and talent screens. The PNGs are decoded 4K source-video frames.

## Suggested description

Beyond the Belt is an incremental space game about mining planets, collecting
gems and building a fleet. Start with manual cannon fire, arm your ship with
new weapons, develop ship systems, and extract the core to learn permanent
talents.

| Heading | Copy | Animation / alt text |
|---|---|---|
| Mine and collect | Fire at the planet and collect the gems your weapons release. Every haul funds another upgrade. | [Collect gems](clips/01_collect_gems_v1.mp4): A working ship collecting gems beside the planet. |
| Arm your ship | Add weapons that knock more gems loose and combine their effects across the planet. | [Weapons](clips/02_weapons_v1.mp4): Cannon fire, beams, harpoons, rockets and railgun shots striking the planet. |
| Upgrade every weapon | Each weapon has its own upgrade tree in Weaponry: fire rate and power, plus nodes such as Overheat Surge and Rocket Count that change how it fires. | [Weaponry](clips/09_weaponry_v1.mp4): Buying Mining Laser and Rocket Pods upgrades in the Weaponry panel. |
| Build a fleet | Unlock different ship classes and let them collect, return and deliver while you plan the next upgrade. | [Fleet](clips/03_fleet_v1.mp4): An upgraded fleet collecting and returning gems. |
| Haul with the Tractor Belt | Right-click to drop a Tractor Belt intake on a cluster, and a line of pods carries the gems back to your ship. Drag the intake to the next cluster. | [Tractor Belt](clips/12_tractor_belt_v1.mp4): Placing a Tractor Belt intake on a cluster of gems, then dragging it to a second cluster. |
| Crack the core | Core fractures pull the loose gems back into the planet before an eruption releases a Core Shard. | [Core fracture](clips/04_core_fracture_v1.mp4): The planet cracking and erupting, then a Core Shard being collected. |
| Develop ship systems | Buy Power Cells and spend them in each system's talent tree to unlock deeper rows and keystones. | [Ship systems tree](clips/10_ship_systems_v1.mp4): Buying a Power Cell and unlocking Genesis Pulse rows in the Ship Systems panel. |
| Fire active systems | Trigger equipped systems such as Genesis Pulse to seed rings of fresh gems around your base. | [Genesis Pulse](clips/05_genesis_pulse_v1.mp4): Genesis Pulse creating rings of gems around the planet. |
| Discover modules | Reveal modules and fit them to your ships. Thunder God gives every ship of a class a branching lightning storm that collects gems as it flies. | [Modules](clips/06_modules_v1.mp4): Revealing Thunder God and fitting it into a Drifter's module slot. |
| Scan deep space | Choose one of three signal discoveries. Their bonuses stack as you keep collecting. | [Signals](clips/07_signals_v1.mp4): Three signal cards with different bonuses and a discovery being selected. |
| Start the next loop | Hold Extract Core to collapse the planet and claim your prestige points. | [Extraction](clips/08_extraction_v1.mp4): The planet's core collapsing into a singularity during extraction. |
| Learn permanent talents | Spend prestige points across six tiers of permanent talents and free tier rewards before starting your next run. | [Prestige upgrade tree](clips/11_prestige_tree_v1.mp4): Spending prestige points to unlock Tier 4 and learn permanent talents. |

Upload the MP4s through the description editor's custom-image/extra-asset
uploader, then add the corresponding alt text.

## Screenshot gallery

Use the JPEG upload versions below (each 3840×2160 and under 4.9 MB). Full-resolution
decoded 3840×2160 PNG masters are kept in `screenshots/`. The separate itch.io
variants are 1920×1080.

Suggested order, with actual filenames:

1. [Working fleet](screenshots-upload/01_working_fleet.jpg)
2. [Weapons](screenshots-upload/02_weapons.jpg)
3. [Tractor Belt](screenshots-upload/13_tractor_belt.jpg)
4. [Fault Scan](screenshots-upload/03_fault_scan.jpg)
5. [Core Shard](screenshots-upload/04_core_shard.jpg)
6. [Genesis Pulse](screenshots-upload/05_genesis_pulse.jpg)
7. [Upgrade tree](screenshots-upload/06_upgrade_tree.jpg)
8. [Weaponry](screenshots-upload/12_weaponry.jpg)
9. [Ship systems](screenshots-upload/07_ship_systems.jpg)
10. [Module reveal](screenshots-upload/08_module_reveal.jpg)
11. [Module slots](screenshots-upload/09_module_slots.jpg)
12. [Signal choices](screenshots-upload/10_signals.jpg)
13. [Permanent talents](screenshots-upload/11_permanent_talents.jpg)

Regenerate with `python3 make_assets.py` from this folder (it skips files that
already exist). The same runner makes the itch.io GIF/JPEG variants from the
same 4K sources. Final local validation is in `quality.json`; sizes and source
choices are in `manifest.json` and `assets.json`.

The scenes in `scenes/` were captured through `CaptureLauncher` from a private
copy of the Debug build. Capture mode needs `Demo.IsDev`, which is off in the
source since the playtest build. See `BTB_CAPTURE_SNAPSHOT` in the shorts skill's
`capture.py`.
