# Comedy-club escalation trailer

New 16:9 trailer following Part 3 of [TRAILER_STRUCTURE.md](../TRAILER_STRUCTURE.md).
Fresh game captures, 60 seconds, 60 fps; 26 shots from 17 capture sessions. Scene files stage progression snapshots;
the cut does not claim an elapsed play time. No generated artwork or music.

## Cut

| Timeline | Beat |
|---|---|
| 0.0–5.4 | Fleet spectacle → Cascade close-up → core fracture |
| 5.4–13.2 | Shell shatter → manual cannon → first upgrades |
| 13.2–25.2 | Five weapons → Prospectors → Trove Hunters |
| 25.2–40.2 | Genesis / Cascade → fit World Eater and see its effect → Signal → Core Shard |
| 40.2–47.4 | Extract Core → buy Thermite Rounds → restart with molten cannon impacts |
| 47.4–55.2 | Weapon barrage, golden rings and Cascade; edit-camera pull-out |
| 55.2–60.0 | Logo and Steam wishlist CTA over continuing gameplay |

The blueprint's approximate times are snapped to the 100 BPM grid of the
existing licensed Synthwave track **Old Starship**. `music_cuts.json` records
source sections; `prepare_edit.py` assembles the track and edit manifests.
The music quietens for the reset, drops into the weapons montage, and builds
again into the final spectacle. Game one-shot effects are reconstructed from
the game's own event logs and mixed alongside it.

World Eater's fitting and payoff use the same session. Extraction, the Thermite Rounds talent
purchase, and restart also use one session. The current game resets
ship systems at extraction, so the restart shows permanent Thermite Rounds on the first manual cannon shots
instead of the blueprint’s carried-over Genesis Pulse. The 5.6-second collapse
is excerpted at normal speed to fit the seven-second prestige beat. Fleet showcases use
auto-refuel and ample fuel; capture states are staged. There is no mid-take
camera-zoom mutation. The finale uses a late-game preset, not Uber endgame.

## Reproduction

From the game root:

```sh
# Reuse the already-built sibling project dependencies in a restricted workspace.
dotnet build UntitledGemGame.csproj -c Debug --no-restore -m:1 -nr:false -p:BuildProjectReferences=false
python3 Marketing/Trailers/04-comedy-club-escalation/capture_trailer.py --preview shell_break planet_clicks upgrade_ui arsenal prospectors trove_hunters s_genesis s_cascade world_eater signals_ui shard loop t_fleet t_cascade t_fracture climax
python3 Marketing/Skills/beyond-the-belt-shorts/scripts/capture.py --no-build Marketing/Trailers/04-comedy-club-escalation/scenes/*.json
python3 Marketing/Trailers/04-comedy-club-escalation/prepare_edit.py
python3 Marketing/Skills/beyond-the-belt-shorts/scripts/render_short.py Marketing/Trailers/04-comedy-club-escalation/edit.json
```

Output names are versioned. Existing trailer 03 and its footage are preserved.
The build succeeds with existing compiler/analyzer warnings. Captures use
isolated temporary saves and settings. Nothing is uploaded or published.

Capture staging uses `warmup_render_interval: 60`: every simulation update runs,
but only occasional warmup frames are drawn until the final second. Recorded
frames retain the requested cadence. Preview-only captures use 15 fps through
`capture_trailer.py`; final sources use 60 fps. The dense spectacle fields are
filled naturally by weapons before the fleet launches.

The opening and finale field caps are 15,000 and 20,000 respectively. The
blueprint’s 50,000-gem suggestion was reduced for clearer ship movement and
effect silhouettes; the trailer makes no on-screen gem-count claim.

Fault Scan is omitted: the current Command Center talent is hidden from the
playable tree. A railgun impact takes its place in the finale.

The last pull-out isolates Genesis Spiral and Golden Age before Cascade cuts
through the ring, giving the finale a clear gold/blue color shift. New spawning
is held during the gilding pulse so the existing ring remains readable.
