# Store page refresh — 18 October 2026

Current gameplay from the 18 October 4K landscape captures (updated 3D planet and
shell, 3D prerendered ships, faceted gem rendering, GPU particles, and `OrbitSkin`
UI). Progression states and discoveries are staged for filming; there are no
play-time claims. Automatic refueling is active across all takes. No new art or
music was generated. Promotional overlays and Shorts end cards are omitted from
these store assets. Nothing has been uploaded or published.

[Review the assets](review.html) · [Source manifest](assets.json) ·
[itch.io versions](../../Itch/2026-10-18-refresh/ITCH_PAGE.md) ·
[Gameplay trailer (1080p)](../../Trailers/03-gameplay-trailer/renders/btb_trailer_03_gameplay_v4.mp4) ·
[Gameplay trailer (4K)](../../Trailers/03-gameplay-trailer/renders/btb_trailer_03_gameplay_v4_4k.mp4)

## Upload formats checked 18 October 2026

Steam's [extra-asset documentation](https://partner.steamgames.com/doc/store/page/assets)
supports PNG/JPG/GIF/WEBP/MP4/WEBM, recommends 1170 px width, limits animations
to 12 seconds, and recommends BT.709 video metadata. The clips here are silent
1170×658 H.264 MP4 at 30 fps, with a short loop crossfade and BT.709 tags.

The [description guide](https://partner.steamgames.com/doc/store/page/description)
asks for images below 5 MB and a total section size below 15 MB. All eleven clips
are budgeted together; use the screenshots in the screenshot gallery, not all
of them in the description.

The [screenshot requirements](https://partner.steamgames.com/doc/store/assets/standard#screenshots)
require at least five gameplay screenshots, minimum 1920×1080, 16:9. Mark at least
four as suitable for all ages. All eleven screenshots here are native 3840×2160
(16:9 4K) captures showing actual gameplay or the game's systems, upgrade, module,
signal and talent interfaces. PNGs are decoded 4K source-video frames.

## Suggested description

Beyond the Belt is an incremental space game about mining planets, collecting
gems and building a fleet. Start with manual cannon fire, buy automated weapons,
develop ship systems, and extract the core to learn permanent talents.

| Heading | Copy | Animation / alt text |
|---|---|---|
| Mine and collect | Fire at the planet and collect the gems your weapons release. Every haul funds another upgrade. | [Collect gems](clips/01_collect_gems_v3.mp4): A working ship collecting gems beside the planet. |
| Expand the upgrade tree | Spend your gems across weapon, harvesting and fleet branches as the planet and ships work in the background. | [Upgrade tree](clips/09_upgrade_tree_v1.mp4): Buying weapon and fleet ranks across the run upgrade tree. |
| Arm your ship | Add weapons that knock more gems loose and combine their effects across the planet. | [Weapons](clips/02_weapons_v3.mp4): Cannon fire, beams, harpoons, rockets and railgun shots striking the planet. |
| Build a fleet | Unlock different ship classes and let them collect, return and deliver while you plan the next upgrade. | [Fleet](clips/03_fleet_v3.mp4): An upgraded fleet collecting and returning gems. |
| Crack the core | Core fractures pull the loose gems back into the planet before an eruption releases a Core Shard. | [Core fracture](clips/04_core_fracture_v4.mp4): Gems collapsing into the planet followed by an eruption and Core Shard. |
| Develop ship systems | Buy Power Cells and spend them in each system's talent tree to unlock deeper rows and keystones. | [Ship systems tree](clips/10_ship_systems_v1.mp4): Buying a Power Cell and unlocking Genesis Pulse rows in the Ship Systems panel. |
| Fire active systems | Trigger equipped systems such as Genesis Pulse to seed rings of fresh gems around your base. | [Genesis Pulse](clips/05_genesis_pulse_v3.mp4): Genesis Pulse creating rings of gems around the planet. |
| Discover modules | Reveal modules and fit them to your ships. World Eater turns a ship into a roaming black hole that collects nearby gems. | [Modules](clips/06_modules_v4.mp4): Revealing World Eater and fitting it into a Drifter's module slot. |
| Scan deep space | Choose one of three signal discoveries. Their bonuses stack as you keep collecting. | [Signals](clips/07_signals_v3.mp4): Three signal cards with different bonuses and a discovery being selected. |
| Start the next loop | Hold Extract Core to collapse the planet and claim your prestige points. | [Extraction](clips/08_extraction_v3.mp4): The planet's core collapsing into a singularity during extraction. |
| Learn permanent talents | Spend prestige points across six tiers of permanent talents and free tier rewards before starting your next run. | [Prestige upgrade tree](clips/11_prestige_tree_v1.mp4): Spending prestige points to unlock Tier 4 and learn permanent talents. |

Upload the MP4s through the description editor's custom-image/extra-asset
uploader, then add the corresponding alt text.

## Screenshot gallery

Use the JPEG upload versions below (each 3840×2160 and under 4.9 MB). Full-resolution
decoded 3840×2160 PNG masters are retained in `screenshots/`. The separate itch.io
variants use 1920×1080.

Suggested order, with actual filenames:

1. [Working fleet](screenshots-upload/01_working_fleet.jpg)
2. [Weapons](screenshots-upload/02_weapons.jpg)
3. [Fault Scan](screenshots-upload/03_fault_scan.jpg)
4. [Core Shard](screenshots-upload/04_core_shard_v3.jpg)
5. [Genesis Pulse](screenshots-upload/05_genesis_pulse.jpg)
6. [Upgrade tree](screenshots-upload/06_upgrade_tree.jpg)
7. [Ship systems](screenshots-upload/07_ship_systems.jpg)
8. [World Eater reveal](screenshots-upload/08_world_eater.jpg)
9. [Module slots](screenshots-upload/09_module_slots.jpg)
10. [Signal choices](screenshots-upload/10_signals.jpg)
11. [Permanent talents](screenshots-upload/11_permanent_talents.jpg)

Regenerate with `python3 make_assets.py` from this folder. The same runner makes
itch.io GIF/JPEG variants from the same 4K sources. Final local validation
is recorded in `quality.json`; sizes and source choices are in `manifest.json`
and `assets.json`.
