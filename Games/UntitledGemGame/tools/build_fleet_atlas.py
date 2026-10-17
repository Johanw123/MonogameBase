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
    turntable_count = int(re.search(r'TurntableFrameCount = (\d+)', catalog)[1])
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
        elif '/Turntables/' in path:
            # Prerendered headings (tools/blender/prerender.py), one square frame
            # per heading in a horizontal strip; frame i = i * 360 / count clockwise.
            size = image.height
            if image.width != size * turntable_count:
                raise ValueError(f'Turntable strip must hold {turntable_count} square frames: {path}')
            for frame in range(turntable_count):
                images[f'{path}#{frame}'] = image.crop((frame * size, 0, (frame + 1) * size, size))
        else:
            images[path] = image
    # Prerendered images (tools/blender/prerender.py) have big transparent margins,
    # since every turntable frame shares a canvas centred on the model's pivot. Trim
    # them and record where that pivot lands so the game can draw around it.
    pivots = {}
    for path, image in list(images.items()):
        if not path.startswith('Textures/Prerender/'):
            continue
        box = image.getchannel('A').getbbox() or (0, 0, 1, 1)
        # Fuel outlines need room inside the sprite quad. Keep the hull's pivot
        # unchanged while retaining a transparent margin for the soft halo.
        margin = 14 if '/Turntables/harvester_' in path else 1
        if margin > 1:
            # crop pads outside the source with transparency when a hull reaches
            # the original canvas edge; don't clip away the outline's margin.
            box = (box[0] - margin, box[1] - margin, box[2] + margin, box[3] + margin)
        else:
            box = (max(0, box[0] - margin), max(0, box[1] - margin), min(image.width, box[2] + margin), min(image.height, box[3] + margin))
        images[path] = image.crop(box)
        pivots[path] = [image.width / 2 - box[0], image.height / 2 - box[1]]
    # The soft outline/burst samples up to 13.44 texels away. Keep neighboring
    # frames isolated even when sampling from the transparent edge of a quad.
    atlas, frames = pack(images, padding=14)
    for path, pivot in pivots.items():
        frames[path] = frames[path] + pivot
    for path in paths:
        if not path.startswith('Textures/Prerender/'):
            continue
        sidecar = (ROOT / 'Content' / path).with_suffix('.json')
        if not sidecar.exists():
            sidecar = ROOT / 'RawAssets' / 'Prerender' / Path(path).stem / 'meta.json'
        if not sidecar.exists():
            continue
        meta = json.loads(sidecar.read_text())
        nozzles = meta.get('nozzles') or meta.get('source', {}).get('nozzles')
        if nozzles:
            key = f'{path}#0' if '/Turntables/' in path else path
            for n in nozzles:
                frames[key].extend([n['x'], n['y'], n['z'], n['width']])
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
