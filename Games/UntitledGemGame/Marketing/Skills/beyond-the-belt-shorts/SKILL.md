---
name: beyond-the-belt-shorts
description: Record and cut YouTube Shorts / TikTok / Reels of Beyond the Belt end to end - stage any game state with the game's own capture mode (debug presets, exact upgrade levels, stats, abilities, modules, signals), script player actions, render native 9:16 footage offline at 4K/60 with the real game audio, then edit to music with text in a fullscreen or branded-banner layout. Use whenever the user wants a short, clip, reel or vertical video of Beyond the Belt (UntitledGemGame) made from new footage, wants footage recorded from the game, or wants to show off a feature, progression or moment in short form.
---

# Beyond the Belt shorts

Everything runs from the game repo (`UntitledGemGame/`): the game itself records
footage (`--capture`), these scripts edit it. You plan, direct and judge; the
user approves the concept and the final cut.

Scripts are in `scripts/` next to this file. Run them with `python3`.

## Ground rules

- **Understated text.** The user prefers plain, concrete captions about what
  the footage shows. Avoid cheesy teaser questions such as "how far will it
  crack", exaggerated hype, and forced suspense. Let the gameplay carry the
  shot; use fewer overlays when no explanation is needed.
- **Keep harvesters active.** Enable auto-refuel in showcase scenes with
  `"stats": {"AutoRefuel": true}`. Check preview footage for harvesters stuck
  requesting fuel before recording final takes. Only show fuel starvation
  when it is the intended subject of the clip.
- **Real gameplay only.** Every frame is rendered by the game. Staged states
  (presets, granted upgrades) are fine for showing features; never imply a
  timeline that is not true ("after 1 hour", "day 1 vs day 30") unless it is.
  Numbers on screen come from the capture log (`{gems}` tokens), not invented.
- **Real music only** (licensed tracks or the game's soundtrack,
  `Content/Music/Holizna/*.ogg`, royalty free). Never generate music.
- **Never touch the player's save or Settings.json**: capture mode uses a temp
  save automatically. Do not change gameplay code to make a shot work; extend
  the capture mode (`Capture/`) instead, in its own commit-ready change.
- New outputs never overwrite old ones: bump `_v2` in take and render names.
- Concept first, footage second: show the user the idea and shot list, then
  record. Small tweaks later need no new approval.

## Workflow

1. **Brief.** Ask only what is missing: the idea or feature to show, layout
   (`fullscreen`, the house style; `banners` is retired), length
   (15-30 s), music track, CTA (default "WISHLIST NOW / Beyond the Belt on
   Steam"; check the Steam page is live). Read `references/showcase.md` for what
   tends to work for this game and `Marketing/README.md` for posting copy.

2. **Work folder:** `Marketing/Shorts/NN-slug/` with `scenes/`, `takes/`,
   `edit.json`, `renders/` (videos are git-ignored, scene and edit files are not).

3. **Concept and shot list** (show the user): hook (first second), beats,
   payoff, CTA; each shot names its game state, action and framing. Snap cut
   points to the music (step 6) when the track is known.

4. **Scenes.** One JSON per shot, format in `references/scenes.md`. Look up
   ids with `python3 scripts/capture.py --list Marketing/Shorts/catalog.json`
   (presets, 300+ upgrades, 200+ stats, abilities, modules, signals).
   Default to 2160x3840 at 60 fps: the game plays in a native 9:16 field, so the
   action fills the frame without cropping. Make takes 2-4 s longer than the
   shot so cuts can slip.

5. **Capture.** `python3 scripts/capture.py scenes/*.json --preview` first
   (540x960, seconds each), Read each `takes/*.sheet.jpg` and the printed
   summary (abilities fired, clicks, gems), fix framing/zoom/timing, then run
   without `--preview` for the 4K takes. The script rebuilds the game when the
   sources changed. A failed capture prints the reason (locked ability, unknown
   id, ...) - fix the scene, do not retry blindly.

6. **Music.** `python3 ~/.claude/skills/resolve-game-video/scripts/beats.py
   <track> --fps 60 -o <work>/beats.json` gives bars, drops and hits; put the
   payoff cut on a drop and cuts on bars/beats.

7. **Edit.** Write `edit.json` (`references/editing.md`): shots from takes
   (in point, duration, punch-in/push, slow motion), text, end card, music,
   game audio. `python3 scripts/render_short.py edit.json --fast` for a quick
   look, then without `--fast` for the final. Read the `.sheet.jpg` it writes.

8. **Review** before calling it done: the hook frame is strong; text readable at
   phone size and inside the safe zone (nothing important in the bottom 20% or
   right 12%); no dead frames at cuts; numbers match the log; ends on the
   CTA or loops; render check passed (size, duration, audio, -14 LUFS). Tell
   the user what is staged.

9. **Optional finishing in Resolve:** `render_short.py edit.json --resolve`
   writes `<render>.resolve/plan.json` for the `resolve-game-video` skill (takes
   on V1, text overlays on V2, premixed audio on A1). Claude only.

## When something is missing

The capture mode lives in `Capture/` (CaptureScene.cs, CaptureSession.cs,
CaptureActions.cs, CaptureStaging.cs) with small hooks in GameMain, Program,
GameSave, PlayAreaBounds, AudioManager and GameInput. If a shot needs a new
action or state (equip a module mid-shot, a camera pan, a new feature), add it
there, keep it out of normal gameplay paths, rebuild, and document it in
`references/scenes.md`.
