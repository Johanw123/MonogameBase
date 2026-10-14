# Ship lighting and material trials

All stills use the current 50 degree orthographic camera and the same model
size, in two headings. `comparison.png` also includes native 64px samples.

- Current: existing EEVEE export materials and lighting.
- Industrial: bronze paint, cooler metal, darker glass, stronger light/shadow
  separation, fine 18mm bevels and small cyan navigation lights.
- Worn: darker muted paint/metal, rougher surfaces and shallow procedural bump.
- Stylized: warmer orange paint, blue-gray panels and brighter cockpit color.

The three new looks use Cycles with 32 samples and denoising, allowing contact
shadows and indirect light. Bevels, material copies and accents belong to the
temporary export scene; the original ship and its materials are preserved.

Run `tools/blender/ship_look_tests.py` in Blender to recreate the individual
1024x1024 transparent stills. These trials are not loaded by the game and do
not directly loaded as sprite sheets. The stylized preset was selected for
gameplay and is exported through `tools/blender/export_stylized_ship.py`.
Added 2026-10-14.
