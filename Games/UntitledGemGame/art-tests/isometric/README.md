# Isometric graphics prototype

The development game starts with this style enabled. Press **F8** during
gameplay to compare it with the previous graphics. The Tab debug window also
has an **Isometric graphics prototype (F8)** checkbox. It is a session-only
visual setting; it does not alter saves or progression.

The current Synty fleet uses seven distinct models:

| Collector | Synty model | Base pickup radius in world units |
| --- | --- | ---: |
| Basic | SM_Ship_Fighter_01 | 12 |
| Advanced | SM_Ship_Fighter_Heavy_01 | 12 |
| Perimeter | SM_Ship_Stealth_01 | 12.15 |
| Expert | SM_Ship_Bomber_01 | 12.8 |
| Ultimate | SM_Ship_Cruiser_01 | 16.8 |
| Drone | SM_Veh_Drone_Repair_01 | 6.4 |
| Homebase | SM_Ship_Cruiser_01 (red/cyan hero material) | 42.5 |

Moving ships have 128 headings and five bank poses (-30, -15, 0, 15, 30).
The homebase uses 128 level poses. Each 256x256 frame is supersampled from
512x512, with 64 frames per 2048x2048 page: ten pages per moving model and
eight 512px beauty pages and eight emission pages for the homebase. They retain the original palette atlas and normals,
with a restrained cyan emissive mask. The shared camera is orthographic at 55 degrees.
Lights are a warm key, cool fill and violet rim. The meshes fit a radius-1.3
sphere inside a 3.7-wide canvas. Runtime canvas size is base pickup radius
(including entity scale) times the smoothed collection multiplier times 3.7/1.3.
This replaces the oversized fixed 88/220-unit canvases without applying scale
twice or changing collection mechanics. Thruster placement uses the same radius.

The planet is the rougher SM_Env_Planet_14 mesh, flat shaded, using the purple
PolygonSpace_Planet_02_B palette, land mask and recreated triplanar contrast
shader. It has 120 512x512 frames, supersampled from 1024x1024, across eight
pages. The rotation pole is vertical on screen with no sideways tilt; the
4.8-second rotation period is retained. AgX exposure 0 and the shared homebase lighting keep its surface treatment
consistent with the ships. A separate Fresnel rim pass supplies bloom. Unity's extra rim/cloud/outline passes
are not ported.

Collectibles use SM_Env_Asteroid_05 as faceted ore fragments: eight orientations,
64x64, supersampled from 128x128 and packed into the existing gem atlas. A neutral
gray rough material lets the existing shader apply resource colors. Values,
spawn/collection animation, picking and collection bounds remain unchanged.

The sprite sheets use about 1.57 GiB in RGBA8 (about 2.09 GiB with mipmaps),
independent of fleet count. Linear filtering and mipmaps keep hulls opaque and
reduce shimmer. Hulls composite after world bloom; effects and thrusters retain
bloom. Paused/blurred scenes keep hulls in the world pass so they blur together.

This remains an art prototype: simulation, picking and weapons use the existing
2D coordinates. The damaged planet shell is visible through the new artwork;
old fracture texture overlays are omitted. Weapon/module effects still use the
previous art style. F8 compares with the legacy graphics.

## Reproduce the assets

The FBX files in the supplied Kenney kit are ASCII FBX, which Blender cannot
import. The bundled GLB copies are the same kit's corresponding models.
Kenney's CC0 license is included in `models/LICENSE.txt`.

In Blender, run `tools/blender/export_isometric.py`. It uses the open
`craft_miner` if available, or imports the bundled miner. It creates an
isolated scene, exports 256 frames to `/tmp/btb-isometric-frames`, and restores
the source scene. Render engine and view transform come from the open scene;
the planet/gem export used EEVEE in Blender 5.2.2 LTS. Pack those base frames
with `python tools/blender/pack_isometric.py` after rendering the earlier
miner bank poses with `export_stylized_ship.py`.

For the current fleet, purple planet and ore, run
`tools/blender/export_synty.py` in Blender. It imports the FBX meshes and
textures from `/home/johan/My project/Assets/Synty/PolygonSciFiSpace` into a
new scene. The licensed source package is read only and is not bundled here.
It renders 3840 moving-ship poses, 120 planet frames and 8 ore orientations
to `/tmp/btb-unified-style-frames`, then 128 main-ship beauty/emission pairs
to `/tmp/btb-main-ship-frames`. Pack and build:

```sh
python tools/blender/pack_synty_fleet.py
dotnet build UntitledGemGame.csproj --no-restore -m:1 -p:BuildProjectReferences=false
```

The packer checks all 4224 frames for transparency, clipping and source
resolution. `fleet-preview.png` shows the current seven ships, planet and ore.
`export_open_ship.py` and `pack_isometric.py` retain the earlier single-model
export workflows. `assets-preview.png` is the earlier fighter/gold-planet trial.

The earlier `export_stylized_ship.py` reproduces the previous orange miner
using Cycles (32 samples, denoising), 128 headings and five bank poses. It
temporarily uses an already-enabled GPU, restoring that preference afterwards.

The build flag reuses existing compiled dependencies outside this workspace.
An ordinary build also works when those dependencies are writable.

## Gameplay preview

`game-test.json` stages an isolated First upgrades session with 250 gems.
It records 8 seconds at 1280x720 and saves stills at 1 and 5 seconds, without
reading or writing the player's save. From the game directory:

```sh
ALSOFT_DRIVERS=null SDL_VIDEODRIVER=offscreen dotnet bin/Debug/net10.0/UntitledGemGame.dll --capture art-tests/isometric/game-test.json --overwrite
```

The environment variables allow a headless preview with a null audio device;
they are not needed for normal gameplay. `assets-preview.png` shows the sheets.

The earlier 16-heading gameplay clip is `game-preview.mp4` (240 frames / 8 seconds),
with stills `game-preview_1.00s.png` and `game-preview_5.00s.png`. The game build,
asset packing transparency/clipping checks, and existing preload checks pass.
That clip predates the smooth sprite sheets. No new preview video was created
for the smooth-animation or high-resolution updates, at the user's request.

The older `--render-check` SpriteBatch-versus-instanced-gem comparison fails
on this environment (3775 differing pixels, maximum channel delta 245). An
isolated replay using the HEAD version of GemRenderBatch produces the same
failure. That replay substitutes zero flight data because this fixture uses
only null Gem components and the flight fields are assembly-internal. This
pre-existing comparison failure was not changed by the graphics prototype.

Added 2026-10-14.

## Nebula background trial (2026-10-15)

The old layered background is restored by default as of 2026-10-15.
`Content/Textures/Backgrounds/nebula-3.png` remains available as an opt-in trial. The Tab debug window's **Nebula 3 background** checkbox compares it
with the previous layered background. This session-only choice is independent
of the F8 ship/planet prototype toggle. The sky stays distant as gameplay zoom
changes and fills the active viewport, cropping for other aspect ratios.

Reproduce the 3840x2160 background by running
`tools/blender/export_nebula_background.py` in Blender with the source at
`/home/johan/Downloads/Nebula+3.hdr`. It renders the panorama from yaw 0/pitch 0
using a 160-by-90-degree equirectangular crop, AgX exposure -1, in a temporary
scene, then restores the open scene. Run
`python tools/blender/finish_nebula_background.py` once after rendering to
compress highlights and retain dim cloud detail under the runtime brightness
tint, then build. Only the PNG is bundled with the game.

The existing `space4kstars.png` is drawn as a separate layer with point filtering.
Both sky layers use RGB tint 56/255 and full opacity in the tint; premultiplied
star alpha blends them without exceeding 56/255. That stays below the default
bloom threshold of 0.25, preventing sky-generated bloom while preserving the
foreground effects and the shared bloom shaders. The HDR's own clouds remain
soft, but the wider framing and separate stars avoid magnifying that softness.

`gameplay-preview-v3.mp4` is a 10-second 1920x1080 / 60 fps gameplay preview
of the current detailed ship, banking, wider nebula view and separate star
layer. `scenes/gameplay-preview-v3.json` stages eight auto-refueled harvesters,
cannon/laser/rocket fire and a 260-gem cap. It uses the isolated capture save.
The earlier `combat-preview-v2.mp4` predates the current ship and background.

## Synty gameplay preview (2026-10-15)

`scenes/synty-preview.json` stages a five-second isolated gameplay test at
1920x1080 / 30 fps, with weapons, eight harvesters and stills at 1 and 4 seconds.
Run with the same headless environment as above; output is `synty-preview.mp4`.
Uses the restored layered background and current Synty ship/planet sheets.
The game still uses its existing 2D simulation and F8 style comparison.
The desktop build, preload checks and all 760 frame transparency/clipping
checks passed. The 150-frame capture completed; both gameplay stills show the
new assets and restored background correctly.

## Full Synty fleet preview (2026-10-15)

`scenes/synty-fleet-preview.json` stages all five harvester types, Drone Swarm,
the transport homebase, upright purple planet and colored ore against the
restored background. It records five seconds at 1920x1080 / 30 fps with stills
at 1 and 4 seconds. Run using the headless capture command above, substituting
this scene path; output is `synty-fleet-preview.mp4`. The capture uses an isolated
save. The earlier `synty-preview` artifacts show the initial fighter/gold trial.

Validation: desktop build, fleet atlas/banking checks, collector scaling checks,
preload checks and all 4096 frame checks passed. The 150-frame gameplay capture
completed successfully; the stills confirm the smaller hulls, upright purple
planet and colored ore render correctly.

## Main ship lighting pass (2026-10-15)

The main ship uses the broader SM_Ship_Cruiser_01 silhouette with red paint,
cyan emissive strips, neutral frame and the original panel detail UV map.
Its camera is at 55 degrees to reveal more side thickness. Stronger warm key,
cool fill and violet rim lights plus fine bevels give the panels clearer edges.
The old 0.75 brightness tint is removed for the homebase. Fleet ships retain
their previous look. Pickup-based world size is preserved.

The main ship has 128 headings at 512x512, supersampled from 1024x1024, with
separate beauty and emission atlases (eight pages each). Only emission enters
world bloom; the opaque detailed hull composites afterwards. This produces
cyan light spill without washing out the paint. The higher resolution keeps
its panel detail sharper when enlarged. No engine plume is added in its normal
stranded state.

Run `tools/blender/export_main_ship.py` in Blender, then:

```sh
python tools/blender/pack_synty_fleet.py --main-only --frames /tmp/btb-main-ship-frames
dotnet build UntitledGemGame.csproj --no-restore -m:1 -p:BuildProjectReferences=false
```

The full fleet exporter now invokes the main-ship exporter automatically, and
the full packer packs its separate frames. `main-ship-look.png` is the detailed
material render. `scenes/main-ship-preview.json` stages a four-second isolated
gameplay comparison, with stills at 1 and 3 seconds. The Unity sources remain
unchanged.

The main-ship build, atlas/banking, pickup-size and preload checks passed.
All 256 main beauty/emission source frames passed packing checks, and the
120-frame gameplay preview completed. The stills show the cyan bloom layer
and brighter opaque red hull correctly at normal playing size.

## Shared rendering treatment (2026-10-15)

Every prerendered element now uses the homebase's 55-degree camera, AgX view
transform at exposure 0, and warm key/cool fill/violet rim energies of
1050/280/650. `render_style.py` owns these settings and the ship material graph.
All ship materials share panel detail, roughness 0.48, cyan light strips at
emission strength 2.5 and fine bevels. The main ship keeps red paint; the fleet
keeps blue paint. Planet and ore keep their natural surface materials under
that same lighting/color pipeline; the planet pole remains upright.

All surfaces now render after world bloom at full brightness, with only the
normal shared world/menu dimming applied to RGB. The 0.75 fleet tint, 0.7 ore
bake multiplier, and prototype planet tint/exposure reduction are removed.
Only emission enters bloom, using one shared intensity: ship light strips,
the planet's natural rim, and ore's colored highlights. Planet, ore and ships
keep their layer order. Paused/blurred scenes draw their surfaces in the world
pass so the shared blur still applies.

Ore's colored surface and emission use the same instanced geometry and GPU
flight animation in both passes, retaining picking, values, collection and
counts. Geometry and buffers are uploaded once; counters accumulate over the
two passes. The palette shader stays because resource type determines color.
This is the same surface/emission policy as the homebase, with each material's
own appropriate color and emissive features.

Fleet emission frames are 128x128 (60 pages at 1024x1024); planet emission
frames are 256x256 (8 pages, last 1024x512). The homebase keeps its 512px
beauty/emission. Lower-resolution light maps are sufficient for bloom while
the full-resolution surface maps preserve detail. The compositor exports
beauty plus emission in a single render. Emission intermediates use ZIP-compressed
half-float EXR to keep temporary storage bounded; the packer uses OpenEXR,
NumPy and Pillow to convert linear premultiplied emission to straight-alpha
sRGB. The content builder premultiplies the PNGs once.

Reproduce using the full `export_synty.py` and `pack_synty_fleet.py` workflow
above. The export stores fleet/planet/ore frames in `/tmp/btb-unified-style-frames`
and main-ship pairs in `/tmp/btb-main-ship-frames`. The packer also validates
emission coverage. `scenes/unified-lighting-preview.json` records four seconds
with every harvester type, Drone Swarm, the planet and colored ore. Output is
`unified-lighting-preview.mp4`, with stills at 1 and 3 seconds.

Atlas readiness checks also verify expected texture dimensions, so a missing
file resolved as the loader's white placeholder cannot become an additive quad.

Validation: all fleet/planet/ore source frames and 256 homebase beauty/emission
frames passed packing checks. All 76 emission atlases have transparent RGBA
coverage. The final desktop build passed (0 errors); ship banking, collection
scale and preload checks passed. The 120-frame gameplay capture completed;
both stills were visually checked for correct transparency, aligned emission,
upright planet and consistently lit surfaces. No white placeholder quads remain.

## Ship highlight compositing (2026-10-15)

`ShipEmission.fx` draws emission over the finished hull, restoring the cyan
core and a compact 2.5-pixel halo that the opaque overlay previously covered.
Light coverage comes from emission RGB rather than the atlas's full silhouette
alpha. The core replaces pale baked trim while the halo adds light. Sampling
accounts for each atlas resolution, ship size and camera scale. All ship types
use this pass; menu dimming and pause blur still apply. Global bloom settings
are unchanged. `scenes/ship-highlights-preview.json` captures the same fleet
scene as the shared-lighting preview for comparison.

Validation: desktop/shader build passed with 0 errors. The 120-frame fleet
capture completed; gameplay stills show saturated cyan trim over intact hull
paint on the homebase and moving harvesters, without rectangular artifacts.
