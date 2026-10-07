import bpy
W=r"D:\Unity\elf-blonde-work"
bpy.ops.wm.open_mainfile(filepath=W+r"\BlondeElf_anim.blend")
arm=bpy.data.objects["Armature"]; me=bpy.data.objects["Elf"]
me.name="BlondeElf_Body"
KEEP={"Elf|Idle":"Idle","Elf|Walk":"Walk","Elf|Smug":"Smug","Elf|AttackFire":"Attack","Elf|Die":"Die"}
for a in list(bpy.data.actions):
    if a.name not in KEEP: bpy.data.actions.remove(a)
arm.animation_data_create(); arm.animation_data.action=None
for old,new in KEEP.items():
    a=bpy.data.actions[old]; a.name=new
    tr=arm.animation_data.nla_tracks.new(); tr.name=new
    st=tr.strips.new(new,int(a.frame_range[0]),a); st.name=new
    if a.slots: st.action_slot=a.slots[0]
    st.extrapolation='NOTHING'
for pb in arm.pose.bones:
    pb.rotation_quaternion=(1,0,0,0); pb.location=(0,0,0)
emp=bpy.data.objects.new("BlondeElf_Ref",None); bpy.context.scene.collection.objects.link(emp)
bpy.context.scene.render.fps=30
bpy.context.scene.frame_set(0)
bpy.context.view_layer.update()
print('LHAND', (arm.matrix_world@arm.pose.bones['mixamorig:LeftHand'].head)[:])
bpy.ops.wm.save_as_mainfile(filepath=W+r"\BlondeElf.blend")
common=dict(apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', add_leaf_bones=False, path_mode='STRIP',
    embed_textures=False, mesh_smooth_type='FACE', use_armature_deform_only=True)
# 1) animations only (armature + marker empty), one take per NLA strip
for o in bpy.data.objects: o.select_set(o.name!="BlondeElf_Body")
bpy.ops.export_scene.fbx(filepath=W+r"\BlondeElf_Anims.fbx", use_selection=True, object_types={'ARMATURE','EMPTY'},
    bake_anim=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0, **common)
# 2) model in rest (T) pose, no animation
arm.animation_data_clear()
for pb in arm.pose.bones: pb.rotation_quaternion=(1,0,0,0); pb.location=(0,0,0)
bpy.context.view_layer.update()
print('LHAND rest', (arm.matrix_world@arm.pose.bones['mixamorig:LeftHand'].head)[:])
bpy.ops.export_scene.fbx(filepath=W+r"\BlondeElf.fbx", use_selection=False, object_types={'ARMATURE','MESH','EMPTY'},
    bake_anim=False, **common)
print("DONE")
