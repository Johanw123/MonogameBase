---
name: short-form-gameplay
description: Record and edit real gameplay into full-screen vertical and square short videos for Instagram, TikTok, YouTube Shorts, Facebook, and X/Twitter, directed by a requested feature, progression stage, or gameplay moment. Use for creating clips, not merely writing captions or publishing posts.
---

# Short-form gameplay

Create actual video files showing the part of the game the user requests. Team Jape is the developer identity; Beyond the Belt is one game, not the account name. Support other games by inspecting their capture facilities rather than assuming this game's APIs.

## Visual brief

- By default, deliver **two versions of the same edit**: **9:16, 1080 × 1920** for TikTok, YouTube Shorts, Instagram Reels, and Facebook Reels; **1:1, 1080 × 1080** for X/Twitter feed posts. If the user requests only one platform/format, deliver that version. Square is the user's X feed preference, not an X requirement; X also supports vertical video. Default to MP4, H.264, yuv420p, 30 fps, AAC 48 kHz, and fast-start metadata.
- **Moving gameplay fills the entire frame, edge to edge, for the entire clip.** No letterboxing, decorative borders, inset gameplay, blurred duplicate backgrounds, title panels, caption backgrounds, or separate intro/outro cards.
- Default to **no added text**. If explanation is useful or requested, put a short phrase directly over the gameplay, usually for 1–3 seconds. Use a subtle glyph outline/shadow for contrast, never a box, panel, banner, or darkened strip. Avoid persistent headlines, logos, feature lists, and calls to action unless requested.
- Text must not cover the featured action. As a working margin, keep essential text within roughly x=100–900, y=220–1450 on the vertical canvas; on the square canvas, keep a roughly 100-pixel text margin and reposition text to avoid the subject. Platform overlays vary, so inspect the export. Use one phrase at a time, at most two lines.
- Preserve genuine gameplay behavior and pacing. Progression presets can stage a scene; distinguish separate stages rather than implying instant progression. Don't fabricate effects, rewards, or gameplay with generated imagery.

## Direct the capture

Turn the request into a concrete shot: what feature should be visible, at what stage, what triggers it, where it happens, and when the clip should end. Default to one 12–20-second edit, exported in both formats, when duration/count are unspecified. Two aspect-ratio exports are variants of one edit, not two different clip concepts. If the user names a feature, show that feature clearly rather than substituting a generic fleet montage. Infer routine choices; ask only if the missing subject or constraint materially prevents choosing the right shot.

Inspect the current game's implementation and existing capture tools. Determine whether the request needs the world render, in-game UI, a player action, a loadout, or a progression transition. Use a separate save/settings session. Reuse a current compiled build when it contains the requested feature; record which build was used. Don't silently record a stale build that lacks recent changes.

For **Beyond the Belt in the UntitledGemGame repository**, read [references/beyond-the-belt.md](references/beyond-the-belt.md). The bundled capture helper supports stage, length, warmup, equipped abilities, and camera zoom. Other mechanics may need a small temporary harness adaptation or real input; do not claim that every feature is already automated.

Choose framing before a long recording. For paired delivery, capture a high-resolution original with enough space for both portrait and square crops; reuse the same take, shot timing, and audio. Derive each version directly from that original, never the square version from an already-cropped vertical export. Adjust camera position/zoom or per-format crop focal points until the requested action fits both. A genuine portrait viewport is useful for vertical-only requests, but should not discard the wider composition needed for a paired square version. Never stretch footage or restore the old panel layout to fit the whole world. If a feature spans the screen, change the camera or use successive close-ups. HUD-free capture is suitable for world effects; menus, upgrades, signals, and shipyard requests require the actual relevant UI.

Capture a brief sample, inspect it, then record enough lead-in and follow-through to show cause and effect. Start the edit near the action, skip loading/debug menus and dead time, and let the result remain visible. Don't reset cooldowns or alter game rules just to force spectacle; wait or use legitimate available actions.

## Edit and validate

Use [scripts/render_vertical.py](scripts/render_vertical.py) for paired full-screen vertical/square crops, cuts, optional unboxed captions, and optional music. One command produces both files; `--format vertical` or `--format square` produces only that version. Read [references/editing.md](references/editing.md) for its manifest and commands. It accepts arbitrary selected takes; it does not hardcode the previous two videos. Adapt the edit for tracking or other needs the simple helper does not implement, while preserving the visual brief.

Keep recorded game audio when available. Music is optional, from supplied or appropriately licensed assets; do not automatically reuse the same track. State if a capture has no live game sound. A silent export is acceptable when requested or when the user intends to add platform music.

Save both clearly named MP4 variants (e.g. `chain-v01-vertical.mp4` and `chain-v01-square.mp4`) and one reproducible shot manifest in a named project marketing output directory. Preserve raw takes when practical. Use new filenames instead of overwriting previously delivered edits. Generate a cover only if requested or useful; derive it from the clip without adding another panel layout. Post copy and hashtags are optional supporting material, not a substitute for recording.

Before delivery:

- Verify both exports independently: format, matching duration and cut timing, and full decode with ffprobe/FFmpeg; ensure audio is present as intended, not clipped or cut short.
- Inspect frames in both aspect ratios from the start, every cut/caption change, the main event, and the end. Check that gameplay fills the frame and the requested feature is recognizable throughout its relevant action. Watch playback when tooling supports it; otherwise inspect sampled frames plus decode/timing checks and describe that limitation accurately.
- Check cropping does not lose the setup or payoff. If it does, reframe or recapture rather than delivering an attractive but irrelevant clip.

Return clickable video links, a short description, duration/format, and any material capture limitation. Creation does not authorize uploading, posting, or sending messages.
