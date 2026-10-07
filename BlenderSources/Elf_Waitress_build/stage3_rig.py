# Stage 3: borrow the Mixamo skeleton of a 1 m Tripo character (T-pose, same bone names as the project's
# heroes) and fit its joints to the elf.
import bpy, sys, math, json
from mathutils import Vector, Matrix
D = "C:/Users/Omistaja/AppData/Local/Temp/claude/D--Unity-3D-DnD-selainpeli/159abac1-df60-4df2-81c6-701ea24cf69b/scratchpad/elf/"
SKEL = "C:/Users/Omistaja/Downloads/Action Adventure Pack/tripo_convert_518b7d3a-bfb7-4f2e-bd7b-543871aa89a8.fbx"
P = {}
if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1:
    P.update(json.loads(sys.argv[sys.argv.index("--") + 1]))

before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=SKEL)
new = [o for o in bpy.data.objects if o not in before]
arm = [o for o in new if o.type == 'ARMATURE'][0]
for o in new:
    if o is not arm: bpy.data.objects.remove(o)
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
arm.animation_data_clear()
arm.name = "Armature"; arm.data.name = "Elf_Waitress_Rig"
bpy.ops.object.select_all(action='DESELECT')
bpy.context.view_layer.objects.active = arm; arm.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
def K(n): return "mixamorig:" + n
# orient the object (keeps bone rolls right): hips -> head = +Z, toes forward = -Y
bl = arm.data.bones
up = (bl[K("Head")].head_local - bl[K("Hips")].head_local).normalized()
R = up.rotation_difference(Vector((0, 0, 1))).to_matrix()
fwd = R @ (bl[K("LeftToeBase")].head_local - bl[K("LeftFoot")].head_local); fwd.z = 0; fwd.normalize()
R = fwd.rotation_difference(Vector((0, -1, 0))).to_matrix() @ R
arm.matrix_world = R.to_4x4() @ arm.matrix_world
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# --- edit in a plain dict of world-space heads; tails follow the children ---
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.data.edit_bones
heads = {b.name: b.head.copy() for b in eb}
tails = {b.name: b.tail.copy() for b in eb}
# ground the feet, centre the hips
minz = min(v.z for v in heads.values())
hip = heads[K("Hips")]
off = Vector((-hip.x, P.get("body_y", -0.02) - hip.y, -minz + P.get("floor", 0.0)))
for n in heads: heads[n] += off; tails[n] += off
# uniform height scale about the floor so the head top lands on the elf's crown
top = heads[K("HeadTop_End")].z
s = P.get("crown", 0.955) / top
for n in heads:
    heads[n] = Vector((heads[n].x * P.get("sx", 1.0), heads[n].y, heads[n].z * s)) if True else heads[n]
    tails[n] = Vector((tails[n].x * P.get("sx", 1.0), tails[n].y, tails[n].z * s))
# --- explicit joint targets measured on the elf (metres, character faces -Y) ---
def move_group(prefix_names, old_root, new_root, scale=1.0):
    for n in list(heads):
        if any(n == K(p) or n.startswith(K(p)) for p in prefix_names):
            for d in (heads, tails):
                d[n] = new_root + (d[n] - old_root) * scale
J = P.get("joints", {})
spine = {"Hips": (0, -0.045, 0.545), "Spine": (0, -0.052, 0.60), "Spine1": (0, -0.056, 0.66), "Spine2": (0, -0.05, 0.725),
         "Neck": (0, -0.035, 0.815), "Head": (0, -0.03, 0.852), "HeadTop_End": (0, -0.03, 0.958)}
spine.update(J.get("spine", {}))
for n, v in spine.items():
    if K(n) in heads: heads[K(n)] = Vector(v)
tails[K("HeadTop_End")] = heads[K("HeadTop_End")] + Vector((0, 0, 0.05))
for side, sg in (("Left", 1), ("Right", -1)):
    A = J.get("arm", {"clav": (0.012, -0.04, 0.79), "sh": (0.085, -0.03, 0.80), "el": (0.218, -0.025, 0.80), "wr": (0.352, -0.022, 0.80), "hand_scale": 0.68})
    def S(v): return Vector((v[0] * sg, v[1], v[2]))
    old_wr = heads[K(side + "Hand")].copy()
    move_group([side + "Hand"], old_wr, S(A["wr"]), A["hand_scale"])
    heads[K(side + "Shoulder")] = S(A["clav"])
    heads[K(side + "Arm")] = S(A["sh"]); heads[K(side + "ForeArm")] = S(A["el"])
    L = J.get("leg", {"hip": (0.052, -0.045, 0.52), "knee": (0.058, -0.035, 0.30), "ankle": (0.062, 0.0, 0.085), "ball": (0.062, -0.062, 0.014), "toe": (0.062, -0.095, 0.012)})
    heads[K(side + "UpLeg")] = S(L["hip"]); heads[K(side + "Leg")] = S(L["knee"]); heads[K(side + "Foot")] = S(L["ankle"])
    heads[K(side + "ToeBase")] = S(L["ball"])
    if K(side + "Toe_End") in heads:
        heads[K(side + "Toe_End")] = S(L["toe"]); tails[K(side + "Toe_End")] = S(L["toe"]) + Vector((0, -0.02, 0))

for b in eb: b.use_connect = False
for b in eb:
    b.head = heads[b.name]
    kids = [c for c in b.children if c.name in heads]
    main = [c for c in kids if "Index" not in c.name and "Ring" not in c.name and "Pinky" not in c.name and "Thumb" not in c.name and "Shoulder" not in c.name and "UpLeg" not in c.name]
    if len(main) == 1 and (heads[main[0].name] - heads[b.name]).length > 1e-4:
        b.tail = heads[main[0].name]
    else:
        b.tail = heads[b.name] + R @ (tails[b.name] - heads[b.name]) * 0 + (tails[b.name] - heads[b.name])
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.armature.select_all(action='SELECT')
bpy.ops.armature.calculate_roll(type='GLOBAL_POS_Z') if False else None
bpy.ops.object.mode_set(mode='OBJECT')

for n in ["Hips", "Spine", "Spine2", "Neck", "Head", "HeadTop_End", "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "LeftHandMiddle1", "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase"]:
    b = arm.data.bones.get(K(n))
    if b: print("J", n, tuple(round(v, 3) for v in b.head_local), tuple(round(v, 3) for v in b.tail_local))
bpy.ops.wm.save_as_mainfile(filepath=D + "stage3.blend")
