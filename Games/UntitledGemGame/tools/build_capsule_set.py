"""Builds a capsule-sized copy of RawAssets/Steam/parts: every part resampled once, with
a proper filter, to the size it would have in a 1232x706 Steam capsule, so nothing has
to be shrunk 4-6x in the paint program (which is what makes edges jagged and grainy).

    python3 tools/build_capsule_set.py RawAssets/Steam/parts RawAssets/Steam/parts/capsule_1232x706 [ship_px]

ship_px is the long side of the home base's canvas (default 912: the hull is then about
850 px long, as in main_capsule.kra). Everything else scales with it.

Adds two things the 4K folder does not have:
  ship/<view>/engine_flames.png  the engine exhaust on the ship's own canvas, laid out
      from the game's nozzle positions (ship/<view>/nozzles.json, written by the
      "nozzles" stage of tools/blender/capsule_parts.py) and its flame sprites, turned
      to the view's angle. Every layer of a view is padded by the same amount so the
      flames fit; all of them still share one canvas.
  effects/<weapon>/  beams, bolts and flashes from tools/build_capsule_fx.py.
"""
import json
import math
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None
PARTS, OUT = Path(sys.argv[1]), Path(sys.argv[2])
SHIP_PX = float(sys.argv[3]) if len(sys.argv) > 3 else 912.0
K = SHIP_PX / 4096.0          # 4K ship canvas -> capsule
REL = SHIP_PX / 912.0         # relative to the default capsule size
SIZES = []                    # (path, size, note) for the README


def clean(img):
    arr = np.asarray(img.convert('RGBA')).copy()
    arr[arr[:, :, 3] == 0] = 0
    return Image.fromarray(arr, 'RGBA')


def shrink(img, size):
    """Lanczos on premultiplied colour (Pillow does the premultiplying for RGBA)."""
    size = (max(1, int(round(size[0]))), max(1, int(round(size[1]))))
    return img.convert('RGBA').resize(size, Image.LANCZOS, reducing_gap=3.0)


def scaled(img, factor):
    return shrink(img, (img.width * factor, img.height * factor))


def save(img, rel_path, note=''):
    path = OUT / rel_path
    path.parent.mkdir(parents=True, exist_ok=True)
    img = clean(img)
    img.save(path, compress_level=6)
    SIZES.append((str(rel_path), img.size, note))
    return img


def trimmed(img, pad=2):
    box = img.getchannel('A').getbbox()
    if not box:
        return img
    return img.crop((max(0, box[0] - pad), max(0, box[1] - pad), min(img.width, box[2] + pad), min(img.height, box[3] + pad)))


# ---- Engine flames -------------------------------------------------------------------

FLAME_FRAME = 1024                   # parts/effects/engine_flame_strip_*.png, 8 frames
GAME_FLAME_FRAME = 384               # the game's strip; PrerenderedShips draws it at 0.25 texel per pixel


def flame_scales(width):
    """Texels per pixel of the game's 384 px flame frame, across and along, at full thrust
    (PrerenderedShips.DrawEngine)."""
    effective = width if width <= 8.0 else 8.0 + (width - 8.0) * 0.15
    main = width > 16.0
    across = (effective / 5.2) * 1.6 * (1.22 if main else 1.08) * 0.25
    along = (effective / 5.2) * 1.75 * (1.12 if main else 1.0) * 0.25
    return across, along


def flame_frames():
    envelope = Image.open(PARTS / 'effects' / 'engine_flame_strip_envelope.png').convert('RGBA')
    core = Image.open(PARTS / 'effects' / 'engine_flame_strip_core.png').convert('RGBA')
    n = envelope.width // FLAME_FRAME
    cut = lambda strip, i: strip.crop((i * FLAME_FRAME, 0, (i + 1) * FLAME_FRAME, FLAME_FRAME))
    return [(cut(envelope, i), cut(core, i)) for i in range(n)]


def tint(img, colour, strength=1.0):
    arr = np.asarray(img, dtype=np.float32) / 255.0
    arr[:, :, :3] *= np.asarray(colour, dtype=np.float32) / 255.0
    arr[:, :, 3] *= strength
    return Image.fromarray(np.round(arr * 255).astype(np.uint8), 'RGBA')


def place(canvas, sprite, nozzle, across_vec, along_vec):
    """Draws sprite (nozzle at its centre, exhaust toward +y) onto canvas with its x axis
    along across_vec and its y axis along along_vec (canvas pixels per sprite pixel)."""
    m = np.array([[across_vec[0], along_vec[0]], [across_vec[1], along_vec[1]]])
    inv = np.linalg.inv(m)
    c = FLAME_FRAME / 2.0
    corners = np.array([[-c, -c], [c, -c], [c, c], [-c, c]]) @ m.T + np.asarray(nozzle)
    x0, y0 = np.floor(corners.min(axis=0)).astype(int)
    x1, y1 = np.ceil(corners.max(axis=0)).astype(int)
    x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, canvas.width), min(y1, canvas.height)
    if x1 <= x0 or y1 <= y0:
        return
    # Output pixel (x, y) in the patch -> source pixel.
    off = inv @ (np.array([x0 + 0.5, y0 + 0.5]) - np.asarray(nozzle)) + c - 0.5
    coeffs = (inv[0, 0], inv[0, 1], off[0], inv[1, 0], inv[1, 1], off[1])
    patch = sprite.convert('RGBa').transform((x1 - x0, y1 - y0), Image.AFFINE, coeffs, resample=Image.BICUBIC)
    canvas.alpha_composite(patch.convert('RGBA'), (x0, y0))


def flame_layer(meta, size, offset, frames, scale):
    """The game's three passes per nozzle (glow halo, plume, white core), far nozzles first."""
    layer = Image.new('RGBA', size, (0, 0, 0, 0))
    aft = np.array(meta['aft'])
    perp = np.array([-aft[1], aft[0]]) / max(np.hypot(*aft), 1e-6)
    texel = meta['px_per_texel'] * scale
    order = sorted(range(len(meta['nozzles'])), key=lambda i: -meta['nozzles'][i]['depth'])
    for i in order:
        nozzle = meta['nozzles'][i]
        at = (nozzle['x'] * scale + offset[0], nozzle['y'] * scale + offset[1])
        across, along = flame_scales(nozzle['width'])
        per_px = GAME_FLAME_FRAME / FLAME_FRAME * texel
        envelope, core = frames[(i * 3) % len(frames)]
        for sprite, wide, long in ((tint(envelope, (72, 194, 255), 0.38), 1.45, 1.12),
                                   (tint(envelope, (217, 245, 255)), 1.06, 1.04),
                                   (core, 1.06, 1.04)):
            place(layer, sprite, at, perp * across * wide * per_px, aft * along * long * per_px)
    return layer


def build_ship(frames):
    for view in sorted(p for p in (PARTS / 'ship').iterdir() if p.is_dir()):
        layers = sorted(view.rglob('*.png'))
        first = Image.open(layers[0])
        w, h = int(round(first.width * K)), int(round(first.height * K))
        meta = json.loads((view / 'nozzles.json').read_text())
        # Flames on a generous canvas first, to see how far they reach past the ship's.
        big = max(w, h)
        probe = flame_layer(meta, (w + 2 * big, h + 2 * big), (big, big), frames, K)
        alpha = np.asarray(probe.getchannel('A'))
        ys, xs = np.nonzero(alpha > 3)
        pad_l = max(0, big - xs.min() + 4)
        pad_t = max(0, big - ys.min() + 4)
        pad_r = max(0, xs.max() + 1 - (big + w) + 4)
        pad_b = max(0, ys.max() + 1 - (big + h) + 4)
        size = (w + pad_l + pad_r, h + pad_t + pad_b)
        flames = probe.crop((big - pad_l, big - pad_t, big + w + pad_r, big + h + pad_b))
        hull = None
        for src in layers:
            small = shrink(Image.open(src), (w, h))
            padded = Image.new('RGBA', size, (0, 0, 0, 0))
            padded.paste(small, (pad_l, pad_t))
            save(padded, Path('ship') / src.relative_to(PARTS / 'ship'), 'same canvas as the other layers of this view')
            # Everything solid in this view, at either turret aim.
            alpha = np.asarray(padded.getchannel('A'))
            hull = alpha if hull is None else np.maximum(hull, alpha)
        if meta['aft_away_from_camera'] > 0.15:
            # The exhaust points away from the viewer: the ship hides what is behind it.
            arr = np.asarray(flames, dtype=np.float32)
            arr[:, :, 3] *= 1.0 - hull.astype(np.float32) / 255.0
            flames = Image.fromarray(np.round(arr).astype(np.uint8), 'RGBA')
            note = 'engine exhaust, already cut where the hull hides it; goes above or below the ship layer'
        else:
            note = 'engine exhaust; goes above the ship layer'
        save(flames, Path('ship') / view.name / 'engine_flames.png', note)
        print('ship', view.name, size, 'pad', (pad_l, pad_t, pad_r, pad_b))


# ---- Everything else -------------------------------------------------------------------

HARVESTER_PX = dict(drifter=111, seeker=111, prospector=118, trove_hunter=155, rimrunner=112, drone=59)


def build_weapons():
    for weapon in sorted(p for p in (PARTS / 'weapons').iterdir() if p.is_dir()):
        for src in sorted((weapon / 'top').glob('*.png')):
            save(scaled(Image.open(src), K), Path('weapons') / weapon.name / 'top' / src.name, 'fits ship/top 1:1')
        for src in sorted(weapon.glob('*_closeup_*.png')):
            save(scaled(Image.open(src), K * 0.82), Path('weapons') / weapon.name / src.name, 'about twice its size on the ship')
        print('weapons', weapon.name)


def build_harvesters():
    for ship in sorted(p for p in (PARTS / 'harvesters').iterdir() if p.is_dir()):
        length = HARVESTER_PX[ship.name]
        for src in sorted(ship.glob('*.png')):
            img = Image.open(src)
            for folder, px in (('harvesters', length * 1.95 * REL), ('harvesters_small', length * 1.0 * REL)):
                save(scaled(img, px / max(img.size)), Path(folder) / ship.name / src.name)
        print('harvesters', ship.name)


def build_pickups():
    for size in (64, 96, 128):
        s = size * REL
        for src in sorted((PARTS / 'pickups' / 'ore_glow').glob('*.png')):
            img = Image.open(src)
            factor = s / img.width
            save(scaled(img, factor), Path('pickups') / ('ore_glow_%d' % size) / src.name)
            plain = Image.open(PARTS / 'pickups' / 'ore_plain' / src.name)
            save(scaled(plain, factor), Path('pickups') / ('ore_plain_%d' % size) / src.name, 'same crystal, no glow')
    for name, sizes in (('core_shard', (128, 192)), ('prestige_orb', (96, 128))):
        img = Image.open(PARTS / 'pickups' / (name + '.png'))
        for size in sizes:
            save(shrink(img, (size * REL, size * REL)), Path('pickups') / ('%s_%d.png' % (name, size)))
    print('pickups')


def build_planet():
    for size in (250, 330, 450):
        for src in sorted((PARTS / 'planet').glob('*.png')):
            img = trimmed(Image.open(src), pad=0)
            save(trimmed(scaled(img, size * REL / img.width)), Path('planet') / ('planet_%d' % size) / src.name)
    print('planet')


def build_effects(frames):
    fx = PARTS / 'effects'
    # Loose flames, at the size the main drive and the four small engines get in ship/top.
    for tag, width in (('main', 26.77), ('small', 6.73)):
        across, along = flame_scales(width)
        per_px = GAME_FLAME_FRAME / FLAME_FRAME * 8.0 * K
        for i, (envelope, core) in enumerate(frames):
            flame = Image.new('RGBA', envelope.size, (0, 0, 0, 0))
            for sprite in (tint(envelope, (217, 245, 255)), core):
                flame.alpha_composite(sprite)
            small = shrink(flame, (flame.width * across * 1.06 * per_px, flame.height * along * 1.04 * per_px))
            save(trimmed(small), Path('effects') / 'engine' / ('engine_flame_%s_%d.png' % (tag, i + 1)), 'exhaust points down')
    for src in sorted(fx.glob('rocket_*.png')) + sorted(fx.glob('harpoon_anchor_*.png')):
        save(scaled(Image.open(src), 0.1 * REL), Path('effects') / 'projectiles' / src.name)
    print('effects')


def build_fx():
    out = OUT / 'effects'
    subprocess.run([sys.executable, str(Path(__file__).with_name('build_capsule_fx.py')), str(out), str(REL)], check=True)
    anchors = json.loads((out / 'anchors.json').read_text())
    for name, entry in sorted(anchors.items()):
        if entry['kind'] == 'line' and entry['start'] == entry['end']:
            note = 'flies right, along the centre line: the slug is at x=%d' % entry['start'][0]
        elif entry['kind'] == 'line':
            note = 'fires right, along the centre line: starts at x=%d, ends at x=%d' % (entry['start'][0], entry['end'][0])
        else:
            note = 'centred in the image'
        SIZES.append(('effects/' + name, tuple(entry['size']), note))


def write_readme():
    lines = [
        'Capsule-sized parts (%d px home base canvas), built by tools/build_capsule_set.py from the 4K' % SHIP_PX,
        'parts one folder up. Made to drop into a 1232x706 capsule without scaling: each image was',
        'reduced once with a Lanczos filter, so edges stay clean. Scaling a part by up to about 2x',
        'either way in Krita is fine; for anything bigger or much smaller, rebuild at another size:',
        '',
        '    python3 tools/build_capsule_set.py RawAssets/Steam/parts <out_dir> <ship_px>',
        '',
        'ship/<view>/   Every PNG in a view has the same canvas, so the layers line up when they',
        '               share a position: ship.png (bare), armed_*.png (with turrets), turrets_*/ (one',
        '               turret each) and engine_flames.png (the exhaust on its own).',
        'effects/       Beams, bolts and flashes fire to the right. Line effects lie on the image\'s',
        '               horizontal centre line (start and end x are listed below and in',
        '               effects/anchors.json); flashes, flares and rings are centred. Rotate the layer',
        '               about its start point to aim it. The game adds this light to the scene:',
        '               Normal blend mode works, Addition gives the game\'s glow.',
        'harvesters/    Sized as escorts next to the home base; harvesters_small/ is half that.',
        'pickups/       Ore at 64, 96 and 128 px (64 is the in-game sprite size).',
        '',
        'file  width x height  notes',
        '',
    ]
    for path, size, note in sorted(SIZES):
        lines.append('%-62s %5d x %-5d %s' % (path, size[0], size[1], note))
    (OUT / 'README.txt').write_text('\n'.join(lines) + '\n')


def main():
    only = set(sys.argv[4:])
    want = lambda name: not only or name in only
    frames = flame_frames()
    if want('ship'):
        build_ship(frames)
    if want('weapons'):
        build_weapons()
    if want('harvesters'):
        build_harvesters()
    if want('pickups'):
        build_pickups()
    if want('planet'):
        build_planet()
    if want('effects'):
        build_effects(frames)
    if want('fx'):
        build_fx()
    if not only:
        write_readme()


if __name__ == '__main__':
    main()
