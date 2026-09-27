# Full-screen vertical and square editor

`scripts/render_vertical.py` uses FFmpeg and ffprobe. It exports the same shots as 1080×1920 vertical and 1080×1080 square videos by default, each cropped directly from the original footage. The filename is retained from the initial vertical-only tool. Cuts, duration, and audio are shared between both versions. Gameplay occupies the complete canvas. It does not add background panels, duplicate blurred footage, logos, or title cards.

## Shot manifest

Paths are relative to the manifest file, not the working directory. Example with no text or music (the default):

```json
{
  "shots": [
    {"source": "../Source/spawner-chain-take01.mp4", "start": 2, "duration": 15, "focus_x": 0.5, "focus_y": 0.5}
  ]
}
```

`focus_x` / `focus_y` are the desired center of the crop as fractions of the source dimensions. The crop is clamped to the frame edges for each output aspect ratio. Square usually retains more horizontal gameplay from a landscape original. They do not identify an object or automatically track it. If action moves outside the crop, choose another shot, adjust the in-game camera, or implement and validate tracking. Input is expected to be normally oriented desktop capture without rotation metadata.

Optional per-format framing overrides let the two crops follow different centers without changing shot timing:

```json
{
  "shots": [
    {
      "source": "../Source/spawner-chain-take01.mp4",
      "start": 2,
      "duration": 15,
      "framing": {
        "vertical": {"focus_x": 0.45, "focus_y": 0.5},
        "square": {"focus_x": 0.5, "focus_y": 0.5}
      }
    }
  ]
}
```

Add more shots for cuts, e.g. a progression comparison. Source ranges are checked rather than silently frozen, looped, or shortened. Longer clips and custom durations are allowed when the user asks.

Optional fields:

```json
{
  "shots": [
    {"source": "../Source/spawner-chain-take01.mp4", "start": 2, "duration": 15}
  ],
  "captions": [
    {"text": "Spawn. Collect. Repeat.", "start": 0.4, "end": 2.4, "y": 0.18, "size": 52}
  ],
  "music": {"source": "../../Content/Music/Holizna/Greys.ogg", "start": 35, "volume": 0.3}
}
```

This music path is an example of a project asset, not a required track or a license assertion. Use music only if wanted and appropriate. Caption times are on the assembled timeline and match in both outputs. To move or resize a caption in just one version, add `"placement": {"square": {"y": 0.12, "size": 44}}` to that caption. Only `y` and `size` can be overridden; text and timing stay shared. Captions have white glyphs with a subtle outline and no background. Use `\n` for a second line. Caption font defaults to fontconfig's bold sans; an optional top-level `font` path overrides it. Text is read from a UTF-8 file with expansion disabled, so punctuation and percent signs remain literal.

Source audio is retained, resampled, and normalized to a common channel/sample format, not loudness-normalized. Music is mixed if selected. Sources without audio receive a silent AAC track; do not describe that as captured game audio. Listen/measure the final mix and adjust the music volume if needed.

## Render and review

From the skill folder:

```sh
python3 scripts/render_vertical.py /path/to/Marketing/Exports/spawner-edit.json \
  --output /path/to/Marketing/Exports/spawner-v01.mp4
ffprobe -v error -show_streams -show_format /path/to/Marketing/Exports/spawner-v01-vertical.mp4
ffmpeg -v error -i /path/to/Marketing/Exports/spawner-v01-vertical.mp4 -f null -
ffmpeg -ss 4 -i /path/to/Marketing/Exports/spawner-v01-vertical.mp4 -frames:v 1 /tmp/spawner-review.png
```

The render command creates `spawner-v01-vertical.mp4` and `spawner-v01-square.mp4`. Specify `--format vertical` or `--format square` for a single export; then `--output` is used verbatim. The default is `--format both`. Existing outputs are checked before either render starts.

Review both variants separately with ffprobe, full decode, and frame samples. Matching timing does not guarantee that both crops show the important action. Do not generate the square video from the vertical export: that cannot recover gameplay already cut away.

Open review images with an image-viewing tool. Review additional times around the event and cut points, not just the first frame. Inspect text fit, subject visibility, and whether the gameplay was actually kept edge to edge. For an optional cover, extract a strong moment from the finished video.

For a cheap test, use a separate manifest selecting 1–2 seconds of a take and a new preview output path. Preview text must fall within that test timeline. Test at full output dimensions so the crop and typography match the final render. The helper refuses to overwrite outputs.
