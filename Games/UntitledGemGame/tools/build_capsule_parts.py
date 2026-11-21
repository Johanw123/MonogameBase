"""Turn the raw renders of tools/blender/capsule_parts.py into the parts folder.

    python3 tools/build_capsule_parts.py <raw_dir> RawAssets/Steam/parts [ship weapons harvesters ore planet misc]

Requires Pillow and NumPy.
"""
import shutil
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

Image.MAX_IMAGE_PIXELS = None
RAW, PARTS = Path(sys.argv[1]), Path(sys.argv[2])
ONLY = set(sys.argv[3:])


def want(name):
    return not ONLY or name in ONLY


def save(img, path, trim=False, pad=8):
    """Clears the colour under fully transparent pixels (smaller files, no fringes)."""
    path.parent.mkdir(parents=True, exist_ok=True)
    arr = np.asarray(img.convert('RGBA')).copy()
    arr[arr[:, :, 3] == 0] = 0
    img = Image.fromarray(arr, 'RGBA')
    if trim:
        box = img.getchannel('A').getbbox()
        if box:
            box = (max(0, box[0] - pad), max(0, box[1] - pad), min(img.width, box[2] + pad), min(img.height, box[3] + pad))
            img = img.crop(box)
    img.save(path, compress_level=6)


# ---- Ship -----------------------------------------------------------------------
if want('ship'):
    for view in sorted((RAW / 'ship').iterdir()):
        for src in sorted(view.rglob('*.png')):
            save(Image.open(src), PARTS / 'ship' / src.relative_to(RAW / 'ship'))
        print('ship', view.name)

# ---- Weapons --------------------------------------------------------------------
if want('weapons'):
    for weapon in sorted((RAW / 'weapons').iterdir()):
        out = PARTS / 'weapons' / weapon.name
        base = Image.open(weapon / 'top' / 'base.png').convert('RGBA')
        save(base, out / 'top' / f'{weapon.name}_base.png')
        frames = sorted((weapon / 'top' / 'frames').glob('frame_*.png'))
        for i, frame in enumerate(frames):
            deg = round(i * 360 / len(frames) * 10) / 10
            tag = ('%05.1f' % deg).replace('.', '_')
            head = Image.open(frame).convert('RGBA')
            save(head, out / 'top' / f'{weapon.name}_head_{tag}.png')
            save(Image.alpha_composite(base, head), out / 'top' / f'{weapon.name}_full_{tag}.png')
        for src in sorted(weapon.glob('closeup_*.png')):
            save(Image.open(src), out / f'{weapon.name}_{src.name}')
        print('weapon', weapon.name, base.size)

# ---- Harvesters -----------------------------------------------------------------
if want('harvesters'):
    for src in sorted((RAW / 'harvesters').rglob('*.png')):
        save(Image.open(src), PARTS / 'harvesters' / src.relative_to(RAW / 'harvesters'))
    print('harvesters')

# ---- Crystal ore pickups: tools/build_gem_surface.py at 20x ----------------------
GEM_COLORS = dict(red=(255, 0, 20), green=(20, 150, 45), blue=(45, 70, 255), teal=(0, 145, 125),
                  lilac=(180, 80, 255), purple=(110, 20, 220), gold=(205, 115, 0), darkblue=(15, 20, 130))
S = 20
CANVAS = 64 * S
CORE_SHADE = (0.60, 1.0)
CORE_HIGHLIGHT = 0.58


def ore_surface(directory):
    shade = np.asarray(Image.open(directory / 'shade.png').convert('RGBA'), dtype=np.float64) / 255
    mask = np.asarray(Image.open(directory / 'mask.png').convert('RGBA'), dtype=np.float64) / 255
    h, w = shade.shape[:2]
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
    solid_rock = (core < 0.15) & (body_alpha > 0.85)
    rlo, rhi = np.percentile(lum[solid_rock], [5, 95])
    rt = np.clip((lum - rlo) / max(rhi - rlo, 1e-6), 0, 1)
    rock_r = 0.24 * (0.25 + 0.75 * rt)
    rock_g = 0.15 * (0.20 + 0.80 * rt)
    crystal_emit = np.clip(core * body_alpha * (0.65 + 0.35 * t), 0, 1)
    emit_img = Image.fromarray(np.round(crystal_emit * 255).astype(np.uint8), 'L')
    glow_near = np.asarray(emit_img.filter(ImageFilter.GaussianBlur(radius=2.5 * S)), dtype=np.float64) / 255
    glow_far = np.asarray(emit_img.filter(ImageFilter.GaussianBlur(radius=5.0 * S)), dtype=np.float64) / 255
    yy, xx = np.mgrid[0:CANVAS, 0:CANVAS]
    edge_dist = np.minimum(np.minimum(xx, CANVAS - 1 - xx), np.minimum(yy, CANVAS - 1 - yy))
    edge_fade = np.clip((edge_dist - 3.0 * S) / (4.0 * S), 0, 1)
    glow_alpha = np.clip((0.50 * glow_near + 0.38 * glow_far) * edge_fade, 0, 0.65)
    total_alpha = np.clip(body_alpha + glow_alpha * (1.0 - body_alpha), 0, 1)
    body_r = core * core_weight + (1.0 - core) * rock_r
    body_g = core * core_light + (1.0 - core) * rock_g
    halo_g = 0.34 * np.clip(glow_near / np.maximum(glow_alpha, 1e-6), 0, 1)
    w_body = body_alpha / np.maximum(total_alpha, 1e-6)
    r = np.clip(w_body * body_r + (1.0 - w_body), 0, 1)
    g = np.clip(w_body * body_g + (1.0 - w_body) * halo_g, 0, 1)
    return dict(glow=(r, g, total_alpha), noglow=(body_r, body_g, body_alpha))


def tinted(r, g, alpha, tint):
    rgb = np.clip(np.asarray(tint) / 255 * r[:, :, None] + g[:, :, None], 0, 1)
    return Image.fromarray(np.round(np.dstack([rgb, alpha]) * 255).astype(np.uint8), 'RGBA')


if want('ore'):
    for i in range(4):
        surf = ore_surface(RAW / 'raw_ore' / f'v{i}')
        for name, tint in GEM_COLORS.items():
            save(tinted(*surf['glow'], tint), PARTS / 'pickups' / 'ore_glow' / f'ore_{i + 1}_{name}.png')
            save(tinted(*surf['noglow'], tint), PARTS / 'pickups' / 'ore_plain' / f'ore_{i + 1}_{name}.png', trim=True)
        print('ore', i)

# ---- Planet, shard, orb, projectiles ----------------------------------------------
if want('planet'):
    for i, src in enumerate(sorted((RAW / 'raw_planet' / 'frames').glob('frame_*.png'))):
        save(Image.open(src), PARTS / 'planet' / f'crystal_planet_{i + 1}.png', trim=True, pad=16)
    print('planet')

if want('misc'):
    misc = RAW / 'raw_misc'
    save(Image.open(misc / 'core_shard.png'), PARTS / 'pickups' / 'core_shard.png')
    save(Image.open(misc / 'prestige_orb.png'), PARTS / 'pickups' / 'prestige_orb.png')
    for name, frames in (('rocket_main', 4), ('rocket_mini', 4), ('harpoon_anchor', 4)):
        strip = Image.open(misc / f'{name}.png').convert('RGBA')
        fw = strip.width // frames
        for f in range(frames):
            save(strip.crop((f * fw, 0, (f + 1) * fw, strip.height)), PARTS / 'effects' / f'{name}_{f + 1}.png')
    envelope = Image.open(misc / 'booster.png').convert('RGBA')
    core = Image.open(misc / 'booster_core.png').convert('RGBA')
    # The untrimmed strips (nozzle at each frame's centre) and the nozzle positions, for
    # the flame layers of tools/build_capsule_set.py.
    shutil.copy(misc / 'booster.png', PARTS / 'effects' / 'engine_flame_strip_envelope.png')
    shutil.copy(misc / 'booster_core.png', PARTS / 'effects' / 'engine_flame_strip_core.png')
    for nozzles in (RAW / 'ship').glob('*/nozzles.json'):
        shutil.copy(nozzles, PARTS / 'ship' / nozzles.parent.name / 'nozzles.json')
    size = envelope.height
    for f in range(envelope.width // size):
        box = (f * size, 0, (f + 1) * size, size)
        e = np.asarray(envelope.crop(box), dtype=np.float64) / 255
        c = np.asarray(core.crop(box), dtype=np.float64) / 255
        # The game's main plume tint (217, 245, 255) under the white-hot core.
        e[:, :, :3] *= np.asarray((217, 245, 255)) / 255
        a = c[:, :, 3:4] + e[:, :, 3:4] * (1 - c[:, :, 3:4])
        rgb = (c[:, :, :3] * c[:, :, 3:4] + e[:, :, :3] * e[:, :, 3:4] * (1 - c[:, :, 3:4])) / np.maximum(a, 1e-6)
        flame = Image.fromarray(np.round(np.clip(np.dstack([rgb, a]), 0, 1) * 255).astype(np.uint8), 'RGBA')
        save(flame, PARTS / 'effects' / f'engine_flame_{f + 1}.png', trim=True)
        if f == 0:
            save(envelope.crop(box), PARTS / 'effects' / 'engine_flame_envelope_only.png', trim=True)
            save(core.crop(box), PARTS / 'effects' / 'engine_flame_core_only.png', trim=True)
    print('misc')
