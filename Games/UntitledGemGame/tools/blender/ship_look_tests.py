"""Still-only ship material/lighting trials. Run in Blender; source stays intact."""
from pathlib import Path
from math import sin, cos, sqrt, atan2, radians, tan
import importlib.util
import bpy
from mathutils import Matrix

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'art-tests/isometric/look-test'
OUTPUT.mkdir(parents=True, exist_ok=True)

STYLES = {
    'industrial': {'paint': (0.48, 0.24, 0.055), 'metal': (0.36, 0.42, 0.49),
                   'shadow': (0.095, 0.12, 0.15), 'glass': (0.008, 0.035, 0.055),
                   'metallic': 0.65, 'roughness': 0.4, 'wear': False},
    'worn': {'paint': (0.29, 0.17, 0.065), 'metal': (0.24, 0.28, 0.31),
             'shadow': (0.075, 0.085, 0.10), 'glass': (0.012, 0.025, 0.035),
             'metallic': 0.75, 'roughness': 0.48, 'wear': True},
    'stylized': {'paint': (0.95, 0.32, 0.045), 'metal': (0.52, 0.67, 0.76),
                 'shadow': (0.055, 0.095, 0.15), 'glass': (0.008, 0.075, 0.12),
                 'metallic': 0.25, 'roughness': 0.38, 'wear': False},
}

def setup():
    spec = importlib.util.spec_from_file_location('btb_look_export', ROOT / 'tools/blender/export_isometric.py')
    export = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(export)
    export.setup()
    state = bpy.app.driver_namespace['btb_iso_export']
    state['look_export'] = export
    scene = state['scene']
    ship, base, *_ = state['models']['ship']
    # Copy mesh and materials before any edits; the open ship shares none of
    # the trial's mutable data. Bake its transforms so bevel widths are metres.
    ship.data = ship.data.copy()
    ship.data.transform(base)
    ship.matrix_world = Matrix.Identity(4)
    state['look_ship'] = ship
    state['look_original_materials'] = list(ship.data.materials)
    state['look_material_indices'] = [p.material_index for p in ship.data.polygons]
    state['look_accents'] = []
    ship.hide_render = False
    scene.camera.data.ortho_scale = 3.7
    scene.render.resolution_x = scene.render.resolution_y = 1024

def accent(scene, name, x, y, z, color):
    mesh = bpy.data.meshes.new(name)
    vertices = [(x+dx, y+dy, z+dz) for dz in (0, .018) for dy in (-.035,.035) for dx in (-.015,.015)]
    mesh.from_pydata(vertices, [], [(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    material = bpy.data.materials.new(name+'Material')
    material.use_nodes = True
    node = next(n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    node.inputs['Base Color'].default_value = (*color, 1)
    node.inputs['Emission Color'].default_value = (*color, 1)
    node.inputs['Emission Strength'].default_value = 2.0
    material.diffuse_color = (*color, 1)
    mesh.materials.append(material)
    return obj

def top_height(mesh, x, y):
    mesh.calc_loop_triangles()
    heights = []
    for tri in mesh.loop_triangles:
        a,b,c = [mesh.vertices[i].co for i in tri.vertices]
        denominator = (b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y)
        if abs(denominator)<1e-8: continue
        u=((b.y-c.y)*(x-c.x)+(c.x-b.x)*(y-c.y))/denominator
        v=((c.y-a.y)*(x-c.x)+(a.x-c.x)*(y-c.y))/denominator
        w=1-u-v
        if min(u,v,w)>=-1e-5: heights.append(u*a.z+v*b.z+w*c.z)
    return max(heights) if heights else None

def apply_style(style):
    state = bpy.app.driver_namespace['btb_iso_export']
    scene, ship = state['scene'], state['look_ship']
    for obj in state['look_accents']:
        mesh = obj.data
        bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.meshes.remove(mesh)
    state['look_accents'] = []
    ship.modifiers.clear()
    ship.data.materials.clear()
    for material in state['look_original_materials']: ship.data.materials.append(material)
    for polygon, index in zip(ship.data.polygons, state['look_material_indices']):
        polygon.material_index = index
    if style != 'current':
        palette = STYLES[style]
        bevel = ship.modifiers.new('FineHullBevels', 'BEVEL')
        bevel.width = .018
        bevel.segments = 3
        for index, original in enumerate(state['look_original_materials']):
            material = original.copy()
            material.name = 'Trial_'+style+'_'+original.name
            ship.data.materials[index] = material
            node = next(n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
            role = {'metalRed':'paint', 'metal':'metal', 'metalDark':'shadow', 'dark':'glass'}[original.name]
            node.inputs['Base Color'].default_value = (*palette[role], 1)
            node.inputs['Metallic'].default_value = palette['metallic'] if role in ('metal','shadow') else .15
            node.inputs['Roughness'].default_value = .28 if role=='glass' else palette['roughness']
            node.inputs['Coat Weight'].default_value = .12 if role=='glass' else .15
            if role=='glass':
                node.inputs['Specular IOR Level'].default_value = .12
                node.inputs['Emission Color'].default_value = (.015,.10,.14,1)
                node.inputs['Emission Strength'].default_value = .12
            if palette['wear'] and role!='glass':
                # Fine roughness variation and shallow bump: no noisy damage
                # texture or silhouette change at the sprite's actual size.
                noise = material.node_tree.nodes.new('ShaderNodeTexNoise')
                noise.inputs['Scale'].default_value = 65
                noise.inputs['Detail'].default_value = 2
                bump = material.node_tree.nodes.new('ShaderNodeBump')
                bump.inputs['Strength'].default_value = .12
                bump.inputs['Distance'].default_value = .008
                material.node_tree.links.new(noise.outputs['Fac'],bump.inputs['Height'])
                material.node_tree.links.new(bump.outputs['Normal'],node.inputs['Normal'])
        background = next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND')
        background.inputs['Strength'].default_value = .18
        bpy.data.objects['IsoKey'].data.energy = 800
        bpy.data.objects['IsoKey'].data.size = 2.0
        bpy.data.objects['IsoFill'].data.energy = 160
        bpy.data.objects['IsoRim'].data.energy = 600
        for i,x in enumerate((-.72,.72)):
            z=top_height(ship.data,x,.55)
            if z is not None:
                state['look_accents'].append(accent(scene,'TrialNavLight'+str(i),x,.55,z+.004,(.04,.5,.8)))
        # Cycles supplies contact shadows and indirect light without a painted
        # AO texture. CPU device keeps the trial independent of GPU setup.
        scene.render.engine = 'CYCLES'
        scene.cycles.samples = 32
        scene.cycles.use_denoising = True

def render(style):
    apply_style(style)
    state = bpy.app.driver_namespace['btb_iso_export']
    scene, ship = state['scene'], state['look_ship']
    # Preserve the original 50-degree still comparison independently of the
    # current gameplay export camera.
    elevation = radians(50)
    scene.camera.location = (6, -6, sqrt(72) * tan(elevation))
    scene.camera.rotation_euler = (-scene.camera.location).to_track_quat('-Z', 'Y').to_euler()
    for heading, name in [(0,'up'),(225,'diagonal')]:
        angle = radians(heading)
        sx,sy=sin(angle),-cos(angle)
        vertical=sy/(sqrt(2)*sin(elevation))
        rotation=atan2(sx/sqrt(2)+vertical, -(sx/sqrt(2)-vertical))
        matrix = Matrix.Rotation(rotation,4,'Z')
        ship.matrix_world=matrix
        for obj in state['look_accents']: obj.matrix_world=matrix
        scene.render.filepath = str(OUTPUT/f'{style}-{name}.png')
        bpy.ops.render.render(write_still=True,scene=scene.name)
    print('Rendered ship look:',style)

def render_frames(start=0, end=128, bank_degrees=0, bank_index=2):
    """Export the applied look's gameplay headings, with lights rotating too."""
    state = bpy.app.driver_namespace['btb_iso_export']
    scene, ship = state['scene'], state['look_ship']
    elevation = state['look_export'].CAMERA_ELEVATION
    scene.camera.location = (6, -6, sqrt(72) * tan(elevation))
    scene.camera.rotation_euler = (-scene.camera.location).to_track_quat('-Z', 'Y').to_euler()
    output = state['look_export'].FRAMES
    for index in range(start, end):
        angle = index * 2 * 3.141592653589793 / 128
        sx, sy = sin(angle), -cos(angle)
        vertical = sy / (sqrt(2) * sin(state['look_export'].CAMERA_ELEVATION))
        yaw = Matrix.Rotation(atan2(sx/sqrt(2)+vertical, -(sx/sqrt(2)-vertical)),4,'Z')
        # Nose is local -Y. Positive bank drops the starboard (-X) wing,
        # matching clockwise screen turns. Roll precedes the heading rotation.
        matrix = yaw @ Matrix.Rotation(radians(-bank_degrees),4,'Y')
        ship.matrix_world = matrix
        for obj in state['look_accents']: obj.matrix_world = matrix
        scene.render.filepath = str(output / f'ship-bank{bank_index}_{index:02}.png')
        bpy.ops.render.render(write_still=True,scene=scene.name)
    print(f'Rendered bank {bank_degrees} degrees, ship frames {start}..{end-1}')

def enable_acceleration():
    """Temporarily use an already-enabled GPU; restore preferences on finish."""
    state = bpy.app.driver_namespace['btb_iso_export']
    preferences = bpy.context.preferences.addons['cycles'].preferences
    preferences.refresh_devices()
    devices = [d for d in preferences.devices if d.use and d.type != 'CPU']
    identifiers = [i.identifier for i in state['scene'].cycles.bl_rna.properties['device'].enum_items]
    gpu = next((i for i in identifiers if i == 'GPU'), None)
    if not devices or gpu is None: return False
    state['look_compute_type'] = preferences.compute_device_type
    try:
        preferences.compute_device_type = devices[-1].type
        state['scene'].cycles.device = gpu
        state['scene'].cycles.denoising_use_gpu = True
    except (TypeError, ValueError):
        preferences.compute_device_type = state.pop('look_compute_type')
        return False
    print('Using render device', devices[-1].name, devices[-1].type)
    return True

def finish():
    state = bpy.app.driver_namespace['btb_iso_export']
    if 'look_compute_type' in state:
        bpy.context.preferences.addons['cycles'].preferences.compute_device_type = state['look_compute_type']
    state['look_export'].finish()

if __name__ == '__main__':
    setup()
    try:
        for style in ('current','industrial','worn','stylized'): render(style)
    finally:
        finish()
