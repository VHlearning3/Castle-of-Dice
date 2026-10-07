# The ghost of Sir Aldric, the Queen's treasury guard (Castle of Dice), as a low-poly spectral knight.
# Run: blender -b --factory-startup --python AldricGhost_build.py
# Z up, the ghost faces +Y ("Front" empty). Origin at floor level; he floats with no legs: his body
# trails off into a wisp that Unity sways (GhostFloat) together with his cloak.
#
#   Ghost_Body    great helm with a flowing crest, breastplate, layered pauldrons, arms, tattered tabard
#   Ghost_Sword   longsword planted point-down in front of him, both hands on the pommel
#   Ghost_Eyes    two glowing eye slits
#   Ghost_Chains  broken shackles on his wrists (bound to his post)
#   Ghost_Cloak   tattered cloak, origin at the shoulders
#   Ghost_Tail_1..3  the wisp below the tabard, each with its origin at its top
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector
from lowpoly_mesh import MeshBuilder, mat, empty, clear_scene, export_fbx

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "Assets", "Models", "Ghost", "Aldric_Ghost.fbx")

M_SPIRIT = mat("Ghost_Spirit", (0.45, 0.78, 1.0), 0.3, alpha=0.45)
M_ARMOR = mat("Ghost_Armor", (0.68, 0.9, 1.0), 0.2, 0.3, alpha=0.6)
M_STEEL = mat("Ghost_Steel", (0.85, 0.97, 1.0), 0.1, 0.5, alpha=0.75)
M_EYES = mat("Ghost_Eyes", (0.9, 1.0, 1.0), emit=(0.8, 1.0, 1.0))
M_CHAIN = mat("Ghost_Chain", (0.25, 0.42, 0.52), 0.5, 0.6, alpha=0.8)


def ring(cx, cy, z, rx, ry, sides=8, rot=0.0, wobble=0.0, seed=0):
    return [(cx + rx * math.cos(rot + 2 * math.pi * i / sides) * (1 + wobble * math.sin(i * 2.7 + seed)),
             cy + ry * math.sin(rot + 2 * math.pi * i / sides) * (1 + wobble * math.cos(i * 1.9 + seed)), z) for i in range(sides)]


def build_body(root):
    mb = MeshBuilder()
    # Breastplate and back: waist to shoulders, a strong chest
    torso = [ring(0, 0, 1.22, 0.27, 0.2), ring(0, 0.02, 1.5, 0.33, 0.24), ring(0, 0.04, 1.8, 0.42, 0.29),
             ring(0, 0.02, 1.98, 0.36, 0.25), ring(0, 0, 2.06, 0.16, 0.14)]
    mb.loft(torso, M_ARMOR)
    # Ridge down the breastplate and a belt with a buckle
    mb.limb((0, 0.31, 1.82), (0, 0.24, 1.33), 0.035, 0.03, M_STEEL, sides=4)
    mb.loft([ring(0, 0, 1.24, 0.3, 0.23, 10), ring(0, 0, 1.34, 0.31, 0.24, 10)], M_STEEL)
    mb.obox((0, 0.245, 1.29), (0.14, 0.04, 0.12), M_STEEL)

    # Tattered tabard: flares from the belt to a hem of ragged points
    sides = 12
    top = ring(0, 0, 1.3, 0.3, 0.23, sides)
    mid = ring(0, 0, 1.05, 0.42, 0.33, sides)
    hem = []
    for i in range(sides):
        a = 2 * math.pi * i / sides
        drop = 0.78 if i % 2 == 0 else 0.92
        hem.append((0.5 * math.cos(a), 0.4 * math.sin(a), drop - 0.05 * math.sin(i * 3.1)))
    mb.loft([top, mid, hem], M_SPIRIT, cap_end=False)

    # Layered pauldrons: two overlapping domes per shoulder
    for s in (-1, 1):
        for k, (dz, r) in enumerate(((0.0, 0.25), (-0.11, 0.22))):
            cx = s * (0.44 + 0.04 * k)
            z0 = 1.86 + dz
            dome = [ring(cx, 0, z0 - 0.06, r, r * 0.9, 8), ring(cx, 0, z0 + 0.06, r * 0.92, r * 0.82, 8),
                    ring(cx - s * 0.03, 0, z0 + 0.14, r * 0.55, r * 0.5, 8), ring(cx - s * 0.05, 0, z0 + 0.17, 0.02, 0.02, 8)]
            mb.loft(dome, M_ARMOR, cap_end=False)

    # Great helm: flat-topped barrel with a cross visor, crest flowing back like smoke
    helm = [ring(0, 0, 2.04, 0.15, 0.15), ring(0, 0, 2.12, 0.19, 0.2), ring(0, 0, 2.43, 0.19, 0.2),
            ring(0, -0.01, 2.5, 0.15, 0.16), ring(0, -0.01, 2.53, 0.06, 0.06)]
    mb.loft(helm, M_ARMOR)
    mb.obox((0, 0.2, 2.29), (0.05, 0.03, 0.3), M_STEEL)          # vertical visor bar
    mb.obox((0, 0.2, 2.31), (0.34, 0.025, 0.05), M_STEEL)        # brow band
    crest = [((0, 0.12, 2.55), 0.06), ((0, -0.05, 2.68), 0.07), ((0, -0.25, 2.62), 0.06), ((0, -0.42, 2.45), 0.05), ((0, -0.55, 2.22), 0.03), ((0, -0.62, 2.0), 0.005)]
    for (p0, r0), (p1, r1) in zip(crest, crest[1:]):
        mb.limb(p0, p1, r0, r1, M_SPIRIT, sides=5)

    # Arms reach down and in to rest both hands on the sword's pommel
    hand = (0, 0.42, 1.5)
    for s in (-1, 1):
        sh = (s * 0.45, 0.0, 1.86)
        el = (s * 0.42, 0.2, 1.52)
        hd = (s * 0.09, hand[1], hand[2])
        mb.limb(sh, el, 0.1, 0.085, M_SPIRIT, sides=6)
        mb.limb(el, hd, 0.085, 0.07, M_SPIRIT, sides=6)
        mb.obox((s * 0.4, 0.17, 1.55), (0.2, 0.2, 0.12), M_ARMOR, rot=(20, 0, s * 10))   # couter
        mb.obox((hd[0] + s * 0.02, hd[1], hd[2] + 0.02), (0.13, 0.15, 0.12), M_ARMOR, rot=(0, 0, s * 15))  # gauntlet
    return mb.finish("Ghost_Body", root)


def build_sword(root):
    mb = MeshBuilder()
    x, y = 0, 0.46
    mb.limb((x, y, 1.62), (x, y, 1.48), 0.05, 0.05, M_STEEL, sides=6)           # pommel
    mb.limb((x, y, 1.48), (x, y, 1.3), 0.03, 0.03, M_STEEL, sides=6)            # grip under the hands
    mb.obox((x, y, 1.28), (0.5, 0.06, 0.06), M_STEEL)                           # crossguard
    for s in (-1, 1):
        mb.limb((s * 0.25, y, 1.28), (s * 0.31, y, 1.33), 0.035, 0.015, M_STEEL, sides=5)
    mb.limb((x, y, 1.25), (x, y + 0.05, 0.42), 0.075, 0.015, M_STEEL, sides=4, roll=0.0)   # blade, diamond section
    return mb.finish("Ghost_Sword", root)


def build_eyes(root):
    mb = MeshBuilder()
    for s in (-1, 1):
        mb.obox((s * 0.08, 0.205, 2.33), (0.1, 0.04, 0.04), M_EYES)
    return mb.finish("Ghost_Eyes", root)


def link(mb, center, axis_rot, m):
    """One chain link: four thin bars in a loop."""
    from mathutils import Matrix
    R = Matrix.Rotation(math.radians(axis_rot[2]), 3, "Z") @ Matrix.Rotation(math.radians(axis_rot[0]), 3, "X")
    c = Vector(center)
    w, h = 0.045, 0.075
    pts = [Vector((-w, 0, -h)), Vector((w, 0, -h)), Vector((w, 0, h)), Vector((-w, 0, h))]
    pts = [c + R @ p for p in pts]
    for i in range(4):
        mb.limb(tuple(pts[i]), tuple(pts[(i + 1) % 4]), 0.014, 0.014, m, sides=4)


def build_chains(root):
    mb = MeshBuilder()
    for s in (-1, 1):
        wx, wy, wz = s * 0.2, 0.36, 1.53
        # Shackle around the wrist
        mb.loft([ring(wx, wy, wz - 0.04, 0.085, 0.085, 8, math.pi / 8), ring(wx, wy, wz + 0.04, 0.085, 0.085, 8, math.pi / 8)], M_CHAIN, cap_start=False, cap_end=False)
        mb.loft([ring(wx, wy, wz + 0.04, 0.075, 0.075, 8, math.pi / 8), ring(wx, wy, wz - 0.04, 0.075, 0.075, 8, math.pi / 8)], M_CHAIN, cap_start=False, cap_end=False)
        # Two broken links dangling
        link(mb, (wx + s * 0.03, wy, wz - 0.16), (0, 0, 0), M_CHAIN)
        link(mb, (wx + s * 0.05, wy - 0.02, wz - 0.3), (0, 0, 90), M_CHAIN)
    return mb.finish("Ghost_Chains", root)


def build_cloak(root):
    mb = MeshBuilder()
    strips = 7
    a0, a1 = math.radians(200), math.radians(340)          # around his back (-Y)
    for k in range(strips):
        ta, tb = a0 + (a1 - a0) * k / strips, a0 + (a1 - a0) * (k + 1) / strips
        bottom = 0.62 + 0.18 * ((k * 37) % 5) / 4.0
        t_out = lambda a, r, z: (r * math.cos(a), r * 0.8 * math.sin(a) - 0.04, z)
        top_out = [t_out(ta, 0.4, 1.98), t_out(tb, 0.4, 1.98)]
        bot_out = [t_out(tb + 0.04, 0.62, bottom), t_out(ta - 0.04, 0.62, bottom + 0.08)]
        outer = top_out + bot_out
        inner = [(x * 0.92, y * 0.92 + 0.0, z) for x, y, z in outer]
        mb.solid(inner, outer, M_SPIRIT, ref=(0, 0.1, (1.98 + bottom) / 2))
    # Clasp chain across the chest between the pauldrons
    mb.limb((-0.34, 0.12, 1.98), (0.34, 0.12, 1.98), 0.018, 0.018, M_STEEL, sides=4)
    return mb.finish("Ghost_Cloak", root, origin=(0, -0.1, 1.98))


def build_tail(root):
    # Wisp from inside the tabard down to a curling tip, in three swaying pieces
    centers = [(0, 0, 1.2, 0.3, 0.24), (0, -0.04, 0.95, 0.26, 0.21), (0.02, -0.12, 0.72, 0.18, 0.15),
               (0.05, -0.22, 0.52, 0.11, 0.1), (0.04, -0.34, 0.38, 0.05, 0.05), (0.0, -0.46, 0.32, 0.0, 0.0)]
    rings = [ring(x, y, z, rx, ry, 8, wobble=0.12, seed=i) for i, (x, y, z, rx, ry) in enumerate(centers)]
    parts = [(0, 2), (2, 4), (4, 6)]
    objs = []
    for n, (i0, i1) in enumerate(parts):
        mb = MeshBuilder()
        mb.loft(rings[i0:i1 + 1], M_SPIRIT, cap_start=True, cap_end=False)
        x, y, z, _, _ = centers[i0]
        objs.append(mb.finish("Ghost_Tail_%d" % (n + 1), root, origin=(x, y, z)))
    return objs


def build_all():
    clear_scene()
    root = bpy.data.objects.new("Aldric_Ghost", None)
    bpy.context.scene.collection.objects.link(root)
    build_body(root)
    build_sword(root)
    build_eyes(root)
    build_chains(root)
    build_cloak(root)
    build_tail(root)
    empty("Front", root, (0, 5, 0))
    return root


root = build_all()
if bpy.app.background:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "AldricGhost.blend"))
    export_fbx(root, OUT)
