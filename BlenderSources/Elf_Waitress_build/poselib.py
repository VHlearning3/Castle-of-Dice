# Posing helpers in armature space (character faces -Y, her left = +X, up = +Z).
import bpy, math
from mathutils import Vector, Matrix, Quaternion

def K(n): return "mixamorig:" + n

def rest_rot(arm, name):
    return arm.data.bones[name].matrix_local.to_3x3()

def aim(arm, name, direction, ref_rest=None, ref_want=None):
    """Point bone Y along `direction`; twist so the rest-space vector `ref_rest` (armature space at rest)
    ends up as close as possible to `ref_want`."""
    pb = arm.pose.bones[name]
    bpy.context.view_layer.update()
    R0 = rest_rot(arm, name)
    y0 = R0.col[1].normalized()
    d = Vector(direction).normalized()
    q = y0.rotation_difference(d)
    R = q.to_matrix() @ R0
    if ref_rest is not None and ref_want is not None:
        cur = q @ Vector(ref_rest)
        cur = (cur - d * cur.dot(d)).normalized()
        want = Vector(ref_want); want = (want - d * want.dot(d)).normalized()
        ang = cur.angle(want)
        if cur.cross(want).dot(d) < 0: ang = -ang
        R = Quaternion(d, ang).to_matrix() @ R
    head = pb.matrix.to_translation()
    pb.matrix = Matrix.Translation(head) @ R.to_4x4()
    bpy.context.view_layer.update()

def apply_pose(arm, pose):
    """pose: list of (bone, dir, ref_rest, ref_want) applied in order (parents first)."""
    for bone, d, rr, rw in pose:
        aim(arm, K(bone), d, rr, rw)

def carry_pose():
    # left hand (+X): beer tray low in front, palm up.  right hand (-X): bread tray raised beside the head.
    return [
        ("LeftShoulder",  (1.0, -0.05, -0.08), None, None),
        ("LeftArm",       (0.30, -0.22, -1.0), (0, -1, 0), (0, -1, 0)),
        ("LeftForeArm",   (-0.30, -1.0, 0.10), (0, 0, -1), (-0.6, 0, 1.0)),
        ("LeftHand",      (-0.15, -1.0, 0.0), (0, 0, -1), (0, 0, 1)),
        ("RightShoulder", (-1.0, -0.05, 0.10), None, None),
        ("RightArm",      (-1.0, -0.35, -0.15), (0, -1, 0), (0, 0, 1)),
        ("RightForeArm",  (-0.12, -0.25, 1.0), (0, 0, -1), (0.6, 0.6, 0)),
        ("RightHand",     (-0.35, -1.0, 0.12), (0, 0, -1), (0, 0, 1)),
    ]

def capture_local(arm, names):
    return {n: arm.pose.bones[n].rotation_quaternion.copy() for n in names}
