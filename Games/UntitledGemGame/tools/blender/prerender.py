"""Prerender Synty POLYGON Sci-Fi Space models to 2D sprites.

Run inside Blender (e.g. through the Blender MCP `execute_blender_code`):

    exec(open('/path/to/tools/blender/prerender.py').read())
    render_model('SM_Ship_Cruiser_01', '/tmp/out/cruiser.png')        # one tight-cropped sprite
    render_turntable('SM_Ship_Bomber_01', '/tmp/out/bomber', frames=32)  # N headings, fixed canvas

The scene is built from scratch in a dedicated "Prerender" scene so the user's
own scene is left alone. Everything tunable lives in CONFIG.

Conventions
-----------
* Every Synty ship faces -Y when imported; we turn it 180 degrees so that
  heading 0 means "nose points to the top of the image", like the game's
  existing sprites.
* heading is measured clockwise on screen, like MonoGame's Transform2.Rotation,
  so frame i of a turntable is the ship rotated i * 360 / frames degrees clockwise.
* The lights stay fixed in the world while the ship turns, so shading always
  comes from the same screen direction, which a rotated 2D sprite cannot do.
"""
import json
import math
import os

import bpy
import mathutils

import numpy as np

SYNTY = '/home/johan/My project/Assets/Synty/PolygonSciFiSpace/'

CONFIG = dict(
    # Camera elevation above the horizon: 90 = straight top-down, 0 = side-on.
    elevation=65.0,
    # Clockwise heading on screen, degrees. 0 = nose points to the top of the image.
    heading=0.0,
    resolution=512,        # longest side of the output image, pixels
    margin=1.08,           # padding around the model, as a fraction
    samples=64,
    # Lighting: warm key from the upper left, cool rim from behind, tinted ambient.
    key_color=(1.0, 0.82, 0.62), key_energy=3.2,
    rim_color=(0.45, 0.65, 1.0), rim_energy=4.0,
    fill_color=(0.75, 0.45, 1.0), fill_energy=0.6,
    ambient_color=(0.34, 0.26, 0.5), ambient_strength=0.9,
    emission_strength=4.0,
    # Emissive boost and warm light bleed for the palette's orange accent stripes.
    accent_emission=0.65,
    accent_glow=1.0,
    # Strength of the panel-line detail overlay (0 = off, 1 = as in Unity).
    detail=1.0,
    # How many times the panel detail texture repeats along the ship's longest
    # side. Keeps the panel size proportional whatever the model's scale.
    detail_tiles=4.2,
    # Colour variant, relative to Textures/ (the default A palette or an Alts/ one).
    palette='PolygonSciFiSpace_Texture_01_A.png',
)

SCENE_NAME = 'Prerender'


def _tex(name, colorspace='sRGB'):
    path = SYNTY + 'Textures/' + name
    image = bpy.data.images.get(os.path.basename(path)) or bpy.data.images.load(path)
    image.colorspace_settings.name = colorspace
    return image


def ship_material(cfg=CONFIG, detail_scale=0.001):
    """Recreates Synty's Ship material: palette texture x panel detail + emissive."""
    name = 'SyntyShip'
    old = bpy.data.materials.get(name)
    if old:
        bpy.data.materials.remove(old)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    nodes.clear()

    out = nodes.new('ShaderNodeOutputMaterial')
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = 0.55
    bsdf.inputs['Metallic'].default_value = 0.0
    links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])

    uv = nodes.new('ShaderNodeTexCoord')
    base = nodes.new('ShaderNodeTexImage')
    base.image = _tex(cfg['palette'])
    base.interpolation = 'Linear'
    links.new(uv.outputs['UV'], base.inputs['Vector'])

    hsv = nodes.new('ShaderNodeSeparateColor')
    hsv.mode = 'HSV'
    links.new(base.outputs['Color'], hsv.inputs['Color'])

    # Detect the palette's orange accent swatch (linear HSV ~ 0.077, 0.92, 0.89)
    # so it can self-illuminate and keep crisp panel lines without dark grime.
    hue_sub = nodes.new('ShaderNodeMath')
    hue_sub.operation = 'SUBTRACT'
    hue_sub.inputs[1].default_value = 0.08
    links.new(hsv.outputs[0], hue_sub.inputs[0])
    hue_dist = nodes.new('ShaderNodeMath')
    hue_dist.operation = 'ABSOLUTE'
    links.new(hue_sub.outputs['Value'], hue_dist.inputs[0])

    hue_mask = nodes.new('ShaderNodeMapRange')
    hue_mask.inputs['From Min'].default_value = 0.035
    hue_mask.inputs['From Max'].default_value = 0.065
    hue_mask.inputs['To Min'].default_value = 1.0
    hue_mask.inputs['To Max'].default_value = 0.0
    links.new(hue_dist.outputs['Value'], hue_mask.inputs['Value'])

    sat_mask = nodes.new('ShaderNodeMapRange')
    sat_mask.inputs['From Min'].default_value = 0.45
    sat_mask.inputs['From Max'].default_value = 0.70
    sat_mask.inputs['To Min'].default_value = 0.0
    sat_mask.inputs['To Max'].default_value = 1.0
    links.new(hsv.outputs[1], sat_mask.inputs['Value'])

    val_mask = nodes.new('ShaderNodeMapRange')
    val_mask.inputs['From Min'].default_value = 0.50
    val_mask.inputs['From Max'].default_value = 0.75
    val_mask.inputs['To Min'].default_value = 0.0
    val_mask.inputs['To Max'].default_value = 1.0
    links.new(hsv.outputs[2], val_mask.inputs['Value'])

    accent_hs = nodes.new('ShaderNodeMath')
    accent_hs.operation = 'MULTIPLY'
    links.new(hue_mask.outputs['Result'], accent_hs.inputs[0])
    links.new(sat_mask.outputs['Result'], accent_hs.inputs[1])

    accent_mask = nodes.new('ShaderNodeMath')
    accent_mask.operation = 'MULTIPLY'
    links.new(accent_hs.outputs['Value'], accent_mask.inputs[0])
    links.new(val_mask.outputs['Result'], accent_mask.inputs[1])

    base_color = base.outputs['Color']
    if cfg.get('accent_emission', 0.0) > 0:
        base_boost = nodes.new('ShaderNodeMix')
        base_boost.data_type = 'RGBA'
        base_boost.inputs['B'].default_value = (0.90, 0.16, 0.005, 1.0)
        links.new(accent_mask.outputs['Value'], base_boost.inputs['Factor'])
        links.new(base_color, base_boost.inputs['A'])
        base_color = base_boost.outputs['Result']

        spec_ior = nodes.new('ShaderNodeMapRange')
        spec_ior.inputs['From Min'].default_value = 0.0
        spec_ior.inputs['From Max'].default_value = 1.0
        spec_ior.inputs['To Min'].default_value = 0.5
        spec_ior.inputs['To Max'].default_value = 0.05
        links.new(accent_mask.outputs['Value'], spec_ior.inputs['Value'])
        links.new(spec_ior.outputs['Result'], bsdf.inputs['Specular IOR Level'])

    # Panel-line detail, projected in object space so it ignores the UV layout.
    detail = nodes.new('ShaderNodeTexImage')
    detail.image = _tex('PolygonSciFiSpace_Ship_Mask_04.png', 'Non-Color')
    detail.projection = 'BOX'
    detail.projection_blend = 0.2
    mapping = nodes.new('ShaderNodeMapping')
    mapping.inputs['Scale'].default_value = (detail_scale,) * 3
    links.new(uv.outputs['Object'], mapping.inputs['Vector'])
    links.new(mapping.outputs['Vector'], detail.inputs['Vector'])

    detail_fac = nodes.new('ShaderNodeMapRange')
    detail_fac.inputs['From Min'].default_value = 0.0
    detail_fac.inputs['From Max'].default_value = 1.0
    detail_fac.inputs['To Min'].default_value = cfg['detail']
    detail_fac.inputs['To Max'].default_value = cfg['detail'] * (0.45 if cfg.get('accent_emission', 0.0) > 0 else 1.0)
    links.new(accent_mask.outputs['Value'], detail_fac.inputs['Value'])

    mix = nodes.new('ShaderNodeMix')
    mix.data_type = 'RGBA'
    mix.blend_type = 'MULTIPLY'
    links.new(detail_fac.outputs['Result'], mix.inputs['Factor'])
    links.new(base_color, mix.inputs['A'])
    links.new(detail.outputs['Color'], mix.inputs['B'])
    color = mix.outputs['Result']

    # Optional tint for the palette's grey metal: recolour (hue and saturation of
    # `tint`, keeping the shading) where the palette is unsaturated, then scale the
    # brightness by `tint_value`. Coloured accents keep their colour.
    if cfg.get('tint'):
        grey = nodes.new('ShaderNodeMapRange')       # saturation 0.12..0.35 -> 1..0
        grey.inputs['From Min'].default_value = 0.12
        grey.inputs['From Max'].default_value = 0.35
        grey.inputs['To Min'].default_value = 1.0
        grey.inputs['To Max'].default_value = 0.0
        links.new(hsv.outputs[1], grey.inputs['Value'])   # outputs: hue, saturation, value
        amount = nodes.new('ShaderNodeMath')
        amount.operation = 'MULTIPLY'
        amount.inputs[1].default_value = cfg.get('tint_strength', 0.8)
        links.new(grey.outputs['Result'], amount.inputs[0])
        recolour = nodes.new('ShaderNodeMix')
        recolour.data_type = 'RGBA'
        recolour.blend_type = 'COLOR'
        recolour.inputs['B'].default_value = (*cfg['tint'], 1)
        links.new(amount.outputs['Value'], recolour.inputs['Factor'])
        links.new(color, recolour.inputs['A'])
        value = nodes.new('ShaderNodeMix')
        value.data_type = 'RGBA'
        value.blend_type = 'MULTIPLY'
        v = cfg.get('tint_value', 1.0)
        value.inputs['B'].default_value = (v, v, v, 1)
        links.new(amount.outputs['Value'], value.inputs['Factor'])
        links.new(recolour.outputs['Result'], value.inputs['A'])
        color = value.outputs['Result']
    links.new(color, bsdf.inputs['Base Color'])

    emissive = nodes.new('ShaderNodeTexImage')
    emissive.image = _tex('PolygonSciFiSpace_01_Emissive.png')
    links.new(uv.outputs['UV'], emissive.inputs['Vector'])
    glow = emissive.outputs['Color']
    # Optional emission recolour (e.g. one colour per gem tier), keeping its brightness.
    if cfg.get('emission_color'):
        bw = nodes.new('ShaderNodeRGBToBW')
        links.new(glow, bw.inputs['Color'])
        tinted = nodes.new('ShaderNodeMix')
        tinted.data_type = 'RGBA'
        tinted.blend_type = 'MULTIPLY'
        tinted.inputs['Factor'].default_value = 1.0
        tinted.inputs['B'].default_value = (*cfg['emission_color'], 1)
        links.new(bw.outputs['Val'], tinted.inputs['A'])
        glow = tinted.outputs['Result']
    elif cfg.get('accent_emission', 0.0) > 0 and not cfg.get('tint'):
        accent_detailed = nodes.new('ShaderNodeMix')
        accent_detailed.data_type = 'RGBA'
        accent_detailed.blend_type = 'MULTIPLY'
        accent_detailed.inputs['Factor'].default_value = 0.5
        accent_detailed.inputs['A'].default_value = (1.0, 0.18, 0.005, 1.0)
        links.new(detail.outputs['Color'], accent_detailed.inputs['B'])

        accent_scale = cfg['accent_emission'] / max(cfg['emission_strength'], 1e-6)
        accent_fac = nodes.new('ShaderNodeMath')
        accent_fac.operation = 'MULTIPLY'
        accent_fac.inputs[1].default_value = accent_scale
        links.new(accent_mask.outputs['Value'], accent_fac.inputs[0])

        add_accent = nodes.new('ShaderNodeMix')
        add_accent.data_type = 'RGBA'
        add_accent.blend_type = 'ADD'
        links.new(accent_fac.outputs['Value'], add_accent.inputs['Factor'])
        links.new(glow, add_accent.inputs['A'])
        links.new(accent_detailed.outputs['Result'], add_accent.inputs['B'])
        glow = add_accent.outputs['Result']
    links.new(glow, bsdf.inputs['Emission Color'])
    bsdf.inputs['Emission Strength'].default_value = cfg['emission_strength']
    return mat


def new_scene(cfg=CONFIG):
    old = bpy.data.scenes.get(SCENE_NAME)
    if old:
        other = next(s for s in bpy.data.scenes if s is not old)
        bpy.context.window.scene = other
        for obj in list(old.objects):
            bpy.data.objects.remove(obj)
        bpy.data.scenes.remove(old)
    scene = bpy.data.scenes.new(SCENE_NAME)
    # Must be the active scene, otherwise view_layer.update() skips its objects
    # and matrix_world stays stale.
    bpy.context.window.scene = scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.view_transform = 'Standard'
    try:
        scene.eevee.taa_render_samples = cfg['samples']
    except AttributeError:
        pass

    world = bpy.data.worlds.get(SCENE_NAME) or bpy.data.worlds.new(SCENE_NAME)
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs['Color'].default_value = (*cfg['ambient_color'], 1)
    bg.inputs['Strength'].default_value = cfg['ambient_strength']
    scene.world = world
    return scene


def set_heading(root, heading):
    """Clockwise heading on screen; Synty ships face -Y, hence the 180."""
    root.rotation_euler = (0, 0, math.radians(180.0 - heading))
    bpy.context.view_layer.update()


def import_model(scene, model, cfg=CONFIG):
    """Imports an FBX, parents all meshes under one empty and returns it."""
    before = set(bpy.data.objects)
    bpy.ops.wm.fbx_import(filepath=SYNTY + 'Models/' + model + '.fbx')
    new = [o for o in bpy.data.objects if o not in before]
    root = bpy.data.objects.new(model, None)
    scene.collection.objects.link(root)
    meshes = [o for o in new if o.type == 'MESH']
    # Object-space size of the longest side, for a scale-independent detail size.
    local_size = max(max(o.dimensions) / max(abs(o.scale.x), 1e-9) for o in meshes)
    mat = (cfg.get('material_fn') or ship_material)(cfg, detail_scale=cfg['detail_tiles'] / local_size)
    for obj in new:
        for coll in list(obj.users_collection):
            coll.objects.unlink(obj)
        scene.collection.objects.link(obj)
        if obj.type == 'MESH':
            obj.data.materials.clear()
            obj.data.materials.append(mat)
            for poly in obj.data.polygons:
                poly.use_smooth = False
        if obj.parent is None:
            obj.parent = root
    set_heading(root, cfg['heading'])
    return root


def _world_points(root):
    """Every vertex of every mesh under root, in world space."""
    pts = []
    for obj in [root] + list(root.children_recursive):
        if obj.type != 'MESH':
            continue
        m = obj.matrix_world
        pts.extend(m @ v.co for v in obj.data.vertices)
    return pts


def add_lights(scene, cfg=CONFIG):
    def sun(name, color, energy, rot_deg):
        data = bpy.data.lights.new(name, 'SUN')
        data.color, data.energy = color, energy
        data.angle = math.radians(12)
        obj = bpy.data.objects.new(name, data)
        obj.rotation_euler = [math.radians(a) for a in rot_deg]
        scene.collection.objects.link(obj)
    # Sun rotation: x tilts away from straight down, z swings around.
    sun('Key', cfg['key_color'], cfg['key_energy'], cfg.get('key_rot', (35, 0, -35)))
    sun('Rim', cfg['rim_color'], cfg['rim_energy'], cfg.get('rim_rot', (120, 0, 150)))
    sun('Fill', cfg['fill_color'], cfg['fill_energy'], cfg.get('fill_rot', (60, 0, 120)))


def _new_camera(scene, cfg):
    cam_data = bpy.data.cameras.new('Cam')
    cam_data.type = 'ORTHO'
    cam = bpy.data.objects.new('Cam', cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.rotation_euler = (math.pi / 2 - math.radians(cfg['elevation']), 0, 0)
    bpy.context.view_layer.update()
    return cam, cam_data


def add_camera(scene, root, cfg=CONFIG):
    """Camera that tightly frames the model at its current heading."""
    cam, cam_data = _new_camera(scene, cfg)
    pts = _world_points(root)
    to_cam = cam.matrix_world.inverted()
    cpts = [to_cam @ p for p in pts]
    min_x, max_x = min(p.x for p in cpts), max(p.x for p in cpts)
    min_y, max_y = min(p.y for p in cpts), max(p.y for p in cpts)
    width, height = max_x - min_x, max_y - min_y
    # Centre the projected bounds on the image.
    rot = cam.matrix_world.to_3x3()
    shift = rot @ mathutils.Vector(((min_x + max_x) / 2, (min_y + max_y) / 2, 0))
    direction = rot @ mathutils.Vector((0, 0, -1))
    dist = max(width, height) * 4 + 50
    # The camera is still at the origin here, so camera-space bounds are relative to it.
    cam.location = shift - direction * dist
    cam_data.clip_start, cam_data.clip_end = 0.1, dist * 3
    cam_data.ortho_scale = max(width, height) * cfg['margin']
    aspect = width / height
    res = cfg['resolution']
    scene.render.resolution_x = res if aspect >= 1 else max(1, round(res * aspect))
    scene.render.resolution_y = res if aspect < 1 else max(1, round(res / aspect))
    scene.render.resolution_percentage = 100
    return shift


def add_turntable_camera(scene, root, cfg=CONFIG):
    """Fixed square camera that fits the model at every heading.

    The camera looks at the model's centre and the frame is sized to its bounding
    sphere, so the pivot stays at the exact centre of the canvas and rotating
    the sprite around its origin matches the prerendered turn.
    Returns (pivot, radius) in world units.
    """
    pts = _world_points(root)
    lo = mathutils.Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = mathutils.Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    pivot = (lo + hi) / 2
    # Heading rotates around the vertical axis through the pivot, so only the
    # horizontal distance changes. Height is already covered by the projection.
    radius = max(math.hypot(p.x - pivot.x, p.y - pivot.y) for p in pts)
    half_height = (hi.z - lo.z) / 2
    cam, cam_data = _new_camera(scene, cfg)
    direction = cam.matrix_world.to_3x3() @ mathutils.Vector((0, 0, -1))
    dist = radius * 8 + 50
    cam.location = pivot - direction * dist
    cam_data.clip_start, cam_data.clip_end = 0.1, dist * 3
    # Horizontal extent projects with sin(elevation) onto the screen's vertical axis;
    # add the height contribution of the model's thickness.
    sin_e, cos_e = math.sin(math.radians(cfg['elevation'])), math.cos(math.radians(cfg['elevation']))
    half_v = radius * sin_e + half_height * cos_e
    cam_data.ortho_scale = max(radius, half_v) * 2 * cfg['margin']
    if cfg.get('length_px'):
        # Fixed texel density instead of a fixed canvas: the ship's longest
        # horizontal side spans length_px texels, and the (even) canvas grows to fit.
        texels_per_m = cfg['length_px'] / max(hi.x - lo.x, hi.y - lo.y)
        cfg['resolution'] = 2 * math.ceil(cam_data.ortho_scale * texels_per_m / 2)
        cam_data.ortho_scale = cfg['resolution'] / texels_per_m
    scene.render.resolution_x = scene.render.resolution_y = cfg['resolution']
    scene.render.resolution_percentage = 100
    return pivot, radius


def detect_nozzles(root, cam_target, px_per_m):
    """Finds aft-facing thruster nozzles on a Synty ship at heading 0.

    Returns a list of dicts with `x`, `y`, `z`, `width` in untrimmed sprite texels
    relative to `cam_target` (`+y` is aft/down at heading 0, `+z` is up).
    """
    set_heading(root, 0)
    em_img = _tex('PolygonSciFiSpace_01_Emissive.png', 'Non-Color')
    ew, eh = em_img.size
    em_px = np.array(em_img.pixels[:], dtype=np.float32).reshape((eh, ew, 4))

    def collect_polys(use_emissive):
        polys = []
        for obj in [root] + list(root.children_recursive):
            if obj.type != 'MESH' or not obj.data.uv_layers:
                continue
            mesh = obj.data
            uv_layer = mesh.uv_layers.active.data
            mw = obj.matrix_world
            rot = mw.to_3x3()
            for poly in mesh.polygons:
                normal = (rot @ poly.normal).normalized()
                verts = [mw @ mesh.vertices[vi].co for vi in poly.vertices]
                center = sum(verts, mathutils.Vector()) / len(verts)
                if center.y >= 0.0:
                    continue
                uvs = [uv_layer[li].uv for li in poly.loop_indices]
                if use_emissive:
                    if normal.y >= -0.75:
                        continue
                    em = max(
                        float(em_px[min(eh - 1, max(0, int((u.y % 1.0) * eh))),
                                    min(ew - 1, max(0, int((u.x % 1.0) * ew))), :3].max())
                        for u in uvs
                    )
                    if em <= 0.5:
                        continue
                else:
                    if normal.y >= -0.85:
                        continue
                    u_avg = sum(u.x for u in uvs) / len(uvs)
                    v_avg = sum(u.y for u in uvs) / len(uvs)
                    if not (0.05 < u_avg < 0.238 and 0.225 < v_avg < 0.235):
                        continue
                vkeys = {(round(v.x, 2), round(v.y, 2), round(v.z, 2)) for v in verts}
                polys.append((center, verts, poly.area, vkeys))
        return polys

    raw = collect_polys(True) or collect_polys(False)
    visited = [False] * len(raw)
    clusters = []
    for i in range(len(raw)):
        if visited[i]:
            continue
        comp = [i]
        visited[i] = True
        vset = set(raw[i][3])
        changed = True
        while changed:
            changed = False
            for j in range(len(raw)):
                if not visited[j] and (vset & raw[j][3]):
                    visited[j] = True
                    comp.append(j)
                    vset |= raw[j][3]
                    changed = True
        area = sum(raw[k][2] for k in comp)
        if area <= 1e-5:
            continue
        c = sum((raw[k][0] * raw[k][2] for k in comp), mathutils.Vector()) / area
        xs = [v.x for k in comp for v in raw[k][1]]
        zs = [v.z for k in comp for v in raw[k][1]]
        w_m, h_m = max(xs) - min(xs), max(zs) - min(zs)
        if w_m >= 0.20:
            clusters.append((c, w_m, h_m, area))

    if any(cl[3] >= 0.05 for cl in clusters):
        clusters = [cl for cl in clusters if cl[3] >= 0.025]

    nozzles = []
    for c, w_m, h_m, _ in clusters:
        cy = c.y - 0.8 * min(w_m, h_m)
        nozzles.append(dict(
            x=round((c.x - cam_target.x) * px_per_m, 2),
            y=round(-(cy - cam_target.y) * px_per_m, 2),
            z=round((c.z - cam_target.z) * px_per_m, 2),
            width=round(w_m * px_per_m, 2),
        ))
    nozzles.sort(key=lambda n: (round(abs(n['x']), 1), n['x'], n['z']))
    return nozzles


def _blur2d(arr, sigma):
    if sigma <= 0:
        return arr
    radius = max(1, int(math.ceil(sigma * 3.0)))
    x = np.arange(-radius, radius + 1, dtype=np.float32)
    k = np.exp(-0.5 * (x / sigma) ** 2)
    k /= k.sum()
    pad_x = np.pad(arr, ((0, 0), (radius, radius)), mode='constant')
    horiz = sum(k[i] * pad_x[:, i:i + arr.shape[1]] for i in range(len(k)))
    pad_y = np.pad(horiz, ((radius, radius), (0, 0)), mode='constant')
    return sum(k[i] * pad_y[i:i + arr.shape[0], :] for i in range(len(k)))


def _apply_accent_glow(filepath, cfg):
    """Adds an incandescent core and warm orange hull light spill to emissive orange stripes."""
    glow_strength = cfg.get('accent_glow', 0.0)
    if glow_strength <= 0 or cfg.get('material_fn') is not None or cfg.get('tint'):
        return
    img = bpy.data.images.load(filepath)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape((h, w, 4))
    r, g, b, a = px[:, :, 0], px[:, :, 1], px[:, :, 2], px[:, :, 3]

    orangeness = (
        np.clip((r - 0.60) / 0.35, 0.0, 1.0)
        * np.clip((r - b - 0.35) / 0.40, 0.0, 1.0)
        * np.clip((g - 0.35) / 0.35, 0.0, 1.0)
        * a
    )
    if orangeness.max() <= 1e-4:
        bpy.data.images.remove(img)
        return

    scale = max(w, h) / 512.0
    core_mask = _blur2d(orangeness, max(0.8, 1.4 * scale)) * orangeness
    glow_tight = _blur2d(orangeness, max(1.2, 3.2 * scale))
    glow_wide = _blur2d(orangeness, max(2.5, 9.5 * scale))
    hull_bleed = (0.58 * glow_tight + 0.42 * glow_wide) * glow_strength

    r = np.clip(r + core_mask * 0.10, 0.0, 1.0)
    g = np.clip(g + core_mask * 0.16 * (0.5 + 0.5 * g), 0.0, 1.0)
    b = np.clip(b + core_mask * 0.03, 0.0, 1.0)

    non_orange = np.clip(1.0 - orangeness * 0.85, 0.0, 1.0)
    spill = hull_bleed * non_orange
    r = np.clip(r + spill * 0.80 * (1.0 - r * 0.45), 0.0, 1.0)
    g = np.clip(g + spill * 0.28 * (1.0 - g * 0.45), 0.0, 1.0)
    b = np.clip(b + spill * 0.02 * (1.0 - b * 0.45), 0.0, 1.0)

    outer_halo = _blur2d(orangeness, max(1.0, 2.2 * scale)) * (1.0 - a) * 0.50 * glow_strength
    outer_halo[0, :] = outer_halo[-1, :] = 0.0
    outer_halo[:, 0] = outer_halo[:, -1] = 0.0
    new_a = np.clip(a + outer_halo, 0.0, 1.0)
    denom = np.maximum(new_a, 1e-5)
    r = np.where(new_a > 1e-5, (r * a + 1.00 * outer_halo) / denom, 0.0)
    g = np.where(new_a > 1e-5, (g * a + 0.36 * outer_halo) / denom, 0.0)
    b = np.where(new_a > 1e-5, (b * a + 0.02 * outer_halo) / denom, 0.0)

    out_px = np.stack([r, g, b, new_a], axis=-1)
    img.pixels[:] = out_px.ravel()
    img.save()
    bpy.data.images.remove(img)


def render_model(model, out_path, **overrides):
    """One tight-cropped sprite at cfg['heading']."""
    cfg = dict(CONFIG, **overrides)
    scene = new_scene(cfg)
    root = import_model(scene, model, cfg)
    shift = add_camera(scene, root, cfg)
    add_lights(scene, cfg)
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    scene.render.filepath = out_path
    bpy.ops.render.render(write_still=True, scene=scene.name)
    _apply_accent_glow(out_path, cfg)
    if model.startswith('SM_Ship_'):
        px_per_m = max(scene.render.resolution_x, scene.render.resolution_y) / scene.camera.data.ortho_scale
        nozzles = detect_nozzles(root, shift, px_per_m)
        if nozzles:
            meta_path = os.path.splitext(out_path)[0] + '.json'
            with open(meta_path, 'w') as f:
                json.dump(dict(model=model, elevation=cfg['elevation'], nozzles=nozzles), f, indent=2)
                f.write('\n')
    return out_path


def render_turntable(model, out_dir, frames=32, **overrides):
    """`frames` square sprites, frame i heading = i * 360 / frames clockwise.

    Writes frame_000.png ... into out_dir and returns metadata for the sheet builder.
    """
    cfg = dict(CONFIG, **overrides)
    scene = new_scene(cfg)
    root = import_model(scene, model, cfg)
    pivot, radius = add_turntable_camera(scene, root, cfg)
    add_lights(scene, cfg)
    os.makedirs(out_dir, exist_ok=True)
    for i in range(frames):
        set_heading(root, i * 360.0 / frames)
        frame_path = os.path.join(out_dir, 'frame_%03d.png' % i)
        scene.render.filepath = frame_path
        bpy.ops.render.render(write_still=True, scene=scene.name)
        _apply_accent_glow(frame_path, cfg)
    set_heading(root, 0)
    meta = dict(model=model, frames=frames, size=cfg['resolution'], elevation=cfg['elevation'],
                world_radius=radius, palette=cfg['palette'])
    if model.startswith(('SM_Ship_', 'SM_Veh_')):
        px_per_m = max(scene.render.resolution_x, scene.render.resolution_y) / scene.camera.data.ortho_scale
        nozzles = detect_nozzles(root, pivot, px_per_m)
        if nozzles:
            meta['nozzles'] = nozzles
    return meta


# Harvester turntables (PrerenderedShips.cs), picked to echo the silhouette of the
# pixel-art hull each one replaces. length_px is the model's longest horizontal side
# in texels; at the game's 0.26 world units per texel that is about 1.2x the old
# hull's visible size, the same growth the Drifter got from 24 to ~29 units.
HARVESTER_SHIPS = dict(
    harvester_bomber01=dict(model='SM_Ship_Bomber_01', resolution=128),         # Drifter (was Scout), ~111 px long
    harvester_fighterheavy01=dict(model='SM_Ship_Fighter_Heavy_01', length_px=111),  # Seeker (was Fighter)
    harvester_stealth01=dict(model='SM_Ship_Stealth_01', length_px=118),        # Prospector (was Bomber)
    harvester_fighterheavy04=dict(model='SM_Ship_Fighter_Heavy_04', length_px=155),  # Trove Hunter (was Frigate)
    harvester_stealth05=dict(model='SM_Ship_Stealth_05', length_px=112),        # Rimrunner (was Torpedo Ship)
    harvester_drone_repair01=dict(model='SM_Veh_Drone_Repair_01', length_px=59),  # Drone (was Support Ship)
)


def render_harvester(name, raw_root, frames=32, **overrides):
    """Renders HARVESTER_SHIPS[name] to raw_root/name/frames/ plus meta.json.

    Then pack it with tools/build_prerender_sheet.py, e.g.
    python3 tools/build_prerender_sheet.py RawAssets/Prerender/<name> \\
        --out Content/Textures/Prerender/Turntables/<name>.png
    """
    spec = dict(HARVESTER_SHIPS[name])
    model = spec.pop('model')
    out_dir = os.path.join(raw_root, name)
    meta = render_turntable(model, os.path.join(out_dir, 'frames'), frames, **dict(spec, **overrides))
    with open(os.path.join(out_dir, 'meta.json'), 'w') as f:
        json.dump(meta, f, indent=2)
        f.write('\n')
    return meta


# ---- Turrets ---------------------------------------------------------------
#
# Synty turrets are a static base with a head (and barrels) parented on top. For
# a 2D game the base is drawn once and the head as a turntable that follows the
# aim. Both images share one square canvas centred on the turret's pivot: the
# point on the yaw axis at barrel height. The barrel axis therefore passes
# through the image centre in every frame, so game code can treat the muzzle as
# `pivot + aim direction * muzzle` like any flat 2D turret.

def _turret_parts(root, model):
    """(base objects, head objects) of an imported turret."""
    meshes = [o for o in root.children_recursive if o.type == 'MESH']
    base = [o for o in meshes if o.name.split('.')[0] == model]
    return base, [o for o in meshes if o not in base]


def render_turret(model, out_dir, frames=32, units_per_m=6.0, texel=0.19, **overrides):
    """Static base.png plus turntable frames/frame_NNN.png of the head.

    units_per_m: game (hull) units per metre of model; turrets are scaled up a lot
    from the ship's true scale so they read at gameplay zoom.
    texel: game units per pixel, the same density as the home base sprite, so a
    sprite drawn at scale `texel` (times the hull scale) matches the hull.
    Returns metadata; lengths are in game units (`*_units`) at the hull's scale.
    """
    cfg = dict(CONFIG, **overrides)
    scene = new_scene(cfg)
    root = import_model(scene, model, cfg)
    base, head = _turret_parts(root, model)
    barrels = [o for o in head if 'barrel' in o.name.lower()] or head

    def verts(objs):
        return [o.matrix_world @ v.co for o in objs for v in o.data.vertices]

    yaw_axis = root.matrix_world.translation        # Synty turrets turn on their origin
    barrel_pts = verts(barrels)
    pivot = mathutils.Vector((yaw_axis.x, yaw_axis.y,
                              (min(p.z for p in barrel_pts) + max(p.z for p in barrel_pts)) / 2))

    # Square canvas that holds base and head at every heading.
    all_pts = verts(base + head)
    radius = max(math.hypot(p.x - pivot.x, p.y - pivot.y) for p in all_pts)
    dz = max(abs(p.z - pivot.z) for p in all_pts)
    sin_e, cos_e = math.sin(math.radians(cfg['elevation'])), math.cos(math.radians(cfg['elevation']))
    half = max(radius, radius * sin_e + dz * cos_e) * cfg['margin']
    metres_per_px = texel / units_per_m
    size = 2 * math.ceil(half / metres_per_px)
    cfg['resolution'] = size

    cam, cam_data = _new_camera(scene, cfg)
    direction = cam.matrix_world.to_3x3() @ mathutils.Vector((0, 0, -1))
    dist = radius * 8 + 50
    cam.location = pivot - direction * dist
    cam_data.clip_start, cam_data.clip_end = 0.1, dist * 3
    cam_data.ortho_scale = size * metres_per_px
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    add_lights(scene, cfg)
    os.makedirs(os.path.join(out_dir, 'frames'), exist_ok=True)

    # Barrel geometry at heading 0 (barrel pointing to the top of the image, +Y).
    set_heading(root, 0)
    barrel_pts = verts(barrels)
    muzzle = max(p.y - pivot.y for p in barrel_pts)
    sides = sorted({round(o.matrix_world.translation.x - pivot.x, 4) for o in barrels})

    for obj in head:
        obj.hide_render = True
    scene.render.filepath = os.path.join(out_dir, 'base.png')
    bpy.ops.render.render(write_still=True, scene=scene.name)
    for obj in head:
        obj.hide_render = False
    for obj in base:
        obj.hide_render = True
    for i in range(frames):
        set_heading(root, i * 360.0 / frames)
        scene.render.filepath = os.path.join(out_dir, 'frames', 'frame_%03d.png' % i)
        bpy.ops.render.render(write_still=True, scene=scene.name)
    set_heading(root, 0)
    return dict(model=model, frames=frames, size=size, elevation=cfg['elevation'], palette=cfg['palette'],
                texel=texel, units_per_m=units_per_m,
                muzzle_units=muzzle * units_per_m,
                barrel_offsets_units=[s * units_per_m for s in sides])


# ---- Planets and rocks -----------------------------------------------------
#
# Synty's planet and asteroid materials use custom Unity shaders, so these are
# approximations in the same spirit: flat-shaded low-poly spheres, two colours
# split by one of the pack's planet masks, a fresnel rim glow, and optionally an
# emissive second colour (glowing crystal, lava) and a cloud layer.
#
#   render_model('SM_Env_Planet_01', out, material_fn=planet_material,
#                planet=dict(base=(...), second=(...), mask='land', glow=0, rim=(...)))

def _sphere_tex(nodes, links, coords, image, colorspace='Non-Color', rotate=0.0, tilt=65.0):
    tex = nodes.new('ShaderNodeTexImage')
    tex.image = _tex(image, colorspace)
    tex.projection = 'SPHERE'
    # Sphere projection puts its poles on Z. Tilt world space so the spin axis is
    # the screen's vertical as seen from the camera (elevation degrees above the
    # horizon); `rotate` spins the surface around that axis.
    mapping = nodes.new('ShaderNodeMapping')
    mapping.inputs['Rotation'].default_value = (math.radians(tilt), 0, rotate)
    links.new(coords, mapping.inputs['Vector'])
    coords = mapping.outputs['Vector']
    links.new(coords, tex.inputs['Vector'])
    return tex


def planet_material(cfg=CONFIG, detail_scale=None):
    p = cfg['planet']
    mat = bpy.data.materials.new('SyntyPlanet')
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    nodes.clear()
    out = nodes.new('ShaderNodeOutputMaterial')
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = p.get('roughness', 0.8)
    spin = p.get('spin', 0.0)      # radians; turn it per frame for a rotating planet
    if cfg.get('planet_space'):
        # render_planet_spin: coordinates in an object whose Z is already the spin
        # axis, so the surface turns with that object (and the mesh parented to it).
        texcoord = nodes.new('ShaderNodeTexCoord')
        texcoord.object = cfg['planet_space']
        coords = texcoord.outputs['Object']
        tilt = 0.0
    else:
        coords = nodes.new('ShaderNodeNewGeometry').outputs['Position']   # world space; planets sit at the origin
        tilt = cfg['elevation']

    mask_image = {'land': 'Planet_Land_01_Mask.png', 'lines': 'PolygonSciFiSpace_PlanetLines_01.png'}[p.get('mask', 'land')]
    mask = _sphere_tex(nodes, links, coords, mask_image, rotate=spin + p.get('mask_rotate', 0.0), tilt=tilt)
    mix = nodes.new('ShaderNodeMix')
    mix.data_type = 'RGBA'
    mix.inputs['A'].default_value = (*p['base'], 1)
    mix.inputs['B'].default_value = (*p['second'], 1)
    links.new(mask.outputs['Color'], mix.inputs['Factor'])
    color = mix.outputs['Result']

    if p.get('clouds'):
        clouds = _sphere_tex(nodes, links, coords, 'Planet_Cloud_01_Mask.png', rotate=spin * 1.3 + 1.3, tilt=tilt)
        cmix = nodes.new('ShaderNodeMix')
        cmix.data_type = 'RGBA'
        cmix.inputs['B'].default_value = (*p.get('cloud_color', (1, 1, 1)), 1)
        fac = nodes.new('ShaderNodeMath')
        fac.operation = 'MULTIPLY'
        fac.inputs[1].default_value = p['clouds']
        links.new(clouds.outputs['Color'], fac.inputs[0])
        links.new(fac.outputs['Value'], cmix.inputs['Factor'])
        links.new(color, cmix.inputs['A'])
        color = cmix.outputs['Result']
    links.new(color, bsdf.inputs['Base Color'])

    # Glowing second colour (crystal fields, lava seas).
    if p.get('glow'):
        glow = nodes.new('ShaderNodeMix')
        glow.data_type = 'RGBA'
        glow.inputs['A'].default_value = (0, 0, 0, 1)
        glow.inputs['B'].default_value = (*p.get('glow_color', p['second']), 1)
        links.new(mask.outputs['Color'], glow.inputs['Factor'])
        links.new(glow.outputs['Result'], bsdf.inputs['Emission Color'])
        bsdf.inputs['Emission Strength'].default_value = p['glow']

    # Fresnel rim, added on top as emission so it also lights the night side.
    weight = nodes.new('ShaderNodeLayerWeight')
    weight.inputs['Blend'].default_value = p.get('rim_width', 0.35)
    ramp = nodes.new('ShaderNodeMath')
    ramp.operation = 'POWER'
    ramp.inputs[1].default_value = p.get('rim_power', 2.0)
    links.new(weight.outputs['Facing'], ramp.inputs[0])
    rim = nodes.new('ShaderNodeEmission')
    rim.inputs['Color'].default_value = (*p.get('rim', (0.6, 0.45, 1.0)), 1)
    links.new(ramp.outputs['Value'], rim.inputs['Strength'])
    rim_gain = nodes.new('ShaderNodeMath')
    rim_gain.operation = 'MULTIPLY'
    rim_gain.inputs[1].default_value = p.get('rim_strength', 2.0)
    links.new(ramp.outputs['Value'], rim_gain.inputs[0])
    links.new(rim_gain.outputs['Value'], rim.inputs['Strength'])
    add = nodes.new('ShaderNodeAddShader')
    links.new(bsdf.outputs['BSDF'], add.inputs[0])
    links.new(rim.outputs['Emission'], add.inputs[1])
    links.new(add.outputs['Shader'], out.inputs['Surface'])
    return mat


def rock_material(cfg=CONFIG, detail_scale=None):
    """Low-poly rock with glowing ore veins (`rock` dict: color, vein_color, veins, glow)."""
    r = cfg['rock']
    mat = bpy.data.materials.new('OreRock')
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    nodes.clear()
    out = nodes.new('ShaderNodeOutputMaterial')
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = 0.9
    links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    coords = nodes.new('ShaderNodeTexCoord').outputs['Object']
    noise = nodes.new('ShaderNodeTexVoronoi')
    noise.feature = 'F1'
    noise.inputs['Scale'].default_value = r.get('vein_scale', 0.06)
    links.new(coords, noise.inputs['Vector'])
    pick = nodes.new('ShaderNodeMath')            # a share of the cells become ore
    pick.operation = 'LESS_THAN'
    pick.inputs[1].default_value = r.get('veins', 0.35)
    links.new(noise.outputs['Color'], pick.inputs[0])
    color = nodes.new('ShaderNodeMix')
    color.data_type = 'RGBA'
    color.inputs['A'].default_value = (*r['color'], 1)
    color.inputs['B'].default_value = (*r['vein_color'], 1)
    links.new(pick.outputs['Value'], color.inputs['Factor'])
    links.new(color.outputs['Result'], bsdf.inputs['Base Color'])
    glow = nodes.new('ShaderNodeMix')
    glow.data_type = 'RGBA'
    glow.inputs['A'].default_value = (0, 0, 0, 1)
    glow.inputs['B'].default_value = (*r['vein_color'], 1)
    links.new(pick.outputs['Value'], glow.inputs['Factor'])
    links.new(glow.outputs['Result'], bsdf.inputs['Emission Color'])
    bsdf.inputs['Emission Strength'].default_value = r.get('glow', 3.0)
    return mat


# The planet the game mines (GameScreen.Planet.cs): glowing cyan crystal fields on
# dark rock, rendered with key_rot=(70, 0, -50), key_energy=3.5, ambient_strength=0.12.
CRYSTAL_PLANET = dict(model='SM_Env_Planet_01', planet=dict(
    base=(0.10, 0.06, 0.18), second=(0.05, 0.55, 0.75), mask='land', glow=0.6, glow_color=(0.05, 0.6, 1.0),
    rim=(0.6, 0.35, 1.0), rim_strength=0.9, rim_power=4.0, rim_width=0.25))


def render_planet_spin(model, out_dir, frames=77, size=288, disc_radius=31.0 / 96.0, **overrides):
    """One revolution of a planet as `frames` square images, frames/frame_NNN.png.

    The planet turns about the screen's vertical axis with its surface moving left
    to right, frame i turned i * 360 / frames degrees, which is the sphere the
    game's planet cracks are drawn on (GameScreen.PlanetCracks.cs). The disc is
    centred with a radius of disc_radius * size pixels; the game's planet maths
    assumes 31 texels of a 96-texel frame. Lights stay fixed while it turns.
    """
    cfg = dict(CONFIG, **overrides)
    cfg['resolution'] = size
    scene = new_scene(cfg)
    # Tilt Z onto the camera's up axis, then spin about it. The texture lives in
    # the spin object's space, so surface and facets turn together.
    tilt = bpy.data.objects.new('PlanetTilt', None)
    tilt.rotation_euler = (-math.radians(cfg['elevation']), 0, 0)
    spin = bpy.data.objects.new('PlanetSpin', None)
    for obj in (tilt, spin):
        scene.collection.objects.link(obj)
    spin.parent = tilt
    cfg['planet_space'] = spin
    root = import_model(scene, model, dict(cfg, material_fn=planet_material))
    pts = _world_points(root)
    center = sum(pts, mathutils.Vector()) / len(pts)
    root.location = -center
    root.parent = spin
    bpy.context.view_layer.update()
    radius = max((p - center).length for p in pts)

    cam, cam_data = _new_camera(scene, cfg)
    direction = cam.matrix_world.to_3x3() @ mathutils.Vector((0, 0, -1))
    dist = radius * 8 + 50
    cam.location = -direction * dist
    cam_data.clip_start, cam_data.clip_end = 0.1, dist * 3
    # The low-poly outline sits inside the furthest vertex. Fit its mean radius
    # (average of the projected extent over 64 directions) to the disc instead.
    bpy.context.view_layer.update()
    to_cam = cam.matrix_world.to_3x3().inverted()
    flat = [to_cam @ p for p in _world_points(root)]
    outline = sum(max(p.x * math.cos(a) + p.y * math.sin(a) for p in flat)
                  for a in (i * 2 * math.pi / 64 for i in range(64))) / 64
    cam_data.ortho_scale = outline / disc_radius
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    add_lights(scene, cfg)
    os.makedirs(os.path.join(out_dir, 'frames'), exist_ok=True)
    for i in range(frames):
        spin.rotation_euler = (0, 0, i * 2 * math.pi / frames)
        scene.render.filepath = os.path.join(out_dir, 'frames', 'frame_%03d.png' % i)
        bpy.ops.render.render(write_still=True, scene=scene.name)
    return dict(model=model, frames=frames, size=size, elevation=cfg['elevation'], disc_radius=disc_radius,
                planet=cfg['planet'])


# ---- Gem pickups -----------------------------------------------------------
#
# World gems are crystal-ore fragments blasted off the crystal planet: chunks of
# dark planetary crust rock (Synty POLYGON Generic / Sci-Fi Space rocks and
# asteroids) with faceted crystal shards erupting out of them. GemShader.fx tints
# the crystal shards per gem type (colour = tint * R + G) while keeping the dark
# rock base and white inner highlights. render_plasma_orb renders four distinct
# 3D cluster variants under the same 65-degree camera and top-left key light as
# the ships and planet; tools/build_gem_surface.py adds the outer glow halo and
# packs them into a 2x2 sheet (Content/Textures/Gems/plasma_orb.png).

SYNTY_GEN = '/home/johan/My project/Assets/Synty/PolygonGeneric/'


def _orb_material(name, color, emission=0.0, metallic=0.0, roughness=0.5, flat=False):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    nodes.clear()
    out = nodes.new('ShaderNodeOutputMaterial')
    if flat:
        # Unlit, for masks: exactly `color` in the image.
        shader = nodes.new('ShaderNodeEmission')
        shader.inputs['Color'].default_value = (*color, 1)
        links.new(shader.outputs['Emission'], out.inputs['Surface'])
        return mat
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Emission Color'].default_value = (*color, 1)
    bsdf.inputs['Emission Strength'].default_value = emission
    links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    return mat


def _import_unit_mesh(scene, folder, name, parent, loc=(0, 0, 0), rot_deg=(0, 0, 0), scale=(1, 1, 1)):
    before = set(bpy.data.objects)
    bpy.ops.wm.fbx_import(filepath=folder + 'Models/' + name + '.fbx')
    new = [o for o in bpy.data.objects if o not in before]
    meshes = []
    for o in new:
        if o.type != 'MESH':
            bpy.data.objects.remove(o)
            continue
        for c in list(o.users_collection):
            c.objects.unlink(o)
        scene.collection.objects.link(o)
        pts = [v.co.copy() for v in o.data.vertices]
        lo = mathutils.Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        hi = mathutils.Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        center = (lo + hi) * 0.5
        span = max(hi - lo)
        for v in o.data.vertices:
            v.co = (v.co - center) / max(span, 1e-6)
        o.data.update()
        for p in o.data.polygons:
            p.use_smooth = False
        o.parent = parent
        o.location = loc
        o.rotation_euler = [math.radians(a) for a in rot_deg]
        o.scale = scale
        meshes.append(o)
    return meshes


def _make_crystal_shard(scene, parent, radius=0.38, length=1.6, sides=6, tip_ratio=0.40, seed=0.0,
                        loc=(0, 0, 0), rot_deg=(0, 0, 0), scale=(1, 1, 1)):
    import bmesh
    mesh = bpy.data.meshes.new('CrystalShard')
    bm = bmesh.new()
    verts_bot, verts_mid = [], []
    mid_z = length * (1.0 - tip_ratio) - length * 0.5
    bot_z = -length * 0.5
    top_z = length * 0.5
    for i in range(sides):
        a = i * 2 * math.pi / sides
        r_mod = 1.0 + 0.14 * math.cos(i * 2.0 + 0.4 + seed)
        x, y = math.cos(a) * radius * r_mod, math.sin(a) * radius * r_mod
        verts_bot.append(bm.verts.new((x * 0.68, y * 0.68, bot_z)))
        verts_mid.append(bm.verts.new((x, y, mid_z + 0.08 * radius * math.sin(i * 1.7 + seed))))
    tip = bm.verts.new((0.0, 0.0, top_z))
    base_center = bm.verts.new((0.0, 0.0, bot_z - 0.18 * radius))
    for i in range(sides):
        j = (i + 1) % sides
        bm.faces.new([verts_bot[i], verts_bot[j], verts_mid[j], verts_mid[i]])
        bm.faces.new([verts_mid[i], verts_mid[j], tip])
        bm.faces.new([verts_bot[j], verts_bot[i], base_center])
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    for p in mesh.polygons:
        p.use_smooth = False
    obj = bpy.data.objects.new('CrystalShard', mesh)
    scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = loc
    obj.rotation_euler = [math.radians(a) for a in rot_deg]
    obj.scale = scale
    return obj


CRYSTAL_ORE_VARIANTS = [
    dict(
        rocks=[
            (SYNTY_GEN, 'SM_Gen_Env_Rock_04', (-0.16, -0.22, -0.16), (25, -20, 35), (1.12, 1.02, 0.90)),
            (SYNTY_GEN, 'SM_Gen_Env_Rock_02', (0.26, 0.20, -0.18), (-20, 25, 140), (0.92, 0.85, 0.78)),
            (SYNTY_GEN, 'SM_Gen_Env_Rock_09', (0.30, -0.20, -0.16), (15, 30, -45), (0.76, 0.70, 0.68)),
        ],
        crystals=[
            dict(radius=0.46, length=1.70, sides=6, tip_ratio=0.42, seed=0.0, loc=(0.0, 0.04, 0.22), rot_deg=(-28, -18, 15), scale=(1.08, 0.95, 1.05)),
            dict(radius=0.35, length=1.45, sides=5, tip_ratio=0.44, seed=1.1, loc=(0.26, 0.18, 0.08), rot_deg=(-48, 38, -28), scale=(1.0, 0.90, 1.0)),
            dict(radius=0.34, length=1.40, sides=6, tip_ratio=0.40, seed=2.2, loc=(-0.28, -0.12, 0.06), rot_deg=(42, -44, 35), scale=(0.98, 0.88, 1.0)),
            dict(radius=0.28, length=1.18, sides=5, tip_ratio=0.42, seed=3.3, loc=(0.18, -0.26, 0.04), rot_deg=(48, 28, -50), scale=(0.92, 0.86, 1.0)),
        ],
        crystal_meshes=[
            (SYNTY_GEN, 'SM_Gen_Env_Rock_08', (-0.05, 0.12, 0.10), (35, -15, 55), (0.85, 0.80, 0.78)),
        ],
    ),
    dict(
        rocks=[
            (SYNTY, 'SM_Env_Astroid_01', (0.14, -0.20, -0.16), (18, 22, -30), (1.08, 1.00, 0.88)),
            (SYNTY_GEN, 'SM_Gen_Env_Rock_06', (-0.28, 0.16, -0.18), (-15, -28, 110), (0.95, 0.88, 0.80)),
        ],
        crystals=[
            dict(radius=0.44, length=1.68, sides=6, tip_ratio=0.40, seed=0.7, loc=(-0.10, 0.10, 0.20), rot_deg=(-36, -32, 28), scale=(1.05, 0.92, 1.05)),
            dict(radius=0.40, length=1.55, sides=6, tip_ratio=0.42, seed=1.9, loc=(0.22, -0.04, 0.16), rot_deg=(28, 42, -35), scale=(1.02, 0.90, 1.0)),
            dict(radius=0.32, length=1.32, sides=5, tip_ratio=0.45, seed=2.8, loc=(-0.22, -0.24, 0.06), rot_deg=(50, -24, 18), scale=(0.94, 0.88, 1.0)),
            dict(radius=0.29, length=1.20, sides=5, tip_ratio=0.40, seed=4.1, loc=(0.08, 0.28, 0.05), rot_deg=(-52, 15, -12), scale=(0.90, 0.85, 1.0)),
        ],
        crystal_meshes=[
            (SYNTY_GEN, 'SM_Gen_Env_Rock_10', (0.04, -0.06, 0.12), (-25, 20, -40), (0.86, 0.82, 0.80)),
        ],
    ),
    dict(
        rocks=[
            (SYNTY_GEN, 'SM_Gen_Env_Rock_03', (-0.26, -0.16, -0.16), (30, -18, 65), (1.06, 0.98, 0.86)),
            (SYNTY_GEN, 'SM_Gen_Env_Rock_04', (0.24, -0.20, -0.17), (12, 28, -55), (1.02, 0.94, 0.84)),
        ],
        crystals=[
            dict(radius=0.47, length=1.72, sides=6, tip_ratio=0.42, seed=1.4, loc=(0.06, 0.08, 0.22), rot_deg=(-32, 22, -18), scale=(1.08, 0.96, 1.05)),
            dict(radius=0.39, length=1.52, sides=5, tip_ratio=0.44, seed=2.5, loc=(-0.26, 0.14, 0.14), rot_deg=(-38, -34, 32), scale=(1.02, 0.90, 1.0)),
            dict(radius=0.35, length=1.38, sides=6, tip_ratio=0.40, seed=3.6, loc=(0.02, -0.24, 0.10), rot_deg=(38, -12, 15), scale=(0.96, 0.88, 1.0)),
            dict(radius=0.30, length=1.24, sides=5, tip_ratio=0.42, seed=4.7, loc=(0.30, -0.06, 0.06), rot_deg=(25, 42, -38), scale=(0.92, 0.86, 1.0)),
        ],
        crystal_meshes=[
            (SYNTY_GEN, 'SM_Gen_Env_Rock_08', (-0.04, -0.02, 0.12), (28, -18, 45), (0.88, 0.84, 0.82)),
        ],
    ),
    dict(
        rocks=[
            (SYNTY_GEN, 'SM_Gen_Env_Rock_09', (0.24, -0.14, -0.16), (-18, 26, -120), (1.06, 0.98, 0.88)),
            (SYNTY_GEN, 'SM_Gen_Env_Rock_02', (-0.22, -0.22, -0.17), (22, -24, 45), (1.02, 0.94, 0.84)),
        ],
        crystals=[
            dict(radius=0.46, length=1.70, sides=6, tip_ratio=0.42, seed=0.4, loc=(-0.04, 0.06, 0.22), rot_deg=(-26, -24, 22), scale=(1.08, 0.95, 1.05)),
            dict(radius=0.38, length=1.48, sides=6, tip_ratio=0.42, seed=1.6, loc=(0.24, 0.14, 0.14), rot_deg=(-34, 32, -24), scale=(1.0, 0.90, 1.0)),
            dict(radius=0.36, length=1.44, sides=5, tip_ratio=0.44, seed=2.9, loc=(-0.30, -0.06, 0.10), rot_deg=(24, -46, 38), scale=(0.98, 0.88, 1.0)),
            dict(radius=0.30, length=1.22, sides=5, tip_ratio=0.40, seed=3.8, loc=(0.14, -0.26, 0.06), rot_deg=(46, 24, -42), scale=(0.92, 0.86, 1.0)),
        ],
        crystal_meshes=[
            (SYNTY_GEN, 'SM_Gen_Env_Rock_10', (0.02, 0.02, 0.12), (32, -20, 65), (0.86, 0.80, 0.78)),
        ],
    ),
]


def render_plasma_orb(out_dir, resolution=50, core_glow=0.15, **overrides):
    """Renders four planetary crystal-ore cluster variants (v0..v3/shade.png and
    mask.png, plus shade.png/mask.png in out_dir) tightly framed at `resolution` px."""
    sizes = []
    for idx, spec in enumerate(CRYSTAL_ORE_VARIANTS):
        cfg = dict(CONFIG, samples=32, elevation=65.0, margin=1.06,
                   key_rot=(32, 0, -35), key_energy=3.8,
                   rim_rot=(115, 0, 145), rim_energy=4.2,
                   fill_rot=(65, 0, 110), fill_energy=1.1,
                   ambient_strength=0.45)
        cfg.update(overrides)
        cfg['resolution'] = resolution
        scene = new_scene(cfg)
        root = bpy.data.objects.new(f'CrystalOre_{idx}', None)
        scene.collection.objects.link(root)
        rocks = []
        for folder, name, loc, rot, sc in spec['rocks']:
            rocks += _import_unit_mesh(scene, folder, name, root, loc=loc, rot_deg=rot, scale=sc)
        crystals = []
        for cspec in spec['crystals']:
            crystals.append(_make_crystal_shard(scene, root, **cspec))
        for folder, name, loc, rot, sc in spec['crystal_meshes']:
            crystals += _import_unit_mesh(scene, folder, name, root, loc=loc, rot_deg=rot, scale=sc)
        bpy.context.view_layer.update()
        add_camera(scene, root, cfg)
        add_lights(scene, cfg)
        vdir = os.path.join(out_dir, f'v{idx}')
        os.makedirs(vdir, exist_ok=True)
        passes = {
            'shade.png': (_orb_material('CrystalMat', (0.62, 0.62, 0.62), emission=core_glow, roughness=0.25),
                          _orb_material('RockMat', (0.35, 0.30, 0.48), metallic=0.1, roughness=0.85)),
            'mask.png': (_orb_material('CrystalMask', (1, 1, 1), flat=True),
                         _orb_material('RockMask', (0, 0, 0), flat=True)),
        }
        for name, (c_mat, r_mat) in passes.items():
            for o in crystals:
                o.data.materials.clear()
                o.data.materials.append(c_mat)
            for o in rocks:
                o.data.materials.clear()
                o.data.materials.append(r_mat)
            scene.render.filepath = os.path.join(vdir, name)
            bpy.ops.render.render(write_still=True, scene=scene.name)
            if idx == 0:
                scene.render.filepath = os.path.join(out_dir, name)
                bpy.ops.render.render(write_still=True, scene=scene.name)
        sizes.append([scene.render.resolution_x, scene.render.resolution_y])
    return dict(resolution=resolution, variants=len(CRYSTAL_ORE_VARIANTS), sizes=sizes,
                elevation=cfg['elevation'], core_glow=core_glow)


# ---- Engine Boosters -------------------------------------------------------
#
# Renders an 8-frame horizontal engine plume strip from Synty's FX_Flame_Booster_Round
# assets (Models/FX/SM_Flame_Mesh_New.fbx, Textures/FX/Gradient_Boost_01.png, and
# Textures/FX/Sphere_Glow_01.png). Each square frame places the nozzle exit at the
# canvas centre (size/2, size/2) with the exhaust shooting downward (+Y), matching
# FleetAtlas engine strip conventions.

def render_booster_strip(out_path, frames=8, size=96):
    """Writes an `frames * size` x `size` RGBA strip of Synty's cyan thruster plume."""
    scene = new_scene(CONFIG)
    before = set(bpy.data.objects)
    bpy.ops.wm.fbx_import(filepath=SYNTY + 'Models/FX/SM_Flame_Mesh_New.fbx')
    flame_obj = next(o for o in bpy.data.objects if o not in before and o.type == 'MESH')
    pts = [flame_obj.matrix_world @ v.co for v in flame_obj.data.vertices]
    ys = sorted({round(p.y, 4) for p in pts})
    y0, y1 = min(ys), max(ys)
    span_y = max(y1 - y0, 1e-6)
    profile_s, profile_r = [], []
    for yv in ys:
        ring = [math.hypot(p.x, p.z) for p in pts if abs(p.y - yv) < 0.01]
        profile_s.append((yv - y0) / span_y)
        profile_r.append(max(ring))
    max_r = max(profile_r)
    profile_s = np.array(profile_s, dtype=np.float32)
    profile_r = np.array([r / max_r for r in profile_r], dtype=np.float32)

    grad_img = _tex('FX/Gradient_Boost_01.png', 'Non-Color')
    gw, gh = grad_img.size
    grad_px = np.array(grad_img.pixels[:], dtype=np.float32).reshape((gh, gw, 4))[::-1, :, 0]

    glow_img = _tex('FX/Sphere_Glow_01.png', 'Non-Color')
    sw, sh = glow_img.size
    glow_px = np.array(glow_img.pixels[:], dtype=np.float32).reshape((sh, sw, 4))[::-1, :, 3]

    def sample_bilinear(arr, u, v):
        h, w = arr.shape
        x = np.clip(u, 0.0, 0.9999) * (w - 1)
        y = np.clip(v, 0.0, 0.9999) * (h - 1)
        x0 = np.floor(x).astype(np.int32)
        y0_i = np.floor(y).astype(np.int32)
        x1 = np.minimum(x0 + 1, w - 1)
        y1_i = np.minimum(y0_i + 1, h - 1)
        fx, fy = x - x0, y - y0_i
        return (arr[y0_i, x0] * (1 - fx) * (1 - fy) + arr[y0_i, x1] * fx * (1 - fy)
                + arr[y1_i, x0] * (1 - fx) * fy + arr[y1_i, x1] * fx * fy)

    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    nx = (xx - size * 0.5) / (size * 0.5)
    ny = (yy - size * 0.5) / (size * 0.5)

    strip = np.zeros((size, size * frames, 4), dtype=np.float32)
    for f in range(frames):
        phase = f / float(frames)
        ang = phase * 2.0 * math.pi
        len_scale = 1.0 + 0.12 * math.sin(ang) + 0.05 * math.cos(2.0 * ang + 0.6)
        wid_scale = 1.0 - 0.05 * math.sin(ang) + 0.04 * math.sin(2.0 * ang)
        plume_len = 0.78 * len_scale
        nozzle_rad = 0.16 * wid_scale

        s = ny / plume_len
        s_clamped = np.clip(s, 0.0, 1.0)
        mesh_r = np.interp(s_clamped, profile_s, profile_r) * nozzle_rad
        entry = np.where(s < 0.0, np.exp(-0.5 * (ny / (0.045 * wid_scale)) ** 2), 1.0)
        tail = np.where(s > 1.0, 0.0, 1.0)
        r_norm = np.abs(nx) / np.maximum(mesh_r, 1e-4)

        u_coord = (0.5 + 0.35 * (nx / np.maximum(mesh_r, 1e-3)) + f * 0.125) % 1.0
        v_coord = np.clip(s_clamped * 0.92 + 0.04, 0.0, 1.0)
        g_streak = sample_bilinear(grad_px, u_coord, v_coord)
        g_streak2 = sample_bilinear(grad_px, (u_coord + 0.43) % 1.0,
                                    np.clip((s_clamped * 1.3 - phase) % 1.0, 0.0, 1.0))
        tex_mod = 0.72 + 0.28 * (0.65 * g_streak + 0.35 * g_streak2)

        outer_rad = np.clip(1.0 - r_norm ** 1.8, 0.0, 1.0) ** 1.3
        outer_long = np.maximum(0.0, np.sin(s_clamped * math.pi)) ** 0.55 * (1.0 - 0.45 * s_clamped)
        outer = outer_rad * outer_long * entry * tail * tex_mod

        mid_r = np.abs(nx) / np.maximum(mesh_r * 0.68, 1e-4)
        mid_rad = np.clip(1.0 - mid_r ** 2.0, 0.0, 1.0) ** 1.4
        mid_long = (1.0 - s_clamped ** 1.25) * np.clip(s_clamped / 0.06, 0.35, 1.0)
        diamond = 0.85 + 0.15 * np.cos((s_clamped * 3.2 - phase) * 2.0 * math.pi)
        mid = mid_rad * mid_long * entry * tail * diamond * tex_mod

        core_s = np.clip(ny / (plume_len * 0.62), 0.0, 1.0)
        core_r = np.abs(nx) / np.maximum(mesh_r * 0.38, 1e-4)
        core_rad = np.clip(1.0 - core_r ** 2.2, 0.0, 1.0) ** 1.5
        core_long = (1.0 - core_s ** 1.4) * (1.0 if f % 2 == 0 else 0.94)
        core = core_rad * core_long * entry * (ny >= -0.04) * (core_s < 1.0)

        gu = np.clip(0.5 + nx / (nozzle_rad * 2.5), 0.0, 1.0)
        gv = np.clip(0.5 + (ny - 0.015) / (nozzle_rad * 1.9), 0.0, 1.0)
        flare = sample_bilinear(glow_px, gu, gv) * (0.90 + 0.10 * math.cos(ang))

        hu = np.clip(0.5 + nx / (nozzle_rad * 3.4), 0.0, 1.0)
        hv = np.clip(0.5 + (ny - plume_len * 0.25) / (plume_len * 0.72), 0.0, 1.0)
        halo = sample_bilinear(glow_px, hu, hv) * 0.42

        c_deep = np.array([0.02, 0.45, 1.00], dtype=np.float32)
        c_cyan = np.array([0.08, 0.88, 1.00], dtype=np.float32)
        c_core = np.array([0.90, 0.99, 1.00], dtype=np.float32)

        light_rgb = (
            halo[..., None] * c_deep * 0.55
            + outer[..., None] * c_deep * 0.95
            + mid[..., None] * c_cyan * 1.10
            + flare[..., None] * (0.55 * c_cyan + 0.45 * c_core) * 1.05
            + core[..., None] * c_core * 1.35
        )
        light_rgb = 1.0 - np.exp(-light_rgb * 1.55)
        edge = np.clip((1.0 - np.maximum(np.abs(nx), np.abs(ny))) / 0.08, 0.0, 1.0)
        light_rgb *= edge[..., None]
        alpha = np.clip(np.max(light_rgb, axis=-1), 0.0, 1.0)
        rgb = np.where(alpha[..., None] > 1e-4, np.clip(light_rgb / np.maximum(alpha[..., None], 1e-4), 0.0, 1.0), 0.0)

        frame_rgba = np.dstack([rgb, alpha])
        strip[:, f * size:(f + 1) * size, :] = frame_rgba

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    out_img = bpy.data.images.new('BoosterStrip', width=size * frames, height=size, alpha=True)
    out_img.filepath_raw = out_path
    out_img.file_format = 'PNG'
    out_img.pixels[:] = strip[::-1, :, :].ravel()
    out_img.save()
    bpy.data.images.remove(out_img)
    return out_path


