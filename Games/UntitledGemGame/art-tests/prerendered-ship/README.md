# Ship prerender test

Rendered from the user's existing `craft_miner` in `/home/johan/test.blend`.
The model and its materials were reused without modification.

- `craft_miner_top_{64,128,512}.png`: orthographic top-down view, nose up.
- `craft_miner_tilted_{64,128,512}.png`: orthographic view tilted about 18 degrees, nose up.
- `craft_miner_isometric_{64,128,512}.png`: true isometric orthographic view,
  camera at equal X, negative Y, and Z offsets (45 degree azimuth and
  35.264 degree elevation), with world Z pointing up on screen.
- All ship PNGs have transparent backgrounds. 64 and 128 pixel versions are
  Lanczos reductions of the 512 pixel renders, not pixel-art conversions.
- `preview.png`: both views on a dark background; the bottom row shows native
  64 and 128 pixel sizes. The current scout hull texture is 64 by 64 pixels.
- `preview_isometric.png`: enlarged isometric render and native 128 and
  64 pixel examples on a dark background.

Rerender the 512 pixel images by running `tools/blender/render_ship_test.py`
in Blender with `craft_miner` present. The script creates and removes an
isolated scene with an orthographic camera and three area lights. It uses the
open scene's render engine and view transform. This test used EEVEE.

These are standalone art experiments; the game does not load them.
Rendered 2026-10-14.
