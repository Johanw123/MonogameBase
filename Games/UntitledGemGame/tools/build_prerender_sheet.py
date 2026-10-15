"""Pack a prerendered turntable (frame_000.png ...) into one sprite sheet.

Usage:
    python3 tools/build_prerender_sheet.py RawAssets/Prerender/harvester_bomber01 \
        [--columns N] [--out Content/Textures/Prerender/Turntables/harvester_bomber01.png]

Reads <dir>/frames/frame_NNN.png and <dir>/meta.json (written by
tools/blender/prerender.py) and writes a sheet plus a .json next to it. By
default the sheet is a single horizontal strip, which is what
tools/build_fleet_atlas.py splits for `/Turntables/` paths.
Frame i is the ship turned i * 360 / frames degrees clockwise from nose-up, the
same convention as MonoGame Transform2.Rotation. Requires Pillow.
"""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageFilter


def outlined(image, width):
    """Dark rim behind the sprite so it separates from whatever it sits on."""
    if width <= 0:
        return image
    alpha = image.getchannel('A').filter(ImageFilter.MaxFilter(2 * width + 1))
    rim = Image.new('RGBA', image.size, (18, 12, 28, 0))
    rim.putalpha(alpha.point(lambda a: int(a * 0.85)))
    rim.alpha_composite(image)
    return rim


def build(directory, columns, out, outline=0, base_out=None):
    meta = json.loads((directory / 'meta.json').read_text())
    frames = sorted((directory / 'frames').glob('frame_*.png'))
    if len(frames) != meta['frames']:
        raise SystemExit(f"expected {meta['frames']} frames, found {len(frames)}")
    columns = columns or len(frames)
    size = meta['size']
    rows = -(-len(frames) // columns)
    sheet = Image.new('RGBA', (columns * size, rows * size), (0, 0, 0, 0))
    for i, path in enumerate(frames):
        with Image.open(path) as frame:
            if frame.size != (size, size):
                raise SystemExit(f'{path} is {frame.size}, expected {size}x{size}')
            sheet.paste(outlined(frame.convert('RGBA'), outline), ((i % columns) * size, (i // columns) * size))
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    info = dict(frames=len(frames), frame_size=size, columns=columns,
                degrees_per_frame=360.0 / len(frames), clockwise=True,
                pivot=[size / 2, size / 2], source=meta)
    out.with_suffix('.json').write_text(json.dumps(info, indent=2) + '\n')
    # Turrets (render_turret) also have a static base image on the same canvas.
    if base_out:
        with Image.open(directory / 'base.png') as base:
            base_out.parent.mkdir(parents=True, exist_ok=True)
            outlined(base.convert('RGBA'), outline).save(base_out)
    return out, sheet.size


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--columns', type=int, default=0, help='0 = one horizontal strip')
    parser.add_argument('--out', type=Path, help='default: <directory>/sheet.png')
    parser.add_argument('--outline', type=int, default=0, help='dark rim width in pixels (0 = none)')
    parser.add_argument('--base-out', type=Path, help='also write <directory>/base.png here (turrets)')
    args = parser.parse_args()
    print('wrote', *build(args.directory, args.columns, args.out or args.directory / 'sheet.png',
                          args.outline, args.base_out))
