# Stage 1: import the Tripo elf, re-lay her UVs (bigger face, smaller skirt, no overlaps) and bake a 2048 texture.
import bpy, bmesh, sys, math
from mathutils import Vector
D = "C:/Users/Omistaja/AppData/Local/Temp/claude/D--Unity-3D-DnD-selainpeli/159abac1-df60-4df2-81c6-701ea24cf69b/scratchpad/elf/"
SRC = "C:/Users/Omistaja/Downloads/Brown fantasy elf 3d model.glb"
TEX = 2048

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
o = [o for o in bpy.data.objects if o.type == 'MESH'][0]
o.name = o.data.name = "Elf_Waitress_Body"
bpy.context.view_layer.objects.active = o
o.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = o.data
old_uv = me.uv_layers[0]
old_uv.name = "UV_Tripo"
new_uv = me.uv_layers.new(name="UVMap")
me.uv_layers.active = new_uv

# --- islands by union-find over loops that share a vertex and the same UV ---
bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
uvl = bm.loops.layers.uv["UVMap"]
parent = list(range(len(bm.faces)))
def find(i):
    while parent[i] != i:
        parent[i] = parent[parent[i]]; i = parent[i]
    return i
for e in bm.edges:
    fs = e.link_faces
    if len(fs) < 2: continue
    for a in range(len(fs) - 1):
        f1, f2 = fs[a], fs[a + 1]
        def uvs(f):
            d = {}
            for l in f.loops: d[l.vert.index] = l[uvl].uv.copy()
            return d
        u1, u2 = uvs(f1), uvs(f2)
        if all((u1[v.index] - u2[v.index]).length < 1e-5 for v in e.verts):
            ra, rb = find(f1.index), find(f2.index)
            if ra != rb: parent[ra] = rb
islands = {}
for f in bm.faces: islands.setdefault(find(f.index), []).append(f)
print("ISLANDS", len(islands))

def factor(faces):
    c = Vector(); n = Vector(); area = 0
    for f in faces:
        a = f.calc_area(); c += f.calc_center_median() * a; n += f.normal * a; area += a
    c /= max(area, 1e-9)
    if c.z > 0.80 and abs(c.x) < 0.085 and c.y < 0.0 and n.y < 0:  # face, eyes, fringe
        return 2.6
    if c.z > 0.78 and abs(c.x) < 0.13:  # rest of head, ears, hair top
        return 1.25
    if abs(c.x) > 0.25 and c.z > 0.6:   # forearms, gloves, hands
        return 1.3
    if 0.55 < c.z <= 0.80 and abs(c.x) < 0.16:  # blouse, corset, ribbon
        return 1.35
    if c.z < 0.55 and abs(c.x) < 0.2 and abs(n.x) < area * 0.9:  # skirt and legs
        return 0.8
    return 1.0

for k, faces in islands.items():
    s = factor(faces)
    if s == 1.0: continue
    cen = Vector((0, 0)); cnt = 0
    for f in faces:
        for l in f.loops: cen += l[uvl].uv; cnt += 1
    cen /= cnt
    for f in faces:
        for l in f.loops: l[uvl].uv = cen + (l[uvl].uv - cen) * s
bm.to_mesh(me); bm.free()

bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.select_all(action='SELECT')
bpy.ops.uv.pack_islands(udim_source='CLOSEST_UDIM', rotate=True, rotate_method='ANY', scale=True,
                        merge_overlap=False, margin_method='FRACTION', margin=0.004, shape_method='CONCAVE')
bpy.ops.object.mode_set(mode='OBJECT')

# --- bake old texture into the new layout ---
src_img = bpy.data.images[0]
dst = bpy.data.images.new("Elf_Waitress_BaseColor", TEX, TEX, alpha=False)
mat = o.material_slots[0].material
mat.name = "M_Elf_Waitress"
nt = mat.node_tree
bsdf = nt.nodes["Principled BSDF"]
tex_old = [n for n in nt.nodes if n.type == 'TEX_IMAGE'][0]
uvnode = nt.nodes.new("ShaderNodeUVMap"); uvnode.uv_map = "UV_Tripo"
nt.links.new(uvnode.outputs["UV"], tex_old.inputs["Vector"])
emit = nt.nodes.new("ShaderNodeEmission")
nt.links.new(tex_old.outputs["Color"], emit.inputs["Color"])
out = nt.nodes["Material Output"]
nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
tdst = nt.nodes.new("ShaderNodeTexImage"); tdst.image = dst
nt.nodes.active = tdst
sc = bpy.context.scene
sc.render.engine = 'CYCLES'
sc.cycles.device = 'CPU'
sc.cycles.samples = 1
sc.render.bake.margin = 16
sc.render.bake.use_selected_to_active = False
bpy.ops.object.bake(type='EMIT', uv_layer="UVMap", margin=16)
dst.filepath_raw = D + "Elf_Waitress_BaseColor.png"
dst.file_format = 'PNG'
dst.save()

# --- final material: new texture on the new UV, drop the Tripo UV + 4k image ---
for n in list(nt.nodes):
    if n not in (bsdf, out): nt.nodes.remove(n)
t = nt.nodes.new("ShaderNodeTexImage"); t.image = dst
nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
bsdf.inputs["Roughness"].default_value = 0.8
bsdf.inputs["Metallic"].default_value = 0.0
dst.filepath = D + "Elf_Waitress_BaseColor.png"
dst.source = 'FILE'
me.uv_layers.remove(me.uv_layers["UV_Tripo"])
bpy.data.images.remove(src_img)
bpy.ops.wm.save_as_mainfile(filepath=D + "stage1.blend")
print("STAGE1 OK", len(me.polygons))
