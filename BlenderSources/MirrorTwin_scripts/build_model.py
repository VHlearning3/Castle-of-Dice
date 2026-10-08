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


# --- body envelope: how far the body (not the hair) reaches from the hip axis, per angle and height
CY = -0.03
import numpy as np
_src = bpy.data.images.load(os.path.join(W, "BlondeElf_BaseColor.png"))
_N = _src.size[0]
_px = np.array(_src.pixels[:], np.float32).reshape(_N, _N, 4)
_me = body.data
_uv = np.zeros((len(_me.vertices), 2))
for _l in _me.loops:
    _uv[_l.vertex_index] = _me.uv_layers.active.data[_l.index].uv
_c = _px[(_uv[:, 1] * _N).astype(int) % _N, (_uv[:, 0] * _N).astype(int) % _N, :3]
_r, _g, _b = _c[:, 0], _c[:, 1], _c[:, 2]
_sat = (_c.max(1) - _c.min(1)) / (_c.max(1) + 1e-5)
_hair = (_r > 0.5) & (_g > 0.4) & (_b < 0.42) & ((_r - _b) > 0.28) & (_sat > 0.35)
_skin = (_r > _g) & (_g > _b) & (_r > 0.62) & ((_r - _b) > 0.12) & ~_hair
_co = np.array([v.co[:] for v in _me.vertices])
_ang = np.degrees(np.arctan2(_co[:, 0], -(_co[:, 1] - CY))) % 360.0
_rad = np.hypot(_co[:, 0], _co[:, 1] - CY)
ENV_Z = np.arange(1.00, 0.54, -0.02)
ENV_A = np.arange(0, 360, 5)
_R = np.zeros((len(ENV_Z), len(ENV_A)))
for zi, z in enumerate(ENV_Z):
    sl = (~_hair) & (np.abs(_co[:, 2] - z) < 0.02) & (_rad < 0.4)
    for ai, a in enumerate(ENV_A):
        d = np.abs(((_ang - a) + 180) % 360 - 180)
        m = sl & (d < 9)
        _R[zi, ai] = _rad[m].max() if m.any() else 0.0
# draping: the cloth falls from the widest point above, so the envelope never shrinks going down
for zi in range(1, len(ENV_Z)):
    _R[zi] = np.maximum(_R[zi], _R[zi - 1])
for ai in range(len(ENV_A)):   # fill holes from neighbours
    if _R[0, ai] == 0:
        _R[:, ai] = np.maximum(_R[:, (ai - 1) % len(ENV_A)], _R[:, (ai + 1) % len(ENV_A)])


def envelope(a_deg, z):
    z = min(max(z, ENV_Z[-1]), ENV_Z[0])
    zi = (ENV_Z[0] - z) / 0.02
    z0 = int(min(zi, len(ENV_Z) - 2)); tz = zi - z0
    ai = (a_deg % 360.0) / 5.0
    a0 = int(ai) % len(ENV_A); a1 = (a0 + 1) % len(ENV_A); ta = ai - int(ai)
    top = _R[z0, a0] * (1 - ta) + _R[z0, a1] * ta
    bot = _R[z0 + 1, a0] * (1 - ta) + _R[z0 + 1, a1] * ta
    return top * (1 - tz) + bot * tz


# --- fitted gloves: the bell sleeves are drawn in to the forearm, and the cuffs end at the wrist
for side in (1, -1):
    a0 = Vector((side * 0.41, -0.058, 1.35)); a1 = Vector((side * 0.635, -0.06, 1.355))
    ax = (a1 - a0).normalized()
    for i, v in enumerate(_me.vertices):
        c = v.co
        sx = c.x * side
        if sx < 0.42 or sx > 0.80 or _skin[i]:
            continue
        t = (c - a0).dot(ax)
        foot = a0 + ax * t
        off = c - foot
        d = off.length
        if sx > 0.625:          # the cuff hanging over the hand folds back to the wrist
            nx = 0.625 + (sx - 0.625) * 0.18
            foot = a0 + ax * ((nx - 0.41) / max(abs(ax.x), 1e-4))
        fit = 0.046 - 0.010 * min(1.0, max(0.0, (sx - 0.42) / 0.2))
        if d > fit:
            d = fit + (d - fit) * 0.12
        if d > 1e-5:
            v.co = foot + off.normalized() * d


# --- skirt: one piece open in front, hugging the hips and falling in soft folds, longer at the back
SK_A0, SK_A1 = 38.0, 322.0           # front-left edge round the back to front-right edge
SK_TOP, SK_COLS, SK_ROWS = 0.975, 64, 24
M_CLOTH = mat("Twin_Cloth", (0.10, 0.12, 0.24), 0.75)


def fold(u, v):
    """Radial fold offset: soft gathers at the belt opening into deep folds at the hem."""
    a = u * math.radians(SK_A1 - SK_A0)
    amp = 0.002 + 0.026 * v ** 1.25
    return amp * (0.62 * math.sin(a * 6.5 + 0.7) + 0.38 * math.sin(a * 11.0 + 2.1 + 1.6 * v))


def skirt_point(u, v):
    a = SK_A0 + (SK_A1 - SK_A0) * u
    back = max(0.0, 1.0 - abs(a - 180.0) / 140.0)            # 1 at the back, 0 at the front edges
    hem = 0.135 - 0.075 * back
    z = SK_TOP + (hem - SK_TOP) * v
    z += 0.014 * v * v * math.sin(u * math.radians(SK_A1 - SK_A0) * 6.5 + 0.7 + 1.57)
    drop = (SK_TOP - z) / (SK_TOP - 0.1)
    r = envelope(a, max(z, 0.62)) + 0.016 + 0.095 * drop ** 1.45 + 0.05 * back * drop + fold(u, v)
    ar = math.radians(a)
    return Vector((r * math.sin(ar), CY - r * math.cos(ar), z)), a


SKIRT_CHAINS = [0.05, 0.27, 0.5, 0.73, 0.95]   # where the skirt's bones hang (u)


def build_skirt():
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    grid = []
    for j in range(SK_ROWS + 1):
        v = j / SK_ROWS
        grid.append([bm.verts.new(skirt_point(i / SK_COLS, v)[0]) for i in range(SK_COLS + 1)])
    for j in range(SK_ROWS):
        for i in range(SK_COLS):
            f = bm.faces.new([grid[j][i], grid[j + 1][i], grid[j + 1][i + 1], grid[j][i + 1]])
            for l, (ii, jj) in zip(f.loops, ((i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j))):
                l[uvl].uv = (ii / SK_COLS, 1.0 - jj / SK_ROWS)
    bm.normal_update()
    for f in bm.faces:   # outward
        c = f.calc_center_median()
        if f.normal.dot(Vector((c.x, c.y - CY, 0))) < 0:
            f.normal_flip()
    me = bpy.data.meshes.new("Twin_Skirt_Mesh")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(M_CLOTH)
    ob = bpy.data.objects.new("Twin_Skirt", me)
    bpy.context.scene.collection.objects.link(ob)
    sol = ob.modifiers.new("Solid", 'SOLIDIFY')
    sol.thickness = 0.008
    sol.offset = -1.0
    sol.use_rim = True
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.ops.object.modifier_apply(modifier="Solid")
    for p in me.polygons:
        p.use_smooth = True
    return ob


def add_skirt_bones():
    """Two bones per chain (hip to knee, knee to hem), children of the hips; Unity sways them (SkirtSway)."""
    bpy.context.view_layer.objects.active = arm
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    hips = arm.data.edit_bones[P + "Hips"]
    for k, u in enumerate(SKIRT_CHAINS):
        p0, a = skirt_point(u, 0.04)
        p1, _ = skirt_point(u, 0.5)
        p2, _ = skirt_point(u, 1.0)
        out = Vector((math.sin(math.radians(a)), -math.cos(math.radians(a)), 0))
        up = arm.data.edit_bones.new("Skirt_%d_Up" % k)
        up.head, up.tail, up.parent = p0 - out * 0.01, p1 - out * 0.01, hips
        low = arm.data.edit_bones.new("Skirt_%d_Low" % k)
        low.head, low.tail, low.parent = p1 - out * 0.01, p2 - out * 0.01, up
        low.use_connect = True
        for b in (up, low):
            b.align_roll(out)
    bpy.ops.object.mode_set(mode='OBJECT')


def skin_skirt(ob):
    ob.parent = arm
    mod = ob.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm
    groups = {}

    def g(name):
        if name not in groups:
            groups[name] = ob.vertex_groups.new(name=name)
        return groups[name]
    for vtx in ob.data.vertices:
        c = vtx.co
        a = math.degrees(math.atan2(c.x, -(c.y - CY))) % 360.0
        u = min(1.0, max(0.0, (a - SK_A0) / (SK_A1 - SK_A0)))
        v = min(1.0, max(0.0, (SK_TOP - c.z) / (SK_TOP - 0.08)))
        # between which two chains
        k = 0
        while k < len(SKIRT_CHAINS) - 2 and u > SKIRT_CHAINS[k + 1]:
            k += 1
        tu = min(1.0, max(0.0, (u - SKIRT_CHAINS[k]) / (SKIRT_CHAINS[k + 1] - SKIRT_CHAINS[k])))
        wh = 1.0 - min(1.0, max(0.0, (v - 0.04) / 0.16))          # the top hangs from the hips
        tl = min(1.0, max(0.0, (v - 0.42) / 0.2))                   # upper vs lower bone
        w = {P + "Hips": wh}
        for kk, wc in ((k, 1 - tu), (k + 1, tu)):
            w["Skirt_%d_Up" % kk] = w.get("Skirt_%d_Up" % kk, 0) + (1 - wh) * wc * (1 - tl)
            w["Skirt_%d_Low" % kk] = w.get("Skirt_%d_Low" % kk, 0) + (1 - wh) * wc * tl
        top = sorted(w.items(), key=lambda kv: -kv[1])[:4]
        s = sum(x for _, x in top) or 1.0
        for name, x in top:
            if x / s > 0.001:
                g(name).add([vtx.index], x / s, 'REPLACE')


def paint_skirt_texture(path, size=(1024, 512)):
    """Navy cloth, darker in the folds' valleys, a gold-bordered hem with a diamond band, gold front edges."""
    Wt, Ht = size
    u = (np.arange(Wt) + 0.5) / Wt
    v = 1.0 - (np.arange(Ht) + 0.5) / Ht            # v = 0 at the belt, 1 at the hem (image rows go up)
    U, V = np.meshgrid(u, v)
    span = math.radians(SK_A1 - SK_A0)
    A = U * span
    f = (0.62 * np.sin(A * 6.5 + 0.7) + 0.38 * np.sin(A * 11.0 + 2.1 + 1.6 * V))
    shade = 0.78 + 0.22 * (f * 0.5 + 0.5) * np.clip(V * 1.4, 0.25, 1.0) + 0.06 * (1 - np.clip(V * 1.4, 0, 1))
    weave = 1.0 + 0.025 * np.sin(U * Wt * 1.3) * np.sin(V * Ht * 1.7)
    navy = np.array((0.105, 0.125, 0.25))
    col = navy[None, None, :] * (shade * weave)[..., None]
    gold = np.array((0.82, 0.62, 0.26))
    gold_dk = np.array((0.52, 0.37, 0.12))
    hem_m = 0.06                                    # 6 % of the length ~ 5 cm
    edge_u = 0.022                                  # front edges ~ 4 cm
    in_hem = V > 1 - hem_m
    hem_line = (V > 1 - hem_m - 0.03) & (V < 1 - hem_m - 0.018)
    band = (V > 1 - hem_m - 0.012) & (V <= 1 - hem_m)
    # diamonds in the band above the hem
    du = (U * 70.0) % 1.0
    dv = (V - (1 - hem_m - 0.03)) / 0.03
    diamond = (np.abs(du - 0.5) * 2 + np.abs(dv - 0.5) * 2 < 0.9) & (V > 1 - hem_m - 0.03) & (V < 1 - hem_m)
    edge = (U < edge_u) | (U > 1 - edge_u)
    edge_line = ((U > edge_u + 0.004) & (U < edge_u + 0.008)) | ((U < 1 - edge_u - 0.004) & (U > 1 - edge_u - 0.008))
    def put(mask, c, k=1.0):
        col[mask] = (c * k)[None, :] * (0.85 + 0.15 * shade[mask])[:, None]
    put(diamond, gold, 0.85)
    put(hem_line | (edge_line & (V > 0.02)), gold)
    put(in_hem | edge | band, gold)
    put(((V > 1 - hem_m * 0.45) & (V < 1 - hem_m * 0.3)) | (((U > edge_u * 0.4) & (U < edge_u * 0.55)) | ((U < 1 - edge_u * 0.4) & (U > 1 - edge_u * 0.55))), gold_dk)
    img = bpy.data.images.new("Twin_Skirt", Wt, Ht, alpha=False)
    rgba = np.ones((Ht, Wt, 4), np.float32)
    rgba[..., :3] = np.clip(col, 0, 1)              # row 0 is the image bottom, v = 0 the belt end of the UVs
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    return img


def build_belt():
    """A dark hip belt hugging the hips over the skirt's top edge, gold-edged, with a gold plate on each hip."""
    mb = MeshBuilder()
    n = 48
    def rr(a_rad):
        return envelope(math.degrees(a_rad), 0.975) + 0.011
    for i in range(n):
        a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
        for z0, z1, m, out in ((0.952, 0.958, M_GOLD, 0.006), (0.958, 0.986, M_BELT, 0.004), (0.986, 0.992, M_GOLD, 0.006)):
            ri0, ri1 = rr(a0) - 0.006, rr(a1) - 0.006
            ro0, ro1 = rr(a0) + out, rr(a1) + out
            p = lambda a, r, z: (r * math.sin(a), CY - r * math.cos(a), z)
            mb.solid([p(a0, ri0, z0), p(a0, ro0, z0), p(a1, ro1, z0), p(a1, ri1, z0)],
                     [p(a0, ri0, z1), p(a0, ro0, z1), p(a1, ro1, z1), p(a1, ri1, z1)], m, ref=(0, CY, (z0 + z1) / 2))
    for k in (1, -1):    # hip plates, pointed like the reference's
        a = k * math.radians(52)
        r = rr(a) + 0.008
        c = Vector((r * math.sin(a), CY - r * math.cos(a), 0.972))
        tang = Vector((math.cos(a), math.sin(a), 0))
        nrm = Vector((math.sin(a), -math.cos(a), 0))
        pts = [c + tang * 0.03 + Vector((0, 0, 0.022)), c - tang * 0.03 + Vector((0, 0, 0.022)), c - tang * 0.022 - Vector((0, 0, 0.02)), c - Vector((0, 0, 0.036)), c + tang * 0.022 - Vector((0, 0, 0.02))]
        mb.solid([tuple(q) for q in pts], [tuple(q + nrm * 0.008) for q in pts], M_GOLD, ref=tuple(c - nrm * 0.01))
    ob = mb.finish("Twin_Belt")
    skin_to(ob, lambda c: {"Hips": 1.0})
    return ob


M_GOLD = mat("Twin_Gold", (0.80, 0.60, 0.24), 0.35, 1.0)
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
add_skirt_bones()
skin_skirt(skirt)
skirt_img = paint_skirt_texture(os.path.join(W, "Twin_Skirt.png"))
_nt = M_CLOTH.node_tree
_tex = _nt.nodes.new("ShaderNodeTexImage")
_tex.image = skirt_img
_nt.links.new(_tex.outputs[0], _nt.nodes.get("Principled BSDF").inputs["Base Color"])
belt = build_belt()
trident = build_trident()
bpy.context.scene.render.fps = 30
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(W, "MirrorTwin_model.blend"))
print("BUILT", len(skirt.data.polygons), len(trident.data.polygons))
