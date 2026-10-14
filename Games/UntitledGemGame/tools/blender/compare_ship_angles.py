"""Blender still comparison: current camera versus 50 degrees, same lighting."""
from pathlib import Path
from math import radians, sin, cos, tan, sqrt, atan2
import importlib.util
from mathutils import Vector, Matrix
import bpy

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'art-tests/isometric/angle-test'
OUTPUT.mkdir(parents=True, exist_ok=True)
spec = importlib.util.spec_from_file_location('btb_angle_export', ROOT / 'tools/blender/export_isometric.py')
export = importlib.util.module_from_spec(spec)
spec.loader.exec_module(export)
export.setup()
try:
    state = bpy.app.driver_namespace['btb_iso_export']
    scene = state['scene']
    ship, base, *_ = state['models']['ship']
    ship.hide_render = False
    scene.camera.data.ortho_scale = 3.7
    scene.render.resolution_x = scene.render.resolution_y = 1024
    for elevation, label in [(atan2(1, sqrt(2)), '35'), (radians(50), '50')]:
        camera = scene.camera
        camera.location = (6, -6, sqrt(72) * tan(elevation))
        camera.rotation_euler = (-camera.location).to_track_quat('-Z', 'Y').to_euler()
        for heading, name in [(0, 'up'), (90, 'right'), (225, 'down-left')]:
            angle = radians(heading)
            sx, sy = sin(angle), -cos(angle)
            wx = sx / sqrt(2) + sy / (sqrt(2) * sin(elevation))
            wy = sx / sqrt(2) - sy / (sqrt(2) * sin(elevation))
            ship.matrix_world = Matrix.Rotation(atan2(wx, -wy), 4, 'Z') @ base
            scene.render.filepath = str(OUTPUT / f'ship-{label}-{name}.png')
            bpy.ops.render.render(write_still=True, scene=scene.name)
    print('Rendered camera comparison to', OUTPUT)
finally:
    export.finish()
