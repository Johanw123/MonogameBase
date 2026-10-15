"""Pack Blender's /tmp/btb-isometric-frames into game sprite atlases."""
from pathlib import Path
import sys
from PIL import Image, ImageDraw, ImageOps

ROOT = Path(__file__).resolve().parents[2]
FRAMES = Path('/tmp/btb-isometric-frames')
if '--frames' in sys.argv:
    FRAMES = Path(sys.argv[sys.argv.index('--frames') + 1])
OUTPUT = ROOT / 'Content/Textures/Isometric'
OUTPUT.mkdir(parents=True, exist_ok=True)

def frame_image(kind, index, size):
    image = Image.open(FRAMES / f'{kind}_{index:02}.png').convert('RGBA')
    assert image.width >= size and image.height >= size, (kind, index, 'source too small')
    assert image.getextrema()[3] == (0, 255), (kind, index, 'missing transparency')
    bounds = image.getbbox()
    assert bounds[0] > 0 and bounds[1] > 0 and bounds[2] < image.width and bounds[3] < image.height, (kind, index, 'clipped')
    image = image.resize((size, size), Image.Resampling.LANCZOS)
    if kind == 'gem':
        alpha = image.getchannel('A')
        image = ImageOps.grayscale(image).convert('RGBA')
        image.putalpha(alpha)
    return image

ship_only = '--ship-only' in sys.argv
sources = [('ship', 128, bank) for bank in range(5)]
if not ship_only: sources.append(('planet', 120, None))
for kind, count, bank in sources:
    size, columns, per_page = 512, 4, 16
    for start in range(0, count, per_page):
        frames = min(per_page, count - start)
        atlas = Image.new('RGBA', (columns * size, ((frames + columns - 1) // columns) * size))
        for local in range(frames):
            source = f'ship-bank{bank}' if kind == 'ship' else kind
            image = frame_image(source, start + local, size)
            atlas.paste(image, ((local % columns) * size, (local // columns) * size))
        page = (bank * 8 if kind == 'ship' else 0) + start // per_page
        path = OUTPUT / f'{kind}-{page:02}.png'
        atlas.save(path)
        print(path.name, atlas.size)

for kind, count, size, columns in ([] if ship_only or '--keep-gems' in sys.argv else [('gem', 8, 64, 8)]):
    atlas = Image.new('RGBA', (columns * size, size))
    for index in range(count):
        image = frame_image(kind, index, size)
        atlas.paste(image, ((index % columns) * size, (index // columns) * size))
    atlas.save(OUTPUT / (kind + '.png'))
    print(kind, atlas.size)

# Replaced by the paged high-resolution sheets.
for name in ('ship.png', 'planet.png'):
    (OUTPUT / name).unlink(missing_ok=True)

preview = Image.new('RGB', (1000, 720), (14, 19, 30))
draw = ImageDraw.Draw(preview)
draw.text((24,20), 'ISOMETRIC GAME ASSETS / 128 SHIP HEADINGS (16 SAMPLED BELOW)', fill=(225,232,244))
for sample in range(16):
    frame = sample * 8
    image = frame_image('ship-bank2', frame, 128)
    preview.paste(image, (16 + (sample % 4) * 128, 55 + (sample // 4) * 128), image)
planet = Image.open(OUTPUT / 'planet-00.png').convert('RGBA').crop((0,0,512,512)).resize((256,256), Image.Resampling.LANCZOS)
preview.paste(planet, (660,75), planet)
draw.text((660,355), 'Rotating planet / 120 frames', fill=(180,195,218))
gems = Image.open(OUTPUT / 'gem.png')
preview.paste(gems, (24,620), gems)
draw.text((24,590), 'Faceted crystals / 8 orientations', fill=(180,195,218))
preview.save(ROOT / 'art-tests/isometric/assets-preview.png')
