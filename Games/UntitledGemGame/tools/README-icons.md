# UI icon atlas

`Content/Atlases/icons.png` and `icons.json` contain the unique PNG paths in
`ShipyardModules.cs` and `SignalCatalog.cs`. The original paths are the lookup
keys. Images keep their original dimensions, transparent margins, and orientation.
Two pixels of edge extrusion protect linear-filtered signal icons from neighbours.
The atlas is built without mipmaps by the normal content pipeline, which also
premultiplies alpha.

After changing either catalog's icon paths or the source artwork:

```sh
python3 -m pip install Pillow  # once, in your preferred Python environment
python3 tools/build_icon_atlas.py
python3 tools/build_icon_atlas.py --check
```

Commit both generated files with the change. Normal game builds use these files
and do not require Python or an installed atlas editor. `--check` fails if either
file differs from regenerated output. The packer fails above 4096px rather than
silently generating textures beyond that limit.

Runtime loading starts one atlas upload after gameplay preloading. Module and
signal source rectangles share that texture. Visible module slot/inventory icons
use one point-filtered pass; visible signal collection/choice icons use one
linear-filtered pass. Tooltips render afterward. A dragged module uses an
additional pass after its ghost panel so it stays above the inventory.

Manual checks: open a full module inventory, equip/drag/remove modules, scroll,
hover tooltips, and reveal a module. Open signal collection pages and all three
animated choices. Check icon proportions, transparent edges, tint/drag opacity,
and that tooltips and drag ghosts cover icons beneath them.

GPU regression check (after building the game and test project):

```sh
dotnet build --no-restore
dotnet build Tests/PersistenceChecks.csproj --no-restore
SDL_VIDEODRIVER=offscreen ALSOFT_DRIVERS=null dotnet Tests/bin/Debug/net10.0/PersistenceChecks.dll --icon-atlas-check .
```

This compares all packed icons to their source PNGs at fractional scaling and
full/quarter opacity with both samplers, verifies catalog coverage, and asserts
one GPU draw per pass and no queued draws leaking into the next pass.

The fleet has a separate [atlas and rebuild workflow](README-fleet.md). Both
scripts share `atlas_packer.py`; the icon atlas output remains unchanged.
