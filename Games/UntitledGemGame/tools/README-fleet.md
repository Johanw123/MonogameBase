# Fleet atlas

The next atlas after UI icons targets ships: gameplay submitted an engine texture
then a hull texture per entity, splitting the deferred SpriteBatch at every
change. The menu and transition also opened a batch for each ship. The fleet now
shares one texture without sorting or changing engine/hull/ship overlap order.

`FleetAtlas.cs` lists the seven used hulls and six engine strips. The generator
splits each eight-frame strip into individually packed frames. Original sizes,
transparent margins, origins, animation speed (150ms/frame), and independent
playback are retained. Four pixels of edge extrusion isolate the harvester
shader's maximum 3.5-texel outline samples. The image is 1024x512 RGBA (2 MiB on
the GPU without mipmaps). The runtime continues to use the existing shader,
with `TexelSize` referring to the atlas dimensions. Movement and UI sizing use
region dimensions, never the shared texture's size.

After changing paths, frame count, or source PNGs:

```sh
python3 tools/build_fleet_atlas.py
python3 tools/build_fleet_atlas.py --check
```

Requires Python 3 and Pillow, as for the icon atlas. Commit the generated
`Content/Atlases/fleet.png` and `fleet.json`. Normal builds don't require Python.
The content builder compiles this image with the usual premultiplied alpha and
copies its metadata; the old wildcard that built the entire fleet art pack is
removed. Existing output directories can still contain older unused compiled
textures until a clean build.

## Validation

```sh
dotnet build --no-restore
dotnet build Tests/PersistenceChecks.csproj --no-restore
mgfxc Content/Shaders/HarvesterShader.fx /tmp/fleet-harvester.mgfx /Profile:OpenGL
SDL_VIDEODRIVER=offscreen ALSOFT_DRIVERS=null dotnet Tests/bin/Debug/net10.0/PersistenceChecks.dll --fleet-atlas-check . /tmp/fleet-harvester.mgfx
```

The isolated check doesn't touch saves. It compares 100 mixed ships plus the home
base against original hulls/strips: all eight engine frames, separate animation
playback, looping, rotation, scale, overlapping ships, engine tint, and the
harvester shader's normal/fade/pulse/hover/burst states. Linear-filtered menu and
shader output must match within 2/255 per channel. The additional rotated
point-filtered check allows up to four boundary pixels where atlas UV rounding
can select the adjacent texel. It asserts 201 original GPU draws become one
packed draw. This is a draw-call measurement, not a whole-game FPS claim.

Other candidates were deliberately left out: gems already share a dedicated
texture and custom batch; space backgrounds use wrapping samplers and a separate
shader; upgrade-tree/Gum images require their own region-aware UI conversion and
would not fix the per-ship texture switches.

Manual checks: menu fleet, new/continued-game transition, all harvester types,
drone swarm, refueling/hover outlines, edge/corner navigation, shipyard previews,
and home base. Confirm original overlap order and collection reach at the edges.
