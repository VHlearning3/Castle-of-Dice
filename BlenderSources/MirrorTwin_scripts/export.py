# Exports the Mirror Twin as two FBX files, like the blonde elf (see BlondeElf_scripts/export.py):
#   MirrorTwin_Anims.fbx  armature only, one take per NLA strip (Idle, Walk, Attack, Cast, Laugh, Die)
#   MirrorTwin.fbx        body, skirt, belt and trident in the rest pose, no animation (Unity builds the avatar from it)
# Run: blender -b MirrorTwin_anim.blend --python export.py -- <work dir> <unity character folder>
import bpy, os, sys

a = sys.argv[sys.argv.index("--") + 1:]
W, OUT = a[0], a[1]
arm = bpy.data.objects["Armature"]
TAKES = {"Twin_Idle": "Idle", "Twin_Walk": "Walk", "Twin_Attack": "Attack", "Twin_Cast": "Cast",
         "Twin_Laugh": "Laugh", "Twin_Hit": "Hit", "Twin_Die": "Die"}
for act in list(bpy.data.actions):
    if act.name not in TAKES:
        bpy.data.actions.remove(act)
arm.animation_data_create()
arm.animation_data.action = None
for t in list(arm.animation_data.nla_tracks):
    arm.animation_data.nla_tracks.remove(t)
for old, new in TAKES.items():
    act = bpy.data.actions[old]
    act.name = new
    tr = arm.animation_data.nla_tracks.new()
    tr.name = new
    st = tr.strips.new(new, int(act.frame_range[0]), act)
    st.name = new
    if act.slots:
        st.action_slot = act.slots[0]
    st.extrapolation = 'NOTHING'
for pb in arm.pose.bones:
    pb.rotation_quaternion = (1, 0, 0, 0)
    pb.location = (0, 0, 0)
# A second root object keeps the armature a named node in both files (Unity otherwise folds a lone root into
# the file root and the takes no longer match the model's avatar)
if "MirrorTwin_Ref" not in bpy.data.objects:
    ref = bpy.data.objects.new("MirrorTwin_Ref", None)
    bpy.context.scene.collection.objects.link(ref)
bpy.context.scene.render.fps = 30
bpy.context.scene.frame_set(0)
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(W, "MirrorTwin.blend"))

common = dict(apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', add_leaf_bones=False, path_mode='STRIP',
              embed_textures=False, mesh_smooth_type='FACE', use_armature_deform_only=True)
meshes = {"Twin_Body", "Twin_Skirt", "Twin_Belt", "Twin_Trident"}
for o in bpy.data.objects:
    o.select_set(o.name not in meshes)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "MirrorTwin_Anims.fbx"), use_selection=True, object_types={'ARMATURE', 'EMPTY'},
                         bake_anim=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
                         bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, **common)
arm.animation_data_clear()
for pb in arm.pose.bones:
    pb.rotation_quaternion = (1, 0, 0, 0)
    pb.location = (0, 0, 0)
bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "MirrorTwin.fbx"), use_selection=False,
                         object_types={'ARMATURE', 'MESH', 'EMPTY'}, bake_anim=False, **common)
print("EXPORTED")
