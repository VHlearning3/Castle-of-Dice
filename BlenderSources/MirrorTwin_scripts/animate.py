# The Mirror Twin's takes (30 fps): Idle, Walk, Attack (trident thrust), Cast (lightning), Laugh, Die.
# Poses are written as rotations in the armature's rest axes (X = her left, -Y = forward, Z = up), each applied
# in its parent's frame: a bone's pose basis is Rest^-1 * L * Rest. Walk and Die reuse the blonde elf's takes with
# the trident arm held steady.
# Run: blender -b MirrorTwin_model.blend --python animate.py -- <work dir>
import bpy, math, sys, os
from mathutils import Matrix, Vector, Quaternion

W = sys.argv[sys.argv.index("--") + 1]
P = "mixamorig:"
arm = bpy.data.objects["Armature"]
bones = arm.data.bones
pbones = arm.pose.bones
REST = {b.name: b.matrix_local.to_3x3() for b in bones}
I3 = Matrix.Identity(3)


def R(axis, deg):
    return Matrix.Rotation(math.radians(deg), 3, axis)


def chain(*rots):
    """Rotations applied first to last: chain(Ry(-75), Rx(-15)) lowers the arm, then swings it forward."""
    m = I3.copy()
    for r in rots:
        m = r @ m
    return m


PARENT = {b.name[len(P):]: (b.parent.name[len(P):] if b.parent else None) for b in bones if b.name.startswith(P)}
REST_DIR = {b.name[len(P):]: (b.tail_local - b.head_local).normalized() for b in bones if b.name.startswith(P)}


def _depth(n):
    d = 0
    while PARENT[n]:
        n = PARENT[n]
        d += 1
    return d


ORDER = sorted(PARENT, key=_depth)


def aim(d, frame=None):
    """Points the bone along <d> (armature axes, or <frame> bone's posed axes)."""
    return ("aim", Vector(d).normalized(), frame)


def grip(trident_dir, hand_dir, frame=None):
    """Turns the right hand so the trident points along trident_dir and the knuckles toward hand_dir."""
    return ("grip", Vector(trident_dir).normalized(), Vector(hand_dir).normalized(), frame)


def two_axis(a1, a2, t1, t2):
    def frame3(u, v):
        u = u.normalized()
        w = u.cross(v).normalized()
        return Matrix((u, w.cross(u), w)).transposed()
    return frame3(t1, t2) @ frame3(a1, a2).transposed()


def resolve(pose):
    """Turns aim/grip entries into rotations, walking the skeleton from the hips out."""
    G, out = {}, {}
    for name in ORDER:
        par = PARENT[name]
        Gp = G[par] if par else I3
        spec = pose.get(name, I3)
        if isinstance(spec, tuple):
            F = G[spec[-1]] if spec[-1] else I3
            if spec[0] == "aim":
                L = REST_DIR[name].rotation_difference(Gp.inverted() @ (F @ spec[1])).to_matrix()
            else:
                L = two_axis(Vector((0, -1, 0)), REST_DIR[name], Gp.inverted() @ (F @ spec[1]), Gp.inverted() @ (F @ spec[2]))
        else:
            L = spec
        out[name] = L
        G[name] = Gp @ L
    return out


def basis(name, L):
    rest = REST[P + name]
    return (rest.inverted() @ L @ rest).to_quaternion()


# ---------------------------------------------------------------- poses
RIGHT_HOLD = {   # the trident upright at her right side, a little forward
    "RightShoulder": R('Y', -4),
    "RightArm": chain(R('Y', -72), R('X', -14)),
    "RightForeArm": R('Z', 78),
    "RightHand": chain(R('X', -8), R('Z', -6)),
}
LEFT_HIP = {     # hand on hip, elbow out
    "LeftShoulder": R('Y', 3),
    "LeftArm": aim((0.55, 0.16, -0.82), "Spine2"),
    "LeftForeArm": aim((-0.6, -0.16, -0.78), "Spine2"),
    "LeftHand": aim((-0.2, -0.25, -0.95), "Spine2"),
}


def idle_pose(t=0.0):
    b = math.sin(2 * math.pi * t)          # breathing, one cycle per loop
    s = math.sin(2 * math.pi * t + 1.2)
    p = {
        "Hips": chain(R('Y', -3), R('Z', 6)),
        "Spine": chain(R('X', -3 + 0.6 * b), R('Y', 2)),
        "Spine1": R('X', -1 + 0.8 * b),
        "Spine2": chain(R('X', -2 + 1.0 * b), R('Z', -4)),
        "Neck": R('X', -2),
        "Head": chain(R('X', -7 + 1.2 * s), R('Z', -10), R('Y', 4)),
        "LeftUpLeg": chain(R('Y', 4), R('X', 2)),
        "LeftLeg": R('X', 2),
        "LeftFoot": R('Z', 8),
        "RightUpLeg": chain(R('X', -10), R('Y', -6)),
        "RightLeg": R('X', 16),
        "RightFoot": chain(R('X', -6), R('Z', -14)),
    }
    p.update(RIGHT_HOLD)
    p.update(LEFT_HIP)
    return p, Vector((0.025, 0.0, -0.012 + 0.002 * b))


def thrust_windup():
    p, hips = idle_pose(0.0)
    p.update({
        "Spine": chain(R('X', 4), R('Z', -14)),
        "Spine1": R('Z', -10),
        "Spine2": chain(R('X', 3), R('Z', -10)),
        "Head": chain(R('X', -4), R('Z', 22)),
        "RightShoulder": R('Z', -6),
        "RightArm": aim((-0.28, 0.3, -0.91), "Spine2"),
        "RightForeArm": aim((0.42, -0.9, -0.08), "Spine2"),
        "RightHand": grip((0.02, -1.0, 0.1), (1.0, 0.0, -0.35), "Spine2"),
        "LeftArm": chain(R('Z', -55), R('Y', 30)),
        "LeftForeArm": R('Z', -35),
        "LeftHand": R('Z', 10),
        "RightUpLeg": chain(R('X', 12), R('Y', -6)),
        "RightLeg": R('X', 22),
        "LeftUpLeg": R('X', -14),
        "LeftLeg": R('X', 18),
    })
    return p, Vector((0.0, 0.03, -0.05))


def thrust_strike():
    p, hips = idle_pose(0.0)
    p.update({
        "Hips": R('Z', 10),
        "Spine": chain(R('X', 10), R('Z', 12)),
        "Spine1": chain(R('X', 4), R('Z', 8)),
        "Spine2": chain(R('X', 4), R('Z', 6)),
        "Head": chain(R('X', 2), R('Z', -16)),
        "RightShoulder": R('Z', 8),
        "RightArm": aim((-0.1, -0.98, 0.12), "Spine2"),
        "RightForeArm": aim((0.3, -0.95, 0.06), "Spine2"),
        "RightHand": grip((0.06, -1.0, 0.06), (1.0, 0.0, -0.35), "Spine2"),
        "LeftArm": chain(R('Y', 70), R('X', 40)),
        "LeftForeArm": R('Z', -20),
        "LeftHand": R('Y', 10),
        "RightUpLeg": chain(R('X', -42), R('Y', -4)),
        "RightLeg": R('X', 38),
        "RightFoot": R('X', 8),
        "LeftUpLeg": R('X', 22),
        "LeftLeg": R('X', 12),
        "LeftFoot": R('X', -16),
    })
    return p, Vector((0.0, -0.16, -0.09))


def cast_raise():
    p, hips = idle_pose(0.0)
    p.update({
        "Spine": R('X', -8),
        "Spine2": R('X', -6),
        "Head": R('X', -14),
        "RightShoulder": R('Y', 12),
        "RightArm": chain(R('Y', 62), R('Z', 20)),
        "RightForeArm": R('Y', 30),
        "RightHand": chain(R('X', 70), R('Z', -10)),
        "LeftArm": chain(R('Y', 40), R('Z', -30)),
        "LeftForeArm": R('Z', -50),
    })
    return p, Vector((0.0, 0.02, -0.01))


def cast_point():
    p, hips = idle_pose(0.0)
    p.update({
        "Spine": chain(R('X', 6), R('Z', 6)),
        "Spine2": chain(R('X', 4), R('Z', 4)),
        "Head": chain(R('X', -2), R('Z', -8)),
        "RightShoulder": R('Z', 6),
        "RightArm": chain(R('Z', 80), R('Y', 8)),
        "RightForeArm": R('Z', 4),
        "RightHand": R('Z', -86),
        "LeftArm": chain(R('Z', -78), R('Y', 12)),
        "LeftForeArm": R('Z', -12),
        "LeftHand": chain(R('Z', 60), R('X', 10)),
        "RightUpLeg": R('X', -18),
        "RightLeg": R('X', 14),
        "LeftUpLeg": R('X', 10),
    })
    return p, Vector((0.0, -0.05, -0.04))


def laugh_pose(t, phase):
    """Head thrown back, left hand to her chest, shoulders shaking with each 'ha'."""
    p, hips = idle_pose(0.0)
    shake = math.sin(2 * math.pi * t * 9) * phase      # nine 'ha's over the take
    p.update({
        "Spine": chain(R('X', -8 * phase - 1.5 * shake), R('Y', 3)),
        "Spine1": R('X', -4 * phase - 1.5 * shake),
        "Spine2": chain(R('X', -5 * phase - 2.5 * shake), R('Z', -4)),
        "Neck": R('X', -6 * phase),
        "Head": chain(R('X', -24 * phase - 3 * shake), R('Z', -8), R('Y', 6 * phase)),
        "LeftArm": aim((0.15, -0.38, -0.91), "Spine2"),
        "LeftForeArm": aim((-0.62, -0.31, 0.71), "Spine2"),
        "LeftHand": aim((-0.85, -0.25, 0.45), "Spine2"),
    })
    return p, hips + Vector((0.0, 0.015 * phase, -0.004 * abs(shake)))


# ---------------------------------------------------------------- keying
def new_action(name):
    a = bpy.data.actions.get(name)
    if a:
        bpy.data.actions.remove(a)
    a = bpy.data.actions.new(name)
    a.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = a
    return a


def key(frame, pose, hips_world=Vector()):
    pose = resolve(pose)
    for pb in pbones:
        if not pb.name.startswith(P):
            continue          # the skirt bones are swung in Unity (SkirtSway)
        short = pb.name[len(P):]
        L = pose.get(short, I3)
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = basis(short, L)
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        if short == "Hips":
            pb.location = REST[pb.name].inverted() @ hips_world
            pb.keyframe_insert("location", frame=frame)
        else:
            pb.location = (0, 0, 0)


def lerp_pose(a, b, t):
    a, b = resolve(a), resolve(b)
    out = {}
    for k in set(a) | set(b):
        qa = a.get(k, I3).to_quaternion()
        qb = b.get(k, I3).to_quaternion()
        out[k] = qa.slerp(qb, t).to_matrix()
    return out


def smooth(t):
    return t * t * (3 - 2 * t)


def fcurves_of(a):
    """The action's F-curves (Blender 5 keeps them in the slot's channel bag)."""
    try:
        return a.fcurves
    except AttributeError:
        from bpy_extras import anim_utils
        return anim_utils.action_get_channelbag_for_slot(a, a.slots[0]).fcurves


def finish(a, cyclic=False):
    for fc in fcurves_of(a):
        for kp in fc.keyframe_points:
            kp.interpolation = 'BEZIER'
        if cyclic:
            fc.modifiers.new('CYCLES')


# Idle: 90 frames, breathing, a slow look around
a = new_action("Twin_Idle")
for f in range(1, 91, 6):
    p, h = idle_pose((f - 1) / 90.0)
    key(f, p, h)
p, h = idle_pose(1.0)
key(91, p, h)
finish(a)

# Attack: wind up, lunge and thrust, hold, recover (45 frames)
a = new_action("Twin_Attack")
idle0, hip0 = idle_pose(0.0)
wind, hipw = thrust_windup()
strike, hips_ = thrust_strike()
for f, (pa, ha), (pb, hb), t in [
        (1, (idle0, hip0), (idle0, hip0), 0),
        (12, (idle0, hip0), (wind, hipw), 1),
        (17, (wind, hipw), (strike, hips_), 0.7),
        (19, (wind, hipw), (strike, hips_), 1),
        (27, (strike, hips_), (strike, hips_), 1),
        (45, (strike, hips_), (idle0, hip0), 1)]:
    key(f, lerp_pose(pa, pb, t), ha.lerp(hb, t))
finish(a)

# Cast: raise the trident, point it, lightning leaves the tip at frame ~26 (48 frames)
a = new_action("Twin_Cast")
up, hu = cast_raise()
pt, hp = cast_point()
for f, pose, hh in [(1, idle0, hip0), (14, up, hu), (20, up, hu), (25, pt, hp), (34, pt, hp), (48, idle0, hip0)]:
    key(f, pose, hh)
finish(a)

# Laugh: 90 frames, builds up, shakes, settles
a = new_action("Twin_Laugh")
for f in range(1, 92, 2):
    t = (f - 1) / 90.0
    phase = smooth(min(1.0, t / 0.18)) * smooth(min(1.0, (1.0 - t) / 0.2))
    p, h = laugh_pose(t, max(phase, 0.35))
    key(f, lerp_pose(idle0, p, phase), hip0.lerp(h, phase))
finish(a)


# Hit: a flinch, 20 frames
a = new_action("Twin_Hit")
hit = dict(idle0)
hit.update({"Spine": chain(R('X', -12), R('Z', 8)), "Spine2": R('X', -6), "Head": chain(R('X', 10), R('Z', 14)),
            "LeftArm": aim((0.7, -0.3, -0.65), "Spine2"), "LeftForeArm": aim((0.2, -0.75, 0.6), "Spine2")})
for f, pose, hh in [(1, idle0, hip0), (5, hit, hip0 + Vector((0, 0.05, -0.02))), (20, idle0, hip0)]:
    key(f, pose, hh)
finish(a)


# Walk and Die: the blonde elf's takes, with the trident arm held as in Idle
def adapt(src_name, dst_name, hold_arm=True, extra_left_hip=False):
    src = bpy.data.actions[src_name]
    dst = src.copy()
    dst.name = dst_name
    dst.use_fake_user = True
    if hold_arm:
        held = {P + k for k in RIGHT_HOLD}
        fcs = fcurves_of(dst)
        frames = sorted({int(k.co.x) for fc in fcs for k in fc.keyframe_points})
        for fc in list(fcs):
            if any(fc.data_path.startswith('pose.bones["%s"]' % b) for b in held):
                fcs.remove(fc)
        arm.animation_data.action = dst
        if dst.slots:
            arm.animation_data.action_slot = dst.slots[0]
        for f in (frames[0], frames[-1]):
            for k, L in RIGHT_HOLD.items():
                pb = pbones[P + k]
                pb.rotation_quaternion = basis(k, L)
                pb.keyframe_insert("rotation_quaternion", frame=f)
    return dst


adapt("Walk", "Twin_Walk")
adapt("Die", "Twin_Die", hold_arm=False)

bpy.context.scene.render.fps = 30
arm.animation_data.action = None
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(W, "MirrorTwin_anim.blend"))
print("ANIMATED", [a.name for a in bpy.data.actions])
