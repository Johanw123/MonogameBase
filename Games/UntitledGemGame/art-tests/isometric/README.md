# Isometric graphics prototype

The development game starts with this style enabled. Press **F8** during
gameplay to compare it with the previous graphics. The Tab debug window also
has an **Isometric graphics prototype (F8)** checkbox. It is a session-only
visual setting; it does not alter saves or progression.

The current exports use an orthographic camera 70 degrees above the movement
plane, diagonally oriented at 45 degrees. The exporter accounts for this
elevation when mapping ship headings to their screen movement directions.
Ships now use the selected stylized sci-fi materials: orange paint, blue-gray
metal, darker glass, fine bevels and small cyan navigation lights. These
frames use Cycles with 32 samples and denoising; the planet and gem exports
keep their existing lighting and materials.

Harvesters, drones and the homebase reuse the miner ship. Its 128 prerendered
headings match screen movement, including the isometric foreshortening. Each
heading has five roll poses: -30, -15, 0, 15 and 30 degrees. Moving ships bank
into turns based on angular speed and settle back to level during straight
flight. The stationary homebase stays level. The Tab debug window has a
**Ship banking** checkbox for comparison. Ships have stable lighting, are
sorted by screen Y, and have a small
animated cyan exhaust. Gems use 8 prerendered crystal orientations, colored
by the existing shader, and keep their spawn/collection animations. The
planet uses 120 rotating frames of Kenney's detailed meteor model, keeping
the original 4.8 second rotation period. Ship headings are 2.8125 degrees
apart; planet frames are 3 degrees apart and advance at 25 frames per second.
Ship and planet frames are now 512x512, supersampled from 1024x1024 Blender
renders. The 640 ship frames occupy 40 pages, and the planet uses eight
pages, at most 2048x2048, with 16 frames per page (the final planet page has
8 frames). They use linear filtering
and mipmaps for smooth enlargement and reduction, keeping hulls fully opaque.
Together the sheets use about 760 MiB in RGBA8, or about 1 GiB with mipmaps.
They retain their previous world size and animation speed. F8 still switches
styles. Prerenders have finite resolution; very large enlargement can still
lose detail. During gameplay, hulls composite after world bloom, behind menus
and HUD controls, preserving their contrast. Thrusters and effects still glow.
Hull brightness follows world dimming; paused scenes with the blur filter
enabled draw hulls in the world pass so they blur with the rest of the scene.

This is an art prototype rather than a full world projection conversion:
simulation and screen positions, picking, collection bounds and weapons still
use the existing coordinates. The planet shell continues to take damage but
the new planet artwork is visible through it; old fracture texture overlays
are omitted. Some weapon/module effects still use the previous art style.
Planet and gem occlusion is still the existing layer order.

## Reproduce the assets

The FBX files in the supplied Kenney kit are ASCII FBX, which Blender cannot
import. The bundled GLB copies are the same kit's corresponding models.
Kenney's CC0 license is included in `models/LICENSE.txt`.

In Blender, run `tools/blender/export_isometric.py`. It uses the open
`craft_miner` if available, or imports the bundled miner. It creates an
isolated scene, exports 256 frames to `/tmp/btb-isometric-frames`, and restores
the source scene. Render engine and view transform come from the open scene;
the planet/gem export used EEVEE in Blender 5.2.2 LTS. To reproduce the chosen
ship appearance, run `tools/blender/export_stylized_ship.py` in Blender after
the base export. It exports 128 ship headings at each of the five bank poses
(640 frames total) and temporarily uses
an already-enabled GPU when available, restoring that preference afterwards.
Then run:

```sh
python tools/blender/pack_isometric.py
dotnet build UntitledGemGame.csproj --no-restore -m:1 -p:BuildProjectReferences=false
```

For a ship-only refresh, run `export_stylized_ship.py`, then use
`python tools/blender/pack_isometric.py --ship-only` to leave the planet and
gem sheets unchanged.

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
