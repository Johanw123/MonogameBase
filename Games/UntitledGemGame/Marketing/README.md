# First social gameplay clips

The two MP4s in `Exports/` are 1080 × 1920 (9:16), 30 fps, H.264 video with AAC audio and fast-start metadata. They use actual gameplay captured from the existing desktop build with the beginning, early-game, and late-game debug presets. The camera is cropped for mobile; the game HUD is omitted. These are staged progression snapshots, not a claim about how quickly a player progresses. Gameplay runs at normal simulation speed.

| File | Length | Content |
| --- | --- | --- |
| `01-one-ship-to-a-fleet.mp4` | 18 seconds | Late-game hook, beginning, early fleet, late fleet |
| `02-when-abilities-combine.mp4` | 15 seconds | Continuous late-game footage showing gem spawning, pulling, and automated collection |

Matching `-cover.jpg` files are included. Captions are burned in and readable without audio. The soundtrack is an excerpt of **Greys by Holizna**, already present in the game's music assets; gameplay sound effects are not included. To use platform-native music, mute the video's audio in the posting app.

These are drafts for review, not published posts. No release date, store availability, or wishlist URL has been invented.

## Suggested post text

**Clip 1**

It starts with one harvester. Then the fleet takes over. A look at progression in Beyond the Belt, my incremental space game in development.

`#indiegame #gamedev #incrementalgame #spacegame`

**Clip 2**

Spawn gems. Pull them in. Let the fleet do its thing. Trying out ability combinations in Beyond the Belt.

`#indiegame #gamedev #incrementalgame #pixelart`

## Recreate the captures and edits

Requires Linux, .NET 10, FFmpeg, an offscreen-capable SDL/OpenGL installation, and the already-built game with its native libraries and content in `bin/Debug/net10.0`. Run from the game directory:

```sh
python3 Marketing/Capture/capture.py
python3 Marketing/Capture/edit.py Marketing/Source
```

`capture.py --build /path/to/build` can select another compatible desktop build. Capture uses the compiled game, so build current source first if gameplay has changed. The capture harness creates temporary settings, saves, and working files; it does not use the developer's normal save. It waits for asset loading and eight seconds of gameplay before recording. Each recorded frame advances the simulation by 1/30 second, so slow offline rendering does not slow the final video. Random gameplay will vary between captures.

The `.cs.txt` file is intentionally a template so the main game's default C# file glob does not compile the capture entry point. Capture and editing tools are independent of the game's source. Raw footage and exports are ignored by Git to keep video binaries out of normal source commits.

## Format references

Vertical 9:16 was selected as the starting format for TikTok, Instagram Reels, and YouTube Shorts. YouTube accepts vertical Shorts, and Meta recommends 9:16 for Reels. See [YouTube Shorts guidance](https://support.google.com/youtube/answer/15424877?hl=en) and [Meta's Reels publishing specifications](https://www.postman.com/meta/instagram/folder/830j7my/reels-publishing). The same master files can be used for the initial cross-platform test; platform previews should be checked for interface overlays when posting.
