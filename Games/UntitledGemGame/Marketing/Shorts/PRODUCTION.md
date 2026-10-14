# Fresh shorts production

Approved 12 October 2026: all ten concepts in PLAN.md. Production order: 01,
04, 06, then 02, 03, 05, 07, 08, 09, 10. Fullscreen portrait, normal-speed game
footage, game sounds and the existing Holizna soundtrack.

Scenes stage progression states, legal feature combinations and discoveries;
they do not measure the time needed to reach them. Later snapshots are labeled.
Every scene enables AutoRefuel. Capture reports include fleet fuel counts to
check for stranded ships; normal automatic refueling is allowed.

The public [Steam page](https://store.steampowered.com/app/5084070/) was checked
on 12 October 2026 and supports wishlisting. The gameplay executable's separate
5426850 ID is not the destination used in posting copy.

Capture build: `dotnet build UntitledGemGame.csproj -c Debug --no-restore -m:1
-nr:false`. Single-node MSBuild avoids silent worker failures in this environment.
The current environment uses software rendering and x264 for the takes.

All captures use temporary saves/settings. No generated images or music are used.

Capture repairs for the current HUD: offscreen capture counts as focused input;
Upgrades and Systems navigation read the shared scripted/real GameInput adapter.
This restores scripted HUD actions after recent menu changes. Normal input still
uses the same real mouse and Gum cursor. No progression or combat rules changed.

Quality review: low-resolution `_draft_` files were used only for timing.
The first final was visually checked at 1080x1920 and passed full decode,
60 fps, audio loudness (-14.04 LUFS), and fuel checks (zero ships waiting).
Subsequent edited review copies use 1080x1920/60. Source takes remain 4K/60.
Earlier source takes use x264 medium CRF 16. Remaining takes use veryfast
CRF 14 to reduce encoding work on this software-only host; final edits use CRF 17.

Hidden-HUD input fix: world-only capture scenes return an offscreen UI cursor,
preventing portrait world clicks from activating invisible landscape HUD buttons.
HUD scenes and ordinary player input retain their existing cursor coordinates.

The module and signal payoffs continue their actual UI takes after closing the
menus. The extraction short also uses one session for holding Extract Core,
spending the earned points and starting the next run. Later fleet snapshots
remain explicitly labeled.

Completed: all ten final cuts, made from 20 native 4K/60 takes. Exports are
1080×1920/60, H.264 with stereo AAC, real game sounds and existing soundtrack.
Every final was visually reviewed; full decoding, duration, frame rate, audio
loudness/peak and source fuel checks passed. Results are in export_qc.json and
per-short quality.json files. Titles/captions are in POSTING.md.

Core extraction pauses the outgoing fleet during the collapse animation. Its
fuel telemetry includes short waiting intervals in the used shots (at most two
seconds); the new-loop ship and later fleet snapshot show none. The check uses
sample timestamps because inclusive endpoints can contain nine samples in two
seconds. No gameplay or fuel rules were changed for the footage.

The capture build passed with zero errors and 102 existing warnings. Python
production/QC scripts compile, and the changed capture/docs files pass whitespace
checks. Player saves/settings and unrelated TODO.md changes were preserved.
Nothing has been uploaded or published.

## 13 October revisions and store reuse

All ten current exports use music only (`game_audio: false`). Eight cuts reused
their original encoded H.264 video packets, verified by SHA-256 packet hashes in
`music_remix_qc.json`; only their audio was replaced. Two peaky music mixes were
limited again with extra AAC headroom, preserving the same encoded video.

Short 7 was cut again from its existing 4K sources: the opening Genesis Pulse
title accompanies the actual upgraded pulse, the base pulse precedes the
Power Cell purchase, and the spending shots frame Pulse Yield and Rich Veins.
The final is eighteen seconds. Both the purchase and spending UI were reviewed
at full output resolution. Later upgraded footage remains labeled.

Short 10 uses a new native 4K/60 extraction/restart take (`extract_v3`), bringing
the source count to twenty-one. The capture-only `dismiss_tooltip` action calls
the game's normal tooltip dismissal API after purchases and before the final
mining segment. The reported 9.2-second frame was reviewed at full resolution;
the Unlock Drifter tooltip is gone and the new ship is working. The take and
all final exports passed normal-speed capture and fuel checks.

The shared renderer now limits at four times the output sample rate with extra
headroom for AAC. All ten revised exports passed complete decoding, dimensions,
duration, frame rate, loudness and true-peak checks. `export_qc.json` and the ten
`quality.json` files identify the current versions. The README links only those
versions; previous review files are retained locally.

Eight silent Steam feature loops and eleven screenshots were made from these
current takes in `../Steam/2026-10-13-refresh/`. Screenshots are decoded native
video frames, with portrait sources cropped to 16:9; full-size PNG masters and
JPEG upload copies are retained. Matching GIFs and 1920×1080 JPEGs live in
`../Itch/2026-10-13-refresh/`. Store copy, platform requirements, a local review
page, source manifest and size/decode reports accompany the packages. No new
art or music was generated. Nothing has been uploaded or published.
