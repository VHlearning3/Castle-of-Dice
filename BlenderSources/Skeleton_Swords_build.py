# Castle of Dice: lighter skeleton enemies with swords and sword animations.
# For the Skeleton Guard (also the archer's body) and the Cursed Commander:
#  - the Tripo mesh decimated (same UVs, weights limited to 4 bones), exported as <Name>_Sword.fbx next to the model
#  - a low-poly sword (one palette material) in the model's rest-pose space, hung on Right_Hand in Unity
#  - Sir Roland's Mixamo sword-and-shield takes retargeted onto each Tripo rig: Idle, Attack, Hit, Die
# Run:  blender -b --factory-startup --python BlenderSources/Skeleton_Swords_build.py [-- preview]
# "preview" renders contact sheets of every candidate take into PREVIEW_DIR instead of exporting.
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
CHARS = os.path.join(ROOT, "Assets", "Characters")
KNIGHT = os.path.join(CHARS, "Player_knight_anims")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = "preview" in ARGS
PREVIEW_DIR = os.environ.get("PREVIEW_DIR", os.path.join(HERE, "_preview"))

MODELS = [
    dict(name="Skeleton", folder="Enemy_Normal_skeleton_3d_model",
         fbx="tripo_convert_5bd365a6-e8a7-40ea-9d29-0f8a1ba6d8b8.fbx", ratio=0.45, sword="rusty"),
    dict(name="Commander", folder="1_BOSS_skeletal_warrior_3d_model",
         fbx="tripo_convert_10a8093e-f398-40af-83c7-e7fd03dc711c.fbx", ratio=0.45, sword="dark"),
]
# take -> (Mixamo file, frame range or None, keep root travel)
TAKES = {
    "Idle": ("sword and shield idle.fbx", None, False),
    "Attack": ("sword and shield attack (4).fbx", None, False),
    "Hit": ("sword and shield block (2).fbx", None, False),
    "Die": ("sword and shield death.fbx", None, True),
}
CANDIDATES = {
    "Attack1": ("sword and shield attack.fbx", None, False),
    "Attack2": ("sword and shield attack (2).fbx", None, False),
    "Attack3": ("sword and shield attack (3).fbx", None, False),
    "Attack4": ("sword and shield attack (4).fbx", None, False),
    "Block": ("sword and shield block.fbx", None, False),
    "Block2": ("sword and shield block (2).fbx", None, False),
}

# Mixamo -> Tripo bone names
MAP = {"Hips": "Hips", "Spine": "Spine", "Spine1": "Chest", "Spine2": "UpperChest", "Neck": "Neck", "Head": "Head"}
for s, t in (("Left", "Left"), ("Right", "Right")):
    MAP.update({f"{s}Shoulder": f"{t}_Shoulder", f"{s}Arm": f"{t}_UpperArm", f"{s}ForeArm": f"{t}_LowerArm",
                f"{s}Hand": f"{t}_Hand", f"{s}UpLeg": f"{t}_UpperLeg", f"{s}Leg": f"{t}_LowerLeg",
                f"{s}Foot": f"{t}_Foot", f"{s}ToeBase": f"{t}_Toes"})
    for mf, tf in (("Thumb", "Thumb"), ("Index", "Index"), ("Middle", "Middle"), ("Ring", "Ring"), ("Pinky", "Pinky")):
        for i, seg in ((1, "Proximal"), (2, "Intermediate"), (3, "Distal")):
            MAP[f"{s}Hand{mf}{i}"] = f"{t}_{tf}{seg}"
MAP = {"mixamorig:" + k: v for k, v in MAP.items()}

# ------------------------------------------------------------------ palette (4x4 cells of 4 px)
PAL = {
    "rust_blade": (0, (150, 138, 120)), "rust_edge": (1, (196, 186, 168)), "rust_stain": (2, (128, 82, 52)),
    "iron": (3, (70, 66, 62)), "leather": (4, (92, 60, 38)), "bone": (5, (206, 196, 168)),
    "dark_blade": (6, (52, 54, 62)), "dark_edge": (7, (120, 124, 136)), "blood_fuller": (8, (110, 22, 26)),
    "black_iron": (9, (30, 28, 30)), "dull_gold": (10, (150, 118, 60)), "dark_grip": (11, (44, 30, 26)),
}
N = 4


def palette_image():
    img = bpy.data.images.new("Skeleton_Swords_Palette", N * 4, N * 4, alpha=False)
    px = [0.0] * (N * 4 * N * 4 * 4)
    for _, (idx, rgb) in PAL.items():
        cx, cy = idx % N, idx // N
        for y in range(cy * 4, cy * 4 + 4):
            for x in range(cx * 4, cx * 4 + 4):
                i = (y * N * 4 + x) * 4
                px[i:i + 4] = [rgb[0] / 255, rgb[1] / 255, rgb[2] / 255, 1.0]
    img.pixels = px
    return img


def cell_uv(color):
    idx = PAL[color][0]
    return ((idx % N + 0.5) / N, (idx // N + 0.5) / N)


def rot_only(M):
    m = M.to_3x3()
    for i in range(3): m.col[i].normalize()
    return m


# ------------------------------------------------------------------ import helpers
def import_fbx(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o not in before]


def load_target(m):
    new = import_fbx(os.path.join(CHARS, m["folder"], m["fbx"]))
    arm = [o for o in new if o.type == 'ARMATURE'][0]
    mesh = [o for o in new if o.type == 'MESH'][0]
    for a in list(bpy.data.actions): bpy.data.actions.remove(a)
    arm.animation_data_clear()
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'; pb.matrix_basis = Matrix()
    arm.name = "Armature"; mesh.name = m["name"] + "_LOD"   # "Armature" keeps the clip paths of the Tripo model
    return arm, mesh


def import_take(path):
    acts_before = set(bpy.data.actions)
    new = import_fbx(path)
    src = [o for o in new if o.type == 'ARMATURE'][0]
    for o in new:
        if o is not src: bpy.data.objects.remove(o)
    act = src.animation_data.action
    return src, act


# ------------------------------------------------------------------ world-space retarget (A-pose Tripo <- T-pose Mixamo)
def retarget(tgt, src, act, name, keep_travel=False, fps_scale=24.0 / 30.0):
    sc = bpy.context.scene
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    sb, tb = src.data.bones, tgt.data.bones
    inv = {v: k for k, v in MAP.items() if k in sb}
    order = sorted([b.name for b in tb], key=lambda n: len(tb[n].parent_recursive))
    Mt = tgt.matrix_world.copy()
    sc.frame_set(f0); bpy.context.view_layer.update()
    Ms = src.matrix_world.copy()
    src_rest = {n: rot_only(Ms @ sb[inv[n]].matrix_local) for n in order if n in inv}
    tgt_rest = {n: rot_only(Mt @ tb[n].matrix_local) for n in order}
    S = {}
    for n in src_rest:
        s, t = sb[inv[n]], tb[n]
        ds = (Ms.to_3x3() @ (s.tail_local - s.head_local)).normalized()
        dt = (Mt.to_3x3() @ (t.tail_local - t.head_local)).normalized()
        S[n] = ds.rotation_difference(dt).to_matrix()
    sh = (Ms @ sb["mixamorig:Hips"].matrix_local).to_translation()
    th = (Mt @ tb["Hips"].matrix_local).to_translation()
    ratio = th.z / max(1e-6, sh.z)
    new_act = bpy.data.actions.new(m_prefix(tgt) + name)
    tgt.animation_data_create(); tgt.animation_data.action = new_act
    samples, hips = [], []
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        Mf = src.matrix_world.copy()
        W = {}
        for n in order:
            if n in src_rest:
                Rs = rot_only(Mf @ src.pose.bones[inv[n]].matrix)
                W[n] = Rs @ src_rest[n].inverted() @ S[n].inverted() @ tgt_rest[n]
            else:
                p = tb[n].parent
                W[n] = (W[p.name] @ tgt_rest[p.name].inverted() @ tgt_rest[n]) if p else tgt_rest[n]
        samples.append(W)
        hips.append((Mf @ src.pose.bones["mixamorig:Hips"].matrix).to_translation())
    a = hips[0]
    Mt_inv = Mt.inverted()
    Rt_inv = rot_only(Mt).inverted()
    for i, W in enumerate(samples):
        tf = i * fps_scale + 1
        for n in order:
            p = tb[n].parent
            rest_local = (tgt_rest[p.name].inverted() @ tgt_rest[n]) if p else tgt_rest[n]
            parent_w = W[p.name] if p else Matrix.Identity(3)
            basis = rest_local.inverted() @ parent_w.inverted() @ W[n]
            pb = tgt.pose.bones[n]
            pb.rotation_quaternion = basis.to_quaternion()
            pb.keyframe_insert("rotation_quaternion", frame=tf, group=n)
        h = hips[i]
        ref = sh if keep_travel else a
        want = Vector((th.x + (h.x - ref.x) * ratio, th.y + (h.y - ref.y) * ratio, h.z * ratio))
        R = tb["Hips"].matrix_local
        loc = rot_only(R).inverted() @ ((Mt_inv @ want) - R.to_translation())
        pbh = tgt.pose.bones["Hips"]
        pbh.location = loc
        pbh.keyframe_insert("location", frame=tf, group="Hips")
    new_act.use_fake_user = True
    return new_act


def m_prefix(tgt):
    return CURRENT[0] + "|"


# ------------------------------------------------------------------ sword
def build_sword(kind, name):
    """Sword in its own space: grip centre at the origin, blade along +Z, edges along +-X."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    faces = []

    def face(pts, color, ref):
        f = bm.faces.new([bm.verts.new(p) for p in pts])
        f.normal_update()
        if f.normal.dot(f.calc_center_median() - Vector(ref)) < 0: f.normal_flip()
        u = cell_uv(color)
        for l in f.loops: l[uv].uv = u

    def solid(bottom, top, color):
        ref = sum((Vector(p) for p in bottom + top), Vector()) / (len(bottom) * 2)
        face(bottom, color, ref); face(top, color, ref)
        for i in range(len(bottom)):
            j = (i + 1) % len(bottom)
            face([bottom[i], bottom[j], top[j], top[i]], color, ref)

    def ring(r, z, n=6, sx=1.0, sy=1.0, rot=0.0):
        return [(r * sx * math.cos(rot + 2 * math.pi * k / n), r * sy * math.sin(rot + 2 * math.pi * k / n), z)
                for k in range(n)]

    def box(x0, x1, y0, y1, z0, z1, color):
        solid([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)],
              [(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], color)

    def blade(z0, z1, half_w, half_t, tip, c_flat, c_edge):
        # diamond cross-section: two flats meeting at the centre ridge, edges at +-x; the tip closes to a point
        ref = Vector((0, 0, (z0 + z1) / 2))
        e0, e1 = (half_w, 0, z0), (half_w * 0.92, 0, z1)
        w0, w1 = (-half_w, 0, z0), (-half_w * 0.92, 0, z1)
        f0, f1 = (0, half_t, z0), (0, half_t * 0.9, z1)
        b0, b1 = (0, -half_t, z0), (0, -half_t * 0.9, z1)
        t = (0, 0, z1 + tip)
        for a0, a1, c0, c1 in ((e0, e1, f0, f1), (f0, f1, w0, w1), (w0, w1, b0, b1), (b0, b1, e0, e1)):
            face([a0, c0, c1, a1], c_flat, ref)
        tref = Vector((0, 0, z1))
        for a1, c1 in ((e1, f1), (f1, w1), (w1, b1), (b1, e1)):
            face([a1, c1, t], c_edge, tref)
        face([e0, b0, w0, f0], c_flat, Vector((0, 0, z0 + 0.01)))

    if kind == "rusty":
        # a notched, rust-stained short sword: 0.30 of the 1-unit model, about 0.9 m in the game
        solid(ring(0.010, -0.075, 6), ring(0.012, -0.060, 6), "iron")                # pommel
        solid(ring(0.009, -0.060, 6), ring(0.009, 0.040, 6), "leather")              # grip
        box(-0.060, 0.060, -0.010, 0.010, 0.040, 0.054, "iron")                      # crossguard
        blade(0.054, 0.150, 0.020, 0.006, 0.0, "rust_blade", "rust_edge")
        blade(0.150, 0.250, 0.019, 0.0055, 0.0, "rust_stain", "rust_edge")
        blade(0.250, 0.285, 0.0175, 0.005, 0.045, "rust_blade", "rust_edge")
    else:
        # the Commander's longer black blade with a blood-red fuller, spiked crossguard and gold pommel
        solid(ring(0.014, -0.110, 6), ring(0.016, -0.090, 6), "dull_gold")           # pommel
        solid(ring(0.011, -0.090, 6), ring(0.011, 0.050, 6), "dark_grip")            # grip
        box(-0.050, 0.050, -0.014, 0.014, 0.050, 0.070, "black_iron")                # crossguard
        for sx in (1, -1):                                                           # spiked guard tips
            solid([(sx * 0.050, -0.012, 0.050), (sx * 0.050, 0.012, 0.050), (sx * 0.050, 0.012, 0.070),
                   (sx * 0.050, -0.012, 0.070)],
                  [(sx * 0.090, -0.003, 0.080), (sx * 0.090, 0.003, 0.080), (sx * 0.090, 0.003, 0.084),
                   (sx * 0.090, -0.003, 0.084)], "black_iron")
        blade(0.070, 0.400, 0.030, 0.008, 0.070, "dark_blade", "dark_edge")
        for sy in (1, -1):                                                           # fuller on both flats
            box(-0.006, 0.006, sy * 0.0055, sy * 0.0085, 0.080, 0.330, "blood_fuller")
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def grip_matrix(arm, side="Right"):
    """Sword frame relative to the posed hand: origin inside the fist, +Z out past the thumb, +X along the knuckles."""
    pb = arm.pose.bones
    Mw = arm.matrix_world
    P = lambda n, tail=False: Mw @ (pb[n].tail if tail else pb[n].head)
    hand = P(f"{side}_Hand")
    knuck = [P(f"{side}_{f}Proximal") for f in ("Index", "Middle", "Ring", "Pinky")]
    tips = [P(f"{side}_{f}Distal", True) for f in ("Index", "Middle", "Ring", "Pinky")]
    k = sum(knuck, Vector()) / 4; t = sum(tips, Vector()) / 4
    centre = (k + t) / 2 * 0.65 + hand * 0.35
    axis = (knuck[0] - knuck[3]).normalized()                    # pinky -> index: the blade side of the fist
    fwd = (k - hand); fwd = (fwd - axis * fwd.dot(axis)).normalized()
    y = axis.cross(fwd)
    M = Matrix((fwd, y, axis)).transposed().to_4x4()
    M.translation = centre
    return M


# ------------------------------------------------------------------ preview renders
def setup_render():
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
    sc.render.resolution_x = 360; sc.render.resolution_y = 420
    sc.render.film_transparent = False
    cam = bpy.data.objects.get("PrevCam")
    if cam is None:
        cam = bpy.data.objects.new("PrevCam", bpy.data.cameras.new("PrevCam"))
        sc.collection.objects.link(cam)
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 1.45
    cam.location = (-1.6, -2.4, 1.15)
    cam.rotation_euler = (math.radians(80), 0, math.radians(-33))
    sc.camera = cam


def render_take(arm, act, tag, n=6):
    sc = bpy.context.scene
    arm.animation_data.action = act
    f0, f1 = act.frame_range
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    for i in range(n):
        f = f0 + (f1 - f0) * i / (n - 1)
        sc.frame_set(int(f), subframe=f - int(f))
        sc.render.filepath = os.path.join(PREVIEW_DIR, f"{tag}_{i}.png")
        bpy.ops.render.render(write_still=True)


# ------------------------------------------------------------------ main
def decimate(mesh, ratio):
    tris0 = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    mod = mesh.modifiers.new("Decimate", 'DECIMATE'); mod.decimate_type = 'COLLAPSE'; mod.ratio = ratio
    mod.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.select_all(action='DESELECT'); mesh.select_set(True)
    for md in list(mesh.modifiers):
        if md.type == 'DECIMATE': bpy.ops.object.modifier_apply(modifier=md.name)
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
    tris1 = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    print(f"DECIMATE {mesh.name}: {tris0} -> {tris1} tris, {len(mesh.data.vertices)} verts")
    return tris0, tris1


CURRENT = [""]


def build_model(m, pal_img):
    CURRENT[0] = m["name"]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene; sc.render.fps = 24
    pal_img = palette_image()
    arm, mesh = load_target(m)
    stats = decimate(mesh, m["ratio"])
    takes = dict(CANDIDATES if PREVIEW else {}, **TAKES)
    acts = {}
    for take, (fname, rng, travel) in takes.items():
        src, act = import_take(os.path.join(KNIGHT, fname))
        src.animation_data.action = act
        acts[take] = retarget(arm, src, act, take, keep_travel=travel)
        bpy.data.objects.remove(src)
        for a in list(bpy.data.actions):
            if a is act: bpy.data.actions.remove(a)
    # grip from the first frame of the idle (fingers closed on the hilt)
    arm.animation_data.action = acts["Idle"]; sc.frame_set(1); bpy.context.view_layer.update()
    G = grip_matrix(arm)
    hand_pose = arm.matrix_world @ arm.pose.bones["Right_Hand"].matrix
    hand_rest = arm.matrix_world @ arm.data.bones["Right_Hand"].matrix_local
    sword = build_sword(m["sword"], m["name"] + "_Sword")
    mat = bpy.data.materials.new("M_Skeleton_Swords"); mat.use_nodes = True
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage"); tex.image = pal_img; tex.interpolation = 'Closest'
    mat.node_tree.links.new(tex.outputs["Color"], mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.6
    mat.node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = 0.3
    sword.data.materials.append(mat)
    # sword placed in rest-pose space: same offset from the hand as in the posed grip
    sword.matrix_world = hand_rest @ hand_pose.inverted() @ G
    return arm, mesh, sword, acts, stats, G, hand_pose


def preview(m):
    arm, mesh, sword, acts, stats, G, hand_pose = build_model(m, None)
    # follow the hand while rendering
    rest_world = sword.matrix_world.copy()
    arm.animation_data.action = None
    for pb in arm.pose.bones: pb.matrix_basis = Matrix()
    bpy.context.view_layer.update()
    sword.parent = arm; sword.parent_type = 'BONE'; sword.parent_bone = "Right_Hand"
    bpy.context.view_layer.update()
    sword.matrix_world = rest_world
    setup_render()
    for take, act in acts.items():
        render_take(arm, act, f"{m['name']}_{take}")


def export(m):
    arm, mesh, sword, acts, stats, G, hand_pose = build_model(m, None)
    out_dir = os.path.join(CHARS, m["folder"])
    pal_path = os.path.join(CHARS, "Materials", "Skeleton_Swords_Palette.png")
    img = bpy.data.images["Skeleton_Swords_Palette"]
    img.filepath_raw = pal_path; img.file_format = 'PNG'; img.save()
    root = bpy.data.objects.new(m["name"] + "_Sword_Root", None)
    bpy.context.scene.collection.objects.link(root)
    arm.animation_data.action = None
    for pb in arm.pose.bones: pb.matrix_basis = Matrix()
    for take in ("Idle", "Attack", "Hit", "Die"):
        act = acts[take]
        tr = arm.animation_data.nla_tracks.new(); tr.name = take
        st = tr.strips.new(take, int(act.frame_range[0]), act)
        st.name = take
    bpy.context.view_layer.update()
    blend = os.path.join(HERE, f"{m['name']}_Sword.blend")
    bpy.context.scene.render.fps = 24   # the Mixamo imports switch the scene to 30 fps
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    bpy.ops.object.select_all(action='DESELECT')
    for o in (arm, mesh, sword, root): o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    path = os.path.join(out_dir, f"{m['name']}_Sword.fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH', 'EMPTY'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.5,
        mesh_smooth_type='FACE', use_mesh_modifiers=True, use_armature_deform_only=False,
        path_mode='STRIP', embed_textures=False)
    print("EXPORT", path, os.path.getsize(path), "tris", stats,
          {k: tuple(a.frame_range) for k, a in acts.items()})


for m in MODELS:
    if PREVIEW: preview(m)
    else: export(m)
print("DONE")
