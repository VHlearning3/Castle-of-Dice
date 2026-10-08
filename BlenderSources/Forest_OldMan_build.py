# Castle of Dice: the old hermit of the Whispering Woods (Zone_2_ForestPath), sitting on a log by the cottage.
# Same build as the tavern patrons (Tavern_Patrons_build.py): rigid low-poly parts on the shared Mixamo skeleton,
# each part weighted 100% to one bone. Long white beard, brown hooded traveller's robe, a walking staff in his
# right hand. Two takes: Sit_Idle (breathing, slow sway) and Sit_Speak (nods, lifts his left hand).
# Run:  blender -b --factory-startup --python BlenderSources/Forest_OldMan_build.py
# Writes BlenderSources/Forest_OldMan.blend and Assets/Characters/Forest_OldMan/Forest_OldMan.fbx + palette.
# Prints SEAT_HEIGHT (seat above the soles, in rig units): the Unity setup scales him so it matches the log.
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
sys.path.insert(0, os.path.join(HERE, "Elf_Waitress_build"))
import poselib                    # shared with the elf waitress build
from poselib import K

OUT = os.path.join(ROOT, "Assets", "Characters", "Forest_OldMan")
SKEL = "C:/Users/Omistaja/Downloads/Action Adventure Pack/tripo_convert_518b7d3a-bfb7-4f2e-bd7b-543871aa89a8.fbx"
NAME = "Forest_OldMan"
SEAT_TARGET = 0.80 / 2.85         # log top (0.8 m) over his standing height in game units
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.fps = 30

# ------------------------------------------------------------------ palette
PAL = {
    "skin": (0, (226, 180, 148)), "nose": (1, (222, 150, 128)), "eye": (2, (34, 28, 26)), "lip": (3, (160, 92, 82)),
    "beard": (4, (240, 238, 232)), "beard_shade": (5, (214, 212, 206)), "robe": (6, (116, 80, 48)),
    "robe_dark": (7, (84, 58, 36)), "hood_inside": (8, (58, 40, 26)), "rope": (9, (200, 176, 124)),
    "pants": (10, (96, 84, 62)), "boot": (11, (60, 42, 30)), "staff": (12, (128, 90, 54)),
    "staff_dark": (13, (90, 62, 38)), "satchel": (14, (146, 104, 62)), "buckle": (15, (176, 152, 92)),
}
N = 4
img = bpy.data.images.new(NAME + "_Palette", N * 4, N * 4, alpha=False)
px = [0.0] * (N * 4 * N * 4 * 4)
for name, (idx, rgb) in PAL.items():
    cx, cy = idx % N, idx // N
    for y in range(cy * 4, cy * 4 + 4):
        for x in range(cx * 4, cx * 4 + 4):
            i = (y * N * 4 + x) * 4
            px[i:i + 4] = [rgb[0] / 255, rgb[1] / 255, rgb[2] / 255, 1.0]
img.pixels = px
img.filepath_raw = os.path.join(OUT, NAME + "_Palette.png"); img.file_format = 'PNG'; img.save()
MAT = bpy.data.materials.new("M_" + NAME); MAT.use_nodes = True
_t = MAT.node_tree.nodes.new("ShaderNodeTexImage"); _t.image = img; _t.interpolation = 'Closest'
MAT.node_tree.links.new(_t.outputs["Color"], MAT.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
MAT.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.85


def cell_uv(color):
    idx = PAL[color][0]
    return ((idx % N + 0.5) / N, (idx // N + 0.5) / N)


# ------------------------------------------------------------------ skeleton (the Mixamo rig of a 1 m Tripo character)
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=SKEL)
rig = [o for o in bpy.data.objects if o.type == 'ARMATURE' and o not in before][0]
for o in [o for o in bpy.data.objects if o not in before and o is not rig]: bpy.data.objects.remove(o)
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
rig.animation_data_clear()
bpy.context.view_layer.objects.active = rig; rig.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bl = rig.data.bones
up = (bl[K("Head")].head_local - bl[K("Hips")].head_local).normalized()
R = up.rotation_difference(Vector((0, 0, 1))).to_matrix()
fwd = R @ (bl[K("LeftToeBase")].head_local - bl[K("LeftFoot")].head_local); fwd.z = 0; fwd.normalize()
R = fwd.rotation_difference(Vector((0, -1, 0))).to_matrix() @ R
rig.matrix_world = R.to_4x4() @ rig.matrix_world
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
off = Vector((-bl[K("Hips")].head_local.x, -bl[K("Hips")].head_local.y, 0))
rig.location = off
bpy.ops.object.transform_apply(location=True)
for pb in rig.pose.bones:
    pb.rotation_mode = 'QUATERNION'; pb.matrix_basis = Matrix()
rig.name = NAME; rig.data.name = NAME + "_Rig"
bpy.context.view_layer.update()


def J(name):
    return rig.data.bones[K(name)].head_local.copy()
def T(name):
    return rig.data.bones[K(name)].tail_local.copy()


# ------------------------------------------------------------------ mesh builder (rigid parts), as in Tavern_Patrons_build.py
class Body:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.groups = {}
        self.vi = 0

    def _face(self, pts, color, ref, bone):
        vs = [self.bm.verts.new(tuple(p)) for p in pts]
        f = self.bm.faces.new(vs)
        f.normal_update()
        if f.normal.dot(f.calc_center_median() - Vector(ref)) < 0: f.normal_flip()
        for l in f.loops: l[self.uv].uv = cell_uv(color)
        self.groups.setdefault(bone, []).extend(range(self.vi, self.vi + len(vs)))
        self.vi += len(vs)

    def box(self, c, s, color, bone, axes=None, taper=1.0):
        c = Vector(c)
        ax, ay, az = axes if axes else (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
        hx, hy, hz = s[0] / 2, s[1] / 2, s[2] / 2
        P = {}
        for i, sx in enumerate((-1, 1)):
            for j, sy in enumerate((-1, 1)):
                for k, sz in enumerate((-1, 1)):
                    t = taper if sz > 0 else 1.0
                    P[(i, j, k)] = c + ax * sx * hx * t + ay * sy * hy * t + az * sz * hz
        quads = [((0, 0, 0), (0, 1, 0), (0, 1, 1), (0, 0, 1)), ((1, 0, 0), (1, 0, 1), (1, 1, 1), (1, 1, 0)),
                 ((0, 0, 0), (0, 0, 1), (1, 0, 1), (1, 0, 0)), ((0, 1, 0), (1, 1, 0), (1, 1, 1), (0, 1, 1)),
                 ((0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)), ((0, 0, 1), (0, 1, 1), (1, 1, 1), (1, 0, 1))]
        for q in quads: self._face([P[k] for k in q], color, c, bone)

    def limb(self, a, b, r1, r2, color, bone, sides=6, flat=1.0):
        a, b = Vector(a), Vector(b)
        d = (b - a).normalized()
        side = d.cross(Vector((0, 1, 0)))
        if side.length < 0.1: side = d.cross(Vector((1, 0, 0)))
        side.normalize(); front = side.cross(d).normalized()
        ring = lambda p, r: [p + (side * math.cos(2 * math.pi * i / sides) + front * flat * math.sin(2 * math.pi * i / sides)) * r for i in range(sides)]
        A, B = ring(a, r1), ring(b, r2)
        mid = (a + b) / 2
        self._face(A, color, mid, bone); self._face(B, color, mid, bone)
        for i in range(sides):
            j = (i + 1) % sides
            self._face([A[i], A[j], B[j], B[i]], color, mid, bone)

    def merge(self, other):
        """Appends another Body's parts (built in rest space) to this one."""
        offset = self.vi
        tmp = bpy.data.meshes.new("tmp"); other.bm.to_mesh(tmp); other.bm.free()
        self.bm.from_mesh(tmp)
        uv = self.bm.loops.layers.uv.active
        bpy.data.meshes.remove(tmp)
        for bone, idxs in other.groups.items():
            self.groups.setdefault(bone, []).extend(i + offset for i in idxs)
        self.vi += other.vi
        self.uv = uv

    def finish(self, rig_obj):
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me); self.bm.free()
        me.materials.append(MAT)
        for p in me.polygons: p.use_smooth = False
        ob = bpy.data.objects.new(self.name, me)
        sc.collection.objects.link(ob)
        for bone, idxs in self.groups.items():
            g = ob.vertex_groups.new(name=K(bone))
            g.add(idxs, 1.0, 'REPLACE')
        ob.parent = rig_obj
        mod = ob.modifiers.new("Armature", 'ARMATURE'); mod.object = rig_obj
        return ob


def build_hermit():
    b = Body(NAME + "_Body")
    w = 0.98
    hips, sp, sp1, sp2, neck, head = J("Hips"), J("Spine"), J("Spine1"), J("Spine2"), J("Neck"), J("Head")
    # robe body
    b.box((0, 0, hips.z - 0.01), (0.18 * w, 0.12 * w, 0.1), "robe", "Hips")
    b.box((0, 0, sp.z + 0.035), (0.17 * w, 0.112 * w, 0.085), "robe", "Spine")
    b.box((0, 0, sp1.z + 0.035), (0.185 * w, 0.118 * w, 0.08), "robe", "Spine1", taper=1.04)
    b.box((0, 0, sp2.z + 0.03), (0.2 * w, 0.118 * w, 0.075), "robe", "Spine2", taper=0.88)
    # rope belt with a knot, and a satchel strap across the chest
    b.box((0, 0, sp.z - 0.012), (0.178 * w, 0.12 * w, 0.016), "rope", "Spine")
    b.box((0.03, -0.062 * w, sp.z - 0.02), (0.022, 0.012, 0.03), "rope", "Spine")
    b.limb((0.035, -0.064 * w, sp.z - 0.035), (0.04, -0.066 * w, sp.z - 0.09), 0.006, 0.005, "rope", "Spine", sides=4)
    b.box((-0.095 * w, 0.0, sp.z - 0.035), (0.03, 0.08, 0.07), "satchel", "Spine")
    b.box((-0.11 * w, -0.0, sp.z - 0.02), (0.008, 0.05, 0.02), "buckle", "Spine")
    # cowl: a thick collar of the hood lying on the shoulders
    b.box((0, 0.005, sp2.z + 0.075), (0.2 * w, 0.13 * w, 0.035), "robe_dark", "Spine2", taper=0.8)
    for s, side in ((1, "Left"), (-1, "Right")):
        b.box((s * 0.075 * w, 0, J(side + "Arm").z + 0.005), (0.075 * w, 0.105 * w, 0.065), "robe", side + "Shoulder")
    b.limb(neck - Vector((0, 0, 0.01)), head + Vector((0, 0, 0.01)), 0.03 * w, 0.028, "skin", "Neck")
    # head
    hc = Vector((0, -0.006, head.z + 0.06))
    b.box(hc, (0.102, 0.118, 0.128), "skin", "Head", taper=0.92)
    b.box((0, hc.y - 0.066, hc.z - 0.004), (0.024, 0.022, 0.036), "nose", "Head")                        # big old nose
    for s in (1, -1):
        b.box((s * 0.024, hc.y - 0.061, hc.z + 0.014), (0.016, 0.006, 0.01), "eye", "Head")
        b.box((s * 0.053, hc.y, hc.z), (0.012, 0.03, 0.038), "skin", "Head")                                # ears
        b.box((s * 0.026, hc.y - 0.063, hc.z + 0.03), (0.032, 0.01, 0.014), "beard", "Head", taper=0.8)    # bushy brows
        b.box((s * 0.05, hc.y + 0.01, hc.z - 0.01), (0.012, 0.07, 0.07), "beard", "Head")                  # white hair at the temples
    # hood: crown, back, sides and a brim framing the face
    b.box((0, hc.y + 0.012, hc.z + 0.078), (0.132, 0.15, 0.034), "robe", "Head")
    b.box((0, hc.y + 0.066, hc.z + 0.005), (0.132, 0.034, 0.17), "robe", "Head")
    for s in (1, -1):
        b.box((s * 0.066, hc.y + 0.008, hc.z + 0.01), (0.014, 0.13, 0.16), "robe", "Head")
        b.box((s * 0.058, hc.y - 0.056, hc.z + 0.02), (0.006, 0.006, 0.12), "hood_inside", "Head")
    b.box((0, hc.y - 0.06, hc.z + 0.072), (0.128, 0.022, 0.03), "robe_dark", "Head")
    b.box((0, hc.y + 0.09, hc.z - 0.07), (0.08, 0.03, 0.06), "robe", "Head", taper=0.6)                    # hood point at the back
    # long white beard down to the belt, and a drooping moustache
    b.box((0, hc.y - 0.045, hc.z - 0.05), (0.104, 0.042, 0.064), "beard", "Head")
    b.box((0, hc.y - 0.064, hc.z - 0.115), (0.09, 0.03, 0.09), "beard", "Head")
    b.box((0, hc.y - 0.07, hc.z - 0.195), (0.072, 0.026, 0.09), "beard_shade", "Head")
    b.box((0, hc.y - 0.073, hc.z - 0.265), (0.05, 0.022, 0.07), "beard", "Head")
    b.box((0, hc.y - 0.074, hc.z - 0.315), (0.026, 0.018, 0.045), "beard_shade", "Head")
    b.box((0, hc.y - 0.066, hc.z - 0.022), (0.064, 0.012, 0.014), "beard", "Head")
    for s in (1, -1):
        b.box((s * 0.034, hc.y - 0.066, hc.z - 0.045), (0.014, 0.012, 0.04), "beard", "Head")
    b.box((0, hc.y - 0.0605, hc.z - 0.034), (0.03, 0.004, 0.006), "lip", "Head")
    # arms: wide sleeves, hands
    for s, side in ((1, "Left"), (-1, "Right")):
        a, e, wr, h = J(side + "Arm"), J(side + "ForeArm"), J(side + "Hand"), T(side + "Hand")
        b.limb(a, e, 0.036 * w, 0.033 * w, "robe", side + "Arm", flat=0.9)
        b.limb(e, wr, 0.033 * w, 0.044 * w, "robe", side + "ForeArm", flat=0.9)
        b.limb(wr - (wr - e).normalized() * 0.012, wr, 0.045 * w, 0.045 * w, "robe_dark", side + "ForeArm", flat=0.9)
        hand_dir = (h - wr).normalized()
        b.box(wr + hand_dir * 0.03, (0.058, 0.05, 0.022), "skin", side + "Hand",
              axes=(hand_dir, Vector((0, 1, 0)), Vector((0, 0, 1))))
        b.box(wr + hand_dir * 0.018 + Vector((0, -0.028, -0.004)), (0.028, 0.014, 0.014), "skin", side + "Hand")
    # legs, boots and the robe skirt (panels move with the legs, so sitting folds them over the thighs)
    for s, side in ((1, "Left"), (-1, "Right")):
        hp, kn, an, toe = J(side + "UpLeg"), J(side + "Leg"), J(side + "Foot"), J(side + "ToeBase")
        b.limb(hp + Vector((s * 0.01, 0, 0.02)), kn, 0.05 * w, 0.038 * w, "pants", side + "UpLeg")
        b.limb(kn, an + Vector((0, 0, 0.02)), 0.038 * w, 0.03, "pants", side + "Leg")
        b.box((an.x, an.y - 0.025, 0.03), (0.062, 0.12, 0.06), "boot", side + "Foot")
        b.box((an.x, toe.y - 0.015, 0.02), (0.06, 0.05, 0.04), "boot", side + "ToeBase")
        b.limb(an, an + Vector((0, 0, 0.08)), 0.037, 0.041, "boot", side + "Leg")
        b.box((s * 0.046 * w, 0, hips.z - 0.06), (0.095 * w, 0.13 * w, 0.06), "robe", side + "UpLeg")
        b.box((s * 0.048 * w, 0.0, (hips.z - 0.09 + kn.z) / 2), (0.104 * w, 0.12 * w, hips.z - 0.09 - kn.z + 0.02), "robe", side + "UpLeg")
        b.box((s * 0.046 * w, 0.0, (kn.z + 0.11) / 2 + 0.01), (0.104 * w, 0.116 * w, kn.z - 0.11), "robe", side + "Leg")
        b.box((s * 0.046 * w, 0.0, 0.115), (0.106 * w, 0.118 * w, 0.014), "robe_dark", side + "Leg")
    return b


# ------------------------------------------------------------------ the seated pose
def seat_hips(drop):
    pb = rig.pose.bones[K("Hips")]
    rest = rig.data.bones[K("Hips")].matrix_local
    want = rest.to_translation() + Vector((0, 0.03, -drop))
    pb.location = rest.to_3x3().normalized().inverted() @ (want - rest.to_translation())
    bpy.context.view_layer.update()


def seat_legs(tilt):
    """Knees a little apart, thighs pointing forward and `tilt` down towards the knee, shins upright."""
    return [
        ("LeftUpLeg", (0.1, -1.0, -tilt), (0, -1, 0), (0, 0, 1)),
        ("LeftLeg", (0.02, 0.06, -1.0), (0, -1, 0), (0, -1, 0)),
        ("LeftFoot", (0.08, -1.0, -0.45), None, None),
        ("RightUpLeg", (-0.1, -1.0, -tilt), (0, -1, 0), (0, 0, 1)),
        ("RightLeg", (-0.02, 0.06, -1.0), (0, -1, 0), (0, -1, 0)),
        ("RightFoot", (-0.08, -1.0, -0.45), None, None),
    ]

# an old man's stoop: back rounded forward, head lifted to look ahead
STOOP = [
    ("Spine", (0, -0.16, 1.0), None, None),
    ("Spine1", (0, -0.22, 1.0), None, None),
    ("Spine2", (0, -0.18, 1.0), None, None),
    ("Neck", (0, -0.25, 1.0), None, None),
    ("Head", (0, -0.02, 1.0), None, None),
]
# left hand resting on the left knee; right hand holding the staff upright beside the right knee
REST_ARMS = [
    ("LeftArm", (0.12, -0.42, -0.9), (0, -1, 0), (0, 0, 1)),
    ("LeftForeArm", (0.02, -1.0, -0.42), (0, 0, -1), (0, 0, -1)),
    ("LeftHand", (0.0, -1.0, -0.7), (0, 0, -1), (0, 0, -1)),
    ("RightArm", (-0.22, -0.38, -0.9), (0, -1, 0), (0, 0, 1)),
    ("RightForeArm", (-0.12, -1.0, 0.22), (0, 0, -1), (1, 0, 0)),
    ("RightHand", (-0.05, -1.0, 0.1), (0, 0, -1), (1, 0, 0)),
]
# "Stay a while and listen": the left hand comes up, palm out, index raised
SPEAK_ARM = [
    ("LeftArm", (0.3, -0.45, -0.75), (0, -1, 0), (0, 0, 1)),
    ("LeftForeArm", (0.12, -0.55, 0.85), (0, 0, -1), (0, -1, 0)),
    ("LeftHand", (0.05, -0.25, 1.0), (0, 0, -1), (0, -1, 0)),
]


def clear_pose():
    for pb in rig.pose.bones: pb.matrix_basis = Matrix()
    bpy.context.view_layer.update()


def pose_quats(pose, drop):
    clear_pose()
    seat_hips(drop)
    poselib.apply_pose(rig, pose)
    q = {pb.name: pb.rotation_quaternion.copy() for pb in rig.pose.bones}
    loc = rig.pose.bones[K("Hips")].location.copy()
    return q, loc


def posed_points(pts_by_bone):
    """Rest-space points carried by bones -> armature space in the current pose."""
    out = []
    for bone, pts in pts_by_bone.items():
        m = rig.pose.bones[K(bone)].matrix @ rig.data.bones[K(bone)].matrix_local.inverted()
        out += [m @ Vector(p) for p in pts]
    return out


def foot_and_seat():
    """Lowest sole point and the underside of the thighs just behind the hip joints (the seat), posed."""
    soles = {}
    for side in ("Left", "Right"):
        an, toe = J(side + "Foot"), J(side + "ToeBase")
        soles[side + "Foot"] = [(an.x + dx, an.y - 0.025 + dy, 0.0) for dx in (-0.03, 0.03) for dy in (-0.06, 0.06)]
        soles[side + "ToeBase"] = [(an.x + dx, toe.y - 0.015 + dy, 0.0) for dx in (-0.03, 0.03) for dy in (-0.025, 0.025)]
    foot = min(p.z for p in posed_points(soles))
    seat_pts = {}
    for side in ("Left", "Right"):
        hp, kn = J(side + "UpLeg"), J(side + "Leg")
        c = hp + (kn - hp) * 0.25
        seat_pts[side + "UpLeg"] = [c + Vector((0, 0.05, 0)), c - Vector((0, 0.05, 0)), c + Vector((0.05, 0, 0)), c - Vector((0.05, 0, 0))]
    seat = min(p.z for p in posed_points(seat_pts))
    return foot, seat


# thigh tilt so that seat height over the soles matches the log; then the hip drop that puts the soles at z = 0
lo, hi, tilt = 0.0, 0.6, 0.2
for _ in range(20):
    tilt = (lo + hi) / 2
    pose_quats(seat_legs(tilt), 0.2)
    foot, seat = foot_and_seat()
    if seat - foot < SEAT_TARGET: lo = tilt
    else: hi = tilt
drop = 0.2
for _ in range(3):
    pose_quats(seat_legs(tilt), drop)
    foot, seat = foot_and_seat()
    drop += foot
pose_quats(seat_legs(tilt), drop)
foot, seat = foot_and_seat()
print("SEAT_HEIGHT %.4f FOOT %.4f TILT %.3f DROP %.3f" % (seat - foot, foot, tilt, drop))

LEGS = seat_legs(tilt)
Q_REST, L_SEAT = pose_quats(LEGS + STOOP + REST_ARMS, drop)
Q_SPEAK, _ = pose_quats(LEGS + STOOP + REST_ARMS + SPEAK_ARM, drop)
clear_pose()


def keyset(act, frames_quats, hips_loc):
    rig.animation_data_create(); rig.animation_data.action = act
    for f, q in frames_quats.items():
        for name, quat in q.items():
            pb = rig.pose.bones[name]
            pb.rotation_quaternion = quat
            pb.keyframe_insert("rotation_quaternion", frame=f, group=name)
        pb = rig.pose.bones[K("Hips")]
        pb.location = hips_loc
        pb.keyframe_insert("location", frame=f, group=K("Hips"))


def bend(q, bone, axis_world, deg):
    rest = rig.data.bones[bone].matrix_local.to_3x3().normalized()
    ax = (rest.inverted() @ Vector(axis_world)).normalized()
    out = dict(q); out[bone] = q[bone] @ Quaternion(ax, math.radians(deg)); return out


def mix(qa, qb, t):
    return {n: qa[n].slerp(qb[n], t) for n in qa}


def smooth(t): return t * t * (3 - 2 * t)


ACTS = {}
# Sit_Idle: slow breath and a small sway, now and then a glance aside (4 s loop)
a = bpy.data.actions.new("Sit_Idle")
fq = {}
for f in range(1, 122, 6):
    u = (f - 1) / 120
    q = bend(Q_REST, K("Spine1"), (1, 0, 0), 2.0 * math.sin(u * 2 * math.pi))
    q = bend(q, K("Head"), (0, 0, 1), 6 * math.sin(u * 2 * math.pi + 1.0))
    q = bend(q, K("Head"), (1, 0, 0), 2 * math.sin(u * 4 * math.pi))
    fq[f] = q
keyset(a, fq, L_SEAT)
ACTS["Sit_Idle"] = a

# Sit_Speak: lifts his hand, nods twice while he talks, lowers it again (3 s)
a = bpy.data.actions.new("Sit_Speak")
fq = {}
for f in list(range(1, 91, 3)) + [90]:
    t = f - 1
    lift = smooth(min(1.0, t / 14)) if t < 70 else smooth(max(0.0, 1 - (t - 70) / 19))
    q = mix(Q_REST, Q_SPEAK, lift)
    nod = math.sin(max(0.0, min(1.0, (t - 10) / 50)) * 2 * math.pi * 2) * 9 * lift
    q = bend(q, K("Head"), (1, 0, 0), nod)
    q = bend(q, K("Spine2"), (1, 0, 0), -3 * lift)
    fq[f] = q
keyset(a, fq, L_SEAT)
ACTS["Sit_Speak"] = a
rig.animation_data.action = None
clear_pose()


# ------------------------------------------------------------------ the staff, modelled in the seated pose and moved back to rest space
def staff_in_hand():
    clear_pose(); seat_hips(drop)
    for name, q in Q_REST.items(): rig.pose.bones[name].rotation_quaternion = q
    bpy.context.view_layer.update()
    pb = rig.pose.bones[K("RightHand")]
    pose_m = pb.matrix
    rest_m = rig.data.bones[K("RightHand")].matrix_local
    to_rest = rest_m @ pose_m.inverted()
    grip = pose_m @ Vector((0, 0.03, 0))
    base = Vector((grip.x - 0.02, grip.y - 0.03, foot))
    top = grip + Vector((0, -0.01, 0.34))
    tmp = Body("tmp")
    tmp.limb(base, top, 0.011, 0.013, "staff", "RightHand", sides=6)
    tmp.limb(top, top + Vector((0.025, -0.01, 0.05)), 0.016, 0.02, "staff_dark", "RightHand", sides=6)     # gnarled knob
    tmp.box(top + Vector((0.035, -0.012, 0.06)), (0.04, 0.04, 0.035), "staff_dark", "RightHand")
    tmp.limb(grip + Vector((0, 0, 0.05)), grip + Vector((0, 0, 0.075)), 0.0145, 0.0145, "rope", "RightHand", sides=6)
    for v in tmp.bm.verts:
        v.co = to_rest @ v.co
    clear_pose()
    return tmp


# ------------------------------------------------------------------ build, export
body = build_hermit()
body.merge(staff_in_hand())
body_ob = body.finish(rig)
rig.animation_data_create()
for take in ("Sit_Idle", "Sit_Speak"):
    tr = rig.animation_data.nla_tracks.new(); tr.name = take
    st = tr.strips.new(take, 1, ACTS[take]); st.name = take
    if hasattr(st, "action_slot") and len(ACTS[take].slots): st.action_slot = ACTS[take].slots[0]   # Blender 4.4+ slotted actions
for a in ACTS.values(): a.use_fake_user = True
print("HERMIT", len(body_ob.data.polygons), "faces")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "BlenderSources", NAME + ".blend"))

bpy.ops.object.select_all(action='DESELECT')
for o in (rig, body_ob): o.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(
    filepath=os.path.join(OUT, NAME + ".fbx"), use_selection=True, object_types={'ARMATURE', 'MESH'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
    add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
    bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.5,
    mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False)

# preview renders (front and side, seated) for review; not part of the game
if os.environ.get("HERMIT_PREVIEW"):
    rig.animation_data.action = ACTS["Sit_Idle"]
    for tr in rig.animation_data.nla_tracks: tr.mute = True
    sc.frame_set(1)
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
    sc.render.resolution_x = 700; sc.render.resolution_y = 700
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 1.05
    for tag, loc in (("front", (0, -3, 0.42)), ("side", (3, -0.15, 0.42)), ("q", (2.0, -2.2, 0.9))):
        cam.location = loc
        d = Vector((0, -0.15, 0.4)) - Vector(loc)
        cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(os.environ["HERMIT_PREVIEW"], "hermit_%s.png" % tag)
        bpy.ops.render.render(write_still=True)
    rig.animation_data.action = ACTS["Sit_Speak"]; sc.frame_set(40)
    cam.location = (0, -3, 0.42); d = Vector((0, -0.15, 0.4)) - Vector(cam.location)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(os.environ["HERMIT_PREVIEW"], "hermit_speak.png")
    bpy.ops.render.render(write_still=True)
print("HERMIT OK")
