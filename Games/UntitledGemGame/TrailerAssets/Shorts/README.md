# Beyond the Belt — three vertical Shorts

Resolve project: **Trailer2**. Each Short is saved as a separate timeline with gameplay on V1, captions on V2, music on A1, and game sound accents on A2.

Exports: **1080 × 1920, 9:16, 60 fps, H.264 with stereo AAC at 48 kHz**. Every video finishes with **Wishlist on Steam**. Nothing was uploaded or published.

| Short | Duration | Resolve timeline | Export |
| --- | --- | --- | --- |
| One click becomes a fleet | 20.97 s | Beyond the Belt - Short One click becomes a fleet | [MP4](exports/Beyond_the_Belt_Short_01_Click_to_Fleet.mp4) |
| Turn gems into a flood | 18.35 s | Beyond the Belt - Short Turn gems into a flood | [MP4](exports/Beyond_the_Belt_Short_02_Abilities.mp4) |
| Build your next upgrade | 23.58 s | Beyond the Belt - Short Build your next upgrade | [MP4](exports/Beyond_the_Belt_Short_03_Build_Your_Upgrade.mp4) |

## Editorial choices

The first Short moves from endgame spectacle back to a single click, then builds toward automated collectors. The second focuses on abilities and fleet commands. The third shows a legendary module, equipping ships, signal discoveries, and prestige upgrades. Cuts follow the existing Sky Fish soundtrack by Holizna at approximately 137.35 BPM. Music excerpts and game sound accents are separate tracks.

Gameplay and feature panels are individually reframed for a phone screen. Recordings are trimmed to avoid recording entry/exit movements. Clips 15 and 16 are omitted. Commands uses the smooth early section of clip 9; module gameplay uses source 10–12.62 seconds in clip 11; clip 17 stays before its worst slowdown. The sources still contain any original animation cadence variations.

## Editing and rebuilding

Keep this folder in place: Resolve references `plates/`, `titles_v02/`, and `audio/` directly. Captions are individual ProRes 4444 graphics clips; cuts and audio are editable in the timelines. Reframing is baked into the prepared gameplay clips. `shorts_plan.json` preserves original source paths, source ranges, crop rectangles, caption copy, timeline ranges, and music offsets. Originals under `/home/johan/Videos/2` are untouched.

Run `python build_shorts.py` to rebuild assets, or `python build_shorts.py --titles-only` after adjusting caption design. The script rebuilds the assets at the paths referenced by Resolve.

## Suggested upload copy

1. **One click becomes a fleet | Beyond the Belt** — Start with a click. Collect gems, unlock upgrades, and build your fleet. Wishlist Beyond the Belt on Steam. #BeyondTheBelt #IndieGame #Shorts
2. **Turn gems into a flood | Beyond the Belt** — Unleash abilities and command your collectors. Wishlist Beyond the Belt on Steam. #BeyondTheBelt #IndieGame #Shorts
3. **Found a legendary module | Beyond the Belt** — Customize your ships, choose discoveries, and prestige for permanent upgrades. Wishlist Beyond the Belt on Steam. #BeyondTheBelt #IndieGame #Shorts

Validation: native timeline ranges match the edit plan, all referenced media is online, and video/audio tracks cover each full edit without gaps or overlaps. Final exports are checked for dimensions, frame count, codecs, stereo audio, complete decoding, and representative rendered frames. Detailed results are in `review/export_qc.json`; visual previews are in `review/all_shorts_contact_sheet.jpg`.
