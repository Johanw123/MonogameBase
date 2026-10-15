"""Shared Synty surface materials, lighting and render color management."""
from pathlib import Path
import bpy

CAMERA_ELEVATION_DEGREES = 55
EMISSION_STRENGTH = 2.5


def material(name):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    return mat, next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')


def ship_material(texture_loader, red_paint=False, has_panel_uv=True):
    mat, bsdf = material('Synty shared ship surface')
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    atlas = texture_loader(mat, 'PolygonSciFiSpace_Texture_01_A.png')
    hsv = nodes.new('ShaderNodeSeparateColor'); hsv.mode = 'HSV'
    links.new(atlas.outputs['Color'], hsv.inputs[0])

    def math_node(operation, a, b):
        node = nodes.new('ShaderNodeMath'); node.operation = operation
        if isinstance(a, (float, int)): node.inputs[0].default_value = a
        else: links.new(a, node.inputs[0])
        if isinstance(b, (float, int)): node.inputs[1].default_value = b
        else: links.new(b, node.inputs[1])
        return node.outputs[0]

    def hue_mask(low, high):
        hue = math_node('MULTIPLY', math_node('GREATER_THAN', hsv.outputs[0], low),
                        math_node('LESS_THAN', hsv.outputs[0], high))
        return math_node('MULTIPLY', hue, math_node('GREATER_THAN', hsv.outputs[1], .18))

    def mix(a, b, fac):
        node = nodes.new('ShaderNodeMixRGB')
        links.new(fac, node.inputs[0]); links.new(a, node.inputs[1])
        node.inputs[2].default_value = b
        return node.outputs[0]

    # Recolor only the atlas's blue paint and gold trim; preserve neutral frame,
    # panel shading and the actual UVs. The reference uses red paint/cyan trim.
    paint = (mix(atlas.outputs['Color'], (.48, .085, .075, 1), hue_mask(.48, .72))
             if red_paint else atlas.outputs['Color'])
    strips = hue_mask(.055, .18)
    paint = mix(paint, (.025, .65, .85, 1), strips)
    detail = texture_loader(mat, 'PolygonSciFiSpace_Ship_Mask_04.png', 'panel')
    detail.image.colorspace_settings.name = 'Non-Color'
    multiply = nodes.new('ShaderNodeMixRGB'); multiply.blend_type = 'MULTIPLY'
    multiply.inputs[0].default_value = .7 if has_panel_uv else 0
    links.new(paint, multiply.inputs[1]); links.new(detail.outputs['Color'], multiply.inputs[2])
    links.new(multiply.outputs[0], bsdf.inputs['Base Color'])
    original_emission = texture_loader(mat, 'PolygonSciFiSpace_01_Emissive.png')
    mask = math_node('MAXIMUM', original_emission.outputs['Color'], strips)
    emission = nodes.new('ShaderNodeMixRGB'); emission.blend_type = 'MULTIPLY'
    emission.inputs[0].default_value = 1; emission.inputs[2].default_value = (.025, .8, 1, 1)
    links.new(mask, emission.inputs[1])
    links.new(emission.outputs[0], bsdf.inputs['Emission Color'])
    bsdf.inputs['Emission Strength'].default_value = EMISSION_STRENGTH
    bsdf.inputs['Roughness'].default_value = .48
    return mat


def lighting(scene):
    for obj in scene.objects:
        if obj.type == 'LIGHT':
            if 'warm key' in obj.name: obj.data.energy = 1050
            if 'cool fill' in obj.name: obj.data.energy = 280
            if 'violet rim' in obj.name: obj.data.energy = 650
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.exposure = 0


def bevel(obj):
    if any(m.type == 'BEVEL' for m in obj.modifiers): return
    mod = obj.modifiers.new('Fine hull edge highlights', 'BEVEL')
    mod.width = .004
    mod.segments = 2


def emission_output(scene, folder):
    """Save the emission pass alongside beauty from the same render."""
    scene.view_layers[0].use_pass_emit = True
    group = bpy.data.node_groups.new('Synty beauty and emission', 'CompositorNodeTree')
    scene.compositing_node_group = group
    group.interface.new_socket(name='Image', in_out='OUTPUT', socket_type='NodeSocketColor')
    output = group.nodes.new('NodeGroupOutput')
    layer = group.nodes.new('CompositorNodeRLayers')
    layer.scene = scene
    group.links.new(layer.outputs['Image'], output.inputs['Image'])
    scale = group.nodes.new('ShaderNodeMixRGB')
    scale.blend_type = 'MULTIPLY'
    scale.inputs[0].default_value = 1
    scale.inputs[2].default_value = (1/EMISSION_STRENGTH,)*3+(1,)
    group.links.new(layer.outputs['Emission'], scale.inputs[1])
    alpha = group.nodes.new('CompositorNodeSetAlpha')
    group.links.new(scale.outputs[0], alpha.inputs['Image'])
    group.links.new(layer.outputs['Alpha'], alpha.inputs['Alpha'])
    file = group.nodes.new('CompositorNodeOutputFile')
    file.directory = str(folder)
    file.file_name = 'emission'
    file.file_output_items.new('RGBA', 'emission')
    group.links.new(alpha.outputs[0], file.inputs[0])
    return file
