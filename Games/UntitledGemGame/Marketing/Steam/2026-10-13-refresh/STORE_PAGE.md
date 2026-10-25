# Store page refresh — 13 October 2026

Superseded by the [18 October refresh](../2026-10-18-refresh/STORE_PAGE.md).

Current gameplay from the fresh shorts takes. Progression states and discoveries
are staged for filming; there are no play-time claims. Automatic refueling is
active. No new art or music was generated. Promotional overlays and Shorts end cards
are omitted from these store assets. Nothing has been uploaded or published.

[Review the assets](review.html) · [Source manifest](assets.json) ·
[itch.io versions](../../Itch/2026-10-13-refresh/ITCH_PAGE.md)

## Upload formats checked 13 October 2026

Steam's [extra-asset documentation](https://partner.steamgames.com/doc/store/page/assets)
supports PNG/JPG/GIF/WEBP/MP4/WEBM, recommends 1170 px width, limits animations
to 12 seconds, and recommends BT.709 video metadata. The clips here are silent
1170×658 H.264 MP4 at 30 fps, with a short loop crossfade and BT.709 tags.

The [description guide](https://partner.steamgames.com/doc/store/page/description)
asks for images below 5 MB and a total section size below 15 MB. All eight clips
are budgeted together; use the screenshots in the screenshot gallery, not all
of them in the description.

The [screenshot requirements](https://partner.steamgames.com/doc/store/assets/standard#screenshots)
require at least five gameplay screenshots, minimum 1920×1080, 16:9. Mark at least
four as suitable for all ages. These eleven screenshots contain actual gameplay
or the game's systems, upgrade, module, signal and talent interfaces. Stills
from landscape takes are 3840×2160; portrait takes are cropped around the action
and downscaled to 1920×1080. PNGs are decoded source-video frames, not lossless
render-target captures.

## Suggested description

Beyond the Belt is an incremental space game about mining planets, collecting
gems and building a fleet. Start with manual cannon fire, buy automated weapons,
develop ship systems, and extract the core to learn permanent talents.

| Heading | Copy | Animation / alt text |
|---|---|---|
| Mine and collect | Fire at the planet and collect the gems your weapons release. Every haul funds another upgrade. | [Collect gems](clips/01_collect_gems_v2.mp4): A working ship collecting gems beside the planet. |
| Arm your ship | Add weapons that knock more gems loose and combine their effects across the planet. | [Weapons](clips/02_weapons_v2.mp4): Cannon fire, beams and other weapons striking the planet. |
| Build a fleet | Unlock different ship classes and let them collect, return and deliver while you plan the next upgrade. | [Fleet](clips/03_fleet_v2.mp4): An upgraded fleet collecting and returning gems. |
| Crack the core | Core fractures pull the loose gems back into the planet before an eruption releases a Core Shard. | [Core fracture](clips/04_core_fracture_v3.mp4): Gems collapsing into the planet followed by an eruption and Core Shard. |
| Develop ship systems | Buy Power Cells and spend them on systems such as Genesis Pulse, growing the effect of each activation. | [Genesis Pulse](clips/05_genesis_pulse_v2.mp4): Genesis Pulse creating rings of gems around the home base. |
| Discover modules | Reveal modules and fit them to your ships. World Eater turns a ship into a roaming black hole that collects nearby gems. | [Modules](clips/06_modules_v3.mp4): Revealing World Eater and fitting it into a Drifter's module slot. |
| Scan deep space | Choose one of three signal discoveries. Their bonuses stack as you keep collecting. | [Signals](clips/07_signals_v2.mp4): Three signal cards with different bonuses and a discovery being selected. |
| Start the next loop | Hold Extract Core, learn a permanent talent, and begin another run with what you have learned. | [Extraction](clips/08_extraction_v2.mp4): The planet's core collapsing into a singularity during extraction. |

Upload the MP4s through the description editor's custom-image/extra-asset
uploader, then add the corresponding alt text. The copy above uses current
feature names and deliberately avoids the obsolete upgrade/module counts and
command names from the older package.

## Screenshot gallery

Use the JPEG upload versions below (each under 4.9 MB). Full-resolution decoded
PNG masters are retained in `screenshots/`. JPEGs retain the master dimensions;
the separate itch.io variants use 1920×1080.

Suggested order, with actual filenames:

1. [Working fleet](screenshots-upload/01_working_fleet.jpg)
2. [Weapons](screenshots-upload/02_weapons.jpg)
3. [Fault Scan](screenshots-upload/03_fault_scan.jpg)
4. [Core Shard](screenshots-upload/04_core_shard_v2.jpg)
5. [Genesis Pulse](screenshots-upload/05_genesis_pulse.jpg)
6. [Upgrade tree](screenshots-upload/06_upgrade_tree.jpg)
7. [Ship systems](screenshots-upload/07_ship_systems.jpg)
8. [World Eater reveal](screenshots-upload/08_world_eater.jpg)
9. [Module slots](screenshots-upload/09_module_slots.jpg)
10. [Signal choices](screenshots-upload/10_signals.jpg)
11. [Permanent talents](screenshots-upload/11_permanent_talents.jpg)

Regenerate with `python3 make_assets.py` from this folder. The same runner makes
itch.io GIF/JPEG variants from exactly the same sources. Final local validation
is recorded in `quality.json`; sizes and source choices are in `manifest.json`
and `assets.json`.
