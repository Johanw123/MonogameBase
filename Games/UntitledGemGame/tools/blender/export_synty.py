"""Render the licensed Synty ship/planet into the existing gameplay frame layout.

Run in Blender, or blender -b --python tools/blender/export_synty.py.
Source assets stay in the user's Unity project. No Unity scripts are executed.
"""
from pathlib import Path
from math import sin, cos, sqrt, atan2, radians, tan, pi
import re
import importlib.util
import bpy
from mathutils import Matrix, Vector

ASSETS = Path('/home/johan/My project/Assets/Synty/PolygonSciFiSpace')
FRAMES = Path('/tmp/btb-unified-style-frames')
SHIPS = {
    'basic': 'SM_Ship_Fighter_01',
    'advanced': 'SM_Ship_Fighter_Heavy_01',
    'perimeter': 'SM_Ship_Stealth_01',
    'expert': 'SM_Ship_Bomber_01',
    'ultimate': 'SM_Ship_Cruiser_01',
    'drone': 'SM_Veh_Drone_Repair_01',
    'home': 'SM_Ship_Cruiser_01',
}


def color(text, key):
    match = re.search(r'- ' + re.escape(key) + r': \{r: ([^,]+), g: ([^,]+), b: ([^,]+)', text)
    return (*map(float, match.groups()), 1)


def material(name):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    return mat, next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')


def texture(mat, filename, uv='map1'):
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    image = nodes.new('ShaderNodeTexImage')
    image.image = bpy.data.images.load(str(ASSETS / 'Textures' / filename), check_existing=True)
    coord = nodes.new('ShaderNodeUVMap')
    coord.uv_map = uv
    links.new(coord.outputs['UV'], image.inputs['Vector'])
    return image


spec = importlib.util.spec_from_file_location('synty_render_style', Path(__file__).with_name('render_style.py'))
STYLE = importlib.util.module_from_spec(spec)
spec.loader.exec_module(STYLE)
ELEVATION = radians(STYLE.CAMERA_ELEVATION_DEGREES)


def ship_material():
    return STYLE.ship_material(texture)


def planet_material():
    # Recreate the source material's two contrast/mix stages using the actual
    # land mask with triplanar projection, as in Synty's Unity planet shader.
    text = (ASSETS / 'Materials/Planets/PolygonSpace_Planet_02_B.mat').read_text()
    mat, bsdf = material('Synty Purple Planet 02 B')
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    tex = nodes.new('ShaderNodeTexImage')
    tex.image = bpy.data.images.load(str(ASSETS / 'Textures/Planet_Land_01_Mask.png'), check_existing=True)
    tex.image.colorspace_settings.name = 'Non-Color'
    tex.projection = 'BOX'
    tex.projection_blend = .2
    coord = nodes.new('ShaderNodeTexCoord')
    mapping = nodes.new('ShaderNodeVectorMath')
    mapping.operation = 'SCALE'
    mapping.inputs['Scale'].default_value = 1.1
    links.new(coord.outputs['Object'], mapping.inputs[0])
    links.new(mapping.outputs[0], tex.inputs['Vector'])
    contrast = nodes.new('ShaderNodeMath')
    contrast.operation = 'MULTIPLY_ADD'
    contrast.inputs[1].default_value = -3.84
    contrast.inputs[2].default_value = 1.92
    links.new(tex.outputs['Color'], contrast.inputs[0])
    base, second = color(text, '_Color0'), color(text, '_Second_Color')
    scale = nodes.new('ShaderNodeVectorMath')
    scale.operation = 'SCALE'
    scale.inputs[0].default_value = tuple(b-a for a,b in zip(base[:3],second[:3]))
    links.new(contrast.outputs[0], scale.inputs['Scale'])
    add = nodes.new('ShaderNodeVectorMath')
    add.operation = 'ADD'
    add.inputs[1].default_value = tuple(2*c for c in base[:3])
    links.new(scale.outputs[0], add.inputs[0])
    lower = nodes.new('ShaderNodeVectorMath'); lower.operation = 'MAXIMUM'
    lower.inputs[1].default_value = (0,0,0)
    links.new(add.outputs[0], lower.inputs[0])
    upper = nodes.new('ShaderNodeVectorMath'); upper.operation = 'MINIMUM'
    upper.inputs[1].default_value = (1,1,1)
    links.new(lower.outputs[0], upper.inputs[0])
    links.new(upper.outputs[0], bsdf.inputs['Base Color'])
    fresnel = nodes.new('ShaderNodeFresnel')
    fresnel.inputs['IOR'].default_value = 1.45
    rim = nodes.new('ShaderNodeVectorMath'); rim.operation = 'SCALE'
    links.new(upper.outputs[0], rim.inputs[0])
    links.new(fresnel.outputs[0], rim.inputs['Scale'])
    links.new(rim.outputs[0], bsdf.inputs['Emission Color'])
    bsdf.inputs['Emission Strength'].default_value = STYLE.EMISSION_STRENGTH
    bsdf.inputs['Roughness'].default_value = 1
    return mat


def ore_material():
    mat, bsdf = material('Synty neutral ore')
    bsdf.inputs['Base Color'].default_value = (.22, .22, .22, 1)
    bsdf.inputs['Roughness'].default_value = .85
    return mat


def setup():
    previous = bpy.context.window.scene
    scene = bpy.data.scenes.new('Synty Sprite Export')
    bpy.context.window.scene = scene
    models = {}
    palette = ship_material()
    entries = [(key, name, 2.6, palette) for key, name in SHIPS.items()]
    entries.append(('planet', 'SM_Env_Planet_14', 2.0, planet_material()))
    entries.append(('gem', 'SM_Env_Asteroid_05', 1.7, ore_material()))
    for kind, name, size, mat in entries:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=str(ASSETS / 'Models' / (name + '.fbx')))
        meshes = sorted((o for o in set(bpy.data.objects) - before if o.type == 'MESH'), key=lambda o:o.name)
        bpy.ops.object.select_all(action='DESELECT')
        for part in meshes: part.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]
        if len(meshes) > 1: bpy.ops.object.join()
        obj = bpy.context.view_layer.objects.active
        obj.name = 'Synty_' + kind
        bpy.context.view_layer.update()
        corners = [obj.matrix_world @ Vector(v) for v in obj.bound_box]
        low = Vector([min(v[i] for v in corners) for i in range(3)])
        high = Vector([max(v[i] for v in corners) for i in range(3)])
        center = (low + high) / 2
        radius = max((obj.matrix_world @ v.co - center).length for v in obj.data.vertices)
        factor = size / max(high-low) if kind == 'planet' else size * .5 / radius
        base = Matrix.Scale(factor, 4) @ Matrix.Translation(-center) @ obj.matrix_world
        # Bake the FBX axis conversion: Unity +Z forward becomes Blender -Y.
        obj.data.transform(base)
        obj.matrix_world = Matrix.Identity(4)
        obj.data.materials.clear()
        obj.data.materials.append(mat)
        if kind in ('planet', 'gem'):
            for polygon in obj.data.polygons: polygon.use_smooth = False
        if kind not in ('planet','gem'):
            STYLE.bevel(obj)
        models[kind] = obj

    def light(name, location, energy, size, tint):
        data = bpy.data.lights.new(name, 'AREA')
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = location
        obj.rotation_euler = (-obj.location).to_track_quat('-Z', 'Y').to_euler()
        data.energy, data.size, data.color = energy, size, tint

    light('Synty warm key', (-3, -4, 7), 850, 3, (1, .83, .65))
    light('Synty cool fill', (4, 1, 5), 180, 4, (.65, .75, 1))
    light('Synty violet rim', (0, 4, 3), 450, 2, (.8, .65, 1))
    world = bpy.data.worlds.new('Synty Ambient')
    world.use_nodes = True
    background = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
    background.inputs['Color'].default_value = (.18, .21, .28, 1)
    background.inputs['Strength'].default_value = .35
    scene.world = world
    cam = bpy.data.objects.new('Synty Camera', bpy.data.cameras.new('Synty Camera'))
    scene.collection.objects.link(cam)
    cam.location = (6, -6, sqrt(72) * tan(ELEVATION))
    cam.rotation_euler = (-cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.type = 'ORTHO'
    scene.camera = cam
    # Keep the scene's available default EEVEE engine, queried from Blender.
    print('Render engine:', scene.render.engine)
    scene.render.film_transparent = True
    scene.render.resolution_x = scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = next(i.identifier for i in scene.render.image_settings.bl_rna.properties['file_format'].enum_items if i.identifier == 'PNG')
    scene.render.image_settings.color_mode = 'RGBA'
    STYLE.lighting(scene)
    FRAMES.mkdir(exist_ok=True)
    bpy.app.driver_namespace['btb_synty'] = dict(scene=scene, models=models, previous=previous, view_transform=scene.view_settings.view_transform)
    bpy.app.driver_namespace['btb_synty']['emission_output'] = STYLE.emission_output(scene, FRAMES)
    return scene


def render(kind, start=0, end=None, bank_index=2):
    state = bpy.app.driver_namespace['btb_synty']
    scene, models = state['scene'], state['models']
    STYLE.lighting(scene)
    obj = models[kind]
    for model in models.values():
        model.hide_render = model != obj
    scene.camera.data.ortho_scale = 2.5 if kind == 'planet' else 3.2 if kind == 'gem' else 3.7
    scene.render.resolution_x = scene.render.resolution_y = 1024 if kind == 'planet' else 128 if kind == 'gem' else 512
    count = 120 if kind == 'planet' else 8 if kind == 'gem' else 128
    for index in range(start, count if end is None else end):
        angle = index * 2 * pi / count
        if kind == 'gem':
            obj.matrix_world = Matrix.Rotation(angle,4,'Z')
            name = f'gem_{index:02}.png'
        elif kind != 'planet':
            sx, sy = sin(angle), -cos(angle)
            vertical = sy / (sqrt(2) * sin(ELEVATION))
            yaw = atan2(sx / sqrt(2) + vertical, -(sx / sqrt(2) - vertical))
            obj.matrix_world = Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(radians(30 - bank_index * 15), 4, 'Y')
            name = f'{kind}-bank{bank_index}_{index:02}.png'
        else:
            # The pole projects vertically on screen, with zero sideways tilt.
            eye = scene.camera.location.normalized()
            right = Vector((1,1,0)).normalized()
            up = right.cross(-eye).normalized()
            pole = Vector((0,0,1)).rotation_difference(up).to_matrix().to_4x4()
            obj.matrix_world = pole @ Matrix.Rotation(angle, 4, 'Z')
            name = f'planet_{index:02}.png'
        scene.render.filepath = str(FRAMES / name)
        state['emission_output'].directory = str(FRAMES)
        state['emission_output'].file_name = Path(name).stem + '-emission'
        bpy.ops.render.render(write_still=True, scene=scene.name)
    print('Rendered', kind, start, end or count, 'bank', bank_index, flush=True)


if __name__ == '__main__':
    setup()
    for kind in SHIPS:
        if kind == 'home': continue
        for bank in range(5):
            render(kind, bank_index=bank)
    render('planet')
    render('gem')
    spec = importlib.util.spec_from_file_location('main_ship_export', Path(__file__).with_name('export_main_ship.py'))
    main = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(main)
    main.setup()
    main.render()
