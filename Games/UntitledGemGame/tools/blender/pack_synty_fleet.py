"""Pack Synty's fleet, upright planet and ore into the runtime atlas layout."""
from pathlib import Path
import argparse
from PIL import Image, ImageDraw, ImageOps
import numpy as np
import OpenEXR

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Content/Textures/Isometric'
KINDS = ('basic', 'advanced', 'perimeter', 'expert', 'ultimate', 'drone', 'home')


def frame(folder, name, size):
    with Image.open(folder / name) as source:
        image = source.convert('RGBA')
    assert min(image.size) >= size, (name, 'resolution')
    assert image.getextrema()[3] == (0, 255), (name, 'transparency')
    bounds = image.getbbox()
    assert bounds and min(bounds[:2]) > 0 and bounds[2] < image.width and bounds[3] < image.height, (name, 'clipping')
    return image.resize((size, size), Image.Resampling.LANCZOS)


def emission_frame(folder, name, size):
    # The compositor exports linear premultiplied emission and the same coverage
    # as beauty. Convert to straight-alpha sRGB, matching the homebase's Standard
    # emission pass; the content processor premultiplies it exactly once.
    channels = OpenEXR.File(str(folder / name)).channels()
    pixels = channels['emission'].pixels.astype(np.float32)
    alpha = np.clip(pixels[:, :, 3:4], 0, 1)
    rgb = np.clip(pixels[:, :, :3] / np.maximum(alpha, 1e-6), 0, 1)
    rgb = np.where(rgb <= .0031308, rgb * 12.92, 1.055 * np.power(rgb, 1/2.4) - .055)
    rgba = np.concatenate((rgb, alpha), axis=2)
    image = Image.fromarray(np.uint8(np.round(rgba * 255)), 'RGBA')
    assert image.getextrema()[3] == (0, 255), (name, 'emission coverage')
    return image.resize((size, size), Image.Resampling.LANCZOS)


def pack(folder):
    OUTPUT.mkdir(exist_ok=True)
    for kind in KINDS:
        if kind == 'home': continue  # Main ship has separate 512px beauty/emission pages.
        banks = [2] if kind == 'home' else range(5)
        for pose, bank in enumerate(banks):
            for start in (0, 64):
                atlas = Image.new('RGBA', (2048, 2048))
                glow = Image.new('RGBA', (1024, 1024))
                for local in range(64):
                    image = frame(folder, f'{kind}-bank{bank}_{start + local:02}.png', 256)
                    atlas.paste(image, ((local % 8) * 256, (local // 8) * 256))
                    light = emission_frame(folder, f'{kind}-bank{bank}_{start + local:02}-emission.exr', 128)
                    glow.paste(light, ((local % 8) * 128, (local // 8) * 128))
                page = pose * 2 + start // 64
                atlas.save(OUTPUT / f'ship-{kind}-{page:02}.png')
                glow.save(OUTPUT / f'ship-{kind}-emission-{page:02}.png')
        print('Packed', kind, flush=True)
    for start in range(0, 120, 16):
        count = min(16, 120 - start)
        atlas = Image.new('RGBA', (2048, ((count + 3) // 4) * 512))
        glow = Image.new('RGBA', (1024, ((count + 3) // 4) * 256))
        for local in range(count):
            image = frame(folder, f'planet_{start + local:02}.png', 512)
            atlas.paste(image, ((local % 4) * 512, (local // 4) * 512))
            light = emission_frame(folder, f'planet_{start + local:02}-emission.exr', 256)
            glow.paste(light, ((local % 4) * 256, (local // 4) * 256))
        atlas.save(OUTPUT / f'planet-{start // 16:02}.png')
        glow.save(OUTPUT / f'planet-emission-{start // 16:02}.png')
    gems = Image.new('RGBA', (512, 64))
    for index in range(8):
        image = frame(folder, f'gem_{index:02}.png', 64)
        alpha = image.getchannel('A')
        image = ImageOps.grayscale(image).convert('RGBA')
        image.putalpha(alpha)
        gems.paste(image, (index * 64, 0))
    gems.save(OUTPUT / 'gem.png')
    # These pages belong to the superseded single-model prototype.
    for old in OUTPUT.glob('ship-[0-9][0-9].png'):
        old.unlink()
    pack_main(Path('/tmp/btb-main-ship-frames'))
    preview(folder)
    print('All fleet, planet, ore and main ship frames passed the packing checks.', flush=True)


def pack_main(folder):
    for kind, prefix in [('home', 'ship-home'), ('home-emission', 'ship-home-emission')]:
        for start in range(0, 128, 16):
            atlas = Image.new('RGBA', (2048, 2048))
            for local in range(16):
                image = frame(folder, f'{kind}_{start + local:02}.png', 512)
                atlas.paste(image, ((local % 4) * 512, (local // 4) * 512))
            atlas.save(OUTPUT / f'{prefix}-{start // 16:02}.png')
    print('Packed 128 main ship beauty and emission frames at 512px.', flush=True)


def preview(folder):
    sheet = Image.new('RGB', (1200, 760), (14, 19, 30))
    draw = ImageDraw.Draw(sheet)
    for index, kind in enumerate(KINDS):
        x, y = (index % 4) * 300, (index // 4) * 280
        image = (frame(Path('/tmp/btb-main-ship-frames'), 'home_48.png', 240) if kind == 'home'
                 else frame(folder, f'{kind}-bank2_16.png', 240))
        sheet.paste(image, (x + 30, y), image)
        draw.text((x + 30, y + 245), kind, fill=(225, 232, 244))
    planet = frame(folder, 'planet_00.png', 240)
    sheet.paste(planet, (930, 280), planet)
    draw.text((930, 525), 'Upright purple planet', fill=(225, 232, 244))
    gems = Image.open(OUTPUT / 'gem.png').convert('RGBA')
    sheet.paste(gems, (30, 610), gems)
    draw.text((30, 700), 'Synty ore fragments / 8 orientations; gameplay colors applied at runtime', fill=(180, 195, 218))
    sheet.save(ROOT / 'art-tests/isometric/fleet-preview.png')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--frames', type=Path, default=Path('/tmp/btb-unified-style-frames'))
    parser.add_argument('--main-only', action='store_true')
    args = parser.parse_args()
    if args.main_only: pack_main(args.frames)
    else: pack(args.frames)
