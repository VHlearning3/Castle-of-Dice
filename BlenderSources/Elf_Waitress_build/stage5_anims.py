# Stage 5: five takes on the elf rig: Idle, Walk (both carrying trays), Serve (hand-made), Attack, Die.
import bpy, sys, math
from mathutils import Vector, Matrix, Quaternion
D = "C:/Users/Omistaja/AppData/Local/Temp/claude/D--Unity-3D-DnD-selainpeli/159abac1-df60-4df2-81c6-701ea24cf69b/scratchpad/elf/"
sys.path.insert(0, D)
import poselib, retarget
from poselib import K

AAP = "C:/Users/Omistaja/Downloads/Action Adventure Pack/"
KN = "D:/Unity/3D DnD selainpeli/Assets/Characters/Player_knight_anims/"
sc = bpy.context.scene
sc.render.fps = 30
tgt = bpy.data.objects["Elf_Waitress"]
for pb in tgt.pose.bones:
    pb.rotation_mode = 'QUATERNION'; pb.matrix_basis = Matrix()
bpy.context.view_layer.update()

ARM_BONES = [K(s + n) for s in ("Left", "Right") for n in ("Shoulder", "Arm", "ForeArm", "Hand")]
FINGERS = [b.name for b in tgt.data.bones if "Hand" in b.name and b.name not in ARM_BONES]

def pose_quats(pose):
    for pb in tgt.pose.bones: pb.matrix_basis = Matrix()
    bpy.context.view_layer.update()
    poselib.apply_pose(tgt, pose)
    q = {n: tgt.pose.bones[n].rotation_quaternion.copy() for n in ARM_BONES}
    for pb in tgt.pose.bones: pb.matrix_basis = Matrix()
    bpy.context.view_layer.update()
    return q
CARRY = pose_quats(poselib.carry_pose())
offer = [p for p in poselib.carry_pose()]
offer[1] = ("LeftArm", (0.25, -0.75, -0.62), (0, -1, 0), (0, 0, 1))
offer[2] = ("LeftForeArm", (0.0, -1.0, 0.25), (0, 0, -1), (-0.4, 0, 1.0))
offer[3] = ("LeftHand", (0.0, -1.0, 0.05), (0, 0, -1), (0, 0, 1))
OFFER = pose_quats(offer)

def fcurve_set(act, bone, quats_by_frame):
    """Replace the rotation keys of one bone with the given {frame: quat}."""
    tgt.animation_data.action = act
    pb = tgt.pose.bones[bone]
    path = 'pose.bones["%s"].rotation_quaternion' % bone
    for fc in [fc for fc in retarget_fcurves(act) if fc.data_path == path]:
        fc.keyframe_points.clear()
    for f, q in quats_by_frame.items():
        pb.rotation_quaternion = q
        pb.keyframe_insert("rotation_quaternion", frame=f, group=bone)

def retarget_fcurves(act):
    # Blender 4.4+ slotted actions keep curves in layers/strips/channelbags
    if hasattr(act, "layers") and len(act.layers):
        out = []
        for layer in act.layers:
            for strip in layer.strips:
                for cb in strip.channelbags: out.extend(cb.fcurves)
        return out
    return list(act.fcurves)

def frames_of(act):
    return range(int(act.frame_range[0]), int(act.frame_range[1]) + 1)

def carry_overlay(act):
    for n in ARM_BONES:
        fcurve_set(act, n, {f: CARRY[n] for f in (frames_of(act)[0], frames_of(act)[-1])})
    for n in FINGERS:
        fcurve_set(act, n, {f: Quaternion() for f in (frames_of(act)[0], frames_of(act)[-1])})

def take(path, name, **kw):
    src, act, objs = retarget.import_take(path)
    a = retarget.retarget(tgt, src, act, name, **kw)
    for o in objs:
        if o.name in bpy.data.objects: bpy.data.objects.remove(o)
    bpy.data.actions.remove(act)
    return a

acts = {}
acts["Idle"] = take(AAP + "idle (2).fbx", "Idle")
carry_overlay(acts["Idle"])
acts["Walk"] = take(AAP + "walking.fbx", "Walk")
carry_overlay(acts["Walk"])
acts["Attack"] = take(KN + "sword and shield attack (4).fbx", "Attack")
acts["Die"] = take(KN + "sword and shield death.fbx", "Die", in_place=False)

# Serve: idle legs, a small bow and nod, the beer tray offered forward and back (frames 1-60)
serve = take(AAP + "idle (2).fbx", "Serve", frame_range=(1, 60))
carry_overlay(serve)
def smooth(t): return t * t * (3 - 2 * t)
def w_at(f):
    if f <= 1: return 0.0
    if f < 18: return smooth((f - 1) / 17)
    if f <= 38: return 1.0
    if f < 56: return smooth(1 - (f - 38) / 18)
    return 0.0
keys = list(range(1, 61, 3)) + [60]
for n in ARM_BONES:
    fcurve_set(serve, n, {f: CARRY[n].slerp(OFFER[n], w_at(f)) for f in keys})
def add_bend(act, bone, axis_world, deg, weight):
    rest = tgt.data.bones[bone].matrix_local.to_3x3().normalized()
    ax = (rest.inverted() @ Vector(axis_world)).normalized()
    tgt.animation_data.action = act
    pb = tgt.pose.bones[bone]
    base = {}
    for f in keys:
        sc.frame_set(f); base[f] = pb.rotation_quaternion.copy()
    fcurve_set(act, bone, {f: base[f] @ Quaternion(ax, math.radians(deg) * weight(f)) for f in keys})
add_bend(serve, K("Spine1"), (1, 0, 0), 7, w_at)
add_bend(serve, K("Spine2"), (1, 0, 0), 7, w_at)
add_bend(serve, K("Head"), (1, 0, 0), 12, lambda f: w_at(f) * (1.0 if f < 30 else 0.6))
acts["Serve"] = serve

# one NLA track per take (FBX export: one take each), nothing left active
tgt.animation_data.action = None
for name in ("Idle", "Walk", "Serve", "Attack", "Die"):
    a = acts[name]
    a.use_fake_user = True
    tr = tgt.animation_data.nla_tracks.new(); tr.name = name
    st = tr.strips.new(name, int(a.frame_range[0]), a); st.name = name
    tr.mute = True
    print("TAKE", name, tuple(a.frame_range))
for pb in tgt.pose.bones: pb.matrix_basis = Matrix()
bpy.ops.wm.save_as_mainfile(filepath=D + "stage5.blend")
print("STAGE5 OK")
