"""Blender sprite exporter. Run setup(), render('ship', start, end), then finish().

Uses the open craft_miner if present, otherwise the bundled Kenney GLB.
The Kenney kit's FBX files are ASCII (unsupported by Blender); its GLBs
contain the same models. Everything is built in a temporary scene.
"""
from pathlib import Path
from math import sin, cos, atan2, sqrt, pi, radians, tan
import bpy
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[2]
MODELS = ROOT / 'art-tests/isometric/models'
FRAMES = Path('/tmp/btb-isometric-frames')
FRAMES.mkdir(exist_ok=True)
CAMERA_ELEVATION = radians(70)

def setup():
    old_scene = bpy.context.window.scene
    old_objects = set(bpy.data.objects)
    old_materials = set(bpy.data.materials)
    scene = bpy.data.scenes.new('IsometricExport')
    state = dict(scene=scene, old_scene=old_scene, old_objects=old_objects,
                 old_materials=old_materials)
    bpy.app.driver_namespace['btb_iso_export'] = state
    bpy.context.window.scene = scene
    try:
        def load(name):
            before = set(bpy.data.objects)
            bpy.ops.import_scene.gltf(filepath=str(MODELS / (name + '.glb')))
            bpy.context.view_layer.update()
            return next(o for o in set(bpy.data.objects) - before if o.type == 'MESH')

        original = bpy.data.objects.get('craft_miner')
        if original is not None and original in old_objects:
            ship = original.copy()
            scene.collection.objects.link(ship)
        else:
            ship = load('craft_miner')
        planet = load('meteor_detailed')
        rocks = load('rock_crystalsLargeA')
        # Keep only the largest connected crystal, excluding the supporting rock.
        crystal_indices = {i for i, m in enumerate(rocks.data.materials) if m and m.name.startswith('crystal')}
        # glTF splits vertices along hard normals. Weld coincident positions for
        # connectivity so we select a whole crystal rather than one flat face.
        canonical = {}
        welded = {}
        for v in rocks.data.vertices:
            key = tuple(round(c, 6) for c in v.co)
            welded[v.index] = canonical.setdefault(key, v.index)
        faces = [[welded[i] for i in p.vertices] for p in rocks.data.polygons
                 if p.material_index in crystal_indices]
        groups = []
        while faces:
            group = [faces.pop()]
            vertices = set(group[0])
            changed = True
            while changed:
                changed = False
                for face in faces[:]:
                    if vertices.intersection(face):
                        group.append(face)
                        vertices.update(face)
                        faces.remove(face)
                        changed = True
            groups.append((vertices, group))
        indices, faces = max(groups, key=lambda g: len(g[0]))
        ordered = sorted(indices)
        lookup = {v: i for i, v in enumerate(ordered)}
        mesh = bpy.data.meshes.new('IsometricCrystalMesh')
        mesh.from_pydata([rocks.matrix_world @ rocks.data.vertices[i].co for i in ordered], [],
                         [[lookup[i] for i in f] for f in faces])
        mesh.update()
        gem = bpy.data.objects.new('IsometricCrystal', mesh)
        scene.collection.objects.link(gem)
        material = bpy.data.materials.new('IsometricCrystalGray')
        material.use_nodes = True
        node = next(n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        node.inputs['Base Color'].default_value = (0.38, 0.38, 0.38, 1)
        node.inputs['Roughness'].default_value = 0.28
        mesh.materials.append(material)
        bpy.context.view_layer.update()

        def center_model(obj, size):
            corners = [obj.matrix_world @ Vector(v) for v in obj.bound_box]
            low = Vector([min(v[i] for v in corners) for i in range(3)])
            high = Vector([max(v[i] for v in corners) for i in range(3)])
            factor = size / max(high - low)
            obj.matrix_world = Matrix.Scale(factor, 4) @ Matrix.Translation(-(low + high) / 2) @ obj.matrix_world
            return obj.matrix_world.copy()

        state['models'] = {
            'ship': (ship, center_model(ship, 2.6), 128, 1024, 3.7),
            'planet': (planet, center_model(planet, 2.0), 120, 1024, 2.5),
            'gem': (gem, center_model(gem, 1.7), 8, 128, 3.2),
        }
        for obj in scene.objects:
            obj.hide_render = True
        world = bpy.data.worlds.new('IsometricExportWorld')
        state['world'] = world
        scene.world = world
        world.use_nodes = True
        background = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
        background.inputs['Color'].default_value = (0.18, 0.21, 0.28, 1)
        background.inputs['Strength'].default_value = 0.35

        def light(name, position, energy, size, color):
            data = bpy.data.lights.new(name, 'AREA')
            obj = bpy.data.objects.new(name, data)
            scene.collection.objects.link(obj)
            obj.location = position
            obj.rotation_euler = (-obj.location).to_track_quat('-Z', 'Y').to_euler()
            data.energy, data.size, data.color = energy, size, color
        light('IsoKey', (-3, -4, 7), 700, 3, (1.0, 0.91, 0.79))
        light('IsoFill', (4, 1, 5), 250, 4, (0.72, 0.84, 1.0))
        light('IsoRim', (0, 4, 3), 350, 2, (1.0, 0.95, 0.85))
        data = bpy.data.cameras.new('IsometricCamera')
        camera = bpy.data.objects.new('IsometricCamera', data)
        scene.collection.objects.link(camera)
        camera.location = (6, -6, sqrt(72) * tan(CAMERA_ELEVATION))
        camera.rotation_euler = (-camera.location).to_track_quat('-Z', 'Y').to_euler()
        data.type = 'ORTHO'
        scene.camera = camera
        scene.render.engine = old_scene.render.engine
        scene.render.film_transparent = True
        scene.render.image_settings.file_format = 'PNG'
        scene.render.image_settings.color_mode = 'RGBA'
        scene.render.resolution_percentage = 100
        scene.view_settings.view_transform = old_scene.view_settings.view_transform
        print('Isometric exporter ready:', {k: v[2] for k, v in state['models'].items()})
    finally:
        bpy.context.window.scene = old_scene

def render(kind, start=0, end=None):
    state = bpy.app.driver_namespace['btb_iso_export']
    scene = state['scene']
    obj, base, count, resolution, scale = state['models'][kind]
    for model, *_ in state['models'].values():
        model.hide_render = model != obj
    scene.camera.data.ortho_scale = scale
    scene.render.resolution_x = scene.render.resolution_y = resolution
    for index in range(start, count if end is None else end):
        angle = index * 2 * pi / count
        if kind == 'ship':
            # Uniform SCREEN headings, clockwise from up. Undo the isometric
            # foreshortening before rotating the model, whose nose is local -Y.
            sx, sy = sin(angle), -cos(angle)
            vertical = sy / (sqrt(2) * sin(CAMERA_ELEVATION))
            wx, wy = sx / sqrt(2) + vertical, sx / sqrt(2) - vertical
            angle = atan2(wx, -wy)
        obj.matrix_world = Matrix.Rotation(angle, 4, 'Z') @ base
        scene.render.filepath = str(FRAMES / f'{kind}_{index:02}.png')
        bpy.ops.render.render(write_still=True, scene=scene.name)
    print(f'Rendered {kind} frames {start}..{(count if end is None else end) - 1}')

def finish():
    state = bpy.app.driver_namespace.pop('btb_iso_export')
    bpy.context.window.scene = state['old_scene']
    for obj in set(bpy.data.objects) - state['old_objects']:
        data, kind = obj.data, obj.type
        bpy.data.objects.remove(obj, do_unlink=True)
        if data and data.users == 0:
            if kind == 'MESH': bpy.data.meshes.remove(data)
            elif kind == 'CAMERA': bpy.data.cameras.remove(data)
            elif kind == 'LIGHT': bpy.data.lights.remove(data)
    bpy.data.scenes.remove(state['scene'])
    bpy.data.worlds.remove(state['world'])
    for material in set(bpy.data.materials) - state['old_materials']:
        if material.users == 0: bpy.data.materials.remove(material)
    print('Removed export scene; source scene restored.')

if __name__ == '__main__':
    setup()
    try:
        for kind in ('ship', 'planet', 'gem'): render(kind)
    finally:
        finish()
