# Builds the castle doorways (double and single wooden door in a pale stone arch) for Castle of Dice.
# Run inside Blender: exec(open(r"<this file>").read()). Blender is Z-up; the door's front faces -Y.
# Each door is an empty root with the stone frame, a dark passage behind the opening and its
# leaves; every leaf's origin sits on its hinge (the outer edge), so Unity can swing it about Y.
import bpy, bmesh, math
from mathutils import Vector

def mat(name, rgb, rough=0.8, metal=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    m.diffuse_color = (*rgb, 1)
    return m

M_STONE = mat("Door_Stone", (0.70, 0.66, 0.57))
M_STONE_DARK = mat("Door_StoneTrim", (0.58, 0.55, 0.48))
M_WOOD = mat("Door_Wood", (0.36, 0.19, 0.08), 0.7)
M_WOOD_DARK = mat("Door_WoodDark", (0.20, 0.11, 0.05), 0.9)
M_IRON = mat("Door_Iron", (0.10, 0.10, 0.11), 0.45, 0.6)
M_VOID = mat("Door_Passage", (0.035, 0.03, 0.03), 1.0)


class MeshBuilder:
    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []

    def mat_index(self, m):
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def prism(self, poly_xz, y0, y1, m):
        """Extrudes a CCW polygon in the XZ plane from y0 (front) to y1 (back)."""
        bm = self.bm
        mi = self.mat_index(m)
        front = [bm.verts.new((x, y0, z)) for x, z in poly_xz]
        back = [bm.verts.new((x, y1, z)) for x, z in poly_xz]
        n = len(poly_xz)
        f = bm.faces.new(front); f.material_index = mi
        f = bm.faces.new(list(reversed(back))); f.material_index = mi
        for i in range(n):
            j = (i + 1) % n
            f = bm.faces.new((front[i], back[i], back[j], front[j])); f.material_index = mi
        return front + back

    def box(self, x0, x1, y0, y1, z0, z1, m):
        return self.prism([(x0, z0), (x1, z0), (x1, z1), (x0, z1)], y0, y1, m)

    def cyl(self, center, radius, depth, sides, m, rot=0.0):
        """Prism with its axis along Y (facing the viewer), e.g. a pentagonal knob."""
        cx, cy, cz = center
        poly = [(cx + radius * math.cos(rot + 2 * math.pi * i / sides),
                 cz + radius * math.sin(rot + 2 * math.pi * i / sides)) for i in range(sides)]
        return self.prism(poly, cy - depth, cy, m)

    def finish(self, name, parent=None, location=(0, 0, 0), bevel=0.0, smooth_angle=35, bevel_segments=1):
        me = bpy.data.meshes.new(name)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        ob.location = location
        if parent is not None:
            ob.parent = parent
        if bevel > 0:
            bv = ob.modifiers.new("Bevel", "BEVEL")
            bv.width = bevel
            bv.segments = bevel_segments
            bv.limit_method = "ANGLE"
            bv.angle_limit = math.radians(40)
            bv.harden_normals = False
        for p in me.polygons:
            p.use_smooth = True
        bpy.context.view_layer.objects.active = ob
        for o in bpy.context.selected_objects:
            o.select_set(False)
        ob.select_set(True)
        bpy.ops.object.shade_auto_smooth(angle=math.radians(smooth_angle))
        return ob


def arch_rise(x, half_w, c):
    """Height above the spring line of a two-centred (slightly pointed) arch at x."""
    r = half_w + c
    ax = min(abs(x), half_w)
    return math.sqrt(max(r * r - (ax + c) ** 2, 0.0))


def arch_point(side, t, half_w, c, spring, extra=0.0):
    """Point on the arch curve, t 0 = spring, 1 = apex. side +1 right half, -1 left half.
    extra pushes the point outward along the curve's radius (the frame's outer edge)."""
    r = half_w + c
    theta_top = math.acos(c / r)
    th = t * theta_top
    rr = r + extra
    x = -c + rr * math.cos(th)
    z = spring + rr * math.sin(th)
    return (side * x, z)


def build_door(name, width, spring, c, leaves, planks_per_leaf, origin_x):
    hw = width * 0.5
    rise = arch_rise(0, hw, c)
    T = 0.42           # stone frame thickness
    D0, D1 = -0.42, 0.42  # frame depth (front at -Y)
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    root.empty_display_size = 1.0
    root.location = (origin_x, 0, 0)

    # ---------- stone frame ----------
    fb = MeshBuilder()
    R = 0.12  # how far the rounded arch moulding stands proud of the wall face
    # jambs: a squat base block, a smooth shaft and an impost block where the arch springs
    base_h = 0.55
    for side in (-1, 1):
        def sx(a, b):
            return (a, b) if side > 0 else (-b, -a)
        xa, xb = sx(hw - 0.04, hw + T + 0.1)
        fb.box(xa, xb, D0 - R - 0.06, D1, 0.0, base_h, M_STONE_DARK)
        # impost block at the spring line (the "block segment" at mid height)
        xa, xb = sx(hw - 0.05, hw + T + 0.12)
        fb.box(xa, xb, D0 - R - 0.08, D1, spring - 0.06, spring + 0.3, M_STONE_DARK)

    # one continuous rounded moulding: up the left jamb, over the arch and down the right jamb
    centre_r = (-c, spring)          # right half of the arch is centred left of the middle
    stations = []
    for side in (-1, 1):
        pts = []
        for k in range(3):           # straight jamb shaft
            z = base_h + (spring - base_h) * k / 3
            pts.append((side * hw, z, side * 1.0, 0.0))
        for k in range(13):          # arch curve to the apex
            t = k / 12
            x, z = arch_point(1, t, hw, c, spring)
            nx, nz = x - centre_r[0], z - centre_r[1]
            ln = math.hypot(nx, nz)
            pts.append((side * x, z, side * nx / ln, nz / ln))
        stations.append(pts)
    path = stations[0] + list(reversed(stations[1][:-1]))
    yf = D0 - R
    profile = []                     # (radial offset, y) around the moulding's cross-section
    for k in range(9):               # rounded front: a half-round from the inner to the outer edge
        a = math.pi * k / 8
        profile.append((T * 0.5 - T * 0.5 * math.cos(a), yf + 0.22 * (1.0 - math.sin(a))))
    profile += [(T, D1), (0.0, D1)]
    bm = fb.bm
    mi = fb.mat_index(M_STONE)
    rings = []
    for px, pz, nx, nz in path:
        rings.append([bm.verts.new((px + nx * r, y, pz + nz * r)) for r, y in profile])
    m = len(profile)
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        for j in range(m):
            jn = (j + 1) % m
            f = bm.faces.new((a[j], a[jn], b[jn], b[j]))
            f.material_index = mi
    for ring in (rings[0], rings[-1]):
        f = bm.faces.new(ring)
        f.material_index = mi
    key_t = 0.86
    # keystone: taller, proud of the face
    kl = arch_point(-1, key_t, hw, c, spring)
    kr = arch_point(1, key_t, hw, c, spring)
    apex = spring + rise
    ktop = apex + T + 0.22
    kw_top = (kr[0] - kl[0]) * 0.5 + 0.12
    fb.prism([(kl[0], kl[1]), (kr[0], kr[1]), (kw_top, ktop), (-kw_top, ktop)], D0 - R - 0.1, D1, M_STONE_DARK)
    # spandrel fill above the arch up to the lintel course so the frame reads as one wall piece
    top_z = apex + T + 0.22
    outer_x = hw + T + 0.12
    for side in (-1, 1):
        pts = [arch_point(side, k / 6 * key_t, hw, c, spring, T) for k in range(7)]
        poly = [(side * outer_x, max(spring + 0.16, pts[0][1]))] + pts + [(side * kw_top, top_z), (side * outer_x, top_z)]
        if side > 0:
            poly = list(reversed(poly))
        fb.prism(poly, D0 + 0.05, D1, M_STONE)
    # pillars with base and capital
    P = 0.78
    ph = top_z + 0.25
    for side in (-1, 1):
        x0, x1 = outer_x, outer_x + P
        if side < 0:
            x0, x1 = -x1, -x0
        fb.box(x0, x1, D0 - 0.1, D1 + 0.05, 0.0, ph, M_STONE)
        fb.box(x0 - 0.1, x1 + 0.1, D0 - 0.2, D1 + 0.12, 0.0, 0.38, M_STONE_DARK)
        fb.box(x0 - 0.08, x1 + 0.08, D0 - 0.18, D1 + 0.1, ph, ph + 0.26, M_STONE_DARK)
    # cornice over the doorway, between the pillars
    fb.box(-outer_x - 0.02, outer_x + 0.02, D0 - 0.04, D1, top_z, top_z + 0.2, M_STONE_DARK)
    # step and threshold
    fb.box(-hw - T - 0.35, hw + T + 0.35, D0 - 0.75, D0 + 0.02, 0.0, 0.14, M_STONE_DARK)
    fb.box(-hw, hw, D0, D1, 0.0, 0.06, M_STONE_DARK)
    frame = fb.finish(name + "_Frame", root, bevel=0.04, bevel_segments=2)

    # ---------- dark passage behind the opening ----------
    pb = MeshBuilder()
    depth_end = D1 + width * (0.55 if leaves == 2 else 1.05)
    pts = [(hw + 0.02, 0.0)] + [arch_point(1, k / 8, hw + 0.02, c, spring) for k in range(9)]
    pts += [(-x, z) for x, z in reversed(pts[:-1])]
    # back wall
    poly = list(reversed(pts))
    pb.prism(poly, depth_end, depth_end + 0.05, M_VOID)
    # tunnel walls following the arch
    bm = pb.bm
    mi = pb.mat_index(M_VOID)
    front = [bm.verts.new((x, D1 - 0.02, z)) for x, z in pts]
    back = [bm.verts.new((x, depth_end, z)) for x, z in pts]
    for i in range(len(pts) - 1):
        f = bm.faces.new((front[i], front[i + 1], back[i + 1], back[i]))
        f.material_index = mi
    floor = [bm.verts.new(v) for v in ((hw + 0.02, D1 - 0.02, 0.005), (-hw - 0.02, D1 - 0.02, 0.005), (-hw - 0.02, depth_end, 0.005), (hw + 0.02, depth_end, 0.005))]
    f = bm.faces.new(floor); f.material_index = mi
    passage = pb.finish(name + "_Passage", root)

    # ---------- leaves ----------
    leaf_y0 = -0.05          # front face of the planks
    plank_d = 0.11
    gap = 0.014
    seam = 0.008
    for li in range(leaves):
        if leaves == 2:
            side = -1 if li == 0 else 1
            inner, outer = side * seam, side * (hw - 0.012)
            leaf_name = name + ("_Leaf_L" if side < 0 else "_Leaf_R")
        else:
            side = -1
            inner, outer = hw - 0.012, -(hw - 0.012)
            leaf_name = name + "_Leaf"
        hinge_x = outer
        lb = MeshBuilder()
        lo, hi = min(inner, outer), max(inner, outer)
        n = planks_per_leaf
        pw = (hi - lo) / n
        # dark backing showing through the plank gaps
        back_pts = []
        steps = 10
        for k in range(steps + 1):
            x = lo + (hi - lo) * k / steps
            back_pts.append((x, spring + arch_rise(x, hw - 0.012, c) - 0.004))
        back_poly = [(lo, 0.06), (hi, 0.06)] + list(reversed(back_pts))
        lb.prism([(x - hinge_x, z) for x, z in back_poly], leaf_y0 + 0.03, leaf_y0 + plank_d + 0.02, M_WOOD_DARK)
        for p in range(n):
            x0 = lo + p * pw + gap * 0.5
            x1 = lo + (p + 1) * pw - gap * 0.5
            xm = (x0 + x1) * 0.5
            h = lambda x: spring + arch_rise(x, hw - 0.012, c) - 0.01
            poly = [(x0, 0.06), (x1, 0.06), (x1, h(x1)), (xm, h(xm)), (x0, h(x0))]
            lb.prism([(x - hinge_x, z) for x, z in poly], leaf_y0, leaf_y0 + plank_d, M_WOOD)
        # thick rounded iron bands across both leaves
        for bz in (spring * 0.12, spring * 0.66):
            lb.box(lo - hinge_x + 0.01, hi - hinge_x - 0.01, leaf_y0 - 0.06, leaf_y0 + 0.005, bz - 0.085, bz + 0.085, M_IRON)
            for k in range(n):
                rx = lo + (k + 0.5) * pw - hinge_x
                lb.cyl((rx, leaf_y0 - 0.06, bz), 0.026, 0.02, 6, M_IRON)
        # big pentagonal knob near the meeting edge: a rim plate with a raised boss
        knob_x = (inner + side * 0.24) if leaves == 2 else (inner - 0.24)
        kz = spring * 0.42
        lb.cyl((knob_x - hinge_x, leaf_y0 - 0.005, kz), 0.16, 0.035, 5, M_IRON, rot=math.pi / 2)
        lb.cyl((knob_x - hinge_x, leaf_y0 - 0.04, kz), 0.095, 0.07, 5, M_IRON, rot=math.pi / 2)
        if (leaves == 2 and side < 0) or leaves == 1:
            # wooden drop bar low on the leaf, held by two iron staples
            bz = spring * 0.27
            bx0 = lo + 0.12 if leaves == 2 else lo + 0.1
            bx1 = inner + 0.18 if leaves == 2 else hi - 0.1
            lb.box(bx0 - hinge_x, bx1 - hinge_x, leaf_y0 - 0.1, leaf_y0, bz - 0.06, bz + 0.06, M_WOOD)
            for sxp in (bx0 + 0.12, (bx0 + bx1) * 0.5):
                lb.box(sxp - 0.03 - hinge_x, sxp + 0.03 - hinge_x, leaf_y0 - 0.115, leaf_y0, bz - 0.085, bz + 0.085, M_IRON)
        if (leaves == 2 and side > 0) or leaves == 1:
            # iron latch strap running from the knob toward the hinge
            sz = kz + 0.02
            sx0 = knob_x + 0.1 if leaves == 2 else knob_x - 0.5
            sx1 = knob_x + 0.5 if leaves == 2 else knob_x - 0.1
            lb.box(sx0 - hinge_x, sx1 - hinge_x, leaf_y0 - 0.05, leaf_y0, sz - 0.05, sz + 0.05, M_IRON)
        leaf = lb.finish(leaf_name, root, location=(hinge_x, 0, 0), bevel=0.018, bevel_segments=2)
    return root


def build_all():
    double = build_door("Door_Double", width=3.0, spring=2.55, c=0.35, leaves=2, planks_per_leaf=4, origin_x=0.0)
    single = build_door("Door_Single", width=1.6, spring=2.35, c=0.2, leaves=1, planks_per_leaf=3, origin_x=7.0)
    return double, single

build_all()
