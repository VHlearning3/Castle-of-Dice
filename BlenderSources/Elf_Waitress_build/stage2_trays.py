# Stage 2: two low-poly serving trays (bread, beer tankards) on one 8x8 palette texture.
import bpy, bmesh, math, random
from mathutils import Vector, Matrix
D = "C:/Users/Omistaja/AppData/Local/Temp/claude/D--Unity-3D-DnD-selainpeli/159abac1-df60-4df2-81c6-701ea24cf69b/scratchpad/elf/"
random.seed(7)

PAL = {  # name: (cell index, rgb 0-255)
    "tray": (0, (122, 78, 44)), "tray_rim": (1, (84, 52, 30)), "crust": (2, (196, 128, 58)),
    "crumb": (3, (238, 214, 160)), "lettuce": (4, (104, 160, 60)), "ham": (5, (214, 120, 118)),
    "cheese": (6, (236, 192, 72)), "mug": (7, (168, 112, 62)), "mug_dark": (8, (120, 78, 42)),
    "band": (9, (70, 66, 64)), "foam": (10, (246, 240, 222)), "beer": (11, (200, 132, 36)),
    "napkin": (12, (226, 226, 220)), "tomato": (13, (200, 52, 40)),
}
N = 8
img = bpy.data.images.new("Elf_Trays_Palette", N * 4, N * 4, alpha=False)
px = [0.0] * (N * 4 * N * 4 * 4)
def srgb2lin(c):
    c /= 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
for name, (idx, rgb) in PAL.items():
    cx, cy = idx % N, idx // N
    for y in range(cy * 4, cy * 4 + 4):
        for x in range(cx * 4, cx * 4 + 4):
            i = (y * N * 4 + x) * 4
            px[i:i + 4] = [rgb[0] / 255, rgb[1] / 255, rgb[2] / 255, 1.0]
img.pixels = px
img.filepath_raw = D + "Elf_Trays_Palette.png"; img.file_format = 'PNG'; img.save()
img.filepath = D + "Elf_Trays_Palette.png"; img.source = 'FILE'; img.reload()

mat = bpy.data.materials.new("M_Elf_Trays"); mat.use_nodes = True
nt = mat.node_tree; bsdf = nt.nodes["Principled BSDF"]
t = nt.nodes.new("ShaderNodeTexImage"); t.image = img; t.interpolation = 'Closest'
nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value = 0.75

def cell_uv(name):
    idx = PAL[name][0]
    return Vector(((idx % N + 0.5) / N, (idx // N + 0.5) / N))

class Part:
    def __init__(self):
        self.bm = bmesh.new(); self.uv = self.bm.loops.layers.uv.new("UVMap")
    def add(self, geom_fn, color, matrix=Matrix()):
        tmp = bmesh.new(); geom_fn(tmp)
        bmesh.ops.transform(tmp, matrix=matrix, verts=tmp.verts)
        me = bpy.data.meshes.new("tmp"); tmp.to_mesh(me); tmp.free()
        before = set(self.bm.faces)
        self.bm.from_mesh(me); bpy.data.meshes.remove(me)
        uvl = self.bm.loops.layers.uv["UVMap"] if "UVMap" in self.bm.loops.layers.uv else self.uv
        c = cell_uv(color)
        for f in self.bm.faces:
            if f in before: continue
            for l in f.loops: l[uvl].uv = c
    def finish(self, name):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(name); self.bm.to_mesh(me); self.bm.free()
        for p in me.polygons: p.use_smooth = False
        ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
        me.materials.append(mat)
        return ob

def cyl(r1, r2, depth, seg=12):
    return lambda bm: bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=depth)
def box(sx, sy, sz):
    def f(bm):
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=bm.verts)
    return f
def wedge(w, d, h):  # triangular sandwich half, lying flat
    def f(bm):
        pts = [(-w / 2, -d / 2), (w / 2, -d / 2), (0, d / 2)]
        bot = [bm.verts.new((x, y, 0)) for x, y in pts]
        top = [bm.verts.new((x, y, h)) for x, y in pts]
        bm.faces.new(bot[::-1]); bm.faces.new(top)
        for i in range(3):
            j = (i + 1) % 3
            bm.faces.new((bot[i], bot[j], top[j], top[i]))
    return f
def ico(r, sub=1):
    return lambda bm: bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)
def T(x=0, y=0, z=0, rz=0, rx=0, ry=0, s=(1, 1, 1)):
    return (Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(rz), 4, 'Z') @
            Matrix.Rotation(math.radians(ry), 4, 'Y') @ Matrix.Rotation(math.radians(rx), 4, 'X') @
            Matrix.Diagonal((*s, 1)))

R_TRAY = 0.085      # character is 1.0 unit tall, so this is a ~28 cm tray on a 1.65 m woman
def tray(p):
    p.add(cyl(R_TRAY, R_TRAY, 0.006, 20), "tray", T(z=0.003))
    p.add(cyl(R_TRAY + 0.004, R_TRAY + 0.006, 0.010, 20), "tray_rim", T(z=0.005, s=(1, 1, 1)))
    p.add(cyl(R_TRAY - 0.003, R_TRAY - 0.003, 0.004, 20), "tray", T(z=0.009))  # inner floor above rim base

# ---- bread tray ----
pb = Part(); tray(pb)
pb.add(cyl(0.05, 0.05, 0.002, 10), "napkin", T(z=0.012, rz=15))
for k in range(5):  # sandwiches: two bread slices + filling, arranged in a ring
    a = k * 72 + random.uniform(-8, 8)
    x, y = 0.045 * math.cos(math.radians(a)), 0.045 * math.sin(math.radians(a))
    base = 0.013
    rot = a + 90
    pb.add(wedge(0.042, 0.036, 0.007), "crust", T(x, y, base, rz=rot))
    pb.add(wedge(0.044, 0.038, 0.004), random.choice(["lettuce", "ham", "cheese"]), T(x, y, base + 0.007, rz=rot))
    pb.add(wedge(0.042, 0.036, 0.007), "crust", T(x, y, base + 0.011, rz=rot))
pb.add(ico(0.022, 1), "crust", T(0, 0, 0.026, s=(1.3, 0.9, 0.7)))   # small loaf in the middle
pb.add(box(0.03, 0.004, 0.003), "crumb", T(0.0, 0.0, 0.040, rz=30))  # score on the loaf
tray_bread = pb.finish("Tray_Bread")

# ---- beer tray: three barrel tankards with spilling foam ----
pe = Part(); tray(pe)
for k in range(3):
    a = k * 120 + 90
    x, y = 0.042 * math.cos(math.radians(a)), 0.042 * math.sin(math.radians(a))
    h = 0.072
    pe.add(cyl(0.025, 0.025, h, 10), "mug", T(x, y, 0.011 + h / 2))
    for bz in (0.012, h - 0.010):
        pe.add(cyl(0.0265, 0.0265, 0.006, 10), "band", T(x, y, 0.011 + bz))
    pe.add(cyl(0.0215, 0.0215, 0.002, 10), "beer", T(x, y, 0.011 + h + 0.0005))
    for f in range(4):  # foam blobs over the rim
        fa = f * 90 + random.uniform(0, 40)
        pe.add(ico(0.014, 1), "foam", T(x + 0.010 * math.cos(math.radians(fa)), y + 0.010 * math.sin(math.radians(fa)), 0.011 + h + 0.006))
    pe.add(ico(0.008, 1), "foam", T(x + 0.022 * math.cos(math.radians(a)), y + 0.022 * math.sin(math.radians(a)), 0.011 + h - 0.008, s=(0.8, 0.8, 1.6)))
    # handle facing outward
    ha = math.radians(a)
    hx, hy = x + 0.031 * math.cos(ha), y + 0.031 * math.sin(ha)
    pe.add(box(0.008, 0.010, 0.046), "mug_dark", T(hx + 0.004 * math.cos(ha), hy + 0.004 * math.sin(ha), 0.011 + h / 2, rz=a))
    for hz in (h / 2 - 0.02, h / 2 + 0.02):
        pe.add(box(0.012, 0.008, 0.007), "mug_dark", T(x + 0.029 * math.cos(ha), y + 0.029 * math.sin(ha), 0.011 + hz, rz=a))
tray_beer = pe.finish("Tray_Beer")

tray_bread.location = (-0.25, 0, 0); tray_beer.location = (0.25, 0, 0)
print("TRAYS", len(tray_bread.data.polygons), len(tray_beer.data.polygons))
bpy.ops.wm.save_as_mainfile(filepath=D + "stage2.blend")
