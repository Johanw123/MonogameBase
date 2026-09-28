"""Build the fleet atlas from FleetAtlas.cs. Requires Python 3 and Pillow."""
import argparse
import io
import json
from pathlib import Path
import re
from PIL import Image
from atlas_packer import pack

ROOT = Path(__file__).resolve().parents[1]


def build():
    catalog = (ROOT / 'FleetAtlas.cs').read_text()
    paths = sorted(set(re.findall(r'public const string \w+ = "(Textures/[^"\n]+\.png)"', catalog)))
    count = int(re.search(r'EngineFrameCount = (\d+)', catalog)[1])
    images = {}
    for path in paths:
        with Image.open(ROOT / 'Content' / path) as source:
            image = source.convert('RGBA')
        if '/Engine Effects/' in path:
            if image.width % count:
                raise ValueError(f'Engine strip width must divide evenly into {count} frames: {path}')
            width = image.width // count
            for frame in range(count):
                images[f'{path}#{frame}'] = image.crop((frame * width, 0, (frame + 1) * width, image.height))
        else:
            images[path] = image
    # Shader samples up to 3.5 texels away. Four extruded pixels keep those
    # samples inside the same image, including at atlas and frame boundaries.
    atlas, frames = pack(images, padding=4)
    output = io.BytesIO()
    atlas.save(output, format='PNG')
    metadata = json.dumps(dict(sorted(frames.items())), indent=2) + '\n'
    return {'fleet.png': output.getvalue(), 'fleet.json': metadata.encode()}, len(images), atlas.size


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Fail if committed output needs rebuilding')
    args = parser.parse_args()
    outputs, count, size = build()
    directory = ROOT / 'Content' / 'Atlases'
    directory.mkdir(parents=True, exist_ok=True)
    for name, data in outputs.items():
        target = directory / name
        if args.check:
            if not target.exists() or target.read_bytes() != data:
                raise SystemExit(f'{target} is stale; run python3 tools/build_fleet_atlas.py')
        elif not target.exists() or target.read_bytes() != data:
            target.write_bytes(data)
    print(f'{count} hulls/engine frames packed into {size[0]}x{size[1]} atlas')
