# Small low-poly mesh toolkit shared by the Castle of Dice Blender build scripts (vault, ghost).
# Every face is oriented away from a reference point inside its solid, so Unity's back-face culling is safe
# without relying on recalc_face_normals (faces here never share vertices).
import bpy, bmesh, math, os
from mathutils import Vector, Matrix


def mat(name, rgb, rough=0.85, metal=0.0, alpha=1.0, emit=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Alpha"].default_value = alpha
    if emit is not None:
        bsdf.inputs["Emission Color"].default_value = (*emit, 1)
        bsdf.inputs["Emission Strength"].default_value = 1.0
    m.diffuse_color = (*rgb, alpha)
    return m


def centroid(pts):
    n = len(pts)
    return tuple(sum(p[i] for p in pts) / n for i in range(len(pts[0])))


class MeshBuilder:
    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []

    def mi(self, m):
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def face(self, pts, m, ref=None):
        f = self.bm.faces.new([self.bm.verts.new(tuple(p)) for p in pts])
        f.material_index = self.mi(m)
        if ref is not None:
            f.normal_update()
            if f.normal.dot(f.calc_center_median() - Vector(ref)) < 0:
                f.normal_flip()
        return f

    def solid(self, bottom, top, m, ref=None):
        """Joins two rings of 3D points (same count) into a closed solid with caps."""
        n = len(bottom)
        if ref is None:
            ref = centroid(list(bottom) + list(top))
        self.face(bottom, m, ref)
        self.face(top, m, ref)
        for i in range(n):
            j = (i + 1) % n
            self.face((bottom[i], bottom[j], top[j], top[i]), m, ref)

    def loft(self, rings, m, cap_start=True, cap_end=True):
        """Tube through a list of rings (lists of 3D points, same count); faces point away from the ring axis."""
        n = len(rings[0])
        cs = [centroid(r) for r in rings]
        for k in range(len(rings) - 1):
            a, b = rings[k], rings[k + 1]
            ref = centroid([cs[k], cs[k + 1]])
            for i in range(n):
                j = (i + 1) % n
                pts = [a[i], a[j], b[j], b[i]]
                # Collapse degenerate quads (a ring shrunk to a point) into triangles
                uniq = []
                for p in pts:
                    if all((Vector(p) - Vector(q)).length > 1e-5 for q in uniq):
                        uniq.append(p)
                if len(uniq) >= 3:
                    self.face(uniq, m, ref)
        if cap_start and len(set(map(tuple, rings[0]))) > 2:
            self.face(rings[0], m, tuple(Vector(cs[1])))
        if cap_end and len(set(map(tuple, rings[-1]))) > 2:
            self.face(rings[-1], m, tuple(Vector(cs[-2])))

    def prism_xz(self, poly, y0, y1, m):
        self.solid([(x, y0, z) for x, z in poly], [(x, y1, z) for x, z in poly], m)

    def prism_xy(self, poly, z0, z1, m):
        self.solid([(x, y, z0) for x, y in poly], [(x, y, z1) for x, y in poly], m)

    def box(self, x0, x1, y0, y1, z0, z1, m):
        self.prism_xy([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], z0, z1, m)

    def obox(self, center, size, m, rot=(0, 0, 0)):
        """Box of size (sx, sy, sz) turned by Euler XYZ degrees about its centre."""
        R = Matrix.Rotation(math.radians(rot[2]), 3, "Z") @ Matrix.Rotation(math.radians(rot[1]), 3, "Y") @ Matrix.Rotation(math.radians(rot[0]), 3, "X")
        c = Vector(center)
        hx, hy, hz = (s * 0.5 for s in size)
        pts = lambda z: [tuple(c + R @ Vector(v)) for v in ((-hx, -hy, z), (hx, -hy, z), (hx, hy, z), (-hx, hy, z))]
        self.solid(pts(-hz), pts(hz), m, ref=tuple(c))

    def limb(self, p0, p1, r0, r1, m, sides=6, roll=0.0):
        """Tapered prism from p0 (radius r0) to p1 (radius r1)."""
        a, b = Vector(p0), Vector(p1)
        axis = (b - a).normalized()
        side = axis.orthogonal().normalized()
        up = axis.cross(side)
        ring = lambda c, r: [tuple(c + (side * math.cos(roll + 2 * math.pi * i / sides) + up * math.sin(roll + 2 * math.pi * i / sides)) * r) for i in range(sides)]
        self.solid(ring(a, r0), ring(b, r1), m, ref=tuple((a + b) * 0.5))

    def cyl(self, center, r, z0, z1, m, sides=12, rot=0.0):
        cx, cy = center
        poly = [(cx + r * math.cos(rot + 2 * math.pi * i / sides), cy + r * math.sin(rot + 2 * math.pi * i / sides)) for i in range(sides)]
        self.prism_xy(poly, z0, z1, m)

    def finish(self, name, parent=None, origin=(0, 0, 0), uv_scale=0.25):
        bm = self.bm
        bm.normal_update()
        uv = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            n = f.normal
            ax = max(range(3), key=lambda k: abs(n[k]))
            for loop in f.loops:
                co = loop.vert.co
                u, v = {0: (co.y, co.z), 1: (co.x, co.z), 2: (co.x, co.y)}[ax]
                loop[uv].uv = (u * uv_scale, v * uv_scale)
        # Move the origin (e.g. a door's hinge) without moving the geometry
        o = Vector(origin)
        for v in bm.verts:
            v.co -= o
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        for m in self.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        if parent is not None:
            ob.parent = parent
            ob.location = o - parent.matrix_world.translation
        else:
            ob.location = o
        for p in me.polygons:
            p.use_smooth = False
        return ob


def empty(name, parent, loc):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.3
    bpy.context.scene.collection.objects.link(e)
    e.parent = parent
    e.location = loc
    return e


def clear_scene():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)


def export_fbx(root, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for ch in root.children_recursive:
        ch.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
                             object_types={"EMPTY", "MESH"}, use_mesh_modifiers=True, add_leaf_bones=False,
                             mesh_smooth_type="FACE")
