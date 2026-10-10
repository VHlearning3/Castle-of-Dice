# Castle of Dice: the Oakhaven tavern interior (Zone_Tavern), built from code.
# Run:  blender -b --factory-startup --python BlenderSources/Tavern_Interior_build.py
# Writes BlenderSources/Tavern_Interior.blend, Assets/Models/Tavern/Tavern_Interior.fbx and the props palette.
#
# Everything is authored in Unity coordinates (x right, y up, z north; 1 unit = 1 m at the game's scale,
# where a villager is ~2.9 tall) and converted to Blender on the way in (FBX import maps Blender
# (x, y, z) -> Unity (x, z, y) with the axis conversion baked). Pieces that Unity gives colliders are separate objects named by role:
#   Floor, Wall_*, Gallery_*, Stairs (+ Stairs_Ramp collider mesh), Bar, Fireplace, Post_*, Table_*, ...
# Empties named Light_* mark where TavernSceneBuilder puts point lights; Spot_* mark NPC and spawn spots.
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "Models", "Tavern")
FBX = os.path.join(OUT_DIR, "Tavern_Interior.fbx")
PALETTE = os.path.join(OUT_DIR, "Tavern_Props_Palette.png")
BLEND = os.path.join(ROOT, "BlenderSources", "Tavern_Interior.blend")
random.seed(11)

# ---------------------------------------------------------------- layout (Unity coordinates)
RX, RZ = 12.0, 9.0          # interior half sizes: x -12..12, z -9..9
WALL_T = 0.8                # wall thickness (outside the room)
WALL_H = 11.0
GAL_Y = 5.2                 # gallery walking height
GAL_T = 0.35                # gallery slab thickness
GAL_N_Z = 4.0               # north gallery inner edge (z 4..9)
GAL_E_X = 7.0               # east gallery inner edge (x 7..12)
DOOR_X, DOOR_W, DOOR_H = -6.0, 3.2, 5.0      # entrance in the south wall
STAIR_X0, STAIR_X1, STAIR_Z0, STAIR_Z1 = 0.5, GAL_E_X, -RZ + 0.1, -RZ + 2.7   # rises towards +x
STEP_COUNT = 13


def U(x, y, z):
    """Unity point -> Blender point."""
    return Vector((x, z, y))


# ---------------------------------------------------------------- materials / palette
def make_mat(name, rgb=(0.8, 0.8, 0.8), emit=None, image=None, rough=0.85):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = rough
    if image is not None:
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = image; t.interpolation = 'Closest'
        nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    if emit is not None:
        bsdf.inputs["Emission Color"].default_value = (*emit, 1)
        bsdf.inputs["Emission Strength"].default_value = 2.0
    m.diffuse_color = (*rgb, 1)
    return m

PAL = {  # cell: rgb (0-255). 8x8 cells, 4 px each
    "wood": (0, (126, 80, 44)), "wood_dark": (1, (84, 52, 30)), "wood_light": (2, (166, 112, 64)),
    "iron": (3, (52, 50, 52)), "brass": (4, (196, 150, 64)), "candle": (5, (240, 230, 200)),
    "plate": (6, (214, 206, 190)), "bread": (7, (206, 140, 64)), "meat": (8, (150, 74, 40)),
    "foam": (9, (244, 232, 196)), "beer": (10, (214, 140, 34)), "bottle_g": (11, (60, 110, 62)),
    "bottle_r": (12, (124, 34, 40)), "rug": (13, (138, 42, 36)), "rug_trim": (14, (212, 168, 76)),
    "book_b": (15, (46, 70, 120)), "book_r": (16, (128, 40, 34)), "book_g": (17, (52, 96, 56)),
    "cheese": (18, (236, 192, 72)), "apple": (19, (196, 48, 40)), "soot": (20, (30, 26, 24)),
    "stone": (21, (120, 112, 104)), "ash": (22, (70, 64, 60)), "cloth": (23, (196, 186, 160)),
    "herb": (24, (96, 130, 60)), "rope": (25, (176, 150, 100)),
}
N = 8
pal_img = bpy.data.images.new("Tavern_Props_Palette", N * 4, N * 4, alpha=False)
px = [0.0] * (N * 4 * N * 4 * 4)
for name, (idx, rgb) in PAL.items():
    cx, cy = idx % N, idx // N
    for y in range(cy * 4, cy * 4 + 4):
        for x in range(cx * 4, cx * 4 + 4):
            i = (y * N * 4 + x) * 4
            px[i:i + 4] = [rgb[0] / 255, rgb[1] / 255, rgb[2] / 255, 1.0]
pal_img.pixels = px
os.makedirs(OUT_DIR, exist_ok=True)
pal_img.filepath_raw = PALETTE; pal_img.file_format = 'PNG'; pal_img.save()

M_STONE = make_mat("M_Tavern_Stone", (0.62, 0.50, 0.40))
M_FLOOR = make_mat("M_Tavern_Floor", (0.55, 0.36, 0.20))
M_WOOD = make_mat("M_Tavern_Timber", (0.38, 0.24, 0.13))
M_PROPS = make_mat("M_Tavern_Props", image=pal_img)
M_GLOW = make_mat("M_Tavern_Glow", (1.0, 0.75, 0.35), emit=(1.0, 0.62, 0.22))
M_WINDOW = make_mat("M_Tavern_Window", (0.16, 0.20, 0.42), emit=(0.18, 0.24, 0.55))
M_DARK = make_mat("M_Tavern_Dark", (0.02, 0.02, 0.02))
TILE = {M_STONE.name: 3.0, M_FLOOR.name: 3.2, M_WOOD.name: 2.4}


def cell_uv(color):
    idx = PAL[color][0]
    return Vector(((idx % N + 0.5) / N, (idx // N + 0.5) / N))


# ---------------------------------------------------------------- mesh builder
class Mesh:
    """Collects faces in Unity space; textured materials get world-scale box UVs, palette faces a colour cell."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.mats = []

    def _mi(self, mat):
        if mat not in self.mats: self.mats.append(mat)
        return self.mats.index(mat)

    def face(self, pts_u, mat, color=None, ref_u=None):
        vs = [self.bm.verts.new(U(*p)) for p in pts_u]
        f = self.bm.faces.new(vs)
        f.material_index = self._mi(mat)
        f.normal_update()
        if ref_u is not None:
            if f.normal.dot(f.calc_center_median() - U(*ref_u)) < 0:
                f.normal_flip()
        n = f.normal
        for l in f.loops:
            if color is not None:
                l[self.uv].uv = cell_uv(color)
            else:
                t = TILE.get(mat.name, 2.0)
                co = l.vert.co
                ax = max(range(3), key=lambda i: abs(n[i]))
                if ax == 0: u, v = co.y, co.z
                elif ax == 1: u, v = co.x, co.z
                else: u, v = co.x, co.y
                l[self.uv].uv = (u / t, v / t)
        return f

    def box(self, c, s, mat, color=None, rot_y=0.0, skip=()):
        """Axis box centred at c with size s (Unity), optionally turned about y (degrees)."""
        cx, cy, cz = c; hx, hy, hz = s[0] / 2, s[1] / 2, s[2] / 2
        r = math.radians(rot_y); cr, sr = math.cos(r), math.sin(r)
        def P(x, y, z):
            return (cx + x * cr + z * sr, cy + y, cz - x * sr + z * cr)
        v = [P(x, y, z) for x in (-hx, hx) for y in (-hy, hy) for z in (-hz, hz)]
        # index = xi*4 + yi*2 + zi
        quads = {"-x": (0, 1, 3, 2), "+x": (4, 6, 7, 5), "-y": (0, 4, 5, 1), "+y": (2, 3, 7, 6), "-z": (0, 2, 6, 4), "+z": (1, 5, 7, 3)}
        for k, q in quads.items():
            if k in skip: continue
            self.face([v[i] for i in q], mat, color, ref_u=c)

    def cyl(self, c, r, h, mat, color=None, seg=10, r_top=None, axis='y'):
        """Cylinder standing on c (base centre) along +y, or lying along x/z when axis is 'x'/'z'."""
        rt = r if r_top is None else r_top
        def P(a, rad, t):
            ca, sa = math.cos(a) * rad, math.sin(a) * rad
            if axis == 'y': return (c[0] + ca, c[1] + t, c[2] + sa)
            if axis == 'x': return (c[0] + t, c[1] + ca, c[2] + sa)
            return (c[0] + ca, c[1] + sa, c[2] + t)
        ang = [2 * math.pi * i / seg for i in range(seg)]
        bot = [P(a, r, 0) for a in ang]; top = [P(a, rt, h) for a in ang]
        mid = P(0, 0, h / 2)
        self.face(bot, mat, color, ref_u=mid); self.face(top, mat, color, ref_u=mid)
        for i in range(seg):
            j = (i + 1) % seg
            self.face([bot[i], bot[j], top[j], top[i]], mat, color, ref_u=mid)

    def finish(self, parent=None, origin_u=None):
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me); self.bm.free()
        for m in self.mats: me.materials.append(m)
        for p in me.polygons: p.use_smooth = False
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        # Pivots stay at the world origin: Unity's baked axis conversion misplaces children of offset parents
        # (a window under a wall, the ramp under the stairs), and colliders and cut-away use bounds anyway.
        if parent is not None:
            ob.parent = parent
            ob.matrix_parent_inverse = parent.matrix_world.inverted()
        return ob


def empty(name, pos_u, parent=None):
    e = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(e)
    e.location = U(*pos_u)
    e.empty_display_size = 0.4
    if parent is not None:
        e.parent = parent
        e.matrix_parent_inverse = parent.matrix_world.inverted()
    return e


bpy.ops.wm.read_factory_settings(use_empty=True)
# read_factory_settings wiped the data made above: rebuild materials and palette image
pal_img = bpy.data.images.new("Tavern_Props_Palette", N * 4, N * 4, alpha=False)
pal_img.pixels = px
pal_img.filepath_raw = PALETTE; pal_img.file_format = 'PNG'; pal_img.save()
M_STONE = make_mat("M_Tavern_Stone", (0.62, 0.50, 0.40))
M_FLOOR = make_mat("M_Tavern_Floor", (0.55, 0.36, 0.20))
M_WOOD = make_mat("M_Tavern_Timber", (0.38, 0.24, 0.13))
M_PROPS = make_mat("M_Tavern_Props", image=pal_img)
M_GLOW = make_mat("M_Tavern_Glow", (1.0, 0.75, 0.35), emit=(1.0, 0.62, 0.22))
M_WINDOW = make_mat("M_Tavern_Window", (0.16, 0.20, 0.42), emit=(0.18, 0.24, 0.55))
M_DARK = make_mat("M_Tavern_Dark", (0.02, 0.02, 0.02))
M_WALL = make_mat("M_Tavern_Wall", (0.48, 0.32, 0.2))   # wooden plank walls (Vili 2026-10-08); stone stays on the hearth
TILE = {M_STONE.name: 3.0, M_FLOOR.name: 3.2, M_WOOD.name: 2.4, M_WALL.name: 2.6}

root = empty("Tavern_Interior", (0, 0, 0))
g_shell = empty("Shell", (0, 0, 0), root)
g_frame = empty("Timber_Frame", (0, 0, 0), root)
g_furn = empty("Furniture", (0, 0, 0), root)
g_lights = empty("Lights", (0, 0, 0), root)
g_spots = empty("Spots", (0, 0, 0), root)

# ---------------------------------------------------------------- floor, rug
m = Mesh("Floor"); m.box((0, -0.25, 0), (2 * RX + 2 * WALL_T, 0.5, 2 * RZ + 2 * WALL_T), M_FLOOR); m.finish(g_shell)
m = Mesh("Rug")
# rug and trim stand clear of the floor and of each other (coplanar faces flickered, Vili 2026-10-07)
m.box((-2.5, 0.04, -2.0), (7.0, 0.04, 4.6), M_PROPS, "rug")
m.box((-2.5, 0.022, -2.0), (7.4, 0.024, 5.0), M_PROPS, "rug_trim", skip=("-y",))
m.finish(g_furn)

# ---------------------------------------------------------------- walls
# Each wall carries its own wooden plinth and top ledge, and its windows and sconces are its children:
# the game camera hides a whole wall (collider + child renderers) when it stands between camera and hero.
def wall(name, c, s, inward, plinth=True):
    """inward: unit (x, z) pointing from the wall into the room."""
    mm = Mesh(name)
    mm.box(c, s, M_WALL)
    ix, iz = inward
    face_x = c[0] + ix * s[0] / 2; face_z = c[2] + iz * s[2] / 2
    along = s[0] if ix == 0 else s[2]
    def ledge(y, h, d):
        if ix == 0: mm.box((c[0], y, face_z + iz * d / 2), (along, h, d), M_WOOD)
        else: mm.box((face_x + ix * d / 2, y, c[2]), (d, h, along), M_WOOD)
    if plinth: ledge(0.35, 0.7, 0.3)
    ledge(WALL_H - 0.25, 0.5, 0.4)
    return mm.finish(g_shell)

oz = RZ + WALL_T / 2; ox = RX + WALL_T / 2
W = {}
W["N"] = wall("Wall_North", (0, WALL_H / 2, oz), (2 * RX, WALL_H, WALL_T), (0, -1))
W["W"] = wall("Wall_West", (-ox, WALL_H / 2, 0), (WALL_T, WALL_H, 2 * RZ + 2 * WALL_T), (1, 0))
W["E"] = wall("Wall_East", (ox, WALL_H / 2, 0), (WALL_T, WALL_H, 2 * RZ + 2 * WALL_T), (-1, 0))
dl, dr = DOOR_X - DOOR_W / 2, DOOR_X + DOOR_W / 2
W["SW"] = wall("Wall_South_West", ((-RX + dl) / 2, WALL_H / 2, -oz), (dl + RX, WALL_H, WALL_T), (0, 1))
W["SE"] = wall("Wall_South_East", ((dr + RX) / 2, WALL_H / 2, -oz), (RX - dr, WALL_H, WALL_T), (0, 1))
W["SL"] = wall("Wall_South_Lintel", (DOOR_X, (DOOR_H + WALL_H) / 2, -oz), (DOOR_W, WALL_H - DOOR_H, WALL_T), (0, 1), plinth=False)
# dark passage behind the door so an open door shows night, not the void
m = Mesh("Door_Passage"); m.box((DOOR_X, DOOR_H / 2, -RZ - WALL_T - 0.6), (DOOR_W + 0.6, DOOR_H + 0.4, 0.4), M_DARK); m.finish(W["SL"])

# windows: arched frames with night glass set into the walls (north above the gallery, west, east, south)
def window(name, c, wall_axis, parent, w=1.6, h=2.6):
    mm = Mesh(name)
    x, y, z = c
    if wall_axis == 'z':   # on a north/south wall, facing along z
        sgn = -1 if z > 0 else 1
        mm.box((x, y, z), (w, h, 0.08), M_WINDOW)
        for dx in (-w / 2, w / 2):
            mm.box((x + dx, y, z + sgn * 0.08), (0.22, h + 0.2, 0.22), M_WOOD)
        mm.box((x, y - h / 2, z + sgn * 0.12), (w + 0.5, 0.2, 0.4), M_WOOD)
        mm.box((x, y + h / 2, z + sgn * 0.08), (w + 0.3, 0.25, 0.22), M_WOOD)
        mm.box((x, y, z + sgn * 0.06), (0.1, h, 0.1), M_PROPS, "iron")
        mm.box((x, y + 0.2, z + sgn * 0.06), (w, 0.1, 0.1), M_PROPS, "iron")
    else:
        sgn = -1 if x > 0 else 1
        mm.box((x, y, z), (0.08, h, w), M_WINDOW)
        for dz in (-w / 2, w / 2):
            mm.box((x + sgn * 0.08, y, z + dz), (0.22, h + 0.2, 0.22), M_WOOD)
        mm.box((x + sgn * 0.12, y - h / 2, z), (0.4, 0.2, w + 0.5), M_WOOD)
        mm.box((x + sgn * 0.08, y + h / 2, z), (0.22, 0.25, w + 0.3), M_WOOD)
        mm.box((x + sgn * 0.06, y, z), (0.1, h, 0.1), M_PROPS, "iron")
        mm.box((x + sgn * 0.06, y + 0.2, z), (0.1, 0.1, w), M_PROPS, "iron")
    return mm.finish(parent)

window("Window_North_1", (-6, 8.0, RZ - 0.02), 'z', W["N"])
window("Window_North_2", (5, 8.0, RZ - 0.02), 'z', W["N"])
window("Window_West_1", (-RX + 0.02, 3.2, -6.0), 'x', W["W"])
window("Window_West_2", (-RX + 0.02, 8.0, 4.5), 'x', W["W"])
window("Window_East_1", (RX - 0.02, 8.0, -3.0), 'x', W["E"])
window("Window_South_1", (-10.0, 3.2, -RZ + 0.02), 'z', W["SW"])
window("Window_South_2", (5.0, 8.4, -RZ + 0.02), 'z', W["SE"])

# ---------------------------------------------------------------- fireplace (west wall)
m = Mesh("Fireplace")
fz = -1.0
# Stone hearth with a real firebox (Vili 2026-10-10: the old block had no front face, so the plank wall showed through).
# The flames are a particle effect made at runtime (TavernDressing).
HF = -RX + 1.4                     # hearth front face
OW, OH, OD = 2.4, 2.2, 1.0         # opening width / height / depth
JW = (4.4 - OW) / 2                # jamb width
for s in (-1, 1):                                                           # jambs
    m.box((-RX + 0.7, 1.6, fz + s * (OW / 2 + JW / 2)), (1.4, 3.2, JW), M_STONE)
m.box((-RX + 0.7, (OH + 3.2) / 2, fz), (1.4, 3.2 - OH, OW), M_STONE)        # lintel over the opening
m.box((-RX + (1.4 - OD) / 2, OH / 2, fz), (1.4 - OD, OH, OW), M_STONE)      # back of the firebox
m.box((HF - OD / 2 + 0.35, 0.09, fz), (OD + 0.7, 0.18, 5.0), M_STONE)       # raised hearth stone, in and in front of the box
m.box((HF + 0.1, 3.35, fz), (0.7, 0.3, 5.0), M_STONE)                       # stone mantel shelf
for s in (-1, 1):                                                           # corbels under the mantel ends
    m.box((HF + 0.05, 3.0, fz + s * 1.95), (0.4, 0.4, 0.5), M_STONE)
m.box((-RX + 0.55, 6.5, fz), (1.1, 6.6, 3.0), M_STONE)                      # chimney breast
o_fireplace = m.finish(g_shell)
# Soot lining inside the firebox (no collider), just proud of the stone so the faces never fight
m = Mesh("Fireplace_Opening")
m.box((HF - OD + 0.02, OH / 2, fz), (0.04, OH, OW - 0.04), M_PROPS, "soot")                 # back
for s in (-1, 1):
    m.box((HF - OD / 2, OH / 2, fz + s * (OW / 2 - 0.02)), (OD, OH, 0.04), M_PROPS, "soot")  # sides
m.box((HF - OD / 2, OH - 0.02, fz), (OD, 0.04, OW), M_PROPS, "soot")                        # roof
m.finish(o_fireplace)
# Logs on a bed of embers inside the firebox
m = Mesh("Fire")
m.box((HF - OD / 2, 0.2, fz), (OD - 0.2, 0.05, OW - 0.5), M_GLOW)
for i, dz in enumerate((-0.45, 0.0, 0.45)):
    m.cyl((HF - OD + 0.05, 0.36 + (0.1 if i == 1 else 0.0), fz + dz), 0.13, OD - 0.15, M_PROPS, "wood_dark", seg=6, axis='x')
m.cyl((HF - OD / 2 + 0.1, 0.52, fz - 0.6), 0.11, 1.2, M_PROPS, "wood", seg=6, axis='z')
m.finish(o_fireplace)
empty("Light_Fire", (-RX + 2.2, 1.6, fz), g_lights)
# a cauldron hook and pot by the fire, firewood stack
m = Mesh("Firewood")
for row in range(3):
    for k in range(4 - row):
        m.cyl((-RX + 0.1, 0.2 + row * 0.36, -5.0 + k * 0.38 + row * 0.19), 0.18, 1.0, M_PROPS, "wood", seg=6, axis="x")
m.finish(g_furn)

# ---------------------------------------------------------------- galleries (north and east), posts, braces
def slab(name, x0, x1, z0, z1):
    mm = Mesh(name)
    mm.box(((x0 + x1) / 2, GAL_Y - GAL_T / 2, (z0 + z1) / 2), (x1 - x0, GAL_T, z1 - z0), M_FLOOR)
    return mm

gn = slab("Gallery_North", -RX, RX, GAL_N_Z, RZ)
# edge beam under the north gallery's open side
gn.box((( -RX + GAL_E_X) / 2, GAL_Y - GAL_T - 0.3, GAL_N_Z + 0.25), (RX + GAL_E_X, 0.6, 0.5), M_WOOD)
# railing along the north gallery edge (x -12..7)
def railing(mm, a, b, fixed, along):
    n = int(abs(b - a) / 1.0)
    for i in range(n + 1):
        t = a + (b - a) * i / n
        p = (t, GAL_Y + 0.65, fixed) if along == 'x' else (fixed, GAL_Y + 0.65, t)
        mm.box(p, (0.14, 1.3, 0.14), M_WOOD)
    mid = (a + b) / 2
    if along == 'x':
        mm.box((mid, GAL_Y + 1.35, fixed), (abs(b - a) + 0.2, 0.16, 0.24), M_WOOD)
        mm.box((mid, GAL_Y + 0.7, fixed), (abs(b - a), 0.1, 0.1), M_WOOD)
    else:
        mm.box((fixed, GAL_Y + 1.35, mid), (0.24, 0.16, abs(b - a) + 0.2), M_WOOD)
        mm.box((fixed, GAL_Y + 0.7, mid), (0.1, 0.1, abs(b - a)), M_WOOD)
railing(gn, -RX + 0.2, GAL_E_X, GAL_N_Z + 0.1, 'x')
o_gn = gn.finish(g_shell)

ge = slab("Gallery_East", GAL_E_X, RX, -RZ, GAL_N_Z)
ge.box((GAL_E_X + 0.25, GAL_Y - GAL_T - 0.3, (-RZ + GAL_N_Z) / 2), (0.5, 0.6, RZ + GAL_N_Z), M_WOOD)
railing(ge, STAIR_Z1 + 0.1, GAL_N_Z, GAL_E_X + 0.1, 'z')
o_ge = ge.finish(g_shell)

posts = [(-8.0, GAL_N_Z + 0.25), (-2.0, GAL_N_Z + 0.25), (3.0, GAL_N_Z + 0.25), (GAL_E_X + 0.25, GAL_N_Z + 0.25),
         (GAL_E_X + 0.25, -1.5), (GAL_E_X + 0.25, STAIR_Z1 + 0.1)]
for i, (x, z) in enumerate(posts):
    mm = Mesh("Post_%d" % (i + 1))
    tall = GAL_Y + 1.4
    mm.box((x, tall / 2, z), (0.55, tall, 0.55), M_WOOD)
    mm.box((x, 0.15, z), (0.8, 0.3, 0.8), M_WOOD)
    # knee braces under the gallery beam
    for d in (-1, 1):
        if along_x := (abs(z - (GAL_N_Z + 0.25)) < 0.01 and i != 3):
            bx, bz, rot = x + d * 0.75, z, 0
            mm.box((bx, GAL_Y - 1.4, bz), (1.7, 0.28, 0.28), M_WOOD, rot_y=0)
        else:
            mm.box((x, GAL_Y - 1.4, z + d * 0.75), (0.28, 0.28, 1.7), M_WOOD)
    mm.finish(g_frame)
# big diagonal braces on the east wall upstairs (the timber frame look of the reference; no beams across the open room, the camera looks down through it)
m = Mesh("Wall_Braces")
for (x, z, rx) in [(RX - 0.2, -5.5, 1), (RX - 0.2, 1.5, -1)]:
    # a long diagonal brace on the east wall, upstairs
    ang = math.degrees(math.atan2(3.6, 4.8))
    bm_len = math.hypot(3.6, 4.8)
    cx, cy, cz = x, GAL_Y + 2.6, z
    # approximate: a tilted box built from 8 corner points
    d = Vector((0, 3.6, 4.8 * rx)).normalized()
    side = Vector((1, 0, 0)); up = d.cross(side).normalized()
    hl, hw = bm_len / 2, 0.16
    corners = []
    for a in (-hl, hl):
        for b in (-hw, hw):
            for c2 in (-hw, hw):
                p = Vector((cx, cy, cz)) + d * a + up * b + side * c2
                corners.append(tuple(p))
    q = {"a": (0, 1, 3, 2), "b": (4, 6, 7, 5), "c": (0, 4, 5, 1), "d": (2, 3, 7, 6), "e": (0, 2, 6, 4), "f": (1, 5, 7, 3)}
    for k, idx in q.items():
        m.face([corners[i] for i in idx], M_WOOD, ref_u=(cx, cy, cz))
m.box((RX - 0.2, GAL_Y + 0.4, -2.0), (0.3, 0.4, 13.5), M_WOOD)
m.box((-RX + 0.2, GAL_Y + 0.4, 6.5), (0.3, 0.4, 5.0), M_WOOD)
m.finish(W["E"])

# ---------------------------------------------------------------- stairs (south wall, up to the east gallery)
m = Mesh("Stairs")
run = (STAIR_X1 - STAIR_X0) / STEP_COUNT
rise = GAL_Y / STEP_COUNT
w = STAIR_Z1 - STAIR_Z0
for i in range(STEP_COUNT):
    x = STAIR_X0 + run * (i + 0.5)
    top = rise * (i + 1)
    m.box((x, top - 0.1, (STAIR_Z0 + STAIR_Z1) / 2), (run, 0.2, w), M_FLOOR)            # tread
    m.box((x + run * 0.5 - 0.05, top - rise / 2, (STAIR_Z0 + STAIR_Z1) / 2), (0.1, rise, w), M_WOOD)  # riser
# stringer along the open side + hand rail
L_ = math.hypot(STAIR_X1 - STAIR_X0, GAL_Y)
slope = math.atan2(GAL_Y, STAIR_X1 - STAIR_X0)
for zside, hgt in ((STAIR_Z1 + 0.1, 0.5), ):
    d = Vector((math.cos(slope), math.sin(slope), 0))
    for (off_y, thick) in ((-0.15, hgt), (1.35, 0.14)):
        c = Vector(((STAIR_X0 + STAIR_X1) / 2, GAL_Y / 2 + off_y, zside))
        up = Vector((-math.sin(slope), math.cos(slope), 0))
        side = Vector((0, 0, 1))
        corners = []
        for a in (-L_ / 2, L_ / 2):
            for b in (-thick / 2, thick / 2):
                for c2 in (-0.1, 0.1):
                    corners.append(tuple(c + d * a + up * b + side * c2))
        q = {"a": (0, 1, 3, 2), "b": (4, 6, 7, 5), "c": (0, 4, 5, 1), "d": (2, 3, 7, 6), "e": (0, 2, 6, 4), "f": (1, 5, 7, 3)}
        for k, idx in q.items():
            m.face([corners[i] for i in idx], M_WOOD, ref_u=tuple(c))
for i in range(0, STEP_COUNT, 3):
    x = STAIR_X0 + run * (i + 0.5); top = rise * (i + 1)
    m.box((x, top + 0.7, STAIR_Z1 + 0.1), (0.12, 1.4, 0.12), M_WOOD)
o_stairs = m.finish(g_shell)
# smooth ramp the NavMesh and the hero walk on (collider only, never drawn)
m = Mesh("Stairs_Ramp")
z0, z1 = STAIR_Z0, STAIR_Z1
ramp = [(STAIR_X0 - 0.3, 0.0, z0), (STAIR_X1, GAL_Y, z0), (STAIR_X1, GAL_Y, z1), (STAIR_X0 - 0.3, 0.0, z1)]
m.face(ramp, M_DARK, ref_u=(STAIR_X0 + 2, -5, (z0 + z1) / 2))
# and its back side (the walk surface must face up whatever the importer does with the winding)
m.face(ramp, M_DARK, ref_u=(STAIR_X0 + 2, 15, (z0 + z1) / 2))
o_ramp = m.finish(o_stairs)

# ---------------------------------------------------------------- bar (north-west, under the gallery)
m = Mesh("Bar")
BX0, BX1, BZ = -11.6, -4.6, 1.8
m.box(((BX0 + BX1) / 2, 0.8, BZ), (BX1 - BX0, 1.6, 1.0), M_WOOD)                  # front run
m.box((BX1 - 0.5, 0.8, (BZ + 3.8) / 2 + 0.25), (1.0, 1.6, 3.8 - BZ + 0.5), M_WOOD)   # return towards the wall
m.box(((BX0 + BX1) / 2, 1.66, BZ - 0.05), (BX1 - BX0 + 0.3, 0.12, 1.3), M_FLOOR)    # counter top
m.box((BX1 - 0.5, 1.66, (BZ + 3.8) / 2 + 0.25), (1.3, 0.12, 3.8 - BZ + 0.6), M_FLOOR)
for i in range(7):  # vertical boards on the front
    m.box((BX0 + 0.5 + i, 0.8, BZ - 0.52), (0.08, 1.5, 0.06), M_PROPS, "wood_dark")
# things on the counter
for (x, z) in [(-10.5, 1.8), (-9.2, 1.6), (-6.4, 1.9)]:
    m.cyl((x, 1.72, z), 0.17, 0.42, M_PROPS, "wood_light", seg=8)
    m.cyl((x, 2.14, z), 0.15, 0.08, M_PROPS, "foam", seg=8)
m.cyl((-7.8, 1.72, 1.7), 0.08, 0.5, M_PROPS, "bottle_g", seg=6, r_top=0.05)
m.box((-8.4, 1.75, 1.8), (0.7, 0.06, 0.5), M_PROPS, "cloth")
o_bar = m.finish(g_furn)
empty("Light_Bar", (-8.0, 3.4, 2.8), g_lights)
empty("Spot_Barkeep", (-8.0, 0, 3.2), g_spots)

m = Mesh("Bar_Shelves")
for k, y in enumerate((2.2, 3.2, 4.1)):
    m.box((-8.0, y, RZ - 0.35), (6.5, 0.12, 0.6), M_WOOD)
    for j in range(9):
        x = -10.8 + j * 0.7
        kind = (j + k) % 4
        if kind == 0:
            m.cyl((x, y + 0.06, RZ - 0.35), 0.1, 0.55, M_PROPS, random.choice(["bottle_g", "bottle_r"]), seg=6, r_top=0.05)
        elif kind == 1:
            m.cyl((x, y + 0.06, RZ - 0.35), 0.16, 0.38, M_PROPS, "wood_light", seg=8)
        elif kind == 2:
            m.box((x, y + 0.3, RZ - 0.45), (0.5, 0.5, 0.06), M_PROPS, "plate", rot_y=0)
        else:
            m.cyl((x, y + 0.06, RZ - 0.35), 0.12, 0.3, M_PROPS, "iron", seg=6)
for x in (-11.2, -4.8):
    m.box((x, 3.15, RZ - 0.35), (0.14, 2.2, 0.6), M_WOOD)
m.finish(g_furn)

# kegs on a rack behind the bar (west end)
m = Mesh("Kegs")
m.box((-10.9, 0.5, 6.2), (1.6, 1.0, 3.6), M_WOOD)
for k, z in enumerate((5.0, 6.2, 7.4)):
    m.cyl((-11.6, 1.6, z), 0.55, 1.4, M_PROPS, "wood", seg=10, axis='x')
    for bx in (-11.4, -10.5):
        m.cyl((bx, 1.6, z), 0.58, 0.12, M_PROPS, "iron", seg=10, axis='x')
    m.cyl((-10.2, 1.5, z), 0.06, 0.25, M_PROPS, "brass", seg=6, axis='x')
m.finish(g_furn)

# ---------------------------------------------------------------- tables
def chair(mm, x, z, face_deg):
    r = math.radians(face_deg)
    fx, fz = math.sin(r), math.cos(r)   # chair faces the table along (fx, fz)
    mm.box((x, 0.75, z), (0.8, 0.12, 0.8), M_PROPS, "wood", rot_y=face_deg)
    for dx in (-0.32, 0.32):
        for dz in (-0.32, 0.32):
            mm.box((x + dx, 0.37, z + dz), (0.1, 0.74, 0.1), M_PROPS, "wood_dark")
    bx, bz = x - fx * 0.36, z - fz * 0.36
    mm.box((bx, 1.35, bz), (0.8, 1.1, 0.1), M_PROPS, "wood", rot_y=face_deg)
    mm.box((bx, 1.75, bz), (0.85, 0.14, 0.14), M_PROPS, "wood_dark", rot_y=face_deg)

def tabletop_props(mm, x, z, w, d, food=True):
    mm.cyl((x, 1.36, z), 0.07, 0.35, M_PROPS, "candle", seg=6)
    mm.cyl((x, 1.71, z), 0.05, 0.14, M_GLOW, seg=5, r_top=0.0)
    for (dx, dz) in [(-w * 0.3, -d * 0.22), (w * 0.3, d * 0.22), (w * 0.28, -d * 0.25)]:
        mm.cyl((x + dx, 1.36, z + dz), 0.15, 0.36, M_PROPS, "wood_light", seg=8)
        mm.cyl((x + dx, 1.72, z + dz), 0.14, 0.07, M_PROPS, "foam", seg=8)
    if food:
        mm.cyl((x - w * 0.18, 1.36, z + d * 0.2), 0.36, 0.05, M_PROPS, "plate", seg=10)
        mm.box((x - w * 0.18, 1.48, z + d * 0.2), (0.42, 0.2, 0.26), M_PROPS, "bread", rot_y=25)
        mm.cyl((x + w * 0.05, 1.36, z - d * 0.05), 0.3, 0.05, M_PROPS, "plate", seg=10)
        mm.cyl((x + w * 0.05, 1.41, z - d * 0.05), 0.16, 0.16, M_PROPS, "meat", seg=6, r_top=0.08)
        mm.box((x + w * 0.05 + 0.18, 1.45, z - d * 0.05), (0.26, 0.05, 0.05), M_PROPS, "plate")

def rect_table(name, x, z, rot=0, parent=g_furn, food=True):
    mm = Mesh(name)
    w, d = 3.0, 1.6
    if rot == 90: w, d = d, w
    mm.box((x, 1.28, z), (w, 0.16, d), M_FLOOR)
    for sx in (-1, 1):
        for sz in (-1, 1):
            mm.box((x + sx * (w / 2 - 0.25), 0.6, z + sz * (d / 2 - 0.25)), (0.18, 1.2, 0.18), M_WOOD)
    tabletop_props(mm, x, z, w, d, food)
    if rot == 0:
        for cx in (-0.75, 0.75):
            chair(mm, x + cx, z - d / 2 - 0.55, 0)
            chair(mm, x + cx, z + d / 2 + 0.55, 180)
    else:
        for cz in (-0.75, 0.75):
            chair(mm, x - w / 2 - 0.55, z + cz, 90)
            chair(mm, x + w / 2 + 0.55, z + cz, -90)
    return mm.finish(parent)

def round_table(name, x, z, parent=g_furn, food=True):
    mm = Mesh(name)
    mm.cyl((x, 1.2, z), 1.05, 0.16, M_FLOOR, seg=14)
    mm.cyl((x, 0.0, z), 0.18, 1.2, M_WOOD, seg=8)
    mm.cyl((x, 0.0, z), 0.6, 0.12, M_WOOD, seg=8)
    tabletop_props(mm, x, z, 1.3, 1.3, food)
    for k in range(4):
        a = k * 90 + 45
        r = math.radians(a)
        chair(mm, x + math.sin(r) * 1.55, z + math.cos(r) * 1.55, a + 180)
    return mm.finish(parent)

rect_table("Table_1", -8.2, -6.0)
round_table("Table_2", -2.6, -1.4)
rect_table("Table_3", 2.6, -3.2)
rect_table("Table_4", -7.8, -1.6, food=False)
round_table("Table_5", 2.8, 1.6)
rect_table("Table_6", 9.6, -3.5, rot=90)
rect_table("Table_7", 9.6, 1.4, rot=90, food=False)
# upstairs tables sit against the north wall so the gallery keeps a walkway along its railing
for _o in (rect_table("Table_Upper_1", -4.0, 7.5, rot=90, food=False), rect_table("Table_Upper_2", 3.0, 7.5, rot=90)):
    _o.location.z += GAL_Y   # Blender z is Unity y

# candle stands (tall candelabras) and wall sconces
def candelabra(name, x, z, parent=g_furn):
    mm = Mesh(name)
    mm.cyl((x, 0, z), 0.4, 0.12, M_PROPS, "iron", seg=8)
    mm.cyl((x, 0, z), 0.06, 2.6, M_PROPS, "iron", seg=6)
    mm.box((x, 2.6, z), (1.1, 0.06, 0.06), M_PROPS, "iron")
    for dx in (-0.5, 0, 0.5):
        mm.cyl((x + dx, 2.6, z), 0.06, 0.4 if dx else 0.55, M_PROPS, "candle", seg=6)
        top = 3.0 if dx else 3.15
        mm.cyl((x + dx, top, z), 0.05, 0.16, M_GLOW, seg=5, r_top=0.0)
    return mm.finish(parent)

candelabra("Candelabra_1", -3.2, -6.9)
candelabra("Candelabra_2", 5.6, -0.4)
empty("Light_Hall_South", (-1.5, 3.2, -5.0), g_lights)
empty("Light_Hall_East", (5.0, 3.2, 0.5), g_lights)

def sconce(name, pos, facing, parent):  # facing: unit (x, z) pointing into the room
    mm = Mesh(name)
    x, y, z = pos
    fx, fz = facing
    mm.box((x, y, z), (0.3 if fx == 0 else 0.1, 0.5, 0.3 if fz == 0 else 0.1), M_PROPS, "iron")
    mm.box((x + fx * 0.25, y - 0.1, z + fz * 0.25), (0.08 if fx == 0 else 0.5, 0.08, 0.08 if fz == 0 else 0.5), M_PROPS, "iron")
    mm.cyl((x + fx * 0.45, y - 0.1, z + fz * 0.45), 0.12, 0.08, M_PROPS, "brass", seg=6)
    mm.cyl((x + fx * 0.45, y - 0.02, z + fz * 0.45), 0.06, 0.3, M_PROPS, "candle", seg=6)
    mm.cyl((x + fx * 0.45, y + 0.28, z + fz * 0.45), 0.05, 0.16, M_GLOW, seg=5, r_top=0.0)
    return mm.finish(parent)

sconce("Sconce_West", (-RX + 0.1, 3.6, -5.2), (1, 0), W["W"])
sconce("Sconce_South", (-1.0, 3.6, -RZ + 0.1), (0, 1), W["SE"])
sconce("Sconce_East", (RX - 0.1, 3.6, -1.0), (-1, 0), W["E"])
sconce("Sconce_Upper_North", (-1.0, GAL_Y + 2.4, RZ - 0.1), (0, -1), W["N"])
sconce("Sconce_Upper_East", (RX - 0.1, GAL_Y + 2.4, 2.5), (-1, 0), W["E"])
empty("Light_Upper", (2.0, GAL_Y + 3.0, 6.0), g_lights)
empty("Light_Upper_East", (10.0, GAL_Y + 3.0, -4.0), g_lights)

# ---------------------------------------------------------------- gallery storage, bookshelf, extras
m = Mesh("Bookshelf")
bx = RX - 0.35
m.box((bx, 1.6, 4.6), (0.6, 3.2, 2.4), M_WOOD, skip=("-x",))
for k, y in enumerate((0.5, 1.4, 2.3)):
    m.box((bx - 0.05, y, 4.6), (0.5, 0.08, 2.2), M_WOOD)
    zc = 3.65
    while zc < 5.5:
        t = random.uniform(0.12, 0.22)
        h = random.uniform(0.5, 0.75)
        m.box((bx - 0.08, y + 0.04 + h / 2, zc + t / 2), (0.42, h, t), M_PROPS, random.choice(["book_b", "book_r", "book_g", "wood_dark"]))
        zc += t + 0.02
m.finish(g_furn)

m = Mesh("Hanging_Herbs")
for i, x in enumerate((-10.5, -9.6, -8.7, -7.8, -6.9, -6.0)):
    m.box((x, GAL_Y - GAL_T - 0.6, GAL_N_Z + 0.9), (0.04, 1.0, 0.04), M_PROPS, "rope")
    m.box((x, GAL_Y - GAL_T - 1.25, GAL_N_Z + 0.9), (0.3, 0.5, 0.3), M_PROPS, "herb" if i % 2 else "cheese", rot_y=20 * i)
m.finish(g_furn)

# spots for things placed in Unity: arrival, waitress route, prefab barrels/chests/crates
empty("Spot_Arrival", (DOOR_X, 0, -RZ + 2.4), g_spots)
empty("Spot_Door", (DOOR_X, 0, -RZ - 0.05), g_spots)
for i, p in enumerate([(-4.8, 0, -3.6), (0.4, 0, -0.6), (5.6, 0, -2.2), (6.6, 0, 2.6), (-5.6, 0, 0.6)]):
    empty("Spot_Waitress_%d" % (i + 1), p, g_spots)
for i, p in enumerate([(-11.0, GAL_Y, 8.2), (-9.8, GAL_Y, 8.3), (-11.1, GAL_Y, 7.0), (-10.5, GAL_Y + 1.25, 7.8),
                       (8.4, GAL_Y, 8.2), (11.2, 0, -6.0), (11.2, 0, -7.6), (-3.2, 0, 8.3)]):
    empty("Spot_Barrel_%d" % (i + 1), p, g_spots)
for i, p in enumerate([(-7.8, GAL_Y, 8.2), (-6.0, GAL_Y, 8.4), (11.0, GAL_Y, 6.8)]):
    empty("Spot_Chest_%d" % (i + 1), p, g_spots)
for i, p in enumerate([(9.6, GAL_Y, 8.3), (10.8, GAL_Y, -8.2), (-1.8, 0, 8.4), (11.2, 0, 7.6)]):
    empty("Spot_Crate_%d" % (i + 1), p, g_spots)

# ---------------------------------------------------------------- save + export
os.makedirs(os.path.dirname(BLEND), exist_ok=True)
pal_img.filepath = PALETTE
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={'MESH', 'EMPTY'},
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                         mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False, bake_anim=False)
nf = sum(len(o.data.polygons) for o in bpy.data.objects if o.type == 'MESH')
print("TAVERN OK objects=%d faces=%d" % (len(bpy.data.objects), nf))
