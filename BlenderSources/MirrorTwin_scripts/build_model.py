# The Mirror Twin (Castle of Dice, Vili 2026-10-08): the tavern's blonde elf turned evil, after Vili's reference.
# Starts from the blonde elf (BlondeElf.blend: rigged body, mixamorig skeleton) and adds:
#   - the recoloured colour map (recolor.py): navy bodysuit with gold piping, sheer tights, black gloves and boots
#   - a right hand closed into a fist around the trident's shaft
#   - long open skirt panels from the hips to the ankles, pleated, gold hem
#   - the trident: black barbed head, violet gem and glowing inlays, skinned to the right hand
# Run: blender -b BlondeElf.blend --python build_model.py -- <work dir>
#
# Whole pipeline, in a work folder holding copies of BlenderSources/BlondeElf.blend and the elf's colour maps
# (BlondeElf_BaseColor.png, Elf_BaseColor_Grin_2048.png), run from that folder:
#   1. blender -b BlondeElf.blend --python texmap.py                  texel -> surface position maps (*.npy)
#   2. blender -b --python recolor.py -- BlondeElf_BaseColor.png Twin_BaseColor.png        (and the grin map)
#   3. blender -b BlondeElf.blend --python build_model.py -- <work>   -> MirrorTwin_model.blend
#   4. blender -b MirrorTwin_model.blend --python animate.py -- <work> -> MirrorTwin_anim.blend
#   5. blender -b MirrorTwin_anim.blend --python export.py -- <work> Assets/Characters/MirrorTwin
#   then the Unity menu CastleOfDice/Mirror Twin (Tower secret boss).
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from lowpoly_mesh import MeshBuilder, mat

W = sys.argv[sys.argv.index("--") + 1]
P = "mixamorig:"
arm = bpy.data.objects["Armature"]
body = bpy.data.objects["BlondeElf_Body"]
body.name = "Twin_Body"
body.data.name = "Twin_Body_Mesh"
for o in list(bpy.data.objects):
    if o.type == 'CAMERA' or o.name == "BlondeElf_Ref":
        bpy.data.objects.remove(o)
if arm.animation_data:
    arm.animation_data.action = None
for pb in arm.pose.bones:
    pb.rotation_quaternion = (1, 0, 0, 0)
    pb.location = (0, 0, 0)

# --- colour map
m_body = body.data.materials[0]
m_body.name = "M_MirrorTwin"
img = bpy.data.images["Elf_BaseColor"]
img.name = "MirrorTwin_BaseColor"
img.filepath = os.path.join(W, "Twin_BaseColor.png")
img.reload()

# --- right fist: the fingers bend down and back around the shaft (this rig has no finger bones)
KNUCKLE_X, FINGER_Z, BEND_R = -0.708, 1.345, 0.026
GRIP = Vector((KNUCKLE_X + 0.004, -0.06, FINGER_Z - BEND_R + 0.005))   # the shaft's centre line runs along Y through here
for v in body.data.vertices:
    c = v.co
    if c.x >= KNUCKLE_X or c.z < 1.30 or c.y < -0.125 or c.y > 0.02:
        continue
    s = KNUCKLE_X - c.x            # distance along the finger
    h = c.z - FINGER_Z             # height above the fingers' underside
    phi = min(s / BEND_R, math.radians(250))
    rr = BEND_R + h
    v.co = Vector((KNUCKLE_X - rr * math.sin(phi), c.y, (FINGER_Z - BEND_R) + rr * math.cos(phi)))


def skin_to(ob, weights_fn):
    ob.parent = arm
    mod = ob.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm
    groups = {}
    for v in ob.data.vertices:
        for bone, w in weights_fn(v.co).items():
            if w <= 0.001:
                continue
            g = groups.get(bone) or ob.vertex_groups.new(name=P + bone)
            groups[bone] = g
            g.add([v.index], w, 'REPLACE')


# --- skirt: two long panels, open in front, pleats deepening toward the hem
M_CLOTH = mat("Twin_Cloth", (0.10, 0.12, 0.24), 0.75)
M_GOLD = mat("Twin_Gold", (0.80, 0.60, 0.24), 0.35, 1.0)
CY = -0.03
Z_TOP, Z_HEM = 0.985, 0.11


def skirt_radius(v, a):
    base = 0.207 + 0.05 * min(1.0, v / 0.18) + 0.085 * max(0.0, v - 0.18) / 0.82
    pleat = 0.011 * math.sin(a * 9.0) * min(1.0, v * 1.6)
    return base + pleat


def build_skirt():
    bm = bmesh.new()
    cols, rows = 20, 16
    for side in (1, -1):
        g = []
        for j in range(rows + 1):
            v = j / rows
            z = Z_TOP + (Z_HEM - Z_TOP) * v
            row = []
            for i in range(cols + 1):
                a = math.radians(48 + (163 - 48) * i / cols)
                if i == 0:
                    a -= math.radians(6) * v      # the front edge swings forward as it falls
                r = skirt_radius(v, a)
                row.append(bm.verts.new((side * r * math.sin(a), CY - r * math.cos(a), z)))
            g.append(row)
        for j in range(rows):
            for i in range(cols):
                f = bm.faces.new([g[j][i], g[j][i + 1], g[j + 1][i + 1], g[j + 1][i]])
                f.material_index = 1 if (j == rows - 1 or i == 0) else 0
    bm.normal_update()
    for f in bm.faces:   # outward
        c = f.calc_center_median()
        if f.normal.dot(Vector((c.x, c.y - CY, 0))) < 0:
            f.normal_flip()
    me = bpy.data.meshes.new("Twin_Skirt_Mesh")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(M_CLOTH)
    me.materials.append(M_GOLD)
    ob = bpy.data.objects.new("Twin_Skirt", me)
    bpy.context.scene.collection.objects.link(ob)
    sol = ob.modifiers.new("Solid", 'SOLIDIFY')
    sol.thickness = 0.007
    sol.offset = -1.0
    sol.use_rim = True
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.ops.object.modifier_apply(modifier="Solid")
    for p in me.polygons:
        p.use_smooth = True
    me.uv_layers.new(name="UVMap")

    def weights(c):
        v = (Z_TOP - c.z) / (Z_TOP - Z_HEM)
        side = "Left" if c.x > 0 else "Right"
        front = max(0.0, min(1.0, (-(c.y - CY)) / 0.22 + 0.5))
        t = max(0.0, min(1.0, (v - 0.12) / 0.55))
        t = t * t * (3 - 2 * t)
        wl = t * (0.35 + 0.35 * front)
        return {"Hips": 1 - wl, side + "UpLeg": wl}
    skin_to(ob, weights)
    return ob


def build_belt():
    """A dark hip belt over the skirt's top edge, gold-edged, with a gold plate on each hip."""
    mb = MeshBuilder()
    n = 28
    def rr(a, z):
        return 0.214 + 0.004 * (0.99 - z) / 0.05
    for i in range(n):
        a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
        for z0, z1, m, out in ((0.952, 0.958, M_GOLD, 0.006), (0.958, 0.986, M_BELT, 0.004), (0.986, 0.992, M_GOLD, 0.006)):
            ri0, ri1 = rr(a0, z0) - 0.006, rr(a1, z0) - 0.006
            ro0, ro1 = rr(a0, z0) + out, rr(a1, z0) + out
            p = lambda a, r, z: (r * math.sin(a), CY - r * math.cos(a), z)
            mb.solid([p(a0, ri0, z0), p(a0, ro0, z0), p(a1, ro1, z0), p(a1, ri1, z0)],
                     [p(a0, ri0, z1), p(a0, ro0, z1), p(a1, ro1, z1), p(a1, ri1, z1)], m, ref=(0, CY, (z0 + z1) / 2))
    for k in (1, -1):    # hip plates, pointed like the reference's
        a = k * math.radians(52)
        c = Vector((0.222 * math.sin(a), CY - 0.222 * math.cos(a), 0.972))
        tang = Vector((math.cos(a), math.sin(a), 0))
        nrm = Vector((math.sin(a), -math.cos(a), 0))
        pts = [c + tang * 0.03 + Vector((0, 0, 0.022)), c - tang * 0.03 + Vector((0, 0, 0.022)), c - tang * 0.022 - Vector((0, 0, 0.02)), c - Vector((0, 0, 0.036)), c + tang * 0.022 - Vector((0, 0, 0.02))]
        mb.solid([tuple(q) for q in pts], [tuple(q + nrm * 0.008) for q in pts], M_GOLD, ref=tuple(c - nrm * 0.01))
    ob = mb.finish("Twin_Belt")
    skin_to(ob, lambda c: {"Hips": 1.0})
    return ob


M_BELT = mat("Twin_Belt", (0.05, 0.05, 0.065), 0.5)


# --- trident: built along T (butt 0 .. tip), prongs spread along S, flat in K; skinned to the right hand
M_SHAFT = mat("Trident_Shaft", (0.05, 0.04, 0.07), 0.35, 0.6)
M_METAL = mat("Trident_Metal", (0.08, 0.075, 0.10), 0.25, 0.9)
M_GRIP = mat("Trident_Grip", (0.10, 0.06, 0.12), 0.8)
M_GLOW = mat("Trident_Glow", (0.62, 0.25, 1.0), 0.3, 0.0, 1.0, (0.62, 0.25, 1.0))
LENGTH, GRIP_T = 1.78, 0.62
TD, SD, KD = Vector((0, -1, 0)), Vector((1, 0, 0)), Vector((0, 0, 1))


def W3(t, s=0.0, k=0.0):
    return tuple(GRIP + TD * (t - GRIP_T) + SD * s + KD * k)


def ring(t, r, n=8, rot=0.0):
    return [W3(t, r * math.cos(rot + 2 * math.pi * i / n), r * math.sin(rot + 2 * math.pi * i / n)) for i in range(n)]


def blade(mb, pts, widths, thick, m):
    """Flat diamond-section blade along a path in the (t, s) plane."""
    rings = []
    for idx, (t, s) in enumerate(pts):
        a = pts[max(idx - 1, 0)]
        b = pts[min(idx + 1, len(pts) - 1)]
        dt, ds = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dt, ds) or 1.0
        nt, ns = -ds / L, dt / L
        w = widths[idx]
        th = thick[idx] if isinstance(thick, (list, tuple)) else thick
        rings.append([W3(t + nt * w, s + ns * w), W3(t, s, th), W3(t - nt * w, s - ns * w), W3(t, s, -th)])
    mb.loft(rings, m)


def build_trident():
    mb = MeshBuilder()
    # butt spike and pommel with four spurs
    mb.loft([ring(0.0, 0.0005), ring(0.07, 0.016), ring(0.085, 0.024), ring(0.11, 0.024), ring(0.125, 0.017)], M_METAL)
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        blade(mb, [(0.10, 0.02 * math.cos(a)), (0.05, 0.055 * math.cos(a))], [0.008, 0.001], 0.004, M_METAL)
    # shaft with ferrules and a wrapped grip
    mb.loft([ring(0.12, 0.0145), ring(1.30, 0.0135)], M_SHAFT)
    for t0 in (0.30, 0.98, 1.16):
        mb.loft([ring(t0, 0.016), ring(t0 + 0.012, 0.019), ring(t0 + 0.03, 0.019), ring(t0 + 0.042, 0.016)], M_METAL)
    for i in range(9):
        t0 = 0.47 + i * 0.034
        mb.loft([ring(t0, 0.0155, 8, 0.2 * i), ring(t0 + 0.024, 0.0172, 8, 0.2 * i)], M_GRIP)
    # socket: tapered collar with two spikes
    mb.loft([ring(1.29, 0.016), ring(1.33, 0.026), ring(1.38, 0.030), ring(1.42, 0.022)], M_METAL)
    for k in (1, -1):
        blade(mb, [(1.36, k * 0.028), (1.33, k * 0.075)], [0.012, 0.001], 0.006, M_METAL)
    # crossbar: a crescent from prong to prong
    cres = [(1.43 - 0.035 * (1 - (-1 + 2 * i / 8) ** 2), 0.19 * (-1 + 2 * i / 8)) for i in range(9)]
    blade(mb, cres, [0.024 - 0.008 * abs(-1 + 2 * i / 8) for i in range(9)], 0.012, M_METAL)
    # centre prong with barbs
    blade(mb, [(1.40, 0.0), (1.52, 0.0), (1.64, 0.0), (LENGTH, 0.0)], [0.022, 0.024, 0.018, 0.0008], [0.011, 0.011, 0.009, 0.002], M_METAL)
    for k in (1, -1):
        blade(mb, [(1.66, k * 0.012), (1.625, k * 0.045)], [0.011, 0.0008], 0.006, M_METAL)
    # side prongs: out, up, curling in at the tips, each with an inner barb and an outer spur
    for k in (1, -1):
        path = [(1.40, k * 0.17), (1.47, k * 0.205), (1.55, k * 0.20), (1.62, k * 0.175), (1.69, k * 0.13)]
        blade(mb, path, [0.02, 0.02, 0.017, 0.012, 0.0008], [0.010, 0.010, 0.009, 0.007, 0.002], M_METAL)
        blade(mb, [(1.58, k * 0.19), (1.55, k * 0.15)], [0.010, 0.0008], 0.005, M_METAL)
        blade(mb, [(1.45, k * 0.21), (1.40, k * 0.25)], [0.010, 0.0008], 0.005, M_METAL)
    # violet gem at the heart of the head
    gc = Vector(W3(1.44))
    gr = 0.03
    ov = [gc + TD * gr * 1.3, gc - TD * gr * 1.3, gc + SD * gr, gc - SD * gr, gc + KD * gr * 0.8, gc - KD * gr * 0.8]
    for a, b in ((2, 4), (4, 3), (3, 5), (5, 2)):
        for apex in (0, 1):
            mb.face([tuple(ov[apex]), tuple(ov[a]), tuple(ov[b])], M_GLOW, ref=tuple(gc))
    # glowing inlays on both faces of the centre prong and the side prongs
    for kk in (1, -1):
        mb.face([W3(1.47, 0.006, kk * 0.0118), W3(1.47, -0.006, kk * 0.0118), W3(1.71, 0.0, kk * 0.0042)], M_GLOW, ref=W3(1.55, 0.0, -kk))
        for k in (1, -1):
            mb.face([W3(1.45, k * 0.205, kk * 0.0108), W3(1.45, k * 0.19, kk * 0.0108), W3(1.62, k * 0.172, kk * 0.0078)], M_GLOW, ref=W3(1.5, k * 0.2, -kk))
    ob = mb.finish("Twin_Trident")
    skin_to(ob, lambda c: {"RightHand": 1.0})
    # marks Unity reads: the head (light, lightning) and the tip
    for name, t in (("Trident_Head", 1.50), ("Trident_Tip", LENGTH)):
        e = bpy.data.objects.new(name, None)
        bpy.context.scene.collection.objects.link(e)
        e.empty_display_size = 0.05
        e.parent = arm
        e.parent_type = 'BONE'
        e.parent_bone = P + "RightHand"
        e.matrix_world = Matrix.Translation(Vector(W3(t)))
    return ob


skirt = build_skirt()
belt = build_belt()
trident = build_trident()
bpy.context.scene.render.fps = 30
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(W, "MirrorTwin_model.blend"))
print("BUILT", len(skirt.data.polygons), len(trident.data.polygons))
