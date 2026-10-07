# Showing off Beyond the Belt in a short

The goal of every short: a viewer who has never heard of the game feels the
"one more upgrade" pull in under 30 seconds and wants to try it.

## Tone: an incremental game first

Beyond the Belt is an incremental game with light roguelike touches (prestige,
signals, modules). Lead with growth: numbers climbing, the field filling, one
ship becoming a fleet, "one more upgrade". Avoid RTS/roguelike framing such as
"WHAT WILL YOU BUILD?", "choose your strategy" or "every run is different";
mention prestige or signals only as a side note. Good closers: the number at
its peak, a plain logo/end card, or a loop back to the opening frame. Keep
captions concrete and understated; avoid teaser questions and hype lines.
(User feedback on Short 05, 2026-10-02, and caption tone, 2026-10-05.)

## What sells an incremental game

- **The number goes up.** Real counters from the capture log (`{gems}`),
  growing by orders of magnitude between shots: 82 -> 12.57T.
- **The screen fills up.** One ship and a few red gems, then a swarm that
  covers the frame. Contrast is the payoff; put it on the music drop.
- **Cause -> effect.** A click, an ability or an upgrade, and the screen
  answers immediately (chain lightning, genesis pulse, collector swarm,
  gravity well pulling gems in). Show the trigger, then let it land for 1-2 s.
- **Satisfying collection.** Gems converging on the homebase, rings of
  collectors, a jackpot. Slow motion (120 fps take, `speed: 0.5`) on the
  single best moment only.
- **Choice and build.** Upgrade tree lighting up, equipping a module, picking
  a signal (needs `hud: true`, 16:9; reframe for 9:16 with `focus` + `zoom`, or
  frame the UI in fullscreen).

## Structure (15-30 s)

1. **Hook, 0-2 s:** the most striking frame first; add a plain feature label
   only when it helps viewers understand the action. No slow fades in.
2. **Build, 2-15 s:** 3-6 shots, each one new thing, cut on bars/beats,
   1.5-3 s each. Escalate size, speed, numbers.
3. **Payoff:** the biggest moment on the music drop or hit.
4. **CTA, last 2-3 s:** end card over running gameplay, or cut back to the
   first frame for a loop.

One idea per short (progression, one ability, the fleet, clicking, prestige).
A series of focused shorts beats one montage.

## Learned from Short 05 (abilities)

- A big late fleet eats gems as fast as they spawn: the field looks empty. For
  ability or gem spectacle use the abilities sandbox or fewer ships.
- Close-ups (zoom 2-2.6) of the homebase make each ability readable; the wide
  late-game shot (zoom ~1.25) is the "everything at once" payoff.
- Takes vary per run: re-check in points on the final 4K takes, not on the
  previews.

## Recipes that worked (Shorts 08-17)

- **Ship classes** (08): `Shipyard: <class>` presets at `zoom` 4.5 (they are
  zoomed far out), count nodes maxed (`HC1-3`, `AHC1-2`, `EHC1-2`, `UHC1-2`,
  `PHC1-2`), `"stats": {"MaxGemCount": 400, "AutoCannon": true, "CannonFireRate": 4}` for gems
  to chase. Traits from the game's tooltips.
- **The big pull** (09): `Abilities: fully upgraded`, `equip: []`,
  `active_gems` 12000, `MaxGemCount` 14000, fps 120 + `speed: 0.5`, Homebase
  Magnetizer; mix game audio +12 dB ("sound on").
- **Clicker build** (10): clicking presets with every ship count node set to 0
  (the unlocks alone do not remove ships), packed field; `gravity` at a fixed
  point under the ship (the well drags the whole field in).
- **Gem events** (11): made before showers, comets and the colour unlock nodes
  were removed; the scene no longer loads. Gem colours now come from each
  weapon's Fire Power, and `event` fires `cannon`, `rockets` or `railgun` on cue.
- **Countdown / gem text** (12, 07): one take per digit; keep shapes off the
  ship (it collects what it overlaps). Hold automatic abilities with
  `"AllAbilityCooldown": 0.1` and fire them with `ability`.
- **Upgrade counts** (13): progression presets in order; `{upgrades}` gives
  the real total. Do not inflate the shown numbers with staging.
- **Logo in gems** (14): `gem_image` of `Content/Textures/logo_4k.png`, Teal
  -> Blue bands, cols 90, zoom 2.0; ~1,300 gems.
- **Time-lapse** (15): `time_scale` 6 on `Late game: production` - the count
  goes 20B -> 450B in 12 s; always say the speed on screen; no game audio.
- **Split screen** (16): pairs captured at 2160x1920, labels EARLY/LATER with
  live counts per half.
- **Prestige** (17): `prestige` then `new_run` at least 2.1 s later (the
  sequence must complete); the reset restores run stats, so re-apply stats with
  `stat` actions after `new_run`. The new run keeps abilities and permanent
  upgrades (Genesis ring right away).

- **UI features: shipyard / signals** (18, 19): record the real UI in one 16:9
  HUD take (`"width": 3840, "height": 2160, "hud": true`) driven by real clicks on
  named targets (`ui`: `nav:shipyard`, `discovery`, `inspect`, `reveal_shipyard`,
  `module:<name>` + `slot:N` for a drag, `ship:<class>`, `nav:signals`, `scan`,
  `card:N`); the capture draws a mouse cursor. `save.reveal` queues sealed
  modules (a legendary reveal is 3.6 s and ends on an impact: put the music drop
  there). In the edit, cut the take into shots and move a 9:16 window over it
  with `focus`/`focus_end` (pan) and `zoom` 1.2-1.6 - use the capture log's click
  positions for the focus points, end a shot on the click that changes the
  screen. Finish with a gameplay payoff (close the panel in the same take, or a
  before/after split like 12 Ring Replicators: 422 -> 1,808 gems per pulse).

## Recipes that worked (Shorts 20-25, trailer v02)

- **Evolve one ability** (20-22): one take per tree stage on `Abilities: starter`
  with `"abilities": {"*": 0, ...nodes}`, `ability_points` 200, `upgrade` the
  stage's headline node at 0.4 s (real purchase sound on the cut), fire at 0.8 s,
  `"AllAbilityCooldown": 0.1` to hold it, `"AutoCannon": false` and a static
  field (~500 gems for chains/drones, ~25 for rings). Gems per activation come
  from the `active_gems` drop in the log: Genesis 5 -> 50 -> 75 -> 130 -> 330,
  Cascade 3 -> 11 -> 67 -> 222 -> 368 -> whole field, Drones 4 -> 20 -> 406 -> all.
  Prerequisite traps: `GSNG*` includes GSNG7, which needs Genesis Spiral;
  GSCD4-6 need Crystal Bloom. Bloom seeds and early chains/drones are too small to
  read at phone size: let the caption carry them, punch in 1.5-1.8.
- **Golden Age turning the spiral gold** (20): fire, then `stat MaxGemCount 1`
  just before the second pulse so it only gilds (otherwise new red rings cover
  the gold). Midas reads best on a scattered field of ~110 gems.
- **Five commands** (23): the HUD command bar sits on the bottom edge, inside the
  Shorts UI zone in any 9:16 crop: caption the commands instead. Planet Cracker
  fires a 2.5 s beam into the planet that streams gems across the field; a sparse
  field (`active_gems` 0) lets the stream read. In 16:9 the cursor can press them: `ui: command:<name>`.
- **Pull-out** (24): do not snap the camera in during warmup (gems get squeezed
  into the small view as a block). Scene `zoom` 2 + `zoom` action 0.5 on
  `Late game: production` keeps ~9,500 gems on screen all the way out; add a 2x
  edit punch-in that eases out for more range.
- **On the beat** (25): at 144 BPM a beat is exactly 25 frames; write actions as
  `take time = edit time + 1.0` with the music from 0. Sparse starter field so each
  ring reads; drop = Magnetizer + Collector Swarm + chain.
- **16:9 trailer** (Marketing/Trailers/02-progression): `render_short.py` with
  `"size": [1920, 1080]`; text scales with width, so set `"size": 40` for captions
  (top, `pos` 0.07, clear of the HUD) and 70 for the end card. Shipyard reveal:
  punch in on the module (`focus` [0.57, 0.24], zoom 1.9) or the panel looks empty.

## Music

The Holizna tracks (`Content/Music/Holizna`) are the game's soundtrack and the
default: Greys (quiet intro, huge drop at 12.0 s - perfect for a reveal),
Pleiadeans (144 BPM, build to 13.3 s), Sky Fish (103 BPM, drops at 62.9/72.2 s),
Hopkinsville Goblins (130 BPM, steady energy). Other tracks in `Content/Music`
are mostly rubato piano (bad for beat cuts); "Floating Dreams" works under a
calm satisfying short, "Beyond the Limits" for a cinematic build (17).

## Framing for a phone

- Capture 2160x3840 (native 9:16 play field: gems and ships spawn across the
  whole vertical frame, the homebase sits in the centre).
- Things must read at phone size: ships and gems clearly visible at 1080 wide.
  Late presets zoom far out (expanded space); use scene `zoom` 1.5-3 for
  detail, or keep the wide view as the "look how big it got" shot.
- Use edit `zoom`/`focus` to punch in on the action (2x is free at 4K).
- Keep text in the middle band: away from the bottom 20% (title, buttons) and
  the right 12% (like/share).

## Feature map (ids for scenes)

| Feature | How to stage | Notes |
|---|---|---|
| "WISHLIST NOW" in gems (opening) | `Beginning`, `zoom: 1.2`, `gem_text` (Teal / Gold lines, `gap: 2` around the ship), ambient gems off | Short 07 (idea from Alex): (a) `from: "home"`, `order: "left"`, `dur: 1.4` - the ship writes the words in ~1.8 s; (b) words placed during warmup, then `manual: Homebase Magnetizer` swallows all 635 gems in ~1 s (put the last ones on the music drop). Gems launched from the ship itself would be collected at once; the action starts them just outside its range. Pair with the flashing `end_card` style. |
| Crash-landing intro (backstory) | `Beginning`, `warmup: 0`, `zoom: 1.8` | The game's own opening: logo at the top (fades by ~1.6 s; put text under it with `pos: 0.26`), ship sputters up from the bottom, impact at 3.017 s (impact sound) and the field bursts into red gems. Align a music lift with the impact. Use in some shorts, not all. Wording used: "ENGINE FAILURE..." / "...RIGHT IN A FIELD OF GEMS" (user-approved; no other lore exists in the game yet). |
| Fresh start, single ship | preset `Beginning` | Red gems only, manual clicks (`click_gems`, rate 2, zoom 2.2 so each pop reads). |
| Growth montage | `Beginning` -> `Developing fleet` -> `Late game: production` | A `live` `{gems}` counter across the shots: 124 -> 337K -> 40B -> 68B. (`First upgrades` looks almost like `Beginning`: skip it.) |
| Clicking build | `Early/Mid/Late game: clicking` | `hold` (sustained harvest), `gravity` (cursor gravity well), click chains. |
| Fleet | `Developing fleet`, `Late game: fleet` | Ship classes: Drifters (HC), Seekers (AHC), Prospectors (EHC), Trove hunters (UHC), Rimrunners (PHC). `level` adds ships mid-shot. |
| Automatic abilities | preset `Abilities: fully upgraded` (busy gem field, no fleet) + `equip` one ability; in late presets `"abilities": {"*": "max"}` | GS1 Genesis Pulse blooms a (golden) ring of gems around the planet; CM1 Graviton Cascade draws constellation chains and collects arcs of the ring; Drones1 sweeps. `ability` fires on cue. Close-ups at zoom 2-2.6 read well. |
| Manual fleet abilities | earnings this run unlock them | Overdrive (250), Planet Cracker (5K), Collector Swarm (250K), Homebase Magnetizer (5M), Ability Surge (100M). |
| Weapons | preset `Weapons: all unlocked`; `event` action | Cannon (click the planet, Auto Cannon), Mining Laser (Twin Lasers), Rocket Pods, Railgun. `event` fires `cannon`, `rockets` or `railgun` on cue. |
| Shipyard / modules | `Shipyard: <class>`, `save.modules` | HUD view (`hud`, `panel: shipyard`). |
| Signals | `Signals: pending choice`, `save.signals` | HUD view. |
| Space expansion | `zoom` action < 1 | Pulling the camera out = the field grows. |
| Endgame chaos | `Uber endgame` | Deliberately unrealistic; label it as such or avoid. |

Verify every feature with a `--preview` take before planning a short around it:
a preset name alone does not prove the effect is visible.

## Honesty

Staged states are snapshots, not play times. Fine: "FROM 0 GEMS... TO 12
TRILLION" (both real states of the game). Not fine: "after 10 minutes" unless
measured. Do not show unreleased or debug-only features as if they ship, and
do not use `Uber endgame` as normal progression.

## Recipes that worked (Shorts 41-43, gem scale and weapons at full power)

- **50,000 gems** (41-42): `Weapons: all unlocked` with every weapon's run-tree values at their real
  maximums as `stats` (cannon FP 16 / rate 2.25, laser 6 / 1.75, harpoon 11 / 1.75, rockets 13 / 1.8 / count 5,
  Railgun 35 / 1.75 / 6 fragments) fills an empty, fleet-free field at ~560 gems/s: `time_scale` 8 reaches
  the 50,000 cap in ~11.7 s of footage (label the speed). For a full field at the start of a take, let the
  weapons fill it during `warmup` (~100 s) and switch them off with `stat` actions at 0 s. Do not use
  `save.active_gems` for big fields: restored gems only spawn inside the camera view at load and look
  like a solid block when the camera pulls out.
- **The 50,000-gem pull** (41): the Homebase Magnetizer now needs the Command Center talent
  (`"meta": {"CC1": 1}`) and `earned_this_run` >= 5e6; at zoom 0.6 it collapses all 50,000 into the ship in
  ~3.8 s (120 fps, play at 0.5). `GemSpawnCooldown` (Short 09's scene) no longer exists.
- **Weapons one by one** (43): switch weapons off in `stats` and on with `stat` actions at 0.2 s so each
  take opens on an empty field. Orbital Strike throws rocket clusters out behind the planet against the
  9:16 field's edge: leave it out of a rocket close-up. The Railgun's Singularity leaves a black hole that
  stays in later shots. The core extraction swallow fades gems in place in ~0.6 s: not a short on its own.
- Live `{on_screen}` counters read best at the top (`pos` 0.12) with a `sub` naming the speed.
- An `end_card` only shows over footage: the edit length is the sum of the shots, so let the last shot
  run on under it.

## Learned from the 60 s gameplay trailer (Marketing/Trailers/03-gameplay-trailer)

- A new run no longer starts in a gem field: the crash intro (`Beginning`, `warmup` 0, impact at 3.01 s)
  has no gem burst, and the first gems come from clicking the planet (`click` with `target: "planet"`
  every ~0.3 s fires the cannon), then `click_gems`. `Beginning` already owns the first Drifter (HU1);
  buy `AC1` (Plasma Repeater) and `HC1` in an upgrade-tree shot instead (HUD at zoom 2.2 on the tree).
- `Mid game: abilities`, `Late game: *` and `Uber endgame` sit far zoomed out: use scene `zoom` 2.5-3.5
  for anything that must read (ships, chains, blasts); a pull-out from `zoom` 4 with a `zoom` action of
  0.25 shows the scale. Ship systems read best in `Abilities: fully upgraded` with every weapon off
  (`MiningLaser`, `AutoCannon`... false), a small `active_gems` field and `zoom` 1.5-2. The Core Drill
  bores on the planet's far side and barely shows.
- A late-game `fracture` (`Late game: production`, `zoom` 2.5) swallows a ~50,000-gem field and erupts
  ~3.5 s after the action: a strong cold-open closer.
- Music for a trailer: cut the track on bar lines with ffmpeg (`atrim` + `concat`, 10-30 ms fades)
  into one WAV in `takes/`, then lay every cut on that grid (Pleiadeans: 4 bars of the high section, then
  the quiet break from 68.33 into the drop at 80.0).
- HUD shots punched in at `zoom` >= 1.2 also crop away the command bar at the bottom.
