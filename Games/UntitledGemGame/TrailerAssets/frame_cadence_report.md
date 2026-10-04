# Recording frame cadence inspection

All 17 originals in `/home/johan/Videos/2` were decoded and inspected. They are 3840 × 2160 and declared 60 fps. Most presentation intervals alternate 16 and 17 ms, as expected with millisecond timestamps. Several recordings have one 33–50 ms interval at their very end; those recording-stop intervals are outside the trailer trims.

The main problem is repeated images inside otherwise correctly timed 60 fps files. Active gameplay advances on one frame, then remains almost unchanged across one or more subsequent frames. Recording compression makes these repeats slightly different at the pixel level, so counting only byte-identical frames would miss the problem.

| Original clip | Finding | Source time | Approximate distinct motion updates/sec |
| --- | --- | --- | --- |
| 9_commands_use.mkv | Clear slowdown; starts near 60 and settles near 29 | Begins around 6–9 s; sustained 9–25 s | ~29 in sustained section |
| 11_modules_equip.mkv | Clear slowdown after closing the module menu; initial gameplay is smooth | Gameplay smooth 10–13 s, slows around 13–15 s; sustained 15–21 s | ~29 in sustained section |
| 15_lategame.mkv | Most severe; progressively slows with frequent repeated frames | Begins around 2 s; worse after 13 s through the end | ~27 at 4–8 s; ~17 averaged over 13–30 s |
| 16_lategame2.mkv | Clear sustained slowdown after initially smooth gameplay | Begins around 2–4 s; continues to the end | Mostly ~23–30 later |
| 17_endgame.mkv | Occasional repeats, then substantial slowdown at the tail | Mostly ~55–57 through 6 s; worse around 7 s onward | ~27 at 7–8.3 s |

Clip 15 contains an approximately 200 ms near-still run around 24.75–24.95 s. Clip 16 contains a roughly 133 ms hold around 21.42–21.55 s. Both are in active gameplay, not idle menu footage. Consecutive-frame contact sheets were generated for clips 9, 11, 15 and 16 in `review/*_cadence_sample.jpg`.

Clips 1–8: no comparably clear sustained slowdown identified. Clip 6's active fleet gameplay has roughly 60 distinct changes per second throughout most of its recording. Early clicking clips have relatively little overall movement, so image-change counts alone cannot certify a precise animation fps.

Clips 10, 12, 13 and much of 14 show menus or low-motion states. Repeated or almost identical images there are expected and are not sufficient evidence of lag. Clip 11 was classified from its active gameplay section separately from its menu.

## Impact on the current trailer

- Clip 16: opening 00:00–00:03.50, fleet shot 01:05.53–01:08.15, and closing-card background 01:12.52–01:18.63.
- Clip 9: commands section 00:32.33–00:37.57 uses source 17–22.23 s, within the sustained slowdown.
- Clip 11: gameplay payoff 00:46.30–00:48.93 uses source 17–19.63 s, within the sustained slowdown. Its earlier menu shot is separate.
- Clip 15: 01:01.15–01:05.53 uses source 4–8.38 s; 01:08.15–01:09.90 uses source 22–23.75 s. The latter is especially choppy.
- Clip 17: 01:09.90–01:12.52 uses source 4–6.62 s, avoiding its worst final slowdown.

## Export verification and method

The 4K export contains exactly 4,718 evenly spaced video frames at 60 fps, lasts 78.633 seconds, fully decodes without errors, and contains stereo AAC audio at 48 kHz. No exported timestamp intervals exceed 25 ms. Sampling exported active gameplay reproduced the source pattern: commands ~28 distinct changes/sec, module gameplay ~28, and the late clip-15 shot ~15.

Source estimates use adjacent-frame mean absolute luminance differences after reducing to 320 × 180, with a 0.3/255 threshold separating near-repeats from obvious changes in these high-motion scenes. The principal findings remain similar at 0.2 and 0.5. These are estimates of distinct visual updates, not direct measurements of the game's internal fps. UI-only changes can also count as updates. Static menus are deliberately excluded from fps claims. Full raw measurements and timestamps are in `frame_cadence_report.json`.

The source recordings establish that the stutter predates trailer editing. They do not establish whether the game itself slowed down or the recorder failed to capture every game frame. Re-recording the affected gameplay with a game fps counter and recorder dropped-frame statistics would distinguish those causes. No interpolation or footage substitution was applied during this inspection; the saved 4K timeline retains the original edit.
