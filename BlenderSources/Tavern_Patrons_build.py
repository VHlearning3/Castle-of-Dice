# Castle of Dice: four low-poly tavern patrons on one Mixamo skeleton, with four shared takes
# (Sit_Idle, Sit_Drink, Stand_Idle, Stand_Drink). Built from code; every body part is weighted 100% to one bone
# (classic low-poly rigid skinning), so there is no weight painting to go wrong.
# Run:  blender -b --factory-startup --python BlenderSources/Tavern_Patrons_build.py
# Writes BlenderSources/Tavern_Patrons.blend and Assets/Characters/Tavern_Patrons/Patron_*.fbx + palette.
import bpy, bmesh, math, os, sys, random
from mathutils import Vector, Matrix, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
sys.path.insert(0, os.path.join(HERE, "Elf_Waitress_build"))
import poselib, retarget          # shared with the elf waitress build
from poselib import K

OUT = os.path.join(ROOT, "Assets", "Characters", "Tavern_Patrons")
SKEL = "C:/Users/Omistaja/Downloads/Action Adventure Pack/tripo_convert_518b7d3a-bfb7-4f2e-bd7b-543871aa89a8.fbx"
IDLE = "C:/Users/Omistaja/Downloads/Action Adventure Pack/idle (2).fbx"
os.makedirs(OUT, exist_ok=True)
random.seed(3)

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.fps = 30

# ------------------------------------------------------------------ palette
PAL = {
    "skin_light": (0, (236, 196, 160)), "skin_tan": (1, (204, 150, 108)), "skin_old": (2, (226, 182, 150)),
    "hair_brown": (3, (96, 58, 32)), "hair_grey": (4, (176, 172, 168)), "hair_red": (5, (176, 72, 34)),
    "hair_black": (6, (40, 32, 30)), "eye": (7, (30, 24, 22)), "lip": (8, (170, 90, 80)),
    "linen": (9, (222, 210, 182)), "vest_brown": (10, (110, 70, 40)), "pants_green": (11, (72, 92, 50)),
    "boot": (12, (60, 40, 28)), "belt": (13, (52, 36, 24)), "buckle": (14, (200, 170, 80)),
    "robe_blue": (15, (58, 80, 132)), "robe_trim": (16, (196, 170, 96)), "dress_red": (17, (150, 40, 40)),
    "apron": (18, (232, 228, 214)), "mail": (19, (130, 134, 140)), "tabard": (20, (120, 30, 36)),
    "pants_brown": (21, (98, 70, 48)), "mug": (22, (150, 98, 54)), "mug_band": (23, (60, 56, 54)),
    "foam": (24, (246, 236, 206)), "beer": (25, (214, 140, 34)), "cap_green": (26, (60, 96, 60)),
    "teeth": (27, (240, 236, 224)),
}
N = 8
img = bpy.data.images.new("Tavern_Patrons_Palette", N * 4, N * 4, alpha=False)
px = [0.0] * (N * 4 * N * 4 * 4)
for name, (idx, rgb) in PAL.items():
    cx, cy = idx % N, idx // N
    for y in range(cy * 4, cy * 4 + 4):
        for x in range(cx * 4, cx * 4 + 4):
            i = (y * N * 4 + x) * 4
            px[i:i + 4] = [rgb[0] / 255, rgb[1] / 255, rgb[2] / 255, 1.0]
img.pixels = px
img.filepath_raw = os.path.join(OUT, "Tavern_Patrons_Palette.png"); img.file_format = 'PNG'; img.save()
MAT = bpy.data.materials.new("M_Tavern_Patrons"); MAT.use_nodes = True
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
# centre the body over the origin (the Tripo rig stands 8 cm behind it)
off = Vector((-bl[K("Hips")].head_local.x, -bl[K("Hips")].head_local.y, 0))
rig.location = off
bpy.ops.object.transform_apply(location=True)
for pb in rig.pose.bones:
    pb.rotation_mode = 'QUATERNION'; pb.matrix_basis = Matrix()
rig.name = "Patron_Rig"; rig.data.name = "Patron_Rig"
bpy.context.view_layer.update()

def J(name):
    return rig.data.bones[K(name)].head_local.copy()
def T(name):
    return rig.data.bones[K(name)].tail_local.copy()

# ------------------------------------------------------------------ mesh builder (rigid parts)
class Body:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.groups = {}      # bone -> [vert indices]
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
        """Box centred at c, size s along axes (x, y, z unit vectors); taper shrinks the +z end."""
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
        """Tapered prism from a to b (radius r1 -> r2); flat squashes it front to back."""
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


def build_patron(name, p):
    """p: dict of look parameters."""
    b = Body(name)
    w = p.get("width", 1.0)
    skin = p["skin"]
    hips, sp, sp1, sp2, neck, head = J("Hips"), J("Spine"), J("Spine1"), J("Spine2"), J("Neck"), J("Head")
    top = J("HeadTop_End")
    # torso
    b.box((0, 0, hips.z - 0.01), (0.17 * w, 0.11 * w, 0.09), p["pants"], "Hips")
    b.box((0, -0.002 - p.get("belly", 0) * 0.5, sp.z + 0.035), (0.165 * w, (0.105 + p.get("belly", 0)) * w, 0.08), p["shirt"], "Spine")
    b.box((0, 0, sp1.z + 0.035), (0.18 * w, 0.115 * w, 0.075), p["shirt"], "Spine1", taper=1.04)
    b.box((0, 0, sp2.z + 0.03), (0.2 * w, 0.115 * w, 0.075), p["shirt"], "Spine2", taper=0.88)
    if p.get("vest"):
        b.box((0, -0.006, sp1.z + 0.02), (0.185 * w, 0.118 * w, 0.13), p["vest"], "Spine1")
    if p.get("tabard"):
        b.box((0, -0.064 * w, sp1.z), (0.12 * w, 0.01, 0.2), p["tabard"], "Spine1")
        b.box((0, 0.064 * w, sp1.z), (0.12 * w, 0.01, 0.2), p["tabard"], "Spine1")
    b.box((0, 0, sp.z - 0.012), (0.175 * w, 0.115 * w + p.get("belly", 0), 0.022), "belt", "Spine")
    b.box((0, -(0.058 * w + p.get("belly", 0) * 0.6), sp.z - 0.012), (0.03, 0.006, 0.026), "buckle", "Spine")
    # shoulders, neck, head
    for s, side in ((1, "Left"), (-1, "Right")):
        b.box((s * 0.075 * w, 0, J(side + "Arm").z + 0.005), (0.07 * w, 0.1 * w, 0.06), p["shirt"], side + "Shoulder")
    b.limb(neck - Vector((0, 0, 0.01)), head + Vector((0, 0, 0.01)), 0.03 * w, 0.028, skin, "Neck")
    hc = Vector((0, -0.006, head.z + 0.06))
    b.box(hc, (0.105, 0.12, 0.13), skin, "Head", taper=0.92)
    b.box((0, hc.y - 0.064, hc.z - 0.005), (0.02, 0.016, 0.03), skin, "Head")                                  # nose
    for s in (1, -1):
        b.box((s * 0.024, hc.y - 0.061, hc.z + 0.012), (0.016, 0.006, 0.012), "eye", "Head")                  # eyes
        b.box((s * 0.054, hc.y, hc.z), (0.012, 0.03, 0.035), skin, "Head")                                       # ears
        b.box((s * 0.024, hc.y - 0.062, hc.z + 0.03), (0.026, 0.006, 0.007), p["hair"], "Head")                 # brows
    b.box((0, hc.y - 0.061, hc.z - 0.033), (0.034, 0.006, 0.008), "lip", "Head")
    hair = p.get("hair_style", "short")
    if hair in ("short", "long", "braid"):
        b.box((0, hc.y + 0.006, hc.z + 0.05), (0.116, 0.132, 0.045), p["hair"], "Head")
        b.box((0, hc.y + 0.05, hc.z + 0.0), (0.112, 0.04, 0.11), p["hair"], "Head")
        for s in (1, -1): b.box((s * 0.054, hc.y + 0.02, hc.z + 0.03), (0.012, 0.07, 0.05), p["hair"], "Head")
        if hair == "long":
            b.box((0, hc.y + 0.055, hc.z - 0.07), (0.11, 0.035, 0.12), p["hair"], "Head")
        if hair == "braid":
            b.box((0, hc.y + 0.065, hc.z - 0.02), (0.035, 0.03, 0.08), p["hair"], "Head")
            b.limb((0, hc.y + 0.075, hc.z - 0.06), (0.0, hc.y + 0.08, hc.z - 0.2), 0.016, 0.01, p["hair"], "Neck")
            b.box((0, hc.y + 0.08, hc.z - 0.205), (0.03, 0.03, 0.02), "cap_green", "Neck")
    elif hair == "bald":
        b.box((0, hc.y + 0.045, hc.z - 0.005), (0.112, 0.04, 0.07), p["hair"], "Head")
        for s in (1, -1): b.box((s * 0.054, hc.y + 0.015, hc.z + 0.005), (0.013, 0.06, 0.05), p["hair"], "Head")
    if p.get("hat"):
        b.box((0, hc.y + 0.004, hc.z + 0.082), (0.13, 0.15, 0.03), p["hat"], "Head")
        b.box((0, hc.y + 0.004, hc.z + 0.11), (0.1, 0.11, 0.05), p["hat"], "Head", taper=0.8)
    beard = p.get("beard")
    if beard == "full":
        b.box((0, hc.y - 0.045, hc.z - 0.05), (0.1, 0.04, 0.06), p["beard_color"], "Head")
        b.box((0, hc.y - 0.06, hc.z - 0.095), (0.07, 0.035, 0.06), p["beard_color"], "Head", taper=0.7)
        b.box((0, hc.y - 0.064, hc.z - 0.022), (0.05, 0.01, 0.012), p["beard_color"], "Head")              # moustache
    elif beard == "long":
        b.box((0, hc.y - 0.045, hc.z - 0.05), (0.1, 0.04, 0.06), p["beard_color"], "Head")
        b.box((0, hc.y - 0.062, hc.z - 0.12), (0.075, 0.03, 0.12), p["beard_color"], "Head", taper=0.55)
        b.box((0, hc.y - 0.064, hc.z - 0.022), (0.056, 0.01, 0.012), p["beard_color"], "Head")
    elif beard == "stubble":
        b.box((0, hc.y - 0.03, hc.z - 0.045), (0.106, 0.07, 0.05), p["beard_color"], "Head")
        b.box((0, hc.y - 0.061, hc.z - 0.033), (0.034, 0.006, 0.008), "lip", "Head")
    # arms and hands
    for s, side in ((1, "Left"), (-1, "Right")):
        a, e, wr, h = J(side + "Arm"), J(side + "ForeArm"), J(side + "Hand"), T(side + "Hand")
        b.limb(a, e, 0.034 * w, 0.03 * w, p.get("sleeve", p["shirt"]), side + "Arm", flat=0.9)
        b.limb(e, wr, 0.03 * w, 0.024 * w, p.get("forearm", p.get("sleeve", p["shirt"])), side + "ForeArm", flat=0.9)
        hand_dir = (h - wr).normalized()
        b.box(wr + hand_dir * 0.03, (0.06, 0.05, 0.022), skin, side + "Hand",
              axes=(hand_dir, Vector((0, 1, 0)), Vector((0, 0, 1))))
        b.box(wr + hand_dir * 0.018 + Vector((0, -0.028, -0.004)), (0.028, 0.014, 0.014), skin, side + "Hand")   # thumb
    # legs and boots
    for s, side in ((1, "Left"), (-1, "Right")):
        hp, kn, an, toe = J(side + "UpLeg"), J(side + "Leg"), J(side + "Foot"), J(side + "ToeBase")
        b.limb(hp + Vector((s * 0.01, 0, 0.02)), kn, 0.05 * w, 0.038 * w, p["pants"], side + "UpLeg")
        b.limb(kn, an + Vector((0, 0, 0.02)), 0.038 * w, 0.03, p.get("legwear", p["pants"]), side + "Leg")
        b.box((an.x, an.y - 0.025, 0.03), (0.06, 0.12, 0.06), p["boots"], side + "Foot")
        b.box((an.x, toe.y - 0.015, 0.02), (0.058, 0.05, 0.04), p["boots"], side + "ToeBase")
        if p.get("boot_top"):
            b.limb(an + Vector((0, 0, 0.0)), an + Vector((0, 0, 0.09)), 0.036, 0.04, p["boots"], side + "Leg")
        # hem panels move with the legs (sitting folds them over the thighs)
        hem = p.get("hem")
        if hem:
            length, color = hem
            b.box((s * 0.045 * w, 0, hips.z - 0.06), (0.09 * w, 0.125 * w, 0.06), color, side + "UpLeg")
            if length > 0.12:
                b.box((s * 0.047 * w, 0.0, (hips.z - 0.09 + kn.z) / 2), (0.1 * w, 0.115 * w, hips.z - 0.09 - kn.z + 0.02), color, side + "UpLeg")
            if length > 0.3:
                b.box((s * 0.045 * w, 0.0, (kn.z + 0.06) / 2 + 0.01), (0.1 * w, 0.11 * w, kn.z - 0.06), color, side + "Leg", taper=1.0)
    return b


def tankard(b, hand="Right"):
    """A wooden tankard held upright in the hand once the arm is in its hold pose (built in pose space later)."""
    return  # built per pose below


PATRONS = {
    "Patron_Farmer": dict(width=1.18, belly=0.03, skin="skin_tan", hair="hair_brown", hair_style="bald", beard="full", beard_color="hair_brown",
                          shirt="linen", vest="vest_brown", pants="pants_green", boots="boot", boot_top=True),
    "Patron_Elder": dict(width=0.96, skin="skin_old", hair="hair_grey", hair_style="long", beard="long", beard_color="hair_grey",
                         shirt="robe_blue", sleeve="robe_blue", pants="robe_blue", legwear="pants_brown", boots="boot", hem=(0.35, "robe_blue")),
    "Patron_Lass": dict(width=0.92, skin="skin_light", hair="hair_red", hair_style="braid", shirt="linen", vest="dress_red",
                        pants="dress_red", legwear="dress_red", boots="boot", hem=(0.5, "dress_red")),
    "Patron_Sellsword": dict(width=1.08, skin="skin_light", hair="hair_black", hair_style="short", beard="stubble", beard_color="hair_black",
                             shirt="mail", tabard="tabard", sleeve="mail", forearm="vest_brown", pants="pants_brown", boots="boot", boot_top=True),
}

# ------------------------------------------------------------------ poses and takes on the shared rig
ARM = [K(s + n) for s in ("Left", "Right") for n in ("Shoulder", "Arm", "ForeArm", "Hand")]

def pose_quats(pose, extra=None):
    for pb in rig.pose.bones: pb.matrix_basis = Matrix()
    bpy.context.view_layer.update()
    if extra: extra()
    poselib.apply_pose(rig, pose)
    q = {pb.name: pb.rotation_quaternion.copy() for pb in rig.pose.bones}
    loc = rig.pose.bones[K("Hips")].location.copy()
    for pb in rig.pose.bones: pb.matrix_basis = Matrix()
    bpy.context.view_layer.update()
    return q, loc

# right hand around a mug at the waist (forearm forward), left arm relaxed down
HOLD = [
    ("LeftArm", (0.12, 0.02, -1.0), (0, -1, 0), (0, -1, 0)),
    ("LeftForeArm", (0.05, -0.15, -1.0), (0, 0, -1), (-1, 0, 0)),
    ("LeftHand", (0.03, -0.1, -1.0), (0, 0, -1), (-1, 0, 0)),
    ("RightArm", (-0.12, 0.05, -1.0), (0, -1, 0), (0, -1, 0)),
    ("RightForeArm", (0.05, -1.0, 0.3), (0, 0, -1), (1, 0, 0)),
    ("RightHand", (0.05, -1.0, 0.15), (0, 0, -1), (1, 0, 0)),
]
# mug at the mouth: elbow forward and out, hand turned in so the mug tips towards the lips
SIP = [
    ("LeftArm", (0.12, 0.02, -1.0), (0, -1, 0), (0, -1, 0)),
    ("LeftForeArm", (0.05, -0.15, -1.0), (0, 0, -1), (-1, 0, 0)),
    ("LeftHand", (0.03, -0.1, -1.0), (0, 0, -1), (-1, 0, 0)),
    ("RightArm", (-0.3, -0.8, -0.15), (0, -1, 0), (0, 0, 1)),
    ("RightForeArm", (0.7, -0.15, 1.0), (0, 0, -1), (1, 0, 0.6)),
    ("RightHand", (0.85, -0.5, 0.45), (0, 0, -1), (1, 0, -0.2)),
]

def seat_hips():
    pb = rig.pose.bones[K("Hips")]
    rest = rig.data.bones[K("Hips")].matrix_local
    want = rest.to_translation() + Vector((0, 0.02, -0.26))
    pb.location = rest.to_3x3().normalized().inverted() @ (want - rest.to_translation())
    bpy.context.view_layer.update()

SEAT_LEGS = [
    ("LeftUpLeg", (0.05, -1.0, -0.05), (0, -1, 0), (0, 0, 1)),
    ("LeftLeg", (0.0, 0.1, -1.0), (0, -1, 0), (0, -1, 0)),
    ("LeftFoot", (0.0, -1.0, -0.45), None, None),
    ("RightUpLeg", (-0.05, -1.0, -0.05), (0, -1, 0), (0, 0, 1)),
    ("RightLeg", (0.0, 0.1, -1.0), (0, -1, 0), (0, -1, 0)),
    ("RightFoot", (0.0, -1.0, -0.45), None, None),
    ("Spine", (0, -0.12, 1.0), None, None),
]
# seated: forearms resting forward on the table, mug in the right hand
SEAT_ARMS = [
    ("LeftArm", (0.2, -0.45, -0.85), (0, -1, 0), (0, 0, 1)),
    ("LeftForeArm", (-0.35, -1.0, 0.1), (0, 0, -1), (0, 0, -1)),
    ("LeftHand", (-0.4, -1.0, 0.0), (0, 0, -1), (0, 0, -1)),
    ("RightArm", (-0.2, -0.45, -0.85), (0, -1, 0), (0, 0, 1)),
    ("RightForeArm", (0.25, -1.0, 0.15), (0, 0, -1), (1, 0, 0)),
    ("RightHand", (0.2, -1.0, 0.1), (0, 0, -1), (1, 0, 0)),
]
SEAT_SIP = SEAT_ARMS[:3] + SIP[3:]

Q_HOLD, _ = pose_quats(HOLD)
Q_SIP, _ = pose_quats(SIP)
Q_SEAT, L_SEAT = pose_quats(SEAT_LEGS + SEAT_ARMS, seat_hips)
Q_SEAT_SIP, _ = pose_quats(SEAT_LEGS + SEAT_SIP, seat_hips)

def keyset(act, frames_quats, hips_locs=None):
    rig.animation_data_create(); rig.animation_data.action = act
    for f, q in frames_quats.items():
        for name, quat in q.items():
            pb = rig.pose.bones[name]
            pb.rotation_quaternion = quat
            pb.keyframe_insert("rotation_quaternion", frame=f, group=name)
    if hips_locs:
        for f, loc in hips_locs.items():
            pb = rig.pose.bones[K("Hips")]
            pb.location = loc
            pb.keyframe_insert("location", frame=f, group=K("Hips"))

def bend(q, bone, axis_world, deg):
    rest = rig.data.bones[bone].matrix_local.to_3x3().normalized()
    ax = (rest.inverted() @ Vector(axis_world)).normalized()
    out = dict(q); out[bone] = q[bone] @ Quaternion(ax, math.radians(deg)); return out

def mix(qa, qb, t):
    return {n: qa[n].slerp(qb[n], t) for n in qa}

def smooth(t): return t * t * (3 - 2 * t)

def drink_curve(f, f_up=30, f_hold=58, f_down=82):
    if f < f_up - 18: return 0.0
    if f < f_up: return smooth((f - (f_up - 18)) / 18)
    if f <= f_hold: return 1.0
    if f < f_down: return smooth(1 - (f - f_hold) / (f_down - f_hold))
    return 0.0

ACTS = {}
# Stand_Idle: the Mixamo idle with the mug held
src, act, objs = retarget.import_take(IDLE)
a = retarget.retarget(rig, src, act, "Stand_Idle")
for o in objs:
    if o.name in bpy.data.objects: bpy.data.objects.remove(o)
bpy.data.actions.remove(act)
ACTS["Stand_Idle"] = a
def overlay_arms(act, qmap_by_frame):
    rig.animation_data.action = act
    for name in ARM:
        path = 'pose.bones["%s"].rotation_quaternion' % name
        for fc in [fc for fc in _fcurves(act) if fc.data_path == path]:
            fc.keyframe_points.clear()
        for f, q in qmap_by_frame.items():
            rig.pose.bones[name].rotation_quaternion = q[name]
            rig.pose.bones[name].keyframe_insert("rotation_quaternion", frame=f, group=name)
def _fcurves(act):
    if hasattr(act, "layers") and len(act.layers):
        out = []
        for layer in act.layers:
            for strip in layer.strips:
                for cb in strip.channelbags: out.extend(cb.fcurves)
        return out
    return list(act.fcurves)
overlay_arms(a, {1: Q_HOLD, int(a.frame_range[1]): Q_HOLD})

# Stand_Drink: idle body, the mug comes up, tips, goes down (4 s loop), head tilts back while drinking
src, act, objs = retarget.import_take(IDLE)
a = retarget.retarget(rig, src, act, "Stand_Drink", frame_range=(1, 120))
for o in objs:
    if o.name in bpy.data.objects: bpy.data.objects.remove(o)
bpy.data.actions.remove(act)
keys = list(range(1, 121, 3)) + [120]
overlay_arms(a, {f: mix(Q_HOLD, Q_SIP, drink_curve(f)) for f in keys})
rig.animation_data.action = a
for f in keys:
    sc.frame_set(f)
    pbh = rig.pose.bones[K("Head")]
    base = pbh.rotation_quaternion.copy()
    rest = rig.data.bones[K("Head")].matrix_local.to_3x3().normalized()
    ax = (rest.inverted() @ Vector((1, 0, 0))).normalized()
    pbh.rotation_quaternion = base @ Quaternion(ax, math.radians(-22 * drink_curve(f)))
    pbh.keyframe_insert("rotation_quaternion", frame=f, group=K("Head"))
ACTS["Stand_Drink"] = a

# Sit_Idle: seated, a slow breath and a small sway (4 s loop)
a = bpy.data.actions.new("Sit_Idle")
fq, fl = {}, {}
for f in range(1, 122, 10):
    t = math.sin((f - 1) / 120 * 2 * math.pi)
    q = bend(Q_SEAT, K("Spine1"), (1, 0, 0), 2.5 * t)
    q = bend(q, K("Head"), (0, 0, 1), 4 * math.sin((f - 1) / 120 * 2 * math.pi + 1.0))
    fq[f] = q; fl[f] = L_SEAT
keyset(a, fq, fl)
ACTS["Sit_Idle"] = a

# Sit_Drink: seated, lifts the mug from the table, drinks, sets it back (4 s loop)
a = bpy.data.actions.new("Sit_Drink")
fq, fl = {}, {}
for f in list(range(1, 121, 3)) + [120]:
    d = drink_curve(f)
    q = mix(Q_SEAT, Q_SEAT_SIP, d)
    q = bend(q, K("Head"), (1, 0, 0), -20 * d)
    q = bend(q, K("Spine1"), (1, 0, 0), -4 * d)
    fq[f] = q; fl[f] = L_SEAT
keyset(a, fq, fl)
ACTS["Sit_Drink"] = a
rig.animation_data.action = None
for pb in rig.pose.bones: pb.matrix_basis = Matrix()
bpy.context.view_layer.update()

# ------------------------------------------------------------------ the tankard, modelled in the hold pose and moved back to rest space
def tankard_on_hand(b, rig_obj):
    for pb in rig_obj.pose.bones: pb.matrix_basis = Matrix()
    bpy.context.view_layer.update()
    for name, q in Q_HOLD.items(): rig_obj.pose.bones[name].rotation_quaternion = q
    bpy.context.view_layer.update()
    pb = rig_obj.pose.bones[K("RightHand")]
    pose_m = pb.matrix                                   # armature space, posed
    rest_m = rig_obj.data.bones[K("RightHand")].matrix_local
    to_rest = rest_m @ pose_m.inverted()
    palm = pose_m @ Vector((0, 0.035, 0))                 # a little along the hand
    inward = (pose_m.to_3x3() @ Vector((1, 0, 0))).normalized()
    c = palm + Vector((0.0, 0.0, -0.005)) + inward * 0.0
    h = 0.075; r = 0.027
    base = c + Vector((0, 0, -h * 0.45))
    tmp = Body("tmp")
    tmp.limb(base, base + Vector((0, 0, h)), r, r, "mug", "RightHand", sides=8)
    tmp.limb(base + Vector((0, 0, 0.01)), base + Vector((0, 0, 0.018)), r * 1.06, r * 1.06, "mug_band", "RightHand", sides=8)
    tmp.limb(base + Vector((0, 0, h - 0.016)), base + Vector((0, 0, h - 0.008)), r * 1.06, r * 1.06, "mug_band", "RightHand", sides=8)
    tmp.limb(base + Vector((0, 0, h)), base + Vector((0, 0, h + 0.014)), r * 0.98, r * 0.7, "foam", "RightHand", sides=8)
    # move into rest space and append to the patron's body
    for v in tmp.bm.verts:
        v.co = to_rest @ v.co
    for pb2 in rig_obj.pose.bones: pb2.matrix_basis = Matrix()
    bpy.context.view_layer.update()
    return tmp

# ------------------------------------------------------------------ build, export
for f in os.listdir(OUT):
    if f.endswith(".fbx") and f.startswith("Patron_"): os.remove(os.path.join(OUT, f))
results = []
for name, look in PATRONS.items():
    arm_obj = rig.copy(); arm_obj.data = rig.data.copy(); arm_obj.name = name; arm_obj.data.name = name + "_Rig"
    sc.collection.objects.link(arm_obj)
    arm_obj.animation_data_clear()
    for pb in arm_obj.pose.bones: pb.matrix_basis = Matrix()
    body = build_patron(name + "_Body", look)
    body_ob = body.finish(arm_obj)
    mug = tankard_on_hand(None, arm_obj)
    mug_ob = mug.finish(arm_obj); mug_ob.name = name + "_Tankard"
    arm_obj.animation_data_create()
    for take in ("Sit_Idle", "Sit_Drink", "Stand_Idle", "Stand_Drink"):
        tr = arm_obj.animation_data.nla_tracks.new(); tr.name = take
        st = tr.strips.new(take, 1, ACTS[take]); st.name = take
        if hasattr(st, "action_slot") and len(ACTS[take].slots): st.action_slot = ACTS[take].slots[0]   # Blender 4.4+ slotted actions
    results.append((arm_obj, body_ob, mug_ob))
    print("PATRON", name, len(body_ob.data.polygons) + len(mug_ob.data.polygons), "faces")

rig.hide_set(True); rig.hide_render = True
for a in ACTS.values(): a.use_fake_user = True
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "BlenderSources", "Tavern_Patrons.blend"))

for arm_obj, body_ob, mug_ob in results:
    bpy.ops.object.select_all(action='DESELECT')
    for o in (arm_obj, body_ob, mug_ob): o.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, arm_obj.name + ".fbx"), use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.5,
        mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False)
print("PATRONS OK")
