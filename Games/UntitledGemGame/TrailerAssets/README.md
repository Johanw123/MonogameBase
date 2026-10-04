# Beyond the Belt gameplay trailer

Resolve project: Trailer2. Timeline: **Beyond the Belt - Gameplay Trailer v01**.

Duration: **01:18:38 at 60 fps** (78.63 seconds). Timeline: 1920 × 1080, 60 fps. Source recordings remain 3840 × 2160.

Tracks:

- V1: 22 trimmed gameplay shots, progressing from clicking to endgame.
- V2: 11 transparent feature/hook/endcard overlays with native fades.
- V3: the existing game logo, scaled and faded for the closing card.
- A1: Sky Fish by Holizna, from the game soundtrack; source in 15.4 seconds, with gain and fades.
- A2: separate accents from the game sound assets. These are editorial accents, not recovered recording audio.

The trailer ends with **Wishlist on Steam**, confirmed by the developer. Export: `exports/Beyond_the_Belt_Gameplay_Trailer_v01.mp4` (H.264, 1920 × 1080 at 60 fps, stereo AAC at 48 kHz). Nothing was published.

Keep this folder in place: Resolve references the `.mov` graphics and `.wav` audio directly. Gameplay references `/home/johan/Videos/2`.

`edit_plan.json` records exact source and timeline ranges, caption copy, and sound placement. `build_assets.py` regenerates the caption images, ProRes 4444 alpha graphics, and PCM audio using Pillow, NumPy, and FFmpeg. Text is in those generated graphics; the timelines expose each caption as a separate overlay clip.

Validation: all planned ranges match Resolve readback; no gaps on gameplay or audio, no overlapping items, and no offline media. Representative final frames were checked via Resolve Gallery stills, including the hook, first clicks, module reveal, signals, and closing card. Combined audio peak is 0.718, below clipping.

| Time | Beat |
| --- | --- |
| 00:00.00 | Cold open: late-game spectacle |
| 00:03.50 | Begin with a click |
| 00:07.87 | Expand the upgrade tree |
| 00:12.23 | Show the upgraded click |
| 00:14.85 | Gravity well in action |
| 00:17.47 | Unlock and upgrade ships |
| 00:20.10 | Growing automated fleet |
| 00:25.33 | Unlock abilities |
| 00:27.95 | Abilities in action |
| 00:32.33 | Command the fleet |
| 00:37.57 | Reveal a legendary module |
| 00:41.93 | Equip modules |
| 00:46.30 | Module payoff in gameplay |
| 00:48.93 | Scan and choose discoveries |
| 00:54.17 | Inspect persistent signal bonuses |
| 00:55.92 | Prestige and rebuild |
| 00:59.42 | Permanent prestige upgrades |
| 01:01.15 | Late-game escalation |
| 01:05.53 | Larger fleet and chain effects |
| 01:08.15 | Gem showers |
| 01:09.90 | Endgame payoff |
| 01:12.52 | Logo and Steam wishlist |

Export audio verified across the full 78.63 seconds: stereo AAC, mean −16.8 dBFS, peak −2.9 dBFS. Export Audio is enabled in the saved Resolve render settings.

4K export: `exports/Beyond_the_Belt_Gameplay_Trailer_v01_4K.mp4` — 3840 × 2160, 60 fps, H.264, stereo AAC 48 kHz, 78.63 seconds. Saved timeline: **Beyond the Belt - Gameplay Trailer v01 - 4K**. Its title graphics use native 4K ProRes 4444 assets in `graphics_4k`; the gameplay sources are already 4K. Verified all 4,718 frames have even 60 fps timestamps, the full file decodes without errors, and audio is present (peak −2.9 dBFS). Nothing was published.

Recording cadence findings: see `frame_cadence_report.md`; repeated gameplay frames are already present in some source recordings and remain in both exports.
