"""Pack the crystal-ore pickup renders into the gem shader's surface texture.

Usage:
    python3 tools/build_gem_surface.py RawAssets/Prerender/plasma_orb \
        [--out Content/Textures/Gems/plasma_orb.png] [--preview preview.png]

Reads v0..v3/{shade,mask}.png (or a single shade.png/mask.png) written by
render_plasma_orb (tools/blender/prerender.py) and writes the channels
GemShader.fx reads, colour = tint * R + G:
  R  how much of the gem type's tint shows: the crystal shards, outer glow halo,
     and subtle bounce light on the dark planetary rock base,
  G  untinted light: hot inner crystal highlights and dark rock shading,
  B  the two-texel inner outline on the solid silhouette for outlined gems,
  A  coverage (solid silhouette plus soft outer glow aura).
The PNG holds straight alpha; the content pipeline premultiplies it, which is
what the shader expects (the same layout GemSurfaceTexture.Bake produces).
--preview draws each variant in every gem colour (GemQualityTable.GetColor) on
the game's background colour. Requires Pillow and NumPy.
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

CANVAS = 64                 # square power-of-two tile canvas, so the game can mipmap it on any GPU
CORE_SHADE = (0.60, 1.0)    # tint weight on the crystal's darkest and brightest facets
CORE_HIGHLIGHT = 0.58       # white added to the brightest crystal facets

# GemQualityTable.GetColor
GEM_COLORS = [(255, 0, 20), (20, 150, 45), (45, 70, 255), (0, 145, 125),
              (180, 80, 255), (110, 20, 220), (205, 115, 0), (15, 20, 130)]


def neighbour_min(alpha, distance):
    """Smallest alpha `distance` texels left, right, up or down (edges clamped)."""
    padded = np.pad(alpha, distance, mode='edge')
    h, w = alpha.shape
    d = distance
    return np.minimum.reduce([padded[d:d + h, 0:w], padded[d:d + h, 2 * d:2 * d + w],
                              padded[0:h, d:d + w], padded[2 * d:2 * d + h, d:d + w]])


def build_tile(directory):
    shade = np.asarray(Image.open(directory / 'shade.png').convert('RGBA'), dtype=np.float64) / 255
    mask = np.asarray(Image.open(directory / 'mask.png').convert('RGBA'), dtype=np.float64) / 255
    if shade.shape != mask.shape:
        raise SystemExit('shade.png and mask.png must be the same size')
    h, w = shade.shape[:2]
    if max(h, w) > CANVAS - 10:
        raise SystemExit(f'render is {w}x{h}; keep it within {CANVAS - 10} px so its glow fits inside the tile')
    top, left = (CANVAS - h) // 2, (CANVAS - w) // 2
    pad = ((top, CANVAS - h - top), (left, CANVAS - w - left), (0, 0))
    shade, mask = np.pad(shade, pad), np.pad(mask, pad)
    body_alpha = shade[:, :, 3]
    lum = shade[:, :, :3] @ [0.2126, 0.7152, 0.0722]
    core = mask[:, :, 0]

    solid_core = (core > 0.85) & (body_alpha > 0.85)
    lo, hi = np.percentile(lum[solid_core], [3, 97])
    t = np.clip((lum - lo) / max(hi - lo, 1e-6), 0, 1)
    core_weight = CORE_SHADE[0] + (CORE_SHADE[1] - CORE_SHADE[0]) * (t ** 0.85)
    core_light = 0.12 * t + CORE_HIGHLIGHT * (np.clip((t - 0.50) / 0.50, 0, 1) ** 1.3)

    # Dark planetary crust rock base with subtle gem-tinted bounce light.
    solid_rock = (core < 0.15) & (body_alpha > 0.85)
    if np.any(solid_rock):
        rlo, rhi = np.percentile(lum[solid_rock], [5, 95])
        rt = np.clip((lum - rlo) / max(rhi - rlo, 1e-6), 0, 1)
    else:
        rt = np.clip(lum * 2.0, 0, 1)
    rock_r = 0.24 * (0.25 + 0.75 * rt)
    rock_g = 0.15 * (0.20 + 0.80 * rt)

    # Soft outer glow aura around the crystal shards, fading to zero 3 texels inside
    # the tile border so adjacent atlas tiles and mipmaps never bleed.
    crystal_emit = np.clip(core * body_alpha * (0.65 + 0.35 * t), 0, 1)
    emit_img = Image.fromarray(np.round(crystal_emit * 255).astype(np.uint8), 'L')
    glow_near = np.asarray(emit_img.filter(ImageFilter.GaussianBlur(radius=2.5)), dtype=np.float64) / 255
    glow_far = np.asarray(emit_img.filter(ImageFilter.GaussianBlur(radius=5.0)), dtype=np.float64) / 255
    yy, xx = np.mgrid[0:CANVAS, 0:CANVAS]
    edge_dist = np.minimum(np.minimum(xx, CANVAS - 1 - xx), np.minimum(yy, CANVAS - 1 - yy))
    edge_fade = np.clip((edge_dist - 3.0) / 4.0, 0, 1)
    glow_alpha = np.clip((0.50 * glow_near + 0.38 * glow_far) * edge_fade, 0, 0.65)
    total_alpha = np.clip(body_alpha + glow_alpha * (1.0 - body_alpha), 0, 1)

    # One-texel subtle contour and two-texel inner outline on the solid body silhouette.
    near = neighbour_min(body_alpha, 1)
    contour = 1.0 - (1.0 - near) * 0.35
    interior = np.minimum(near, neighbour_min(body_alpha, 2))

    body_r = (core * core_weight + (1.0 - core) * rock_r) * contour
    body_g = (core * core_light + (1.0 - core) * rock_g) * contour
    halo_r = np.ones_like(body_r)
    halo_g = 0.34 * np.clip(glow_near / np.maximum(glow_alpha, 1e-6), 0, 1)

    w_body = body_alpha / np.maximum(total_alpha, 1e-6)
    r = np.clip(w_body * body_r + (1.0 - w_body) * halo_r, 0, 1)
    g = np.clip(w_body * body_g + (1.0 - w_body) * halo_g, 0, 1)
    b = np.clip((1.0 - interior) * (body_alpha > 0.5), 0, 1)
    surface = np.stack([r, g, b, total_alpha], axis=-1)
    surface[total_alpha < 1.0 / 255.0] = 0
    return Image.fromarray(np.round(surface * 255).astype(np.uint8), 'RGBA')


def build(directory):
    variant_dirs = [directory / f'v{i}' for i in range(4)]
    if all((d / 'shade.png').exists() for d in variant_dirs):
        sheet = Image.new('RGBA', (CANVAS * 2, CANVAS * 2), (0, 0, 0, 0))
        for i, d in enumerate(variant_dirs):
            sheet.paste(build_tile(d), ((i % 2) * CANVAS, (i // 2) * CANVAS))
        return sheet
    return build_tile(directory)


def preview(surface, scale=3):
    s = np.asarray(surface, dtype=np.float64) / 255
    sh, sw = s.shape[:2]
    cols, rows = max(1, sw // CANVAS), max(1, sh // CANVAS)
    tiles = [s[r * CANVAS:(r + 1) * CANVAS, c * CANVAS:(c + 1) * CANVAS]
             for r in range(rows) for c in range(cols)]
    out = Image.new('RGB', (len(GEM_COLORS) * (CANVAS + 4) * scale, len(tiles) * (CANVAS + 4) * scale), (14, 10, 22))
    bg = np.asarray((14, 10, 22)) / 255
    for row_idx, tile_s in enumerate(tiles):
        for col_idx, tint in enumerate(GEM_COLORS):
            colour = np.asarray(tint) / 255 * tile_s[:, :, :1] + tile_s[:, :, 1:2]
            a = tile_s[:, :, 3:4]
            rgb = np.clip(colour * a + bg * (1 - a), 0, 1)
            tile = Image.fromarray(np.round(rgb * 255).astype(np.uint8), 'RGB').resize(
                (CANVAS * scale, CANVAS * scale), Image.LANCZOS)
            out.paste(tile, (col_idx * (CANVAS + 4) * scale + 2 * scale,
                             row_idx * (CANVAS + 4) * scale + 2 * scale))
    return out


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--out', type=Path, default=Path('Content/Textures/Gems/plasma_orb.png'))
    parser.add_argument('--preview', type=Path)
    args = parser.parse_args()
    surface = build(args.directory)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    surface.save(args.out)
    print('wrote', args.out, surface.size)
    if args.preview:
        preview(surface).save(args.preview)
        print('wrote', args.preview)

