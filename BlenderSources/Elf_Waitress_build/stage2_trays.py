# Stage 2: two low-poly serving trays (bread, beer tankards) on one 8x8 palette texture.
import bpy, bmesh, math, random
from mathutils import Vector, Matrix
D = "C:/Users/Omistaja/AppData/Local/Temp/claude/D--Unity-3D-DnD-selainpeli/159abac1-df60-4df2-81c6-701ea24cf69b/scratchpad/elf/"
random.seed(7)

PAL = {  # name: (cell index, rgb 0-255) -- colours picked from Vili's comic panel
    "tray": (0, (62, 60, 62)), "tray_rim": (1, (40, 39, 42)), "bun": (2, (206, 140, 64)),
    "bun_top": (3, (186, 112, 46)), "lettuce": (4, (112, 170, 58)), "patty": (5, (96, 56, 34)),
    "cheese": (6, (240, 196, 70)), "mug": (7, (98, 58, 32)), "mug_dark": (8, (70, 40, 22)),
    "band": (9, (44, 36, 30)), "foam": (10, (244, 232, 196)), "beer": (11, (214, 140, 34)),
    "sesame": (12, (246, 236, 200)), "tomato": (13, (204, 58, 42)), "splash": (14, (236, 160, 44)),
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

R_TRAY = 0.09       # character is 1.0 unit tall, so this is a ~30 cm tray on a 1.65 m woman
def tray(p):
    p.add(cyl(R_TRAY, R_TRAY, 0.005, 20), "tray", T(z=0.0025))
    p.add(cyl(R_TRAY + 0.004, R_TRAY + 0.006, 0.009, 20), "tray_rim", T(z=0.0045))
    p.add(cyl(R_TRAY - 0.004, R_TRAY - 0.004, 0.003, 20), "tray", T(z=0.0075))

def burger(p, x, y, z, s=1.0, rot=0):
    r = 0.021 * s
    p.add(cyl(r, r * 1.02, 0.007 * s, 10), "bun", T(x, y, z + 0.0035 * s))
    p.add(cyl(r * 1.08, r * 1.08, 0.0025 * s, 10), "lettuce", T(x, y, z + 0.0085 * s, rz=rot + 9))
    p.add(cyl(r * 1.02, r * 1.02, 0.006 * s, 10), "patty", T(x, y, z + 0.0125 * s))
    p.add(box(r * 1.6, r * 1.6, 0.0018 * s), "cheese", T(x, y, z + 0.016 * s, rz=rot + 45))
    p.add(cyl(r * 0.9, r * 0.9, 0.002 * s, 8), "tomato", T(x, y, z + 0.0178 * s))
    p.add(ico(r * 1.02, 1), "bun_top", T(x, y, z + 0.0185 * s, s=(1, 1, 0.62)))
    for k in range(3):  # sesame seeds
        a = math.radians(rot + k * 120 + 30)
        p.add(box(0.003, 0.0018, 0.0015), "sesame", T(x + r * 0.45 * math.cos(a), y + r * 0.45 * math.sin(a), z + 0.0185 * s + r * 0.55, rz=rot + k * 60))

def sub(p, x, y, z, rot):  # long sandwich like the one in the panel
    p.add(box(0.075, 0.022, 0.008), "bun", T(x, y, z + 0.004, rz=rot))
    p.add(box(0.079, 0.026, 0.003), "lettuce", T(x, y, z + 0.0095, rz=rot))
    p.add(box(0.074, 0.021, 0.004), "tomato", T(x, y, z + 0.0125, rz=rot))
    p.add(box(0.072, 0.022, 0.009), "bun_top", T(x, y, z + 0.019, rz=rot, s=(1, 1, 1)))

# ---- bread tray: burgers and a long sandwich ----
pb = Part(); tray(pb)
base = 0.009
sub(pb, 0.0, 0.040, base, 6)
for k, (x, y) in enumerate([(-0.045, -0.012), (0.0, -0.02), (0.045, -0.012), (-0.022, -0.055), (0.024, -0.055)]):
    burger(pb, x, y, base, 1.0 if k < 3 else 0.9, rot=k * 37)
tray_bread = pb.finish("Tray_Bread")

# ---- beer tray: four dark barrel tankards, foam spilling over, one splashing ----
pe = Part(); tray(pe)
mugs = [(-0.034, 0.030), (0.034, 0.030), (-0.034, -0.034), (0.034, -0.034)]
for k, (x, y) in enumerate(mugs):
    h = 0.078
    z0 = 0.009
    out = math.atan2(y, x)
    pe.add(cyl(0.024, 0.024, h, 12), "mug", T(x, y, z0 + h / 2))
    pe.add(cyl(0.0255, 0.0255, h * 0.5, 12), "mug", T(x, y, z0 + h / 2))  # barrel belly
    for bz in (0.010, h - 0.012):
        pe.add(cyl(0.0262, 0.0262, 0.006, 12), "band", T(x, y, z0 + bz))
    for st in range(6):  # dark stave lines
        a = st * math.pi / 3 + 0.2
        pe.add(box(0.002, 0.002, h * 0.86), "mug_dark", T(x + 0.0258 * math.cos(a), y + 0.0258 * math.sin(a), z0 + h / 2, rz=math.degrees(a)))
    pe.add(cyl(0.021, 0.021, 0.002, 12), "beer", T(x, y, z0 + h + 0.0005))
    for fb in range(5):  # foam crown
        fa = fb * 72 + random.uniform(0, 30)
        pe.add(ico(0.0145, 1), "foam", T(x + 0.011 * math.cos(math.radians(fa)), y + 0.011 * math.sin(math.radians(fa)), z0 + h + 0.007))
    pe.add(ico(0.013, 1), "foam", T(x, y, z0 + h + 0.014))
    for dr in range(2):  # drips running down the outside
        da = out + (dr - 0.5) * 0.9
        pe.add(ico(0.0075, 1), "foam", T(x + 0.024 * math.cos(da), y + 0.024 * math.sin(da), z0 + h - 0.012 - dr * 0.008, s=(0.8, 0.8, 2.2)))
    # handle on the outer side
    hx, hy = x + 0.032 * math.cos(out), y + 0.032 * math.sin(out)
    pe.add(box(0.008, 0.009, 0.048), "mug_dark", T(hx, hy, z0 + h / 2, rz=math.degrees(out)))
    for hz in (h / 2 - 0.021, h / 2 + 0.021):
        pe.add(box(0.012, 0.008, 0.007), "mug_dark", T(x + 0.028 * math.cos(out), y + 0.028 * math.sin(out), z0 + hz, rz=math.degrees(out)))
    if k == 1:  # the splash flying off one tankard, as in the panel
        for sp in range(4):
            pe.add(ico(0.0045 + 0.0015 * (sp % 2), 1), "splash", T(x + 0.03 + sp * 0.009, y + 0.012 + sp * 0.004, z0 + h + 0.010 + sp * 0.006))
tray_beer = pe.finish("Tray_Beer")

tray_bread.location = (-0.25, 0, 0); tray_beer.location = (0.25, 0, 0)
print("TRAYS", len(tray_bread.data.polygons), len(tray_beer.data.polygons))
bpy.ops.wm.save_as_mainfile(filepath=D + "stage2.blend")
