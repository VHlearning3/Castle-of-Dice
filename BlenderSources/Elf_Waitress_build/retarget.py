# World-space retarget of Mixamo takes onto the elf rig (same bone names, both T-pose).
import bpy
from mathutils import Vector, Matrix, Quaternion

def rot_only(M):
    return M.to_3x3().normalized()

def import_take(path):
    before = set(bpy.data.objects)
    acts_before = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    src = [o for o in new if o.type == 'ARMATURE'][0]
    for o in new:
        if o is not src: bpy.data.objects.remove(o)
    act = src.animation_data.action
    return src, act, new

def retarget(tgt, src, act, name, frame_range=None, in_place=True, fps_scale=1.0):
    sc = bpy.context.scene
    f0, f1 = (int(act.frame_range[0]), int(act.frame_range[1])) if frame_range is None else frame_range
    sb, tb = src.data.bones, tgt.data.bones
    names = [b.name for b in tgt.data.bones if b.name in sb]
    order = [b.name for b in tgt.data.bones]  # parents before children
    sc.frame_set(f0); bpy.context.view_layer.update()
    Ms = src.matrix_world.copy()  # knight takes animate the object itself (constant rot/scale keys)
    src_rest = {n: rot_only(Ms @ sb[n].matrix_local) for n in names}
    tgt_rest = {n: rot_only(tb[n].matrix_local) for n in order}
    # swing from source rest direction to target rest direction
    S = {}
    for n in names:
        ds = (src_rest[n].col[1]).normalized(); dt = tgt_rest[n].col[1].normalized()
        S[n] = ds.rotation_difference(dt).to_matrix()
    hip = "mixamorig:Hips"
    src_hip_rest = (Ms @ sb[hip].matrix_local).to_translation()
    tgt_hip_rest = tb[hip].matrix_local.to_translation()
    hscale = tgt_hip_rest.z / max(1e-6, src_hip_rest.z)
    new_act = bpy.data.actions.new(name)
    tgt.animation_data_create(); tgt.animation_data.action = new_act
    for pb in tgt.pose.bones: pb.rotation_mode = 'QUATERNION'
    frames = list(range(f0, f1 + 1))
    hip_locs = []
    samples = []
    for f in frames:
        sc.frame_set(f)
        Mf = src.matrix_world.copy()
        Mr = Mf.copy(); Mr.translation = Ms.translation if in_place else Mf.translation
        W = {}
        for n in order:
            if n in names:
                Rs = rot_only(Mf @ src.pose.bones[n].matrix)
                W[n] = Rs @ src_rest[n].inverted() @ S[n].inverted() @ tgt_rest[n]
            else:  # no source: keep rest relative to parent
                p = tb[n].parent
                W[n] = (W[p.name] @ tgt_rest[p.name].inverted() @ tgt_rest[n]) if p else tgt_rest[n]
        hp = (Mr @ src.pose.bones[hip].matrix).to_translation()
        samples.append(W); hip_locs.append(hp)
    # hips: horizontal drift removed (in place) or kept relative to the rest spot; height kept; scaled to her size
    n = len(hip_locs); a, b = hip_locs[0], hip_locs[-1]
    targets = []
    for i, h in enumerate(hip_locs):
        if in_place:
            t = i / max(1, n - 1)
            dx, dy = h.x - (a.x + (b.x - a.x) * t), h.y - (a.y + (b.y - a.y) * t)
        else:
            dx, dy = h.x - src_hip_rest.x, h.y - src_hip_rest.y
        targets.append(Vector((tgt_hip_rest.x + dx * hscale, tgt_hip_rest.y + dy * hscale, h.z * hscale)))
    for i, f in enumerate(frames):
        W = samples[i]
        tf = (f - f0) * fps_scale + 1
        for n in order:
            p = tb[n].parent
            rest_local = (tgt_rest[p.name].inverted() @ tgt_rest[n]) if p else tgt_rest[n]
            parent_w = W[p.name] if p else Matrix.Identity(3)
            basis = rest_local.inverted() @ parent_w.inverted() @ W[n]
            pb = tgt.pose.bones[n]
            pb.rotation_quaternion = basis.to_quaternion()
            pb.keyframe_insert("rotation_quaternion", frame=tf, group=n)
        # hips location in bone-local space
        want = targets[i]
        R = tb[hip].matrix_local
        loc = R.to_3x3().normalized().inverted() @ (want - R.to_translation())
        pbh = tgt.pose.bones[hip]
        pbh.location = loc
        pbh.keyframe_insert("location", frame=tf, group=hip)
    return new_act
