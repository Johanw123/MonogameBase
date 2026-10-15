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
    links.new(mix.outputs['Result'], bsdf.inputs['Base Color'])

    emissive = nodes.new('ShaderNodeTexImage')
    emissive.image = _tex('PolygonSciFiSpace_01_Emissive.png')
    links.new(uv.outputs['UV'], emissive.inputs['Vector'])
    links.new(emissive.outputs['Color'], bsdf.inputs['Emission Color'])
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
    mat = ship_material(cfg, detail_scale=cfg['detail_tiles'] / local_size)
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
    sun('Key', cfg['key_color'], cfg['key_energy'], (35, 0, -35))
    sun('Rim', cfg['rim_color'], cfg['rim_energy'], (120, 0, 150))
    sun('Fill', cfg['fill_color'], cfg['fill_energy'], (60, 0, 120))


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
