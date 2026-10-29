"""Blender-authored, metre-scale source assets. Run with --background --factory-startup.
Exports editable .blend, standard FBX, and evaluated geometry for lossless Unity mesh baking.
"""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'SourceArt' / 'Blender'
FBX = ROOT / 'Assets' / 'Art' / 'Models' / 'Blender'
PREVIEW = ROOT / 'Docs' / 'Previews'
for directory in (OUT, FBX, PREVIEW):
    directory.mkdir(parents=True, exist_ok=True)

# This script is launched in a separate factory-startup process, never a user's open file.
for obj in list(bpy.data.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
scene = bpy.context.scene
bpy.context.preferences.filepaths.save_version = 0
scene.unit_settings.scale_length = 1
unit_choices = [item.identifier for item in scene.unit_settings.bl_rna.properties['system'].enum_items]
if 'METRIC' in unit_choices:
    scene.unit_settings.system = 'METRIC'

def B(point):
    return Vector((point[0], -point[2], point[1]))

def U(point):
    return (point.x, point.z, -point.y)

materials = {}
def mat(name, color, metallic=0, vertex_tint=None, emission=0):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = next(node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value = (*color[:3], 1)
    shader.inputs['Roughness'].default_value = .72 if metallic == 0 else .43
    shader.inputs['Metallic'].default_value = metallic
    if emission and shader.inputs.get('Emission Color'):
        shader.inputs['Emission Color'].default_value = (*color[:3], 1)
        shader.inputs['Emission Strength'].default_value = emission
    material.diffuse_color = (*color[:3], 1)
    material['unity_tint'] = list(vertex_tint or (1, 1, 1, 1))
    material['unity_key'] = name
    materials[name] = material
    return material

for key, color in {
    'HoneyWood': (.67, .39, .17), 'LightWood': (.79, .51, .25),
    'DarkWood': (.46, .25, .10), 'Rope': (.83, .69, .43),
    'IronNails': (.18, .22, .21), 'LanternWood': (.43, .26, .14),
    'LanternMetal': (.20, .31, .30), 'LanternGlass': (1, .78, .42),
    'LanternCore': (1, .86, .48), 'DriftWood': (.65, .37, .16),
    'SupplyCream': (.93, .91, .77), 'BottleWater': (.18, .59, .72),
    'BottleInk': (.07, .30, .35)
}.items():
    mat(key, color, .45 if key in ('IronNails', 'LanternMetal') else 0, emission=1.5 if key == 'LanternCore' else 0)

assets = []
def empty(name, parent=None, position=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = B(position)
    return obj

def asset(name):
    root = empty(name)
    entry = {'id': name, 'root': root, 'groups': []}
    assets.append(entry)
    return entry

def group(entry, name, position=(0, 0, 0)):
    obj = empty(name, entry['root'], position)
    entry['groups'].append(obj)
    return obj

def finish(obj, name, parent, material):
    obj.name = name
    obj.parent = parent
    obj.data.materials.append(material)
    return obj

def bevel(obj, width=.018, segments=2):
    supported = [item.identifier for item in bpy.types.Modifier.bl_rna.properties['type'].enum_items]
    if 'BEVEL' not in supported:
        raise RuntimeError('Blender lacks bevel modifier')
    modifier = obj.modifiers.new('Crafted edges', 'BEVEL')
    modifier.width = width
    modifier.segments = segments
    return obj

def cube(name, parent, center, size, material, edge=.015, rotation=0):
    bpy.ops.mesh.primitive_cube_add(size=1)
    obj = finish(bpy.context.object, name, parent, material)
    obj.location = B(center)
    obj.scale = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.rotation_euler.z = math.radians(rotation)
    if edge:
        bevel(obj, edge)
    return obj

def cylinder(name, parent, center, radius, depth, material, vertices=12, longitudinal=False, edge=.008):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth)
    obj = finish(bpy.context.object, name, parent, material)
    obj.location = B(center)
    if longitudinal:
        obj.rotation_euler.x = math.pi / 2
    if edge:
        bevel(obj, edge, 1)
    return obj

def sphere(name, parent, center, size, material, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1)
    obj = finish(bpy.context.object, name, parent, material)
    obj.location = B(center)
    obj.scale = (size[0], size[2], size[1])
    return obj

def mesh_part(name, parent, vertices, faces, material):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([B(v) for v in vertices], [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    finish(obj, name, parent, material)
    return obj

def tube(name, parent, points, radius, material, closed=False):
    curve = bpy.data.curves.new(name, 'CURVE')
    dimensions = [item.identifier for item in curve.bl_rna.properties['dimensions'].enum_items]
    curve.dimensions = '3D' if '3D' in dimensions else dimensions[0]
    curve.resolution_u = 3
    curve.bevel_depth = radius
    curve.bevel_resolution = 0
    spline_types = [item.identifier for item in bpy.types.Spline.bl_rna.properties['type'].enum_items]
    spline = curve.splines.new('POLY' if 'POLY' in spline_types else spline_types[0])
    spline.points.add(len(points) - 1)
    for dest, source in zip(spline.points, points):
        dest.co = (*B(source), 1)
    spline.use_cyclic_u = closed
    obj = bpy.data.objects.new(name, curve)
    scene.collection.objects.link(obj)
    return finish(obj, name, parent, material)

def ring(name, parent, center, radius, rope_radius, material, plane='xy', segments=16):
    points = []
    for step in range(segments):
        angle = step * math.tau / segments
        a, b = math.cos(angle) * radius, math.sin(angle) * radius
        point = (a, b, 0) if plane == 'xy' else (a, 0, b)
        points.append(tuple(center[i] + point[i] for i in range(3)))
    return tube(name, parent, points, rope_radius, material, True)

# A single shared 2m tile: irregular bevels, shallow grain, pegged boards and rope-lashed floats.
deck = asset('DeckTile')
boards = group(deck, 'Deck')
for plank in range(6):
    x = (plank - 2.5) * .326
    y = ((plank * 7) % 3 - 1) * .003
    cube('Rounded plank', boards, (x, y, 0), (.308, .15, 1.94), materials['HoneyWood' if plank % 2 == 0 else 'LightWood'], .021)
    for z in (-.73, .73):
        cylinder('Octagonal iron peg', boards, (x, y + .081, z), .020, .012, materials['IronNails'], 8, edge=.002)
    for mark in range(2):
        z = -.54 + mark * .71 + (plank % 3) * .05
        points = [(x + .028 * math.sin(i * .8 + plank), y + .077, z + i * .085) for i in range(5)]
        tube('Quiet wood grain', boards, points, .0028, materials['DarkWood'])
    if plank in (1, 4):
        points = [(x + math.cos(i * math.tau / 12) * .036, y + .0775, .30 + math.sin(i * math.tau / 12) * .075) for i in range(12)]
        tube('Small timber knot', boards, points, .0025, materials['DarkWood'], True)
for x in (-.62, .62):
    cylinder('Twelve-sided buoyant log', boards, (x, -.27, 0), .23, 1.97, materials['DarkWood'], 12, True)
    for z in (-.985, .985):
        cylinder('End grain', boards, (x, -.27, z), .17, .009, materials['HoneyWood'], 12, True, .002)
        ring('Growth ring', boards, (x, -.27, z * 1.006), .105, .005, materials['DarkWood'])
    for z in (-.65, .65):
        ring('Braided float binding', boards, (x, -.27, z), .238, .024, materials['Rope'])
        sphere('Rope knot', boards, (x + .20, -.12, z), (.038, .035, .05), materials['Rope'], subdivisions=1)
for z in (-.95, .95):
    cube('Braced underside rail', boards, (0, -.055, z), (1.96, .13, .075), materials['DarkWood'], .015)

# Three actual silhouettes, each with an independently pivoted tail and solid fins.
fish_specs = [
    ('FishSilver', (.70, .83, .74), .20, .135, 1.0, .43),
    ('FishCoral', (.95, .68, .35), .235, .235, .83, .35),
    ('FishBlue', (.25, .62, .72), .285, .18, .94, .43)
]
for species, color, width, height, length, neck in fish_specs:
    fish = asset(species)
    body_group = group(fish, 'Body')
    tail_group = group(fish, 'Tail', (0, 0, -neck))
    body_mat = mat(species + '_Body', color, vertex_tint=(1, 1, 1, 1))
    belly_mat = mat(species + '_Belly', tuple(min(1, c * 1.14) for c in color), vertex_tint=(.92, .96, 1, 1))
    back_mat = mat(species + '_Back', tuple(c * .75 for c in color), vertex_tint=(.72, .78, .80, 1))
    fin_mat = mat(species + '_Fins', tuple(c * .85 for c in color), vertex_tint=(.72, .83, .87, .9))
    eye_mat = materials.get('FishEye') or mat('FishEye', (.035, .065, .065), vertex_tint=(.035, .065, .065, 1))
    eye_rim = materials.get('FishEyeRim') or mat('FishEyeRim', (.88, .91, .80), vertex_tint=(.92, .95, .87, 1))
    positions = [.59, .48, .32, .10, -.12, -.30, -neck / length]
    radii = [.055, .58, .91, 1, .83, .45, .16]
    vertices, faces = [], []
    for z, factor in zip(positions, radii):
        for edge in range(12):
            angle = edge * math.tau / 12
            vertices.append((math.cos(angle) * width * factor, math.sin(angle) * height * factor, z * length))
    for row in range(len(positions) - 1):
        for edge in range(12):
            nxt = (edge + 1) % 12
            faces.append((row * 12 + edge, (row + 1) * 12 + edge, (row + 1) * 12 + nxt, row * 12 + nxt))
    faces.extend([tuple(range(12)), tuple((len(positions) - 1) * 12 + i for i in reversed(range(12)))])
    body = mesh_part('Tapered sculpted body', body_group, vertices, faces, body_mat)
    body.data.materials.append(belly_mat)
    body.data.materials.append(back_mat)
    for polygon in body.data.polygons:
        center = sum((body.data.vertices[i].co for i in polygon.vertices), Vector()) / len(polygon.vertices)
        polygon.material_index = 1 if center.z < -.03 else 2 if center.z > height * .55 else 0
    for side in (-1, 1):
        eye_x = side * width * .60
        sphere('Raised ivory eye surround', body_group, (eye_x, height * .30, .39 * length), (.016, .043, .043), eye_rim)
        sphere('Dark curious eye', body_group, (eye_x + side * .009, height * .30, .40 * length), (.018, .027, .027), eye_mat)
        points = [(side * width * .95, -.015, .08), (side * (width + .16), -.035, -.18), (side * width * .72, -.03, -.15)]
        verts = [(x, y + offset, z * length) for offset in (-.012, .012) for x, y, z in points]
        mesh_part('Solid swept side fin', body_group, verts, [(0, 2, 1), (3, 4, 5), (0, 1, 4, 3), (1, 2, 5, 4), (2, 0, 3, 5)], fin_mat)
    dorsal = [(0, height * .7, .17 * length), (0, height + (.12 if species != 'FishBlue' else .18), -.08), (0, height * .55, -.30 * length)]
    verts = [(x + offset, y, z) for offset in (-.012, .012) for x, y, z in dorsal]
    mesh_part('Sculpted dorsal fin', body_group, verts, [(0, 2, 1), (3, 4, 5), (0, 1, 4, 3), (1, 2, 5, 4), (2, 0, 3, 5)], fin_mat)
    tail_width = .23 if species != 'FishCoral' else .21
    outline = [(0, 0, .025), (-tail_width, .015, -.32), (0, 0, -.21), (tail_width, .015, -.32)]
    verts = [(x, y + offset, z) for offset in (-.014, .014) for x, y, z in outline]
    mesh_part('Volume fork tail', tail_group, verts, [(0, 2, 1), (0, 3, 2), (4, 5, 6), (4, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)], fin_mat)

# Fewer mesh renderers after baking; rounded hood, brass cage, curved handle and visible warm core.
lantern = asset('Lantern')
frame = group(lantern, 'Frame')
glass_group = group(lantern, 'Lantern Glass')
core_group = group(lantern, 'Warm Lamp Core')
cube('Chamfered wooden stand', frame, (0, .52, 0), (.115, 1.04, .115), materials['LanternWood'], .018)
cylinder('Foot collar', frame, (0, .07, 0), .10, .08, materials['LanternMetal'], 8)
cylinder('Lantern foot', frame, (0, 1.055, 0), .175, .065, materials['LanternMetal'], 12, edge=.014)
cylinder('Warm glass volume', glass_group, (0, 1.245, 0), .132, .32, materials['LanternGlass'], 12, edge=.018)
for angle in range(4):
    rad = angle * math.pi / 2 + math.pi / 4
    cylinder('Rounded cage post', frame, (math.cos(rad) * .14, 1.245, math.sin(rad) * .14), .016, .36, materials['LanternMetal'], 6, edge=.004)
# Profiled shallow dome, not a flat cylinder cap.
hood_vertices, hood_faces = [], []
for radius, y in ((.20, 1.42), (.18, 1.465), (.085, 1.53), (.035, 1.54)):
    for step in range(12):
        a = step * math.tau / 12
        hood_vertices.append((math.cos(a) * radius, y, math.sin(a) * radius))
for row in range(3):
    for step in range(12):
        hood_faces.append((row * 12 + step, row * 12 + (step + 1) % 12, (row + 1) * 12 + (step + 1) % 12, (row + 1) * 12 + step))
hood_faces.append(tuple(36 + i for i in range(12)))
mesh_part('Shaped lantern hood', frame, hood_vertices, hood_faces, materials['LanternMetal'])
sphere('Visible warm crown', core_group, (0, 1.56, 0), (.065, .045, .065), materials['LanternCore'])
handle = [(math.cos(math.pi * i / 12) * .12, 1.53 + math.sin(math.pi * i / 12) * .17, 0) for i in range(13)]
tube('Arched carrying handle', frame, handle, .012, materials['LanternMetal'])

bottle = asset('WaterBottle')
bottle_group = group(bottle, 'Bottle')
profile = [(-.045, .10), (-.015, .145), (.04, .16), (.32, .16), (.39, .145), (.46, .075), (.53, .072)]
vertices, faces = [], []
for y, radius in profile:
    for step in range(12):
        a = step * math.tau / 12
        vertices.append((math.cos(a) * radius, y, math.sin(a) * radius))
for row in range(len(profile) - 1):
    for step in range(12):
        faces.append((row * 12 + step, row * 12 + (step + 1) % 12, (row + 1) * 12 + (step + 1) % 12, (row + 1) * 12 + step))
faces.extend([tuple(reversed(range(12))), tuple((len(profile) - 1) * 12 + i for i in range(12))])
mesh_part('Rounded bottle shoulders', bottle_group, vertices, faces, materials['BottleWater'])
cylinder('Cream wrap label', bottle_group, (0, .20, 0), .164, .145, materials['SupplyCream'], 12, edge=.003)
cylinder('Cork stopper', bottle_group, (0, .56, 0), .083, .08, materials['Rope'], 12, edge=.012)
ring('Neck tie', bottle_group, (0, .49, 0), .079, .009, materials['SupplyCream'], 'xz', 12)
drop = [(0, .255, -.168), (-.034, .197, -.168), (-.026, .175, -.168), (0, .165, -.168), (.026, .175, -.168), (.034, .197, -.168)]
mesh_part('Water seal', bottle_group, drop, [(0, 1, 2, 3, 4, 5)], materials['BottleInk'])

crate = asset('SupplyCrate')
crate_group = group(crate, 'Crate')
cube('Bevelled crate shell', crate_group, (0, .20, 0), (.86, .54, .76), materials['DriftWood'], .025)
for plank in range(4):
    cube('Individual lid board', crate_group, ((plank - 1.5) * .216, .485, 0), (.204, .055, .77), materials['Rope'], .009)
for x in (-.30, .30):
    rope_path = [(x, -.075, -.37), (x, -.075, .37), (x, -.04, .405), (x, .49, .405), (x, .53, .36), (x, .53, -.36), (x, .49, -.405), (x, -.04, -.405)]
    tube('Rounded cream lashing', crate_group, rope_path, .024, materials['SupplyCream'], True)
for z in (-.375, .375):
    cube('Crate edge rail', crate_group, (0, .04, z), (.90, .07, .045), materials['Rope'], .008)
cube('Supply plaque', crate_group, (0, .23, -.397), (.28, .23, .025), materials['SupplyCream'], .015)
sphere('Blue ocean seal', crate_group, (0, .23, -.42), (.065, .075, .01), materials['BottleWater'])

# Export evaluated Blender geometry, with original material names and separately animated pivots.
bpy.context.view_layer.update()
depsgraph = bpy.context.evaluated_depsgraph_get()
manifest = {'generator': 'Blender ' + bpy.app.version_string, 'units': 'metres', 'assets': []}
for entry in assets:
    exported = {'id': entry['id'], 'nodes': [], 'triangles': 0}
    root = entry['root']
    for pivot in entry['groups']:
        origin = pivot.matrix_world.inverted()
        positions, normals, colors = [], [], []
        welded_vertices = {}
        buckets = {}
        for obj in pivot.children_recursive:
            if obj.type not in ('MESH', 'CURVE'):
                continue
            evaluated = obj.evaluated_get(depsgraph)
            mesh = evaluated.to_mesh()
            mesh.calc_loop_triangles()
            transform = origin @ evaluated.matrix_world
            normal_matrix = transform.to_3x3().inverted().transposed()
            for triangle in mesh.loop_triangles:
                material = mesh.materials[triangle.material_index] if mesh.materials else materials['HoneyWood']
                key = material.get('unity_key', material.name)
                bucket = buckets.setdefault(key, [])
                tint = list(material.get('unity_tint', (1, 1, 1, 1)))
                for vertex, corner in zip(triangle.vertices, triangle.loops):
                    point = U(transform @ mesh.vertices[vertex].co)
                    normal = U((normal_matrix @ mesh.corner_normals[corner].vector).normalized())
                    point = tuple(round(component, 7) for component in point)
                    normal = tuple(round(component, 7) for component in normal)
                    key_vertex = (*point, *normal, *tint)
                    if key_vertex not in welded_vertices:
                        welded_vertices[key_vertex] = len(positions)
                        positions.append(dict(zip(('x', 'y', 'z'), point)))
                        normals.append(dict(zip(('x', 'y', 'z'), normal)))
                        colors.append(dict(zip(('r', 'g', 'b', 'a'), tint)))
                    bucket.append(welded_vertices[key_vertex])
            evaluated.to_mesh_clear()
        local = U((root.matrix_world.inverted() @ pivot.matrix_world).translation)
        submeshes = [{'material': key, 'triangles': value} for key, value in buckets.items()]
        exported['nodes'].append({'name': pivot.name, 'position': {'x': local[0], 'y': local[1], 'z': local[2]}, 'vertices': positions, 'normals': normals, 'colors': colors, 'submeshes': submeshes})
        exported['triangles'] += sum(len(value) // 3 for value in buckets.values())
    manifest['assets'].append(exported)
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for obj in root.children_recursive:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    export_props = bpy.ops.export_scene.fbx.get_rna_type().properties
    forward = [item.identifier for item in export_props['axis_forward'].enum_items]
    up = [item.identifier for item in export_props['axis_up'].enum_items]
    kinds = [item.identifier for item in export_props['object_types'].enum_items]
    bpy.ops.export_scene.fbx(filepath=str(FBX / (entry['id'] + '.fbx')), use_selection=True,
        axis_forward='-Z' if '-Z' in forward else forward[0], axis_up='Y' if 'Y' in up else up[0],
        object_types={kind for kind in ('EMPTY', 'MESH', 'OTHER') if kind in kinds}, use_mesh_modifiers=True, bake_anim=False)
    print('BLENDER_ASSET', entry['id'], 'triangles=' + str(exported['triangles']))
(OUT / 'raft_models.json').write_text(json.dumps(manifest, separators=(',', ':')), encoding='utf-8')

# A clean editable studio layout for inspecting the asset family in Blender.
layout = {'DeckTile': (0, .05, .3), 'Lantern': (-2.6, -.4, .8), 'FishSilver': (2.8, .1, 1.15),
          'FishCoral': (2.8, .1, -.30), 'FishBlue': (2.8, .1, -1.70), 'SupplyCrate': (-.85, -.40, -2.30), 'WaterBottle': (-2.35, -.4, -2.10)}
for entry in assets:
    entry['root'].location = B(layout[entry['id']])
    if entry['id'].startswith('Fish'):
        entry['root'].rotation_euler.z = math.radians(-30)
ground_mat = mat('Studio Paper', (.85, .86, .79))
cube('Studio pedestal', None, (0, -.65, -.3), (9, .18, 7), ground_mat, .10)
world = scene.world or bpy.data.worlds.new('Raft Studio World')
scene.world = world
world.use_nodes = True
background = next(node for node in world.node_tree.nodes if node.type == 'BACKGROUND')
background.inputs['Color'].default_value = (.15, .23, .25, 1)
background.inputs['Strength'].default_value = .65
for name, position, power, size in [('Large warm key', (1, 8, 4), 1400, 7), ('Soft fill', (-5, 5, -3), 800, 6)]:
    light_data = bpy.data.lights.new(name, 'AREA')
    light_data.energy = power
    light_data.shape = next(item.identifier for item in light_data.bl_rna.properties['shape'].enum_items if item.identifier == 'DISK')
    light_data.size = size
    obj = bpy.data.objects.new(name, light_data)
    scene.collection.objects.link(obj)
    obj.location = B(position)
    obj.rotation_euler = (B((0, 0, -.5)) - obj.location).to_track_quat('-Z', 'Y').to_euler()
camera_data = bpy.data.cameras.new('Model review camera')
camera = bpy.data.objects.new('Model review camera', camera_data)
scene.collection.objects.link(camera)
camera.location = B((8.5, 9.8, -11))
camera.rotation_euler = (B((0, .25, -.3)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
camera_types = [item.identifier for item in camera_data.bl_rna.properties['type'].enum_items]
camera_data.type = 'ORTHO' if 'ORTHO' in camera_types else camera_types[0]
camera_data.ortho_scale = 10.5
scene.camera = camera
scene.render.resolution_x = 1600
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
formats = [item.identifier for item in scene.render.image_settings.bl_rna.properties['file_format'].enum_items]
scene.render.image_settings.file_format = 'PNG' if 'PNG' in formats else formats[0]
scene.render.filepath = str(PREVIEW / 'Blender_ModelKit.png')
scene.render.film_transparent = False
if hasattr(scene.view_settings, 'view_transform'):
    transforms = [item.identifier for item in scene.view_settings.bl_rna.properties['view_transform'].enum_items]
    if 'AgX' in transforms:
        scene.view_settings.view_transform = 'AgX'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Raft_ModelKit.blend'))
bpy.ops.render.render(write_still=True)
print('BLENDER_MODEL_KIT_OK', sum(entry['triangles'] for entry in manifest['assets']))
