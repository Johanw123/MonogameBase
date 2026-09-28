"""Build the module/signal atlas. Requires Python 3 and Pillow (pip install Pillow)."""
import argparse
import io
import json
from pathlib import Path
import re
from PIL import Image
from atlas_packer import pack

ROOT = Path(__file__).resolve().parents[1]
PADDING = 2


def build():
    paths = sorted(set(path for catalog in ('ShipyardModules.cs', 'SignalCatalog.cs')
                       for path in re.findall(r'"(Textures/[^"\n]+\.png)"',
                                              (ROOT / catalog).read_text())))
    images = {path: Image.open(ROOT / 'Content' / path).convert('RGBA') for path in paths}
    atlas, frames = pack(images, PADDING)
    output = io.BytesIO()
    atlas.save(output, format='PNG')
    metadata = json.dumps(dict(sorted(frames.items())), indent=2) + '\n'
    return {'icons.png': output.getvalue(), 'icons.json': metadata.encode()}, len(paths), atlas.size


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
                raise SystemExit(f'{target} is stale; run python3 tools/build_icon_atlas.py')
        elif not target.exists() or target.read_bytes() != data:
            target.write_bytes(data)
    print(f'{count} unique icons packed into {size[0]}x{size[1]} atlas')
