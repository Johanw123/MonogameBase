"""Run in Blender's Python console or through Blender MCP; uses the open model.

Creates an isolated render scene and removes it afterwards. Does not save or
change the source .blend. Output goes to art-tests/prerendered-ship.
"""
from pathlib import Path
from math import pi
import bpy
from mathutils import Vector, Quaternion

OUTPUT = Path('/home/johan/Dev/workspaces/MonogameBase/Games/UntitledGemGame/art-tests/prerendered-ship')
OUTPUT.mkdir(parents=True, exist_ok=True)
source = bpy.data.objects['craft_miner']
bpy.context.view_layer.update()
scene = bpy.data.scenes.new('ShipSpriteTest')
created_objects = []
created_data = []
world = bpy.data.worlds.new('ShipSpriteTestWorld')
# Override RENDER_VIEWS before exec() to render only selected views.
# Equal X/Y/Z camera offsets give true isometric projection: 45 degrees
# around the vertical axis and 35.264 degrees above the horizontal plane.
views = globals().get('RENDER_VIEWS', [
    ('top', (0, 0, 9), pi, 3.1),
    ('tilted', (0, -3, 9), pi, 3.1),
    ('isometric', (6, -6, 6), 0, 3.6),
])

try:
    ship = source.copy()
    scene.collection.objects.link(ship)
    created_objects.append(ship)
    corners = [source.matrix_world @ Vector(v) for v in source.bound_box]
    center = sum(corners, Vector()) / 8
    scene.world = world
    world.use_nodes = True
    background = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
    background.inputs['Color'].default_value = (0.18, 0.21, 0.28, 1)
    background.inputs['Strength'].default_value = 0.45

    def area_light(name, offset, power, size, color):
        data = bpy.data.lights.new(name, 'AREA')
        created_data.append(data)
        obj = bpy.data.objects.new(name, data)
        created_objects.append(obj)
        scene.collection.objects.link(obj)
        obj.location = center + Vector(offset)
        obj.rotation_euler = (center - obj.location).to_track_quat('-Z', 'Y').to_euler()
        data.energy, data.size, data.color = power, size, color

    area_light('SpriteKey', (-3, -4, 7), 850, 3, (1.0, 0.91, 0.79))
    area_light('SpriteFill', (4, 1, 5), 400, 4, (0.72, 0.84, 1.0))
    area_light('SpriteRim', (0, 4, 3), 500, 2, (1.0, 0.95, 0.85))
    camera_data = bpy.data.cameras.new('SpriteCamera')
    created_data.append(camera_data)
    camera = bpy.data.objects.new('SpriteCamera', camera_data)
    created_objects.append(camera)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = 3.1
    # Use the current valid renderer rather than assuming enum availability.
    scene.render.engine = bpy.context.scene.render.engine
    scene.render.resolution_x = scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.render.image_settings.color_depth = '8'
    scene.view_settings.view_transform = bpy.context.scene.view_settings.view_transform
    for name, offset, roll, ortho_scale in views:
        camera.location = center + Vector(offset)
        camera.rotation_mode = 'QUATERNION'
        camera.rotation_quaternion = (center - camera.location).to_track_quat('-Z', 'Y') @ Quaternion((0, 0, 1), roll)
        camera_data.ortho_scale = ortho_scale
        scene.render.filepath = str(OUTPUT / f'craft_miner_{name}_512.png')
        bpy.ops.render.render(write_still=True, scene=scene.name)
        print('Rendered', scene.render.filepath)
finally:
    bpy.data.scenes.remove(scene)
    for obj in created_objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    for data in created_data:
        if isinstance(data, bpy.types.Camera):
            bpy.data.cameras.remove(data)
        else:
            bpy.data.lights.remove(data)
    bpy.data.worlds.remove(world)
