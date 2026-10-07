# The Queen's treasure vault for the Castle Hall's south-east corner (Castle of Dice).
# Run: blender -b --factory-startup --python HallVault_build.py
# Blender X = world X, Blender Y = world Z (north), Z up. Origin = the hall's inner south-east corner
# (world x 39, z -39); the vault fills x -9..0, y 0..9 and its door faces the hall (+Y, north).
#
#   Vault_Walls   west and north walls of cut stone, the door opening in the north wall
#   Vault_Roof    a stone slab roof with a moulded cornice
#   Vault_Frame   pale stone door surround: jambs, round arch, keystone and a crowned plaque
#   Vault_Door    the iron door leaf, origin on its hinge: riveted plates, straps, strap hinges,
#                 a lock wheel and a golden d20 boss
#   Vault_Treasure gold heaps, coin stacks, gems and a shield inside
#   North, Chest_Spot, Ghost_Spot, Light_Spot   empties Unity reads
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from lowpoly_mesh import MeshBuilder, mat, empty, clear_scene, export_fbx

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "Assets", "Models", "Vault", "Hall_Vault.fbx")

SIZE = 9.0          # outer size of the vault (square, in the corner)
T = 0.9             # wall thickness
H = 7.0             # wall height (the slab roof sits on top)
DOOR_X = -4.5       # door centre along the north wall
DOOR_HW = 1.2       # half width of the opening
SPRING = 2.7        # arch springing height

M_STONE = mat("Vault_Stone", (0.45, 0.42, 0.33))          # mapped to the hall's wall stone in Unity
M_TRIM = mat("Door_Stone", (0.70, 0.66, 0.57))
M_TRIM_DARK = mat("Door_StoneTrim", (0.58, 0.55, 0.48))
M_IRON = mat("Vault_IronPlate", (0.16, 0.17, 0.19), 0.45, 0.85)
M_IRON_DARK = mat("Vault_IronDark", (0.07, 0.07, 0.08), 0.5, 0.8)
M_GOLD = mat("Vault_Gold", (0.95, 0.68, 0.18), 0.3, 1.0)
M_RUBY = mat("Vault_Ruby", (0.75, 0.05, 0.12), 0.15)
M_SAPPHIRE = mat("Vault_Sapphire", (0.08, 0.25, 0.85), 0.15)
M_SHIELD = mat("Vault_ShieldBlue", (0.12, 0.2, 0.45), 0.6)

NORTH_Y0 = SIZE - T


def arch(hw, spring, segs=12):
    return [(hw * math.cos(math.pi * i / segs), spring + hw * math.sin(math.pi * i / segs)) for i in range(segs + 1)]


def build_walls(root):
    mb = MeshBuilder()
    # West wall (full), north wall in three parts around the door
    mb.box(-SIZE, -SIZE + T, 0, SIZE, 0, H, M_STONE)
    x_l, x_r = DOOR_X - DOOR_HW, DOOR_X + DOOR_HW
    mb.box(-SIZE + T, x_l, NORTH_Y0, SIZE, 0, H, M_STONE)
    mb.box(x_r, 0, NORTH_Y0, SIZE, 0, H, M_STONE)
    # Above the opening: one slab per arch segment, curve up to the wall top
    pts = arch(DOOR_HW, SPRING)
    for i in range(len(pts) - 1):
        (xa, za), (xb, zb) = pts[i], pts[i + 1]
        mb.prism_xz([(DOOR_X + xb, zb), (DOOR_X + xa, za), (DOOR_X + xa, H), (DOOR_X + xb, H)], NORTH_Y0, SIZE, M_STONE)
    # Plinth along the hall faces
    mb.box(-SIZE - 0.12, -SIZE + 0.2, 0, SIZE + 0.12, 0, 0.45, M_TRIM_DARK)
    mb.box(-SIZE - 0.12, x_l - 0.55, SIZE - 0.2, SIZE + 0.12, 0, 0.45, M_TRIM_DARK)
    mb.box(x_r + 0.55, 0, SIZE - 0.2, SIZE + 0.12, 0, 0.45, M_TRIM_DARK)
    # Inside floor: worn flagstones a touch above the hall floor
    mb.box(-SIZE + T, 0, 0, NORTH_Y0 + 0.4, 0, 0.03, M_TRIM_DARK)
    return mb.finish("Vault_Walls", root)


def build_roof(root):
    mb = MeshBuilder()
    mb.box(-SIZE - 0.35, 0, -0.0, SIZE + 0.35, H, H + 0.35, M_TRIM_DARK)       # cornice
    mb.box(-SIZE - 0.15, 0, 0, SIZE + 0.15, H + 0.35, H + 0.75, M_STONE)        # slab
    # Low hipped cap so the top does not read as a flat box from the camera
    c = (-SIZE / 2, SIZE / 2)
    corners = [(-SIZE - 0.15, 0), (0, 0), (0, SIZE + 0.15), (-SIZE - 0.15, SIZE + 0.15)]
    ring_lo = [(x, y, H + 0.75) for x, y in corners]
    ring_hi = [(c[0] + (x - c[0]) * 0.55, c[1] + (y - c[1]) * 0.55, H + 1.55) for x, y in corners]
    mb.solid(ring_lo, ring_hi, M_TRIM_DARK)
    # Corner finials on the hall side
    for x, y in ((-SIZE - 0.15, SIZE + 0.15), (-SIZE - 0.15, 0.2), (-0.2, SIZE + 0.15)):
        mb.cyl((x + (0.25 if x < -1 else -0.25), y - (0.25 if y > 1 else -0.25)), 0.32, H + 0.75, H + 1.4, M_TRIM, sides=8)
        mb.limb((x + (0.25 if x < -1 else -0.25), y - (0.25 if y > 1 else -0.25), H + 1.4),
                (x + (0.25 if x < -1 else -0.25), y - (0.25 if y > 1 else -0.25), H + 2.1), 0.26, 0.02, M_TRIM, sides=8)
    return mb.finish("Vault_Roof", root)


def build_frame(root):
    mb = MeshBuilder()
    y0, y1 = SIZE - 0.05, SIZE + 0.28
    ring_in, ring_out = DOOR_HW, DOOR_HW + 0.5
    for s in (-1, 1):
        x_in, x_out = DOOR_X + s * ring_in, DOOR_X + s * ring_out
        mb.box(min(x_in, x_out), max(x_in, x_out), y0, y1, 0, SPRING, M_TRIM)
        mb.box(min(x_in, x_out) - 0.1, max(x_in, x_out) + 0.1, y0, y1 + 0.1, SPRING - 0.25, SPRING, M_TRIM_DARK)
        mb.box(min(x_in, x_out) - 0.1, max(x_in, x_out) + 0.1, y0, y1 + 0.1, 0, 0.35, M_TRIM_DARK)
    segs = 10
    for i in range(segs):
        a0, a1 = math.pi * i / segs, math.pi * (i + 1) / segs
        p = [(DOOR_X + ring_in * math.cos(a0), SPRING + ring_in * math.sin(a0)),
             (DOOR_X + ring_out * math.cos(a0), SPRING + ring_out * math.sin(a0)),
             (DOOR_X + ring_out * math.cos(a1), SPRING + ring_out * math.sin(a1)),
             (DOOR_X + ring_in * math.cos(a1), SPRING + ring_in * math.sin(a1))]
        mb.prism_xz(p, y0, y1, M_TRIM if i % 2 == 0 else M_TRIM_DARK)
    top = SPRING + ring_out
    mb.prism_xz([(DOOR_X - 0.3, top - 0.55), (DOOR_X + 0.3, top - 0.55), (DOOR_X + 0.4, top + 0.15), (DOOR_X - 0.4, top + 0.15)], y0, y1 + 0.12, M_TRIM)
    # Plaque with a little crown above the door
    pz = top + 0.55
    mb.box(DOOR_X - 1.0, DOOR_X + 1.0, y0, y1 + 0.05, pz, pz + 0.75, M_TRIM_DARK)
    mb.box(DOOR_X - 0.9, DOOR_X + 0.9, y1 + 0.05, y1 + 0.1, pz + 0.08, pz + 0.67, M_TRIM)
    cz = pz + 0.2
    mb.box(DOOR_X - 0.35, DOOR_X + 0.35, y1 + 0.1, y1 + 0.18, cz, cz + 0.12, M_GOLD)
    for k in (-1, 0, 1):
        mb.prism_xz([(DOOR_X + k * 0.28 - 0.08, cz + 0.12), (DOOR_X + k * 0.28 + 0.08, cz + 0.12), (DOOR_X + k * 0.28, cz + (0.42 if k == 0 else 0.32))],
                    y1 + 0.1, y1 + 0.18, M_GOLD)
    return mb.finish("Vault_Frame", root)


def build_door(root):
    """Iron leaf in the opening, hinged on its west edge; it swings into the vault."""
    mb = MeshBuilder()
    hw = DOOR_HW - 0.04
    yf, yb = SIZE - 0.32, SIZE - 0.5           # hall face and back face of the leaf
    hinge = (DOOR_X - hw, (yf + yb) * 0.5, 0)
    outline = [(DOOR_X - hw, 0.02), (DOOR_X + hw, 0.02)] + [(DOOR_X + hw * math.cos(math.pi * i / 14), SPRING + hw * math.sin(math.pi * i / 14)) for i in range(15)]
    mb.prism_xz(outline, yb, yf, M_IRON)
    # Raised rim just inside the edge
    rim = 0.12
    inner = [(DOOR_X - hw + rim, 0.14), (DOOR_X + hw - rim, 0.14)] + \
            [(DOOR_X + (hw - rim) * math.cos(math.pi * i / 14), SPRING + (hw - rim) * math.sin(math.pi * i / 14)) for i in range(15)]
    for i in range(len(outline)):
        j = (i + 1) % len(outline)
        a, b, c, d = outline[i], outline[j], inner[j], inner[i]
        mb.prism_xz([a, b, c, d], yf, yf + 0.05, M_IRON_DARK)
    # Horizontal straps with rivets, a vertical strap down the middle
    for z in (0.55, 1.6, 2.65):
        half = hw if z < SPRING else math.sqrt(max(0.0, hw * hw - (z - SPRING) ** 2))
        mb.box(DOOR_X - half + 0.05, DOOR_X + half - 0.05, yf, yf + 0.07, z - 0.11, z + 0.11, M_IRON_DARK)
        n = 7
        for k in range(n):
            x = DOOR_X - half + 0.2 + k * (2 * half - 0.4) / (n - 1)
            mb.prism_xz([(x - 0.04, z - 0.04), (x + 0.04, z - 0.04), (x + 0.04, z + 0.04), (x - 0.04, z + 0.04)], yf + 0.07, yf + 0.12, M_IRON)
    mb.box(DOOR_X - 0.1, DOOR_X + 0.1, yf, yf + 0.07, 0.15, SPRING + hw - 0.15, M_IRON_DARK)
    # Strap hinges on the hinge side, reaching across the leaf with a spearhead tip
    for z in (0.45, 1.6, 2.75):
        mb.box(DOOR_X - hw - 0.06, DOOR_X - hw + 1.25, yf + 0.04, yf + 0.11, z - 0.08, z + 0.08, M_IRON_DARK)
        tip = DOOR_X - hw + 1.25
        mb.prism_xz([(tip, z - 0.14), (tip + 0.26, z), (tip, z + 0.14)], yf + 0.04, yf + 0.11, M_IRON_DARK)
        mb.box(DOOR_X - hw - 0.1, DOOR_X - hw + 0.08, yb - 0.04, yf + 0.13, z - 0.16, z + 0.16, M_IRON_DARK)
    # Lock wheel: a ring with six spokes and a golden d20 boss
    cx, cz, ry = DOOR_X + 0.15, 1.6, yf + 0.12
    sides = 16
    r0, r1 = 0.48, 0.58
    for i in range(sides):
        a0, a1 = 2 * math.pi * i / sides, 2 * math.pi * (i + 1) / sides
        mb.prism_xz([(cx + r0 * math.cos(a0), cz + r0 * math.sin(a0)), (cx + r1 * math.cos(a0), cz + r1 * math.sin(a0)),
                     (cx + r1 * math.cos(a1), cz + r1 * math.sin(a1)), (cx + r0 * math.cos(a1), cz + r0 * math.sin(a1))], ry, ry + 0.09, M_GOLD)
    for k in range(6):
        a = math.pi * k / 3 + math.pi / 6
        mb.limb((cx, ry + 0.045, cz), (cx + 0.5 * math.cos(a), ry + 0.045, cz + 0.5 * math.sin(a)), 0.035, 0.035, M_IRON_DARK, sides=5)
        hx, hz = cx + 0.62 * math.cos(a), cz + 0.62 * math.sin(a)
        mb.limb((hx, ry + 0.045, hz), (hx + 0.12 * math.cos(a), ry + 0.045, hz + 0.12 * math.sin(a)), 0.05, 0.03, M_GOLD, sides=6)
    d20(mb, (cx, ry + 0.17, cz), 0.2, M_GOLD)
    # Keyhole plate near the meeting edge
    mb.box(DOOR_X + hw - 0.45, DOOR_X + hw - 0.17, yf + 0.04, yf + 0.1, 1.05, 1.45, M_GOLD)
    mb.box(DOOR_X + hw - 0.33, DOOR_X + hw - 0.29, yf + 0.1, yf + 0.11, 1.13, 1.33, M_IRON_DARK)
    return mb.finish("Vault_Door", root, origin=hinge)


def d20(mb, center, r, m):
    """Icosahedron (the castle's die) as a gold boss."""
    from mathutils import Vector
    p = (1 + 5 ** 0.5) / 2
    v = [(-1, p, 0), (1, p, 0), (-1, -p, 0), (1, -p, 0), (0, -1, p), (0, 1, p), (0, -1, -p), (0, 1, -p),
         (p, 0, -1), (p, 0, 1), (-p, 0, -1), (-p, 0, 1)]
    faces = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
             (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)]
    s = r / Vector(v[0]).length
    c = Vector(center)
    for f in faces:
        mb.face([tuple(c + Vector(v[i]) * s) for i in f], m, ref=tuple(c))


def build_treasure(root):
    mb = MeshBuilder()

    def heap(x, y, r, h):
        sides = 9
        rings = []
        for k, (rr, zz) in enumerate(((r, 0.03), (r * 0.8, h * 0.45), (r * 0.42, h * 0.85), (0.0, h))):
            rings.append([(x + rr * math.cos(2 * math.pi * i / sides + k * 0.3) * (1 + 0.08 * math.sin(i * 2.3)),
                           y + rr * math.sin(2 * math.pi * i / sides + k * 0.3) * (1 + 0.08 * math.cos(i * 1.7)), zz) for i in range(sides)])
        mb.loft(rings, M_GOLD, cap_end=False)

    def stack(x, y, n, tilt=0.0):
        for k in range(n):
            mb.cyl((x + 0.02 * math.sin(k * 1.7), y + 0.02 * math.cos(k * 2.1)), 0.13, 0.03 + k * 0.05, 0.03 + k * 0.05 + 0.045, M_GOLD, sides=10)

    heap(-7.3, 1.6, 1.1, 0.75)
    heap(-1.7, 1.5, 0.9, 0.6)
    heap(-6.9, 6.4, 0.75, 0.5)
    heap(-1.6, 6.6, 0.7, 0.45)
    for x, y, n in ((-5.9, 1.1, 7), (-5.6, 1.4, 4), (-2.8, 1.0, 6), (-3.1, 1.3, 9), (-7.6, 4.2, 5), (-1.2, 4.0, 8), (-1.5, 3.7, 3)):
        stack(x, y, n)
    # Gems resting on the heaps
    for (x, y, z), m in (((-7.2, 1.7, 0.8), M_RUBY), ((-1.8, 1.4, 0.64), M_SAPPHIRE), ((-6.8, 6.3, 0.55), M_RUBY)):
        d20(mb, (x, y, z), 0.16, m)
    # A round shield and two crossed swords hung on the hall's south wall (y = 0)
    sides = 14
    sx, sz = -4.5, 2.6
    rim = [(sx + 0.75 * math.cos(2 * math.pi * i / sides), 0.16, sz + 0.75 * math.sin(2 * math.pi * i / sides)) for i in range(sides)]
    rim_b = [(x, 0.08, z) for x, _, z in rim]
    mb.solid(rim_b, rim, M_SHIELD)
    boss = [(sx + 0.2 * math.cos(2 * math.pi * i / 8), 0.28, sz + 0.2 * math.sin(2 * math.pi * i / 8)) for i in range(8)]
    mb.solid([(x, 0.16, z) for x, _, z in boss], boss, M_GOLD)
    for s in (-1, 1):
        mb.limb((sx - s * 1.1, 0.06, sz - 1.1), (sx + s * 1.1, 0.06, sz + 1.1), 0.05, 0.02, M_IRON, sides=4)
        mb.obox((sx - s * 0.82, 0.07, sz - 0.82), (0.5, 0.06, 0.08), M_GOLD, rot=(0, -45 * s, 0))
    return mb.finish("Vault_Treasure", root)


def build_all():
    clear_scene()
    root = bpy.data.objects.new("Hall_Vault", None)
    bpy.context.scene.collection.objects.link(root)
    build_walls(root)
    build_roof(root)
    build_frame(root)
    build_door(root)
    build_treasure(root)
    empty("North", root, (0, 20, 0))
    empty("Chest_Spot", root, (DOOR_X, 4.0, 0))
    empty("Ghost_Spot", root, (DOOR_X - 2.6, SIZE + 2.6, 0))
    empty("Light_Spot", root, (DOOR_X, 4.2, 4.8))
    return root


root = build_all()
if bpy.app.background:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "HallVault.blend"))
    export_fbx(root, OUT)
