"""Render a 4K view of the supplied Nebula 3 HDR in an isolated Blender scene."""
from pathlib import Path
from math import radians
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path('/home/johan/Downloads/Nebula+3.hdr')
OUTPUT = ROOT / 'Content/Textures/Backgrounds/nebula-3.png'

def setup():
    original = bpy.context.window.scene
    scene = bpy.data.scenes.new('NebulaBackgroundExport')
    world = bpy.data.worlds.new('NebulaBackgroundWorld')
    existing = set(bpy.data.images)
    hdr = bpy.data.images.load(str(SOURCE), check_existing=True)
    world.use_nodes = True
    environment = world.node_tree.nodes.new('ShaderNodeTexEnvironment')
    environment.image = hdr
    background = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
    world.node_tree.links.new(environment.outputs['Color'], background.inputs['Color'])
    scene.world = world
    camera_data = bpy.data.cameras.new('NebulaBackgroundCamera')
    camera = bpy.data.objects.new('NebulaBackgroundCamera', camera_data)
    scene.collection.objects.link(camera)
    # A 160 x 90 degree panorama crop uses much more of the source than the
    # previous perspective view, without stretching features near the edges.
    camera_data.type = 'PANO'
    camera_data.panorama_type = 'EQUIRECTANGULAR'
    camera_data.longitude_min = radians(-80)
    camera_data.longitude_max = radians(80)
    camera_data.latitude_min = radians(-45)
    camera_data.latitude_max = radians(45)
    scene.camera = camera
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 1  # Direct environment rays have no sampling noise.
    scene.cycles.use_denoising = False
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGB'
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.exposure = -1
    bpy.app.driver_namespace['btb_nebula_export'] = dict(
        original=original, scene=scene, world=world, camera=camera,
        camera_data=camera_data, hdr=hdr, remove_image=hdr not in existing)

def render(yaw=0, pitch=0, width=3840, path=OUTPUT):
    from math import sin, cos
    state = bpy.app.driver_namespace['btb_nebula_export']
    scene, camera = state['scene'], state['camera']
    yaw, pitch = radians(yaw), radians(pitch)
    direction = Vector((cos(yaw)*cos(pitch), sin(yaw)*cos(pitch), sin(pitch)))
    camera.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    scene.render.resolution_x = width
    scene.render.resolution_y = width * 9 // 16
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True, scene=scene.name)
    print('Rendered nebula:', path)

def finish():
    state = bpy.app.driver_namespace.pop('btb_nebula_export')
    bpy.context.window.scene = state['original']
    bpy.data.objects.remove(state['camera'], do_unlink=True)
    bpy.data.cameras.remove(state['camera_data'])
    bpy.data.scenes.remove(state['scene'])
    bpy.data.worlds.remove(state['world'])
    if state['remove_image']: bpy.data.images.remove(state['hdr'])

if __name__ == '__main__':
    setup()
    try: render()
    finally: finish()
