# Beyond the Belt capture

Use these details only for the UntitledGemGame repository. Resolve paths from the supplied/current game root; do not assume the skill remains inside the repository after installation.

## Existing facilities

- `DebugProgressionPresets.cs`: beginning (0), early (1), mid (2), late (3), endgame (4). These are developer snapshots, not elapsed play times.
- `Screens/GameScreen.cs`: save restoration, world camera, gameplay lifecycle.
- `Entities/HomeBase.cs`: equipped abilities and normal cooldown activation.
- `Entities/GemSpawnerAbility.cs`: gem spawning effects.
- `Marketing/Capture/`: original capture/editor used for the first two clips. Its `edit.py` implements the old inset/panel design and **must not be reused for this visual brief**.

The bundled harness derives from the compiled `GameMain` and uses the game's real update/render pipeline. It waits for assets, initializes an isolated game, saves/restores a selected preset, then advances 1/30 second per recorded frame. Randomness varies per take. It reads the bloom world render target, not the desktop window. This avoids an observed offscreen backbuffer/viewport problem but omits HUD and sound.

The full landscape render is normally 3840×2160 and is stored at its native render-target resolution, retaining detail for independent portrait and square crops from the same take. A 9:16 crop retains only a narrow portion of that world; check samples in both formats, especially for moving ships and effects near screen edges. Zoom below 1 can show more world, but might make details too small. Zoom changes presentation only, not upgrade progression.

## Capture a selected take

Requires the Linux desktop build, .NET 10, FFmpeg, SDL offscreen OpenGL, native dependencies, built Content/JFContent, and source assets. `--game-root` is required; `--build` defaults to its `bin/Debug/net10.0`. Verify build freshness for the requested feature before using it.

From the skill folder (replace the game path):

```sh
python3 scripts/capture_beyond_the_belt.py \
  --game-root /path/to/UntitledGemGame \
  --stage late --abilities spawner,chain \
  --seconds 18 --warmup 12 --zoom 0.8 \
  --output /path/to/UntitledGemGame/Marketing/Source/spawner-chain-take01.mp4
```

- Stages: `beginning`, `early`, `mid`, `late`, `endgame`.
- Abilities: `all` retains the preset loadout; `none` equips no active abilities. Otherwise use unique comma-separated names: `spawner`, `speed`, `magnet`, `drones`, `chain`.
- The preset must unlock the selected abilities and have enough slots. The harness fails rather than silently substituting a different loadout. Passive upgrades and ship effects remain those of the selected stage; this is not an isolation switch for all game systems.
- `--seconds` controls the take length; `--warmup` is simulation time before recording. Allow cooldowns and the scene to settle. No forced activation or cooldown reset is performed.
- `--zoom` multiplies the preset camera zoom; 1 preserves it.
- `--view world` (default) records the world target. `hud`, `shipyard`, `signals`, `upgrades`, `abilities`, and `meta` compose the real HUD over the world at 1920×1080. These captures remain silent.
- The scripted `shipyard` view equips the first available module at 2 seconds, selects the second ship class at 5 seconds, and opens Discovery at 8 seconds. The `signals` view performs a paid scan at 1.5 seconds and selects the middle choice at 6.5 seconds. Use takes long enough to include the desired actions. Both fail if the feature/action is unavailable; they do not grant extra resources or bypass game rules.
- Logs and `.capture.json` metadata are written beside the take. Existing video files are not overwritten.

Temporary directories isolate settings, local save data (`XDG_DATA_HOME`), working files, and the harness build. `SDL_VIDEODRIVER=offscreen` and `ALSOFT_DRIVERS=null` make this a silent offline recording. Native libraries are loaded from the desktop build's `runtimes/linux-x64/native`. Raw source Content and JFContent are linked into the temporary working directory; built content is linked beside the harness executable. Both are needed for this game's asset loading.

## Requests beyond presets

For a specific module, signal, upgrade purchase, interaction, or camera movement, inspect the real APIs and adapt a copy of `assets/CaptureGame.cs.txt` in a temporary workspace. Do not change shipping gameplay to make the shot work. Use the corresponding legitimate state/action, and verify the feature visibly triggers. Selecting a late preset alone does not prove a requested mechanic appears.

For shipyard/upgrade/UI requests, use the corresponding `--view` option or adapt the action timing in the harness. The world-only default does not include these subjects. Fit the relevant UI in each requested composition; do not replace it with an invented marketing panel. The UI composition includes the game's dimming and HUD, but omits the desktop pointer and the final backbuffer blur.

If the build or capture API has changed, read the implementation and update the adapter. After a diagnostic failure, inspect the log and fix the concrete cause; do not repeatedly rerun an unchanged capture or silently use unrelated old footage.
