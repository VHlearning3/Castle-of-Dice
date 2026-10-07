# Stage 4: skin the elf to the rig (bone heat + hair fixes), hang the trays on the hands.
import bpy, bmesh, sys
from mathutils import Vector, Matrix
D = "C:/Users/Omistaja/AppData/Local/Temp/claude/D--Unity-3D-DnD-selainpeli/159abac1-df60-4df2-81c6-701ea24cf69b/scratchpad/elf/"
sys.path.insert(0, D)
from poselib import K

arm = bpy.data.objects["Armature"]
body = bpy.data.objects["Elf_Waitress_Body"]
arm.name = "Elf_Waitress"  # FBX root node name in Unity
for pb in arm.pose.bones:
    pb.rotation_mode = 'QUATERNION'
    pb.matrix_basis = Matrix()  # the imported skeleton kept the first frame of its Mixamo take
bpy.context.view_layer.update()

# Bone heat fails on the Tripo mesh (239 loose shells), so weight a watertight voxel proxy and copy its weights.
proxy = body.copy(); proxy.data = body.data.copy(); proxy.name = "WeightProxy"
bpy.context.scene.collection.objects.link(proxy)
rm = proxy.modifiers.new("remesh", 'REMESH'); rm.mode = 'VOXEL'; rm.voxel_size = P_VOXEL = 0.0045
bpy.ops.object.select_all(action='DESELECT')
bpy.context.view_layer.objects.active = proxy; proxy.select_set(True)
bpy.ops.object.modifier_apply(modifier="remesh")
dc = proxy.modifiers.new("dec", 'DECIMATE'); dc.ratio = min(1.0, 40000 / max(1, len(proxy.data.polygons)))
bpy.ops.object.modifier_apply(modifier="dec")
print("PROXY faces", len(proxy.data.polygons))
# bone heat is unreliable at 1 m scale; solve it 10x bigger
proxy.scale = (10, 10, 10); arm.scale = (10, 10, 10)
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT')
proxy.select_set(True); arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
print("PROXY unweighted", sum(1 for v in proxy.data.vertices if not v.groups))
proxy.parent = None
proxy.scale = (1, 1, 1); arm.scale = (1, 1, 1); proxy.matrix_world = Matrix()
bpy.context.view_layer.update()
for g in proxy.vertex_groups:
    body.vertex_groups.new(name=g.name)
dt = body.modifiers.new("dt", 'DATA_TRANSFER')
dt.object = proxy; dt.use_vert_data = True; dt.data_types_verts = {'VGROUP_WEIGHTS'}
dt.vert_mapping = 'POLYINTERP_NEAREST'
dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
bpy.ops.object.select_all(action='DESELECT')
bpy.context.view_layer.objects.active = body; body.select_set(True)
bpy.ops.object.modifier_apply(modifier="dt")
bpy.data.objects.remove(proxy)
body.parent = arm
am = body.modifiers.new("Armature", 'ARMATURE'); am.object = arm
vg = body.vertex_groups
empty = sum(1 for v in body.data.vertices if not v.groups)
print("UNWEIGHTED after heat:", empty)

# --- loose parts ---
bm = bmesh.new(); bm.from_mesh(body.data); bm.verts.ensure_lookup_table()
seen = set(); parts = []
for v in bm.verts:
    if v.index in seen: continue
    st = [v]; comp = []; seen.add(v.index)
    while st:
        a = st.pop(); comp.append(a.index)
        for e in a.link_edges:
            b = e.other_vert(a)
            if b.index not in seen: seen.add(b.index); st.append(b)
    parts.append(comp)
co = [v.co.copy() for v in body.data.vertices]
def bbox(p):
    return [min(co[i][k] for i in p) for k in range(3)], [max(co[i][k] for i in p) for k in range(3)]

def set_weights(idx, table):
    for g in vg:
        g.remove([idx])
    for name, w in table.items():
        if w > 1e-4:
            g = vg.get(name) or vg.new(name=name)
            g.add([idx], w, 'REPLACE')

# prune influences from bones that are far from the vertex (the proxy blends straps and ears into the arms)
bones = {b.name: (b.head_local.copy(), b.tail_local.copy()) for b in arm.data.bones}
def seg_dist(p, a, b):
    ab = b - a; t = 0.0 if ab.length_squared < 1e-12 else max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
    return (a + ab * t - p).length
PRUNE = 0.075
pruned = 0
for v in body.data.vertices:
    ws = {vg[g.group].name: g.weight for g in v.groups if g.weight > 0}
    keep = {n: w for n, w in ws.items() if n in bones and seg_dist(v.co, *bones[n]) < PRUNE}
    if not keep:
        n = min(bones, key=lambda n: seg_dist(v.co, *bones[n]))
        keep = {n: 1.0}
    if len(keep) != len(ws):
        pruned += 1
        set_weights(v.index, keep)
print("PRUNED", pruned)
# below the arms and behind the shoulder line is hair or back, never arm
armish = ("Arm", "ForeArm", "Hand")
fixed = 0
for v in body.data.vertices:
    if v.co.z < 0.765 or (v.co.y > 0.025 and v.co.z < 0.80):
        ws = {vg[g.group].name: g.weight for g in v.groups if g.weight > 0}
        keep = {n: w for n, w in ws.items() if not any(n.startswith(K("Left") + a) or n.startswith(K("Right") + a) for a in armish)}
        if len(keep) != len(ws):
            if not keep: keep = {K("Spine2"): 1.0}
            set_weights(v.index, keep); fixed += 1
print("ARM-FREE", fixed)

tails = bunches = 0
for p in parts:
    mn, mx = bbox(p)
    cx, cz = (mn[0] + mx[0]) / 2, (mn[2] + mx[2]) / 2
    if cz > 0.80 and mn[2] > 0.77 and abs(cx) < 0.12 and mx[1] - mn[1] < 0.2:  # ears, face, fringe, bunches
        for i in p:
            if co[i].z > 0.83: set_weights(i, {K("Head"): 1.0})
            else: set_weights(i, {K("Head"): 0.6, K("Neck"): 0.4})
        continue
    mn, mx = bbox(p)
    # twin tails: hang behind the back beside the spine, from the bunches down to the hips
    if mn[1] > 0.0 and mn[2] > 0.40 and mx[2] < 0.84 and abs((mn[0] + mx[0]) / 2) > 0.07:
        tails += 1
        for i in p:
            z = co[i].z
            h = max(0.0, min(1.0, (z - 0.66) / 0.14))
            s = max(0.0, min(1.0, (z - 0.50) / 0.16))
            set_weights(i, {K("Head"): 0.75 * h, K("Neck"): 0.25 * h, K("Spine2"): (1 - h) * s + (1 - h) * (1 - s) * 0.7, K("Spine1"): (1 - h) * (1 - s) * 0.3})
    # hair bunches / ties beside the head
    elif mn[2] > 0.79 and mx[2] < 0.9 and abs((mn[0] + mx[0]) / 2) > 0.05 and mn[1] > -0.03:
        bunches += 1
        for i in p: set_weights(i, {K("Head"): 1.0})
print("TAILS", tails, "BUNCHES", bunches)
# head-only region: anything above the neck that is not near the shoulders
for i, c in enumerate(co):
    if c.z > 0.86 and abs(c.x) < 0.12:
        set_weights(i, {K("Head"): 1.0})
# keep the face and the fringe off the neck/shoulders
bm.free()

# --- limit to 4 influences and normalise (Unity default quality) ---
bpy.context.view_layer.objects.active = body
bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
bpy.ops.object.mode_set(mode='OBJECT')

# --- trays on the hands (bone-parented, in rest space; palm normal at rest = -Z) ---
def hang(tray_name, hand, side):
    t = bpy.data.objects[tray_name]
    hb = arm.data.bones[K(hand)]
    mid = arm.data.bones[K(hand + "Middle1")]
    palm = (hb.head_local * 0.45 + mid.head_local * 0.55)
    # tray upside down under the palm (so it stands on the palm once the palm turns up)
    M = Matrix.Translation(palm + Vector((0, 0, -0.014))) @ Matrix.Rotation(3.14159265, 4, 'Y')
    t.parent = None
    t.matrix_world = M
    t.parent = arm; t.parent_type = 'BONE'; t.parent_bone = K(hand)
    t.matrix_world = M
hang("Tray_Bread", "RightHand", -1)
hang("Tray_Beer", "LeftHand", 1)
bpy.ops.wm.save_as_mainfile(filepath=D + "stage4.blend")
print("STAGE4 OK")
