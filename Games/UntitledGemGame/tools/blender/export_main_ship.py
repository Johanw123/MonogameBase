"""Export the broad red cruiser and its separate cyan emission for the homebase."""
from pathlib import Path
from math import radians, sin, cos, sqrt, atan2, pi
import importlib.util
import bpy
from mathutils import Matrix

ROOT = Path(__file__).resolve().parents[2]
FRAMES = Path('/tmp/btb-main-ship-frames')


def setup():
    spec = importlib.util.spec_from_file_location('main_ship_base', ROOT / 'tools/blender/export_synty.py')
    base = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(base)
    base.SHIPS = {'home': 'SM_Ship_Cruiser_01'}
    base.ELEVATION = radians(base.STYLE.CAMERA_ELEVATION_DEGREES)
    base.setup()
    state = bpy.app.driver_namespace['btb_synty']
    scene, ship = state['scene'], state['models']['home']
    scene.compositing_node_group = None
    for model in state['models'].values(): model.hide_render = model != ship
    style = base.STYLE
    mat = style.ship_material(base.texture, red_paint=True)
    ship.data.materials.clear(); ship.data.materials.append(mat)
    style.bevel(ship)
    # Copy the same UV/mask graph for an emission-only render, excluding lights.
    glow = mat.copy(); glow.name = 'Main cruiser emission only'
    output = next(n for n in glow.node_tree.nodes if n.type == 'OUTPUT_MATERIAL')
    glow_bsdf = next(n for n in glow.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    source = glow_bsdf.inputs['Emission Color'].links[0].from_socket
    shader = glow.node_tree.nodes.new('ShaderNodeEmission')
    glow.node_tree.links.new(source, shader.inputs['Color'])
    shader.inputs['Strength'].default_value = 1
    glow.node_tree.links.new(shader.outputs[0], output.inputs['Surface'])
    style.lighting(scene)
    state.update(main_beauty=mat, main_glow=glow, main_base=base)
    FRAMES.mkdir(exist_ok=True)
    return scene


def render(start=0, end=128):
    state = bpy.app.driver_namespace['btb_synty']
    scene, ship, base = state['scene'], state['models']['home'], state['main_base']
    scene.camera.data.ortho_scale = 3.7
    scene.render.resolution_x = scene.render.resolution_y = 1024
    for index in range(start, end):
        angle = index * 2*pi/128
        sx, sy = sin(angle), -cos(angle)
        vertical = sy / (sqrt(2)*sin(base.ELEVATION))
        yaw = atan2(sx/sqrt(2)+vertical, -(sx/sqrt(2)-vertical))
        ship.matrix_world = Matrix.Rotation(yaw,4,'Z')
        for kind, mat, transform in [('home',state['main_beauty'],state['view_transform']),
                                      ('home-emission',state['main_glow'],'Standard')]:
            ship.data.materials[0] = mat
            scene.view_settings.view_transform = transform
            scene.view_settings.exposure = 0
            scene.render.filepath = str(FRAMES / f'{kind}_{index:02}.png')
            bpy.ops.render.render(write_still=True,scene=scene.name)
    ship.data.materials[0] = state['main_beauty']
    scene.view_settings.view_transform = state['view_transform']
    print('Rendered main ship beauty/emission',start,end,flush=True)


if __name__ == '__main__':
    setup()
    render()
