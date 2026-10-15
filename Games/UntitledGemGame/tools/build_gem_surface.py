"""Pack the plasma orb render into the gem shader's surface texture.

Usage:
    python3 tools/build_gem_surface.py RawAssets/Prerender/plasma_orb \
        [--out Content/Textures/Gems/plasma_orb.png] [--preview preview.png]

Reads shade.png and mask.png written by render_plasma_orb (tools/blender/prerender.py)
and writes the channels GemShader.fx reads, colour = tint * R + G:
  R  how much of the gem type's tint shows: the core, shaded by its lighting,
  G  untinted light: the grey ring and highlights on the core,
  B  the two-texel inner outline used for outlined gems,
  A  coverage.
The PNG holds straight alpha; the content pipeline premultiplies it, which is
what the shader expects (the same layout GemSurfaceTexture.Bake produces).
--preview draws the orb in every gem colour (GemQualityTable.GetColor) on the
game's background colour. Requires Pillow and NumPy.
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image

CANVAS = 64                 # square power-of-two canvas, so the game can mipmap it on any GPU
CORE_SHADE = (0.7, 1.0)     # tint weight on the core's darkest and brightest facets
CORE_HIGHLIGHT = 0.25       # white added to the brightest facets
RING_GAIN = 0.8             # ring brightness

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


def build(directory):
    shade = np.asarray(Image.open(directory / 'shade.png').convert('RGBA'), dtype=np.float64) / 255
    mask = np.asarray(Image.open(directory / 'mask.png').convert('RGBA'), dtype=np.float64) / 255
    if shade.shape != mask.shape:
        raise SystemExit('shade.png and mask.png must be the same size')
    h, w = shade.shape[:2]
    if max(h, w) > CANVAS - 4:
        raise SystemExit(f'render is {w}x{h}; keep it within {CANVAS - 4} px so it has a transparent border')
    top, left = (CANVAS - h) // 2, (CANVAS - w) // 2
    pad = ((top, CANVAS - h - top), (left, CANVAS - w - left), (0, 0))
    shade, mask = np.pad(shade, pad), np.pad(mask, pad)
    alpha = shade[:, :, 3]
    lum = shade[:, :, :3] @ [0.2126, 0.7152, 0.0722]
    core = mask[:, :, 0]

    solid_core = (core > 0.9) & (alpha > 0.9)
    lo, hi = np.percentile(lum[solid_core], [2, 98])
    t = np.clip((lum - lo) / max(hi - lo, 1e-6), 0, 1)
    core_weight = CORE_SHADE[0] + (CORE_SHADE[1] - CORE_SHADE[0]) * t
    core_light = CORE_HIGHLIGHT * np.clip((t - 0.7) / 0.3, 0, 1)

    # One-texel dark contour and two-texel inner outline, as in GemSurfaceTexture.Bake.
    near = neighbour_min(alpha, 1)
    contour = 1 - (1 - near) * 0.55
    interior = np.minimum(near, neighbour_min(alpha, 2))

    r = core * core_weight * contour
    g = (core * core_light + (1 - core) * np.clip(lum * RING_GAIN, 0, 1)) * contour
    b = 1 - interior
    surface = np.stack([r, g, b, alpha], axis=-1)
    surface[alpha <= 0] = 0
    return Image.fromarray(np.round(surface * 255).astype(np.uint8), 'RGBA')


def preview(surface, scale=3):
    s = np.asarray(surface, dtype=np.float64) / 255
    h, w = s.shape[:2]
    out = Image.new('RGB', (len(GEM_COLORS) * (w + 4) * scale, (h + 4) * scale), (14, 10, 22))
    for i, tint in enumerate(GEM_COLORS):
        colour = np.asarray(tint) / 255 * s[:, :, :1] + s[:, :, 1:2]
        a = s[:, :, 3:4]
        bg = np.asarray((14, 10, 22)) / 255
        rgb = np.clip(colour * a + bg * (1 - a), 0, 1)
        tile = Image.fromarray(np.round(rgb * 255).astype(np.uint8), 'RGB').resize((w * scale, h * scale), Image.LANCZOS)
        out.paste(tile, (i * (w + 4) * scale + 2 * scale, 2 * scale))
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
