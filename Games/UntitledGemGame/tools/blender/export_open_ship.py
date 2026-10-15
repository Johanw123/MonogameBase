"""Render the textured ship in Johan's blender.blend without editing its scene.

Run setup(), render_frames(...) in batches, then finish(), or run this file.
The source's nose points along +X; the gameplay renderer expects local -Y.
"""
from pathlib import Path
from math import radians
import importlib.util
import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
FRAMES = Path('/tmp/btb-new-ship-frames')
PARTS = ('Plane', 'Plane.001', 'Plane.002', 'Plane.003', 'Cylinder.004',
         'Cylinder.021', 'Cylinder:003', 'Cylinder_Plate.001', 'Plane.004',
         'Plane.005', 'Plane.006', 'Plane.007', 'Plane.008', 'Plane.009',
         'Plane.010', 'Plane.014')

def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / 'tools/blender' / filename)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result

looks = module('btb_open_ship_looks', 'ship_look_tests.py')

def setup():
    source = bpy.context.window.scene
    originals = [source.objects[name] for name in PARTS]
    assert all(o.type == 'MESH' and len(o.data.materials) for o in originals)
    corners = [o.matrix_world @ Vector(v) for o in originals for v in o.bound_box]
    low = Vector([min(v[i] for v in corners) for i in range(3)])
    high = Vector([max(v[i] for v in corners) for i in range(3)])
    normalize = (Matrix.Rotation(radians(-90), 4, 'Z')
                 @ Matrix.Scale(2.6 / max(high - low), 4)
                 @ Matrix.Translation(-(low + high) / 2))
    export = module('btb_open_ship_base', 'export_isometric.py')
    FRAMES.mkdir(exist_ok=True)
    export.FRAMES = FRAMES
    export.setup()
    state = bpy.app.driver_namespace['btb_iso_export']
    scene = state['scene']
    root = bpy.data.objects.new('NewShipExportRoot', None)
    scene.collection.objects.link(root)
    for original in originals:
        copy = original.copy()
        copy.animation_data_clear()
        copy.parent = None
        copy.matrix_world = normalize @ original.matrix_world
        scene.collection.objects.link(copy)
        copy.parent = root
        copy.hide_render = False
    state['look_ship'] = root
    state['look_accents'] = []
    state['look_export'] = export
    scene.camera.data.ortho_scale = 3.7
    scene.render.resolution_x = scene.render.resolution_y = 1024
    # EEVEE keeps the textured hull crisp while making all 640 poses practical
    # to iterate. The planet/gem sprites use the same render engine.
    scene.render.engine = 'BLENDER_EEVEE'
    print('New ship ready:', len(originals), 'textured parts; source:', bpy.data.filepath)

render_frames = looks.render_frames
finish = looks.finish

if __name__ == '__main__':
    setup()
    try:
        for index, bank in enumerate((-30, -15, 0, 15, 30)):
            render_frames(bank_degrees=bank, bank_index=index)
    finally:
        finish()
