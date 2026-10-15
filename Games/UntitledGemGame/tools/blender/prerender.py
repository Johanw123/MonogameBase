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
import math
import os

import bpy
import mathutils

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

    # Panel-line detail, projected in object space so it ignores the UV layout.
    detail = nodes.new('ShaderNodeTexImage')
    detail.image = _tex('PolygonSciFiSpace_Ship_Mask_04.png', 'Non-Color')
    detail.projection = 'BOX'
    detail.projection_blend = 0.2
    mapping = nodes.new('ShaderNodeMapping')
    mapping.inputs['Scale'].default_value = (detail_scale,) * 3
    links.new(uv.outputs['Object'], mapping.inputs['Vector'])
    links.new(mapping.outputs['Vector'], detail.inputs['Vector'])

    mix = nodes.new('ShaderNodeMix')
    mix.data_type = 'RGBA'
    mix.blend_type = 'MULTIPLY'
    mix.inputs['Factor'].default_value = cfg['detail']
    links.new(base.outputs['Color'], mix.inputs['A'])
    links.new(detail.outputs['Color'], mix.inputs['B'])
    color = mix.outputs['Result']

    # Optional tint for the palette's grey metal: recolour (hue and saturation of
    # `tint`, keeping the shading) where the palette is unsaturated, then scale the
    # brightness by `tint_value`. Coloured accents keep their colour.
    if cfg.get('tint'):
        hsv = nodes.new('ShaderNodeSeparateColor')
        hsv.mode = 'HSV'
        links.new(base.outputs['Color'], hsv.inputs['Color'])
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
    scene.render.resolution_x = scene.render.resolution_y = cfg['resolution']
    scene.render.resolution_percentage = 100
    return pivot, radius


def render_model(model, out_path, **overrides):
    """One tight-cropped sprite at cfg['heading']."""
    cfg = dict(CONFIG, **overrides)
    scene = new_scene(cfg)
    root = import_model(scene, model, cfg)
    add_camera(scene, root, cfg)
    add_lights(scene, cfg)
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    scene.render.filepath = out_path
    bpy.ops.render.render(write_still=True, scene=scene.name)
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
        scene.render.filepath = os.path.join(out_dir, 'frame_%03d.png' % i)
        bpy.ops.render.render(write_still=True, scene=scene.name)
    set_heading(root, 0)
    return dict(model=model, frames=frames, size=cfg['resolution'], elevation=cfg['elevation'],
                world_radius=radius, palette=cfg['palette'])


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
# The game draws every gem from one image and tints it per gem type in
# GemShader.fx (colour = tint * R + G). The plasma orb is built from primitives:
# a faceted energy core, which takes the tint, inside a tilted metal ring that
# stays grey. render_plasma_orb writes the lighting and a core mask on one canvas;
# tools/build_gem_surface.py packs them into the shader's channels.

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


def render_plasma_orb(out_dir, resolution=60, core_glow=0.1, **overrides):
    """shade.png (the orb lit, with a grey core) and mask.png (white where the
    core shows, black on the ring), both tightly framed on the same canvas.
    resolution is the longest side in pixels."""
    cfg = dict(CONFIG, samples=32)
    cfg.update(overrides)
    cfg['resolution'] = resolution
    scene = new_scene(cfg)
    root = bpy.data.objects.new('Orb', None)
    scene.collection.objects.link(root)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1.0)
    core = bpy.context.active_object
    bpy.ops.mesh.primitive_torus_add(major_radius=1.35, minor_radius=0.18, major_segments=12, minor_segments=4)
    ring = bpy.context.active_object
    ring.rotation_euler = (math.radians(70), math.radians(20), 0)
    for obj in (core, ring):
        for coll in list(obj.users_collection):
            coll.objects.unlink(obj)
        scene.collection.objects.link(obj)
        obj.parent = root
        for poly in obj.data.polygons:
            poly.use_smooth = False
    bpy.context.view_layer.update()
    add_camera(scene, root, cfg)
    add_lights(scene, cfg)
    os.makedirs(out_dir, exist_ok=True)
    passes = {
        'shade.png': (_orb_material('OrbCore', (0.5, 0.5, 0.5), emission=core_glow),
                      _orb_material('OrbRing', (0.25, 0.24, 0.3), metallic=0.3, roughness=0.6)),
        'mask.png': (_orb_material('OrbCoreMask', (1, 1, 1), flat=True),
                     _orb_material('OrbRingMask', (0, 0, 0), flat=True)),
    }
    for name, (core_mat, ring_mat) in passes.items():
        for obj, mat in ((core, core_mat), (ring, ring_mat)):
            obj.data.materials.clear()
            obj.data.materials.append(mat)
        scene.render.filepath = os.path.join(out_dir, name)
        bpy.ops.render.render(write_still=True, scene=scene.name)
    return dict(resolution=resolution, size=[scene.render.resolution_x, scene.render.resolution_y],
                elevation=cfg['elevation'], core_glow=core_glow)
