# The Treasure Tower's new pieces (Castle of Dice, 2026-10-07):
#   Tower_Stairs_Top     the top end of the spiral stairs that climb from the Castle Hall: the Tower's south
#                        gate becomes a round-arched opening onto a landing, with the stone turret behind it and
#                        steps winding down out of sight. Matches Hall_Tower_Stairs (HallTowerStairs_build.py).
#   Tower_Mirror         the great mirror of the hidden room: a gilded frame on a stone plinth, a crest with a
#                        d20, and a separate Mirror_Glass quad that Unity turns into a reflecting surface.
#   Mirror_Room_Dressing pale stone pilasters along the hidden room's walls, a carpet runner up to the mirror
#                        and two braziers flanking it.
# Run: blender -b --factory-startup --python TowerMirror_build.py
# Blender is Z-up. Units are metres at game scale.
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from lowpoly_mesh import MeshBuilder, mat, empty, clear_scene, export_fbx

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_STAIRS = os.path.join(HERE, "..", "Assets", "Models", "Doors", "Tower_Stairs_Top.fbx")
OUT_MIRROR = os.path.join(HERE, "..", "Assets", "Models", "Tower", "Tower_Mirror.fbx")
OUT_DRESS = os.path.join(HERE, "..", "Assets", "Models", "Tower", "Mirror_Room_Dressing.fbx")
BLEND = os.path.join(HERE, "TowerMirror.blend")

# Materials: the stone and door names match Hall_Tower_Stairs, so Unity maps them the same way
M_WALL = mat("Tower_Wall", (0.40, 0.34, 0.27))
M_STONE = mat("Door_Stone", (0.70, 0.66, 0.57))
M_TRIM = mat("Door_StoneTrim", (0.58, 0.55, 0.48))
M_STEP = mat("Stair_Stone", (0.52, 0.50, 0.47))
M_TOWER = mat("Stair_Tower", (0.46, 0.44, 0.42))
M_ROOF = mat("Stair_Roof", (0.17, 0.19, 0.24))
M_IRON = mat("Door_Iron", (0.10, 0.10, 0.11), 0.45, 0.6)
M_VOID = mat("Door_Passage", (0.035, 0.03, 0.03), 1.0)
M_GOLD = mat("Mirror_Gold", (0.95, 0.70, 0.22), 0.3, 1.0)
M_GOLD_DARK = mat("Mirror_GoldDark", (0.55, 0.36, 0.10), 0.4, 1.0)
M_GLASS = mat("Mirror_Glass", (0.70, 0.78, 0.86), 0.05, 1.0)
M_GEM = mat("Mirror_Gem", (0.45, 0.20, 0.85), 0.15)
M_CARPET = mat("Mirror_Carpet", (0.42, 0.06, 0.08), 0.95)
M_CARPET_HEM = mat("Mirror_CarpetHem", (0.80, 0.58, 0.20), 0.8)
M_FLAME = mat("Mirror_Flame", (1.0, 0.62, 0.18), 1.0, 0.0, 1.0, (1.0, 0.55, 0.15))


# ---------------------------------------------------------------- stairs top
# Same frame as the hall stairs: the room lies toward -Y, the wall's inner face at y = -0.7, outer at 1.4.
WALL_Y0, WALL_Y1, WALL_H = -0.7, 1.4, 8.0
OPEN_HW, OPEN_SPRING = 1.7, 3.2
TURRET_Y, TURRET_R, TURRET_T = 4.2, 4.0, 0.7
TURRET_BOTTOM, TURRET_TOP = -7.0, 10.5
NEWEL_R = 0.45
STEP_RISE, STEP_DEG, STEP_COUNT = 0.25, 20.0, 26
LANDING_DEG = 70.0   # the landing covers this many degrees either side of the opening


def arch_pts(hw, spring, segs=12):
    return [(hw * math.cos(math.pi * i / segs), spring + hw * math.sin(math.pi * i / segs)) for i in range(segs + 1)]


def ring_segment(mb, a0, a1, r_in, r_out, z0, z1, m, cy=TURRET_Y):
    p = lambda a, r: (r * math.cos(a), cy + r * math.sin(a))
    mb.prism_xy([p(a0, r_in), p(a0, r_out), p(a1, r_out), p(a1, r_in)], z0, z1, m)


def build_facade(root):
    mb = MeshBuilder()
    W = 4.7
    top_arch = OPEN_SPRING + OPEN_HW
    mb.box(-W, -OPEN_HW, WALL_Y0, WALL_Y1, 0, WALL_H, M_WALL)
    mb.box(OPEN_HW, W, WALL_Y0, WALL_Y1, 0, WALL_H, M_WALL)
    pts = arch_pts(OPEN_HW, OPEN_SPRING)
    for i in range(len(pts) - 1):
        (x0, z0), (x1, z1) = pts[i], pts[i + 1]
        mb.prism_xz([(x1, z1), (x0, z0), (x0, WALL_H), (x1, WALL_H)], WALL_Y0, WALL_Y1, M_WALL)

    # Pale voussoirs and keystone, proud of the wall on the room side
    segs = 12
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
    for s in (-1, 1):
        x_in, x_out = s * OPEN_HW, s * (OPEN_HW + 0.45)
        mb.box(min(x_in, x_out), max(x_in, x_out), ry0, ry1, 0, OPEN_SPRING, M_STONE)
        mb.box(min(x_in, x_out) - 0.08 * (s < 0), max(x_in, x_out) + 0.08 * (s > 0), ry0 - 0.08, ry1, OPEN_SPRING - 0.22, OPEN_SPRING, M_TRIM)

    # Buttresses with stepped caps either side (the exit braziers stand on them)
    for s in (-1, 1):
        cx = s * 3.15
        mb.box(cx - 0.55, cx + 0.55, WALL_Y0 - 0.75, WALL_Y0 + 0.05, 0, 5.4, M_STONE)
        mb.box(cx - 0.65, cx + 0.65, WALL_Y0 - 0.85, WALL_Y0 + 0.05, 0, 0.45, M_TRIM)
        mb.box(cx - 0.65, cx + 0.65, WALL_Y0 - 0.85, WALL_Y0 + 0.05, 5.4, 5.7, M_TRIM)
    # Cornice along the wall top, sill and the floor through the opening
    mb.box(-W, W, WALL_Y0 - 0.25, WALL_Y1 + 0.1, WALL_H - 0.35, WALL_H, M_TRIM)
    mb.box(-OPEN_HW - 0.45, OPEN_HW + 0.45, WALL_Y0 - 0.55, WALL_Y0 + 0.1, 0, 0.1, M_TRIM)
    mb.box(-OPEN_HW, OPEN_HW, WALL_Y0, WALL_Y1 + 0.2, -0.05, 0.02, M_STEP)
    ob = mb.finish("Stairs_Facade", root)

    empty("PillarTop_L", root, (-3.15, WALL_Y0 - 0.35, 5.7))
    empty("PillarTop_R", root, (3.15, WALL_Y0 - 0.35, 5.7))
    empty("Sign_Anchor", root, (0, WALL_Y0 - 0.6, top_arch + 1.6))
    return ob


def build_turret(root):
    mb = MeshBuilder()
    sides = 20
    r_in, r_out = TURRET_R - TURRET_T, TURRET_R
    gap = math.asin((OPEN_HW + 0.1) / r_out)
    for i in range(sides):
        a0, a1 = 2 * math.pi * i / sides, 2 * math.pi * (i + 1) / sides
        mid = (a0 + a1) * 0.5
        facing_room = abs(math.atan2(math.sin(mid + math.pi / 2), math.cos(mid + math.pi / 2))) < gap + math.pi / sides
        # The side toward the room is open from the landing up to the arch top; everywhere else full height
        if facing_room:
            ring_segment(mb, a0, a1, r_in, r_out, TURRET_BOTTOM, -0.05, M_TOWER)
            ring_segment(mb, a0, a1, r_in, r_out, OPEN_SPRING + OPEN_HW + 0.2, TURRET_TOP, M_TOWER)
        else:
            ring_segment(mb, a0, a1, r_in, r_out, TURRET_BOTTOM, TURRET_TOP, M_TOWER)
        ring_segment(mb, a0, a1, r_out - 0.05, r_out + 0.15, TURRET_TOP - 2.2, TURRET_TOP - 1.95, M_TRIM)
        ring_segment(mb, a0, a1, r_in - 0.05, r_out + 0.2, TURRET_TOP, TURRET_TOP + 0.3, M_TRIM)
        if i % 2 == 0:
            ring_segment(mb, a0, a1, r_out - 0.45, r_out + 0.2, TURRET_TOP + 0.3, TURRET_TOP + 1.1, M_TOWER)
        if not facing_room and i % 5 == 2:
            ring_segment(mb, mid - 0.05, mid + 0.05, r_out, r_out + 0.02, 6.4, 7.5, M_VOID)
    # Dark well at the bottom: the stairs vanish into it
    poly = [(r_in * math.cos(2 * math.pi * i / sides), TURRET_Y + r_in * math.sin(2 * math.pi * i / sides)) for i in range(sides)]
    mb.prism_xy(poly, TURRET_BOTTOM - 0.1, TURRET_BOTTOM, M_VOID)
    # Landing at floor level on the room side, from the newel out to the turret wall
    start = -math.pi / 2 - math.radians(LANDING_DEG)
    end = -math.pi / 2 + math.radians(LANDING_DEG)
    n = 8
    p = lambda a, r: (r * math.cos(a), TURRET_Y + r * math.sin(a))
    for i in range(n):
        a0 = start + (end - start) * i / n
        a1 = start + (end - start) * (i + 1) / n
        mb.prism_xy([p(a0, NEWEL_R), p(a0, r_in + 0.02), p(a1, r_in + 0.02), p(a1, NEWEL_R)], -0.4, 0.02, M_STEP)
    # Cone roof with a finial
    rb = r_out + 0.35
    apex = (0, TURRET_Y, TURRET_TOP + 6.0)
    base_z = TURRET_TOP + 1.0
    for i in range(sides):
        a0, a1 = 2 * math.pi * i / sides, 2 * math.pi * (i + 1) / sides
        mb.face([(rb * math.cos(a0), TURRET_Y + rb * math.sin(a0), base_z),
                 (rb * math.cos(a1), TURRET_Y + rb * math.sin(a1), base_z), apex], M_ROOF, ref=(0, TURRET_Y, base_z))
        mb.face([(rb * math.cos(a1), TURRET_Y + rb * math.sin(a1), base_z),
                 (rb * math.cos(a0), TURRET_Y + rb * math.sin(a0), base_z), (0, TURRET_Y, base_z)], M_ROOF, ref=apex)
    mb.box(-0.06, 0.06, TURRET_Y - 0.06, TURRET_Y + 0.06, TURRET_TOP + 5.8, TURRET_TOP + 7.0, M_IRON)
    return mb.finish("Stairs_Turret", root)


def build_steps(root):
    mb = MeshBuilder()
    sides = 12
    poly = [(NEWEL_R * math.cos(2 * math.pi * i / sides), TURRET_Y + NEWEL_R * math.sin(2 * math.pi * i / sides)) for i in range(sides)]
    mb.prism_xy(poly, TURRET_BOTTOM, TURRET_TOP + 0.2, M_TRIM)
    r_out = TURRET_R - TURRET_T + 0.02
    p = lambda a, r: (r * math.cos(a), TURRET_Y + r * math.sin(a))
    # Down from the landing's far edge, counter-clockwise, into the dark well
    start = -math.pi / 2 + math.radians(LANDING_DEG)
    for i in range(STEP_COUNT):
        a0 = start + math.radians(STEP_DEG) * i
        a1 = a0 + math.radians(STEP_DEG)
        top = -STEP_RISE * (i + 1)
        bottom = top - STEP_RISE - 0.35
        am = (a0 + a1) * 0.5
        mb.prism_xy([p(a0, NEWEL_R), p(a0, r_out), p(am, r_out), p(a1, r_out), p(a1, NEWEL_R)], bottom, top, M_STEP)
        mb.prism_xy([p(a0 - 0.012, NEWEL_R), p(a0 - 0.012, r_out), p(a0 + 0.06, r_out), p(a0 + 0.06, NEWEL_R)], top - 0.06, top + 0.01, M_STONE)
    return mb.finish("Stairs_Steps", root)


def build_stairs_top():
    root = bpy.data.objects.new("Tower_Stairs_Top", None)
    bpy.context.scene.collection.objects.link(root)
    build_facade(root)
    build_turret(root)
    build_steps(root)
    empty("Room_Side", root, (0, -5.0, 0))
    return root


# ---------------------------------------------------------------- the mirror
# The mirror faces -Y (into the room). Its back stands against the wall at y = 0.
GLASS_W, GLASS_H, GLASS_Z0 = 6.4, 4.6, 1.15
FRAME_W = 0.42


def build_mirror():
    root = bpy.data.objects.new("Tower_Mirror", None)
    bpy.context.scene.collection.objects.link(root)
    mb = MeshBuilder()
    hw = GLASS_W * 0.5
    z0, z1 = GLASS_Z0, GLASS_Z0 + GLASS_H
    # Stone plinth the mirror stands on, with a moulded top
    mb.box(-hw - 1.0, hw + 1.0, -1.1, 0.0, 0.0, 0.55, M_TRIM)
    mb.box(-hw - 1.1, hw + 1.1, -1.2, 0.05, 0.55, 0.75, M_STONE)
    # Backing board behind the glass (dark, so edges never show the wall)
    mb.box(-hw - 0.1, hw + 0.1, -0.12, 0.0, z0 - 0.1, z1 + 0.1, M_GOLD_DARK)
    # Gilded frame: outer moulding, inner bead
    f = FRAME_W
    mb.box(-hw - f, hw + f, -0.42, -0.02, z0 - f, z0, M_GOLD)
    mb.box(-hw - f, hw + f, -0.42, -0.02, z1, z1 + f, M_GOLD)
    mb.box(-hw - f, -hw, -0.42, -0.02, z0, z1, M_GOLD)
    mb.box(hw, hw + f, -0.42, -0.02, z0, z1, M_GOLD)
    b = 0.1
    mb.box(-hw, hw, -0.30, -0.02, z0, z0 + b, M_GOLD_DARK)
    mb.box(-hw, hw, -0.30, -0.02, z1 - b, z1, M_GOLD_DARK)
    mb.box(-hw, -hw + b, -0.30, -0.02, z0, z1, M_GOLD_DARK)
    mb.box(hw - b, hw, -0.30, -0.02, z0, z1, M_GOLD_DARK)
    # Corner bosses
    for sx in (-1, 1):
        for zz in (z0 - f * 0.5, z1 + f * 0.5):
            mb.obox((sx * (hw + f * 0.5), -0.5, zz), (0.62, 0.22, 0.62), M_GOLD, rot=(0, 45, 0))
    # Side pilasters: tall gilded columns with capitals, standing on the plinth
    for sx in (-1, 1):
        cx = sx * (hw + f + 0.45)
        mb.cyl((cx, -0.55), 0.28, 0.75, z1 + 0.2, M_GOLD, sides=10)
        mb.box(cx - 0.42, cx + 0.42, -0.97, -0.13, 0.75, 1.05, M_GOLD_DARK)
        mb.box(cx - 0.45, cx + 0.45, -1.0, -0.1, z1 + 0.2, z1 + 0.55, M_GOLD_DARK)
        mb.obox((cx, -0.55, z1 + 0.85), (0.5, 0.5, 0.5), M_GOLD, rot=(45, 0, 45))
    # Crest: a gilded arch over the frame with a purple-gem d20 at its heart
    crest_hw = hw * 0.62
    pts = []
    segs = 14
    for i in range(segs + 1):
        a = math.pi * i / segs
        pts.append((crest_hw * math.cos(a), z1 + f + 1.1 * math.sin(a)))
    for i in range(segs):
        (xa, za), (xb, zb) = pts[i], pts[i + 1]
        mb.prism_xz([(xa, z1 + f), (xb, z1 + f), (xb, zb + 0.28), (xa, za + 0.28)], -0.36, -0.06, M_GOLD)
    d20_c = (0, -0.55, z1 + f + 0.75)
    r = 0.55
    ico = [(0, 0, 1), (0.894, 0, 0.447), (0.276, 0.851, 0.447), (-0.724, 0.526, 0.447), (-0.724, -0.526, 0.447),
           (0.276, -0.851, 0.447), (0.724, 0.526, -0.447), (-0.276, 0.851, -0.447), (-0.894, 0, -0.447),
           (-0.276, -0.851, -0.447), (0.724, -0.526, -0.447), (0, 0, -1)]
    V = [(d20_c[0] + r * x, d20_c[1] + r * y, d20_c[2] + r * z) for x, y, z in ico]
    tris = [(0, 1, 2), (0, 2, 3), (0, 3, 4), (0, 4, 5), (0, 5, 1), (1, 6, 2), (2, 7, 3), (3, 8, 4), (4, 9, 5), (5, 10, 1),
            (6, 7, 2), (7, 8, 3), (8, 9, 4), (9, 10, 5), (10, 6, 1), (11, 7, 6), (11, 8, 7), (11, 9, 8), (11, 10, 9), (11, 6, 10)]
    for t in tris:
        mb.face([V[t[0]], V[t[1]], V[t[2]]], M_GEM, ref=d20_c)
    mb.finish("Mirror_Frame", root)

    # The glass on its own, slightly in front of the backing board, facing -Y
    gb = MeshBuilder()
    gy = -0.14
    gb.face([(-hw, gy, z0), (hw, gy, z0), (hw, gy, z1), (-hw, gy, z1)], M_GLASS, ref=(0, 1.0, (z0 + z1) * 0.5))
    glass = gb.finish("Mirror_Glass", root)
    # UVs 0..1 across the glass (the reflection shader uses screen space; this is for the fallback look)
    me = glass.data
    uv = me.uv_layers.active.data
    for poly in me.polygons:
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv[li].uv = ((co.x + hw) / GLASS_W, (co.z - z0) / GLASS_H)
    empty("Front", root, (0, -3.0, 0))
    empty("Glass_Center", root, (0, gy, (z0 + z1) * 0.5))
    return root


# ---------------------------------------------------------------- room dressing
# Room frame: X = world x, Y = world z - 15 (north). Interior x -13..13, y 1..19 (world z 16..34).
ROOM_X, ROOM_Y0, ROOM_Y1 = 13.0, 1.0, 19.0


def pilaster(mb, x, y, nx, ny, h=7.6):
    """Flat stone pilaster against a wall; (nx, ny) points into the room."""
    w, d = 0.55, 0.32
    if nx != 0:
        mb.box(min(x, x + nx * d), max(x, x + nx * d), y - w, y + w, 0, h, M_TRIM)
        mb.box(min(x, x + nx * (d + 0.12)), max(x, x + nx * (d + 0.12)), y - w - 0.1, y + w + 0.1, 0, 0.5, M_STONE)
        mb.box(min(x, x + nx * (d + 0.12)), max(x, x + nx * (d + 0.12)), y - w - 0.1, y + w + 0.1, h - 0.45, h, M_STONE)
    else:
        mb.box(x - w, x + w, min(y, y + ny * d), max(y, y + ny * d), 0, h, M_TRIM)
        mb.box(x - w - 0.1, x + w + 0.1, min(y, y + ny * (d + 0.12)), max(y, y + ny * (d + 0.12)), 0, 0.5, M_STONE)
        mb.box(x - w - 0.1, x + w + 0.1, min(y, y + ny * (d + 0.12)), max(y, y + ny * (d + 0.12)), h - 0.45, h, M_STONE)


def brazier(mb, fb, x, y):
    # Stone foot, iron stem, bowl with flames
    mb.cyl((x, y), 0.55, 0.0, 0.25, M_STONE, sides=8)
    mb.cyl((x, y), 0.14, 0.25, 1.35, M_IRON, sides=8)
    bowl_bot = [(x + 0.25 * math.cos(2 * math.pi * i / 10), y + 0.25 * math.sin(2 * math.pi * i / 10), 1.35) for i in range(10)]
    bowl_top = [(x + 0.62 * math.cos(2 * math.pi * i / 10), y + 0.62 * math.sin(2 * math.pi * i / 10), 1.75) for i in range(10)]
    mb.solid(bowl_bot, bowl_top, M_IRON)
    for i in range(5):
        a = 2 * math.pi * i / 5
        fx, fy = x + 0.22 * math.cos(a), y + 0.22 * math.sin(a)
        fb.limb((fx, fy, 1.7), (fx * 0.6 + x * 0.4, fy * 0.6 + y * 0.4, 2.35 + 0.15 * (i % 2)), 0.16, 0.02, M_FLAME, sides=5)
    fb.limb((x, y, 1.7), (x, y, 2.6), 0.2, 0.02, M_FLAME, sides=5)


def build_room_dressing():
    root = bpy.data.objects.new("Mirror_Room_Dressing", None)
    bpy.context.scene.collection.objects.link(root)
    mb = MeshBuilder()
    # Pilasters along the east and west walls and either side of the mirror
    for y in (5.5, 10.0, 14.5):
        pilaster(mb, -ROOM_X, y, 1, 0)
        pilaster(mb, ROOM_X, y, -1, 0)
    for x in (-8.0, 8.0):
        pilaster(mb, x, ROOM_Y1, 0, -1)
    # Carpet runner from the opening to the mirror's plinth, with a gold hem
    mb.box(-1.6, 1.6, ROOM_Y0 + 0.4, ROOM_Y1 - 1.4, 0.0, 0.03, M_CARPET_HEM)
    mb.box(-1.35, 1.35, ROOM_Y0 + 0.55, ROOM_Y1 - 1.55, 0.0, 0.045, M_CARPET)
    fb = MeshBuilder()
    for x in (-5.4, 5.4):
        brazier(mb, fb, x, ROOM_Y1 - 2.2)
    mb.finish("Room_Stone", root)
    fb.finish("Room_Flames", root)
    empty("North", root, (0, 5.0, 0))
    empty("Brazier_L", root, (-5.4, ROOM_Y1 - 2.2, 2.3))
    empty("Brazier_R", root, (5.4, ROOM_Y1 - 2.2, 2.3))
    return root


def isolate_export(root, path):
    export_fbx(root, path)


clear_scene()
stairs = build_stairs_top()
mirror = build_mirror()
dressing = build_room_dressing()
if bpy.app.background:
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    isolate_export(stairs, OUT_STAIRS)
    isolate_export(mirror, OUT_MIRROR)
    isolate_export(dressing, OUT_DRESS)
