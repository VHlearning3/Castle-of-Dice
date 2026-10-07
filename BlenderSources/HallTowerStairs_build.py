# Builds the foot of the Tower's spiral staircase for the Castle Hall (Castle of Dice).
# Run: blender -b --factory-startup --python HallTowerStairs_build.py  (or exec() it inside Blender).
# Blender is Z-up; the hall lies toward -Y. Units are metres at game scale (no scaling in Unity).
#
#   Facade      the hall's wall piece with a round-arched opening, two buttresses and a cornice
#   Turret      a round stone stair tower behind the wall, rising above it to a slate cone roof
#   Stairs      a stone newel and wedge steps winding up counter-clockwise from the opening
#   Hall_Side   empty on the hall side (-Y): Unity turns the model so it points along -Z
#   PillarTop_L/R, Sign_Anchor   empties for the exit braziers and the "To Tower" sign
import bpy, bmesh, math, os
from mathutils import Vector

OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Models", "Doors")
BLEND = os.path.join(os.path.dirname(os.path.abspath(__file__)), "HallTowerStairs.blend")

# The hall's wall: inner face at y = -0.7, outer face at y = 1.4 (matches the gate's Door_Wall_Fill)
WALL_Y0, WALL_Y1, WALL_H = -0.7, 1.4, 8.0
OPEN_HW, OPEN_SPRING = 1.7, 3.2          # arch opening half width and springing height
TURRET_Y, TURRET_R, TURRET_T, TURRET_H = 4.2, 4.0, 0.7, 12.5
NEWEL_R = 0.45
STEP_RISE, STEP_DEG, STEP_COUNT = 0.25, 20.0, 46


def mat(name, rgb, rough=0.85, metal=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    m.diffuse_color = (*rgb, 1)
    return m

# Names match the castle door materials, so Unity maps them to the same M_Door_* assets;
# Hall_Wall maps to the hall's wall material.
M_WALL = mat("Hall_Wall", (0.45, 0.40, 0.30))
M_STONE = mat("Door_Stone", (0.70, 0.66, 0.57))
M_TRIM = mat("Door_StoneTrim", (0.58, 0.55, 0.48))
M_STEP = mat("Stair_Stone", (0.52, 0.50, 0.47))
M_TOWER = mat("Stair_Tower", (0.46, 0.44, 0.42))
M_ROOF = mat("Stair_Roof", (0.17, 0.19, 0.24))
M_IRON = mat("Door_Iron", (0.10, 0.10, 0.11), 0.45, 0.6)
M_VOID = mat("Door_Passage", (0.035, 0.03, 0.03), 1.0)


def centroid(poly):
    return (sum(p[0] for p in poly) / len(poly), sum(p[1] for p in poly) / len(poly))


class MeshBuilder:
    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []

    def mi(self, m):
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def face(self, pts, m, ref=None):
        """Adds a face; with ref, its normal is turned to point away from that point (outward)."""
        f = self.bm.faces.new([self.bm.verts.new(p) for p in pts])
        f.material_index = self.mi(m)
        if ref is not None:
            f.normal_update()
            if f.normal.dot(f.calc_center_median() - Vector(ref)) < 0:
                f.normal_flip()
        return f

    def prism_xz(self, poly, y0, y1, m):
        """Extrudes a polygon drawn in the XZ plane from y0 to y1."""
        n = len(poly)
        c = centroid(poly)
        ref = (c[0], (y0 + y1) * 0.5, c[1])
        front = [(x, y0, z) for x, z in poly]
        back = [(x, y1, z) for x, z in poly]
        self.face(front, m, ref)
        self.face(list(reversed(back)), m, ref)
        for i in range(n):
            j = (i + 1) % n
            self.face((front[i], back[i], back[j], front[j]), m, ref)

    def prism_xy(self, poly, z0, z1, m):
        """Extrudes a polygon drawn in the XY plane (plan view) from z0 up to z1."""
        n = len(poly)
        c = centroid(poly)
        ref = (c[0], c[1], (z0 + z1) * 0.5)
        bot = [(x, y, z0) for x, y in poly]
        top = [(x, y, z1) for x, y in poly]
        self.face(list(reversed(bot)), m, ref)
        self.face(top, m, ref)
        for i in range(n):
            j = (i + 1) % n
            self.face((bot[i], bot[j], top[j], top[i]), m, ref)

    def box(self, x0, x1, y0, y1, z0, z1, m):
        self.prism_xy([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], z0, z1, m)

    def finish(self, name, parent, uv_scale=0.25):
        bm = self.bm
        bm.normal_update()
        # Box-projected UVs so textured materials (the hall wall) tile at about 4 m per repeat
        uv = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            n = f.normal
            ax = max(range(3), key=lambda k: abs(n[k]))
            for loop in f.loops:
                co = loop.vert.co
                u, v = {0: (co.y, co.z), 1: (co.x, co.z), 2: (co.x, co.y)}[ax]
                loop[uv].uv = (u * uv_scale, v * uv_scale)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        for m in self.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        ob.parent = parent
        for p in me.polygons:
            p.use_smooth = False
        return ob


def arch_poly(hw, spring, segs=12, base=0.0):
    """Round arch outline (CCW in XZ): up the right side, over the top, down the left."""
    pts = [(hw, base)]
    for i in range(segs + 1):
        a = math.pi * i / segs
        pts.append((hw * math.cos(a), spring + hw * math.sin(a)))
    pts.append((-hw, base))
    return pts


def empty(name, parent, loc):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.3
    bpy.context.scene.collection.objects.link(e)
    e.parent = parent
    e.location = loc
    return e


def build_facade(root):
    mb = MeshBuilder()
    # Wall body around the opening: left and right piers plus the part above the arch
    W = 4.7
    top_arch = OPEN_SPRING + OPEN_HW
    mb.box(-W, -OPEN_HW, WALL_Y0, WALL_Y1, 0, WALL_H, M_WALL)
    mb.box(OPEN_HW, W, WALL_Y0, WALL_Y1, 0, WALL_H, M_WALL)
    # Spandrel above the arch: one slab per arch segment, from the curve up to the wall top
    segs = 12
    for i in range(segs):
        a0, a1 = math.pi * i / segs, math.pi * (i + 1) / segs
        x0, z0 = OPEN_HW * math.cos(a0), OPEN_SPRING + OPEN_HW * math.sin(a0)
        x1, z1 = OPEN_HW * math.cos(a1), OPEN_SPRING + OPEN_HW * math.sin(a1)
        mb.prism_xz([(x1, z1), (x0, z0), (x0, WALL_H), (x1, WALL_H)], WALL_Y0, WALL_Y1, M_WALL)

    # Pale stone arch ring (voussoirs) proud of the wall, with a keystone
    ring_in, ring_out, ry0, ry1 = OPEN_HW, OPEN_HW + 0.45, WALL_Y0 - 0.18, WALL_Y0 + 0.05
    for i in range(segs):
        a0, a1 = math.pi * i / segs, math.pi * (i + 1) / segs
        p = [(ring_in * math.cos(a0), OPEN_SPRING + ring_in * math.sin(a0)),
             (ring_out * math.cos(a0), OPEN_SPRING + ring_out * math.sin(a0)),
             (ring_out * math.cos(a1), OPEN_SPRING + ring_out * math.sin(a1)),
             (ring_in * math.cos(a1), OPEN_SPRING + ring_in * math.sin(a1))]
        mb.prism_xz(list(reversed(p)), ry0, ry1, M_STONE if i % 2 == 0 else M_TRIM)
    k = top_arch
    mb.prism_xz([(-0.32, k - 0.05), (0.32, k - 0.05), (0.42, k + 0.62), (-0.42, k + 0.62)], ry0 - 0.06, ry1, M_STONE)
    # Jambs: straight stone up to the springing, and an impost block on each side
    for s in (-1, 1):
        x_in, x_out = s * OPEN_HW, s * (OPEN_HW + 0.45)
        mb.box(min(x_in, x_out), max(x_in, x_out), ry0, ry1, 0, OPEN_SPRING, M_STONE)
        mb.box(min(x_in, x_out) - 0.08 * (s < 0), max(x_in, x_out) + 0.08 * (s > 0), ry0 - 0.08, ry1, OPEN_SPRING - 0.22, OPEN_SPRING, M_TRIM)

    # Buttresses with stepped caps either side, where the exit braziers stand
    for s in (-1, 1):
        cx = s * 3.15
        mb.box(cx - 0.55, cx + 0.55, WALL_Y0 - 0.75, WALL_Y0 + 0.05, 0, 5.4, M_STONE)
        mb.box(cx - 0.65, cx + 0.65, WALL_Y0 - 0.85, WALL_Y0 + 0.05, 0, 0.45, M_TRIM)
        mb.box(cx - 0.65, cx + 0.65, WALL_Y0 - 0.85, WALL_Y0 + 0.05, 5.4, 5.7, M_TRIM)
    # Cornice along the wall top and a sill step in the opening
    mb.box(-4.7, 4.7, WALL_Y0 - 0.25, WALL_Y1 + 0.1, WALL_H - 0.35, WALL_H, M_TRIM)
    mb.box(-OPEN_HW - 0.45, OPEN_HW + 0.45, WALL_Y0 - 0.55, WALL_Y0 + 0.1, 0, 0.1, M_TRIM)
    # Floor through the opening
    mb.box(-OPEN_HW, OPEN_HW, WALL_Y0, WALL_Y1 + 0.2, -0.05, 0.02, M_STEP)
    ob = mb.finish("Stairs_Facade", root)

    empty("PillarTop_L", root, (-3.15, WALL_Y0 - 0.35, 5.7))
    empty("PillarTop_R", root, (3.15, WALL_Y0 - 0.35, 5.7))
    empty("Sign_Anchor", root, (0, WALL_Y0 - 0.6, top_arch + 2.5))
    return ob


def ring_segment(mb, a0, a1, r_in, r_out, z0, z1, m, cy=TURRET_Y):
    p = lambda a, r: (r * math.cos(a), cy + r * math.sin(a))
    mb.prism_xy([p(a0, r_in), p(a0, r_out), p(a1, r_out), p(a1, r_in)], z0, z1, m)


def build_turret(root):
    mb = MeshBuilder()
    sides = 20
    r_in, r_out = TURRET_R - TURRET_T, TURRET_R
    gap = math.asin((OPEN_HW + 0.1) / r_out)
    for i in range(sides):
        a0, a1 = 2 * math.pi * i / sides, 2 * math.pi * (i + 1) / sides
        mid = (a0 + a1) * 0.5
        # Leave the passage from the hall open at ground level (the side facing -Y)
        facing_hall = abs(math.atan2(math.sin(mid + math.pi / 2), math.cos(mid + math.pi / 2))) < gap + math.pi / sides
        z0 = OPEN_SPRING + OPEN_HW + 0.2 if facing_hall else 0.0
        ring_segment(mb, a0, a1, r_in, r_out, z0, TURRET_H, M_TOWER)
        # String course and a crenellated top
        ring_segment(mb, a0, a1, r_out - 0.05, r_out + 0.15, TURRET_H - 3.2, TURRET_H - 2.95, M_TRIM)
        ring_segment(mb, a0, a1, r_in - 0.05, r_out + 0.2, TURRET_H, TURRET_H + 0.3, M_TRIM)
        if i % 2 == 0:
            ring_segment(mb, a0, a1, r_out - 0.45, r_out + 0.2, TURRET_H + 0.3, TURRET_H + 1.1, M_TOWER)
        # Narrow arrow-slit windows (dark) on the outward side, staggered with the stairs
        if not facing_hall and i % 5 == 2:
            for wz in (5.5, 9.5):
                a = mid
                ring_segment(mb, a - 0.05, a + 0.05, r_out + 0.0, r_out + 0.02, wz, wz + 1.1, M_VOID)
    # Floor inside the turret
    poly = [(r_in * math.cos(2 * math.pi * i / sides), TURRET_Y + r_in * math.sin(2 * math.pi * i / sides)) for i in range(sides)]
    mb.prism_xy(poly, -0.05, 0.02, M_STEP)
    # Slate cone roof with a finial
    rb = r_out + 0.35
    apex = (0, TURRET_Y, TURRET_H + 6.0)
    base_z = TURRET_H + 1.0
    for i in range(sides):
        a0, a1 = 2 * math.pi * i / sides, 2 * math.pi * (i + 1) / sides
        mb.face([(rb * math.cos(a0), TURRET_Y + rb * math.sin(a0), base_z),
                 (rb * math.cos(a1), TURRET_Y + rb * math.sin(a1), base_z), apex], M_ROOF, ref=(0, TURRET_Y, base_z))
        mb.face([(rb * math.cos(a1), TURRET_Y + rb * math.sin(a1), base_z),
                 (rb * math.cos(a0), TURRET_Y + rb * math.sin(a0), base_z), (0, TURRET_Y, base_z)], M_ROOF, ref=apex)
    mb.box(-0.06, 0.06, TURRET_Y - 0.06, TURRET_Y + 0.06, TURRET_H + 5.8, TURRET_H + 7.0, M_IRON)
    return mb.finish("Stairs_Turret", root)


def build_stairs(root):
    mb = MeshBuilder()
    sides = 12
    # Newel: the stone column the steps wind around
    poly = [(NEWEL_R * math.cos(2 * math.pi * i / sides), TURRET_Y + NEWEL_R * math.sin(2 * math.pi * i / sides)) for i in range(sides)]
    mb.prism_xy(poly, 0, TURRET_H + 0.2, M_TRIM)
    r_out = TURRET_R - TURRET_T + 0.02
    start = -math.pi / 2 - math.radians(STEP_DEG) * 0.5   # first step straight ahead of the opening
    for i in range(STEP_COUNT):
        a0 = start + math.radians(STEP_DEG) * i
        a1 = a0 + math.radians(STEP_DEG)
        top = STEP_RISE * (i + 1)
        bottom = max(0.0, top - STEP_RISE - 0.35)
        # Wedge from the newel out to the turret wall, split in two so the outer edge reads round
        am = (a0 + a1) * 0.5
        p = lambda a, r: (r * math.cos(a), TURRET_Y + r * math.sin(a))
        mb.prism_xy([p(a0, NEWEL_R), p(a0, r_out), p(am, r_out), p(a1, r_out), p(a1, NEWEL_R)], bottom, top, M_STEP)
        # Paler nosing on the tread's front edge
        mb.prism_xy([p(a0 - 0.012, NEWEL_R), p(a0 - 0.012, r_out), p(a0 + 0.06, r_out), p(a0 + 0.06, NEWEL_R)], top - 0.06, top + 0.01, M_STONE)
    return mb.finish("Stairs_Steps", root)


def build_all():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    root = bpy.data.objects.new("Hall_Tower_Stairs", None)
    bpy.context.scene.collection.objects.link(root)
    build_facade(root)
    build_turret(root)
    build_stairs(root)
    empty("Hall_Side", root, (0, -5.0, 0))
    return root


def export(root):
    os.makedirs(OUT_DIR, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for ch in root.children_recursive:
        ch.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_DIR, "Hall_Tower_Stairs.fbx"), use_selection=True,
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
                             object_types={"EMPTY", "MESH"}, use_mesh_modifiers=True, add_leaf_bones=False,
                             mesh_smooth_type="FACE")


root = build_all()
if bpy.app.background:
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    export(root)
