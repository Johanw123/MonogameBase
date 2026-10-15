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

from PIL import Image


def build(directory, columns, out):
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
            sheet.paste(frame.convert('RGBA'), ((i % columns) * size, (i // columns) * size))
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    info = dict(frames=len(frames), frame_size=size, columns=columns,
                degrees_per_frame=360.0 / len(frames), clockwise=True,
                pivot=[size / 2, size / 2], source=meta)
    out.with_suffix('.json').write_text(json.dumps(info, indent=2) + '\n')
    return out, sheet.size


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--columns', type=int, default=0, help='0 = one horizontal strip')
    parser.add_argument('--out', type=Path, help='default: <directory>/sheet.png')
    args = parser.parse_args()
    print('wrote', *build(args.directory, args.columns, args.out or args.directory / 'sheet.png'))
