# Edit files (`render_short.py`)

```json
{
  "output": "renders/btb_short_06_swarm_v1.mp4",
  "layout": "fullscreen",
  "size": [1080, 1920], "fps": 60,
  "music": {"file": "~/Dev/workspaces/MonogameBase/Games/UntitledGemGame/Content/Music/Holizna/Sky Fish.ogg",
            "in": 15.4, "gain_db": 0, "fade_out": 1.0},
  "game_audio": {"gain_db": 6},
  "shots": [
    {"take": "takes/beginning.mp4", "in": 1.0, "dur": 2.6},
    {"take": "takes/swarm.mp4", "in": 3.2, "dur": 3.0, "zoom": 1.0, "zoom_end": 1.25, "focus": [0.5, 0.45]},
    {"take": "takes/chain120.mp4", "in": 4.4, "dur": 2.0, "speed": 0.5}
  ],
  "texts": [
    {"at": 0.0, "dur": 2.6, "text": "ONE SHIP.", "sub": "Every run starts here."},
    {"at": 2.6, "dur": 3.0, "text": "*{gems}* GEMS", "pos": "center"}
  ],
  "end_card": {"at": 19.0, "dur": 3.0, "title": "WISHLIST NOW", "sub": "*Beyond the Belt* on Steam"}
}
```

Paths are relative to the edit file (`~` works). Shots play back to back in
order; the edit length is the sum of shot durations. The render refuses to
overwrite an existing output.

## shots

| key | default | meaning |
|---|---|---|
| `take` | required | A capture take (its `.capture.json` and `.sfx.wav` are picked up). |
| `in`, `dur` | required | Source in point and timeline duration (s). |
| `focus` | [0.5, 0.5] | Point of interest in the take (normalized). Crops to the frame aspect around it and zooms toward it. |
| `focus_end` | | Pan: the frame glides from `focus` to `focus_end` (eased) over the shot - the way to travel across a 16:9 HUD take in a 9:16 short (with `zoom` 1.3-2 to read the UI). |
| `zoom`, `zoom_end` | 1 | Punch-in (2160x3840 takes allow 2x at 1080x1920 without losing detail). `zoom_end` animates a smooth push. |
| `split` | | Instead of `take`/`in`: `[{"take", "in", "focus", "zoom"}, {...}]` stacks two takes (top, bottom halves, a thin accent divider). Capture split takes at 9:8 (`"width": 2160, "height": 1920`). Game audio of both halves is mixed. |
| `fade_in`, `fade_out` | | Seconds of a dip from / to black at the shot's start / end (a title transition: `fade_out` on the last cold-open shot, `fade_in` on the title shot). |
| `speed` | 1 | 0.5 = slow motion (capture that take at 120 fps for smooth frames), 1.5 = speed up (a zoom-out reveal that fits a bar). Game audio follows (0.5-2). |

## texts

`text` (headline, `\n` for lines), `sub` (smaller second line), `at`, `dur`,
`pos` (`top` default, `center`, `bottom`, or a number: the text's top as a fraction of the height), `size` (headline px at 1080 wide,
default 104; text shrinks to fit), `fade` (s, 0.12). Wrap words in `*stars*`
for the accent colour (game cyan). Tokens filled from the take's capture log at
that moment: `{gems}` (12.57T), `{gems_words}` (12 TRILLION), `{rate}`,
`{rate_words}` (per minute), `{earned}`, `{on_screen}` (gems on the field), `{upgrades}` (levels bought across all trees).

`"source": 1` reads tokens from the bottom half of a split shot (0 = top/only).
`"live": true` turns a text into a counter that follows the log while it is on
screen (re-rendered every `step`, 0.1 s; interpolated between samples, and
across shots: the number jumps where the shot changes). The incremental
"number goes up" in one line: `{"text": "*{gems}* GEMS", "live": true}`.

Fullscreen text is white with a dark outline and soft shadow, straight over the
gameplay (no boxes). Keep it to 2-6 words, one idea per text, on screen at least
1.2 s per 3 words.

## end_card

`at`, `dur`, `title` (default "WISHLIST\nNOW", `*accent*` markup), `sub` (default
"*Beyond the Belt* on Steam"), `dim` (0.5), `size` (150). `style`:

| style | animation |
|---|---|
| `pop` | words bounce in one by one, then breathe gently |
| `slam` | the title drops in big and lands with a shockwave ring and a short shake |
| `type` | typewriter with a caret, then one light sweep |
| `shine` | fades in; a light band sweeps across it now and then |
| `rise` | floats up out of a blur |
| `crack` | glowing fractures split out from the title across the screen and cool (fits core fractures) |
| `glitch` | the title stutters in with a colour split and torn slices, then settles (relapses now and then) |
| `burst` | gem-coloured shards explode outward with a ring as the title pops in |
| `drop` | the words fall in from above one by one and bounce to rest |
| `split` | the lines slide in from opposite sides and lock together with a soft flash |
| `zoom` | the title rushes in from far away with a motion trail, overshoots and settles |
| `card` | static logo + title + sub (no animation) |
| `flash` | blinks on the beat - the user found it annoying; do not use |

Rotate styles between shorts. An in-game alternative with no end card: the
ship writes WISHLIST NOW in gems (`gem_text` in the last take, see Short 17)
plus a plain text line below it.

## layouts

- `fullscreen` (default): gameplay edge to edge; text over it.
- `banners` (retired: the user only uses fullscreen now): Shorts 01-04 look. Top band (logo + 2-line headline = `text`),
  gameplay window 1080x1014 at y=380, bottom band (`sub` as caption, tagline,
  progress line). Texts plus the end card must cover the whole edit. Frame
  takes for the window: capture `"width": 2160, "height": 2028` or use `focus`
  to choose the slice of a 9:16 take.

## audio

`music` is trimmed from `in`, faded out over `fade_out`. `game_audio` mixes each
shot's slice of its take's rebuilt game audio (`gain_db`, default +6 because the
game's own levels are low); set `"game_audio": null` to drop it. The mix is
brought to about -14 LUFS with peaks under -1.5 dBFS and encoded AAC 192k.

## output

`<output>.mp4` (H.264 high quality, yuv420p, faststart, 48 kHz AAC) and
`<output>.sheet.jpg` (a frame per shot, text and the end). `--fast` uses NVENC
for review renders; `--resolve` adds `<output>.resolve/plan.json` +
ProRes overlays for the resolve-game-video skill (fullscreen layout).
