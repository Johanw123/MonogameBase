"""High-resolution parts for store art (RawAssets/Steam/parts), rendered with the
game's own prerender pipeline (prerender.py) at 8-20x the in-game resolution.

Runs headless, one stage per call so only one render uses the GPU at a time:

    blender -b --factory-startup --python tools/blender/capsule_parts.py -- <raw_dir> <stage> [names...]
    python3 tools/build_capsule_parts.py <raw_dir> RawAssets/Steam/parts

Stages: ship (optionally one or more of SHIP_VIEWS), weapons, harvesters, ore,
planet, shard, orb, projectiles, and nozzles (no rendering: writes
ship/<view>/nozzles.json, where the engine nozzles sit on each view's canvas,
for the flame layers of tools/build_capsule_set.py).
"""
import json
import math
import os
import sys
import time

import bpy
import mathutils
import numpy as np

GAME = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))) + '/'
G = {'__name__': 'prerender'}
exec(open(GAME + 'tools/blender/prerender.py').read(), G)
CONFIG = G['CONFIG']

argv = sys.argv[sys.argv.index('--') + 1:]
OUT, STAGE, ARGS = argv[0], argv[1], argv[2:]
T0 = time.time()


def log(*a):
    print('[parts %6.1fs]' % (time.time() - T0), *a, flush=True)


def fast_blur(arr, sigma):
    """Gaussian blur with zero padding, like prerender._blur2d but FFT based."""
    if sigma <= 0:
        return arr
    radius = max(1, int(math.ceil(sigma * 3.0)))
    h, w = arr.shape
    padded = np.zeros((h + 2 * radius, w + 2 * radius), dtype=np.float32)
    padded[radius:radius + h, radius:radius + w] = arr
    fy = np.fft.fftfreq(padded.shape[0])[:, None]
    fx = np.fft.rfftfreq(padded.shape[1])[None, :]
    transfer = np.exp(-2.0 * (math.pi * sigma) ** 2 * (fx * fx + fy * fy))
    out = np.fft.irfft2(np.fft.rfft2(padded) * transfer, s=padded.shape)
    return np.clip(out[radius:radius + h, radius:radius + w], 0.0, None).astype(np.float32)


G['_blur2d'] = fast_blur


def load_px(path):
    img = bpy.data.images.load(path)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape((h, w, 4))
    bpy.data.images.remove(img)
    return px


def accent_glow(filepath, cfg, exclude=None):
    """prerender._apply_accent_glow, with an optional coverage mask (turrets) that
    must not count as glowing orange hull stripes."""
    glow_strength = cfg.get('accent_glow', 0.0)
    img = bpy.data.images.load(filepath)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape((h, w, 4))
    r, g, b, a = px[:, :, 0], px[:, :, 1], px[:, :, 2], px[:, :, 3]
    orangeness = (np.clip((r - 0.60) / 0.35, 0.0, 1.0) * np.clip((r - b - 0.35) / 0.40, 0.0, 1.0)
                  * np.clip((g - 0.35) / 0.35, 0.0, 1.0) * a)
    if exclude is not None:
        orangeness = orangeness * (1.0 - np.clip(exclude, 0.0, 1.0))
    if orangeness.max() <= 1e-4:
        bpy.data.images.remove(img)
        return
    scale = max(w, h) / 512.0
    core_mask = fast_blur(orangeness, max(0.8, 1.4 * scale)) * orangeness
    glow_tight = fast_blur(orangeness, max(1.2, 3.2 * scale))
    glow_wide = fast_blur(orangeness, max(2.5, 9.5 * scale))
    hull_bleed = (0.58 * glow_tight + 0.42 * glow_wide) * glow_strength
    r = np.clip(r + core_mask * 0.10, 0.0, 1.0)
    g = np.clip(g + core_mask * 0.16 * (0.5 + 0.5 * g), 0.0, 1.0)
    b = np.clip(b + core_mask * 0.03, 0.0, 1.0)
    non_orange = np.clip(1.0 - orangeness * 0.85, 0.0, 1.0)
    spill = hull_bleed * non_orange
    r = np.clip(r + spill * 0.80 * (1.0 - r * 0.45), 0.0, 1.0)
    g = np.clip(g + spill * 0.28 * (1.0 - g * 0.45), 0.0, 1.0)
    b = np.clip(b + spill * 0.02 * (1.0 - b * 0.45), 0.0, 1.0)
    outer_halo = fast_blur(orangeness, max(1.0, 2.2 * scale)) * (1.0 - a) * 0.50 * glow_strength
    outer_halo[0, :] = outer_halo[-1, :] = 0.0
    outer_halo[:, 0] = outer_halo[:, -1] = 0.0
    new_a = np.clip(a + outer_halo, 0.0, 1.0)
    denom = np.maximum(new_a, 1e-5)
    r = np.where(new_a > 1e-5, (r * a + 1.00 * outer_halo) / denom, 0.0)
    g = np.where(new_a > 1e-5, (g * a + 0.36 * outer_halo) / denom, 0.0)
    b = np.where(new_a > 1e-5, (b * a + 0.02 * outer_halo) / denom, 0.0)
    img.pixels[:] = np.stack([r, g, b, new_a], axis=-1).ravel()
    img.save()
    bpy.data.images.remove(img)


def render(scene, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True, scene=scene.name)


# ---- Home base, bare and armed ----------------------------------------------

TURRET_TINT = dict(tint=(1.0, 0.55, 0.18), tint_strength=0.85, tint_value=1.3)
# name, model, hull mount (Weaponry.cs, hull units from the sprite centre, y down), units_per_m
TURRETS = [
    ('cannon_left', 'SM_Prop_Turret_Base_Single_01', (-17, -30), 4.0),
    ('cannon_right', 'SM_Prop_Turret_Base_Single_01', (17, -30), 4.0),
    ('laser', 'SM_Prop_Turret_Base_Single_05', (0, -17), 4.0),
    ('harpoon', 'SM_Prop_Turret_Base_Single_06', (15, -6), 4.0),
    ('rockets', 'SM_Prop_Turret_Base_Single_08', (-13, 30), 4.0),
    ('railgun', 'SM_Prop_Turret_Large_02', (13, 30), 4.5),
]
SHIP_VIEWS = dict(
    top=(65, 0),
    rear34_right=(35, 40), rear34_left=(35, 320),
    front34_right=(30, 140), front34_left=(30, 220),
    side_right=(25, 90), side_left=(25, 270),
)
# Clockwise head angle from "barrel along the nose", per turret.
AIMS = dict(
    forward=dict(),
    starboard=dict(cannon_left=90, cannon_right=90, laser=90, harpoon=90, rockets=90, railgun=90),
)


GAME_FRAME = {}


def build_armed(cfg):
    scene = G['new_scene'](cfg)
    ship = G['import_model'](scene, 'SM_Ship_Cruiser_01', dict(cfg, heading=0))
    bpy.data.materials['SyntyShip'].name = 'HullMat'
    # The game's sprite: 512 px at 65 degrees, 0.19 hull units per texel, pivot at the centre.
    shift = G['add_camera'](scene, ship, dict(cfg, elevation=65.0, heading=0, resolution=512))
    cam = scene.camera
    rot = cam.matrix_world.to_3x3()
    right, up, fwd = rot @ mathutils.Vector((1, 0, 0)), rot @ mathutils.Vector((0, 1, 0)), rot @ mathutils.Vector((0, 0, -1))
    px_per_m = 512 / cam.data.ortho_scale
    units_per_ship_m = 0.19 * px_per_m
    GAME_FRAME.update(shift=shift.copy(), px_per_m=px_per_m)
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    hits = {}
    for name, model, (mx, my), upm in TURRETS:
        p0 = shift + right * (mx / units_per_ship_m) + up * (-my / units_per_ship_m)
        ok, loc, normal, _idx, _obj, _m = scene.ray_cast(deps, p0 - fwd * 500.0, fwd)
        hits[name] = (p0, loc.copy() if ok else None)
        log('mount', name, 'hit' if ok else 'MISS', tuple(round(c, 2) for c in (loc if ok else p0)))
    bpy.data.objects.remove(cam)

    turrets = {}
    for i, (name, model, mount, upm) in enumerate(TURRETS):
        root = G['import_model'](scene, model, dict(cfg, heading=0, **TURRET_TINT))
        bpy.data.materials['SyntyShip'].name = 'TurretMat_%d' % i
        base, head = G['_turret_parts'](root, model)
        barrels = [o for o in head if 'barrel' in o.name.lower()] or head
        bpy.context.view_layer.update()
        zs = [(o.matrix_world @ v.co).z for o in barrels for v in o.data.vertices]
        zb = (min(zs) + max(zs)) / 2 - root.matrix_world.translation.z
        s = upm / units_per_ship_m
        p0, hit = hits[name]
        deck_z = hit.z if hit is not None else 0.0
        t = (deck_z + zb * s - p0.z) / fwd.z
        q = p0 + fwd * t
        root.location = (q.x, q.y, deck_z)
        root.scale = (s, s, s)
        bpy.context.view_layer.update()
        tops = [o for o in head if o.parent not in head]
        turrets[name] = dict(root=root, base=base, head=head, tops=tops)
    rig = bpy.data.objects.new('Rig', None)
    scene.collection.objects.link(rig)
    ship.parent = rig
    for t in turrets.values():
        t['root'].parent = rig
    bpy.context.view_layer.update()
    return scene, rig, ship, turrets


def aim_turrets(turrets, rig, aims):
    """Aim is set with the rig at heading 0."""
    rig.rotation_euler = (0, 0, 0)
    bpy.context.view_layer.update()
    for name, t in turrets.items():
        if 'orig' not in t:
            t['orig'] = [o.matrix_world.copy() for o in t['tops']]
        o0 = t['root'].matrix_world.translation
        m = (mathutils.Matrix.Translation(o0) @ mathutils.Matrix.Rotation(-math.radians(aims.get(name, 0)), 4, 'Z')
             @ mathutils.Matrix.Translation(-o0))
        for o, orig in zip(t['tops'], t['orig']):
            o.matrix_world = m @ orig
    bpy.context.view_layer.update()


def set_visible(objs, visible):
    for o in objs:
        o.hide_render = not visible


def tree(root):
    return [root] + list(root.children_recursive)


def set_border(scene, objs, pad=0.03):
    cam = scene.camera
    to_cam = cam.matrix_world.inverted()
    pts = [to_cam @ (o.matrix_world @ v.co) for o in objs if o.type == 'MESH' for v in o.data.vertices]
    rx, ry = scene.render.resolution_x, scene.render.resolution_y
    ow = cam.data.ortho_scale * (1.0 if rx >= ry else rx / ry)
    oh = cam.data.ortho_scale * (1.0 if ry >= rx else ry / rx)
    r = scene.render
    r.use_border, r.use_crop_to_border = True, False
    r.border_min_x = max(0.0, min(p.x for p in pts) / ow + 0.5 - pad)
    r.border_max_x = min(1.0, max(p.x for p in pts) / ow + 0.5 + pad)
    r.border_min_y = max(0.0, min(p.y for p in pts) / oh + 0.5 - pad)
    r.border_max_y = min(1.0, max(p.y for p in pts) / oh + 0.5 + pad)


def setup_view(view, res=4096):
    """The armed ship framed for one of SHIP_VIEWS, on the canvas every layer of that view shares."""
    elevation, heading = SHIP_VIEWS[view]
    cfg = dict(CONFIG, elevation=float(elevation), heading=0.0, resolution=res)
    scene, rig, ship, turrets = build_armed(cfg)
    ship_objs = tree(ship)
    all_turret_objs = [o for t in turrets.values() for o in tree(t['root'])]
    # Frame on everything, at any aim, so every layer of this view shares a canvas.
    aim_turrets(turrets, rig, AIMS['starboard'])
    rig.rotation_euler = (0, 0, -math.radians(heading))
    bpy.context.view_layer.update()
    G['add_camera'](scene, rig, cfg)
    cam_a = scene.camera
    bounds_a = (cam_a.location.copy(), cam_a.data.ortho_scale, scene.render.resolution_x, scene.render.resolution_y)
    # Union with the forward aim: widen the ortho frame around the same centre.
    aim_turrets(turrets, rig, AIMS['forward'])
    rig.rotation_euler = (0, 0, -math.radians(heading))
    bpy.context.view_layer.update()
    to_cam = cam_a.matrix_world.inverted()
    pts = [to_cam @ p for p in G['_world_points'](rig)]
    rx, ry = bounds_a[2], bounds_a[3]
    half_w = max(abs(p.x) for p in pts) * cfg['margin']
    half_h = max(abs(p.y) for p in pts) * cfg['margin']
    ow = cam_a.data.ortho_scale * (1.0 if rx >= ry else rx / ry)
    oh = cam_a.data.ortho_scale * (1.0 if ry >= rx else ry / rx)
    ow, oh = max(ow, 2 * half_w), max(oh, 2 * half_h)
    cam_a.data.ortho_scale = max(ow, oh)
    if view == 'top':
        # Exactly 8x the game's sprite density, so parts/weapons renders fit 1:1.
        ship_only = [p for o in ship_objs if o.type == 'MESH' for p in (o.matrix_world @ v.co for v in o.data.vertices)]
        cpts = [to_cam @ p for p in ship_only]
        longest = max(max(p.x for p in cpts) - min(p.x for p in cpts), max(p.y for p in cpts) - min(p.y for p in cpts))
        px_per_m = 8 * 512 / (longest * cfg['margin'])
    else:
        px_per_m = res / max(ow, oh)
    scene.render.resolution_x = 2 * round(ow * px_per_m / 2)
    scene.render.resolution_y = 2 * round(oh * px_per_m / 2)
    G['add_lights'](scene, cfg)
    return scene, rig, ship, turrets, cfg, px_per_m, ship_objs, all_turret_objs


def stage_ship():
    views = ARGS or list(SHIP_VIEWS)
    for view in views:
        elevation, heading = SHIP_VIEWS[view]
        out = os.path.join(OUT, 'ship', view)
        scene, rig, ship, turrets, cfg, px_per_m, ship_objs, all_turret_objs = setup_view(view)
        log(view, 'canvas', scene.render.resolution_x, scene.render.resolution_y)
        meta = dict(view=view, elevation=elevation, heading=heading,
                    canvas=[scene.render.resolution_x, scene.render.resolution_y], px_per_m=px_per_m)

        # Bare ship.
        set_visible(all_turret_objs, False)
        scene.render.use_border = False
        render(scene, os.path.join(out, 'ship.png'))
        accent_glow(os.path.join(out, 'ship.png'), cfg)
        log(view, 'ship')

        for aim_name, aims in AIMS.items():
            aim_turrets(turrets, rig, aims)
            rig.rotation_euler = (0, 0, -math.radians(heading))
            bpy.context.view_layer.update()
            # Each turret on its own, on the shared canvas.
            set_visible(ship_objs, False)
            mask = None
            for name, t in turrets.items():
                set_visible(all_turret_objs, False)
                set_visible(tree(t['root']), True)
                set_border(scene, tree(t['root']))
                path = os.path.join(out, 'turrets_%s' % aim_name, '%s.png' % name)
                render(scene, path)
                alpha = load_px(path)[:, :, 3]
                mask = alpha if mask is None else np.maximum(mask, alpha)
            log(view, aim_name, 'turret layers')
            # Everything together.
            scene.render.use_border = False
            set_visible(ship_objs, True)
            set_visible(all_turret_objs, True)
            path = os.path.join(out, 'armed_%s.png' % aim_name)
            render(scene, path)
            accent_glow(path, cfg, exclude=mask)
            log(view, aim_name, 'armed')
        with open(os.path.join(out, 'meta.json'), 'w') as f:
            json.dump(meta, f, indent=2)


# ---- Weapons on their own -----------------------------------------------------

WEAPONS = dict(
    cannon=('SM_Prop_Turret_Base_Single_01', 4.0),
    laser=('SM_Prop_Turret_Base_Single_05', 4.0),
    harpoon=('SM_Prop_Turret_Base_Single_06', 4.0),
    rockets=('SM_Prop_Turret_Base_Single_08', 4.0),
    railgun=('SM_Prop_Turret_Large_02', 4.5),
)
CLOSEUP_VIEWS = dict(rear34_right=(35, 40), rear34_left=(35, 320), front34_right=(30, 140), front34_left=(30, 220),
                     side_right=(25, 90))


def stage_nozzles():
    """Writes ship/<view>/nozzles.json: each engine nozzle of the home base on that view's
    canvas (pixels), the on-screen direction and length of one sprite texel of exhaust,
    and whether the exhaust points away from the camera (so the hull hides it).
    tools/build_capsule_set.py lays the engine flames out from this. Renders nothing."""
    fleet = json.load(open(os.path.join(GAME, 'Content', 'Atlases', 'fleet.json')))
    data = fleet['Textures/Prerender/homebase_cruiser01.png'][6:]
    nozzles = [data[i:i + 4] for i in range(0, len(data), 4)]
    for view in ARGS or list(SHIP_VIEWS):
        scene, rig, ship, turrets, cfg, px_per_m, _s, _t = setup_view(view)
        cam = scene.camera
        to_cam = cam.matrix_world.inverted()
        rx, ry = scene.render.resolution_x, scene.render.resolution_y
        game_ppm, shift = GAME_FRAME['px_per_m'], GAME_FRAME['shift']
        turn = rig.matrix_world
        aft = to_cam.to_3x3() @ (turn.to_3x3() @ mathutils.Vector((0, -1, 0)))
        out = []
        for x, y, z, width in nozzles:
            world = turn @ (shift + mathutils.Vector((x, -y, z)) / game_ppm)
            c = to_cam @ world
            out.append(dict(x=rx / 2 + c.x * px_per_m, y=ry / 2 - c.y * px_per_m, depth=-c.z, width=width))
        meta = dict(view=view, canvas=[rx, ry], px_per_texel=px_per_m / game_ppm,
                    aft=[aft.x, -aft.y], aft_away_from_camera=-aft.z, nozzles=out)
        path = os.path.join(OUT, 'ship', view, 'nozzles.json')
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, 'w') as f:
            json.dump(meta, f, indent=2)
            f.write('\n')
        log(view, 'nozzles', rx, ry, round(px_per_m / game_ppm, 3))


def stage_weapons():
    for name, (model, upm) in WEAPONS.items():
        if ARGS and name not in ARGS:
            continue
        out = os.path.join(OUT, 'weapons', name)
        # Game angle at 8x the game's density: static base plus the head at 16 headings.
        meta = G['render_turret'](model, os.path.join(out, 'top'), frames=16, units_per_m=upm, texel=0.19 / 8,
                                  **TURRET_TINT)
        with open(os.path.join(out, 'top', 'meta.json'), 'w') as f:
            json.dump({k: v for k, v in meta.items()}, f, indent=2)
        log(name, 'top', meta['size'])
        for view, (elevation, heading) in CLOSEUP_VIEWS.items():
            res = 2048 if name == 'railgun' else 1536
            G['render_model'](model, os.path.join(out, 'closeup_%s.png' % view), elevation=float(elevation),
                              heading=float(heading), resolution=res, **TURRET_TINT)
        log(name, 'closeups')


# ---- Harvesters ---------------------------------------------------------------

HARVESTERS = dict(
    drifter='SM_Ship_Bomber_01', seeker='SM_Ship_Fighter_Heavy_01', prospector='SM_Ship_Stealth_01',
    trove_hunter='SM_Ship_Fighter_Heavy_04', rimrunner='SM_Ship_Stealth_05', drone='SM_Veh_Drone_Repair_01',
)
HARVESTER_VIEWS = dict(top=(65, 0), rear34_right=(35, 40), rear34_left=(35, 320), front34_right=(30, 140),
                       front34_left=(30, 220), side_right=(25, 90))


def stage_harvesters():
    for name, model in HARVESTERS.items():
        if ARGS and name not in ARGS:
            continue
        for view, (elevation, heading) in HARVESTER_VIEWS.items():
            path = os.path.join(OUT, 'harvesters', name, '%s_%s.png' % (name, view))
            G['render_model'](model, path, elevation=float(elevation), heading=float(heading), resolution=2048)
            meta = os.path.splitext(path)[0] + '.json'
            if os.path.exists(meta):
                os.remove(meta)
        log(name)


# ---- Pickups, planet, projectiles ---------------------------------------------

def stage_ore():
    meta = G['render_plasma_orb'](os.path.join(OUT, 'raw_ore'), resolution=1000)
    log('ore', meta)


def stage_planet():
    planet = G['CRYSTAL_PLANET']
    meta = G['render_planet_spin'](planet['model'], os.path.join(OUT, 'raw_planet'), frames=6, size=2304,
                                   planet=planet['planet'], samples=48, ambient_strength=0.12, key_energy=3.5,
                                   key_rot=(70, 0, -50))
    log('planet', meta['size'])


def stage_shard():
    # The molten pockets' soft falloff is written in texels of the game's 256 px
    # canvas; scale those distances with the canvas so the look carries over.
    k = 4
    src = open(GAME + 'tools/blender/prerender.py').read()
    for old, new in [
        ('_neighbour_min(pocket_poly, 2)', '_neighbour_min(pocket_poly, %d)' % (2 * k)),
        ('_neighbour_min(pocket_poly, 5)', '_neighbour_min(pocket_poly, %d)' % (5 * k)),
        ('_neighbour_min(pocket_poly, 9)', '_neighbour_min(pocket_poly, %d)' % (9 * k)),
        ('_blur2d(pocket_poly, 0.55)', '_blur2d(pocket_poly, %g)' % (0.55 * k)),
        ('_blur2d(pocket_erode1, 1.5)', '_blur2d(pocket_erode1, %g)' % (1.5 * k)),
        ('_blur2d(pocket_erode2, 2.5)', '_blur2d(pocket_erode2, %g)' % (2.5 * k)),
        ('_blur2d(pocket_erode3, 3.5)', '_blur2d(pocket_erode3, %g)' % (3.5 * k)),
    ]:
        assert src.count(old) == 1, old
        src = src.replace(old, new)
    scaled = {'__name__': 'prerender'}
    exec(src, scaled)
    scaled['_blur2d'] = fast_blur
    scaled['render_core_shard'](os.path.join(OUT, 'raw_misc', 'core_shard.png'), canvas=256 * k, resolution=216 * k)
    log('core shard')


def stage_orb():
    G['render_prestige_orb'](os.path.join(OUT, 'raw_misc', 'prestige_orb.png'), canvas=512)
    log('prestige orb')


def stage_projectiles():
    out = os.path.join(OUT, 'raw_misc')
    os.makedirs(out, exist_ok=True)
    for name, spec in G['ROCKET_SPECS'].items():
        s = dict(spec)
        model = s.pop('model')
        s['frame_w'] *= 8
        s['frame_h'] *= 8
        G['render_rocket_strip'](model, os.path.join(out, name + '.png'), frames=4, **s)
        log(name)
    G['render_harpoon_strip'](os.path.join(out, 'harpoon_anchor.png'), frames=4, frame_w=512, frame_h=768)
    log('harpoon')
    G['render_booster_strip'](os.path.join(out, 'booster.png'), frames=8, size=1024)
    log('booster')


globals()['stage_' + STAGE]()
log('stage', STAGE, 'done')
