# Stage 6: final .blend + FBX (mesh, rig, trays, five takes) + textures for Unity.
import bpy, os, shutil
D = "C:/Users/Omistaja/AppData/Local/Temp/claude/D--Unity-3D-DnD-selainpeli/159abac1-df60-4df2-81c6-701ea24cf69b/scratchpad/elf/"
WT = "D:/Unity/castle-elf-waitress/"
OUT = WT + "Assets/Characters/NPC_Elf_Waitress/"
SRC = WT + "BlenderSources/"
os.makedirs(OUT, exist_ok=True)

arm = bpy.data.objects["Elf_Waitress"]
body = bpy.data.objects["Elf_Waitress_Body"]
for n in ("ShotCam", "sticks"):
    if n in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects[n])

# 1024 game texture (the 2048 bake stays next to the .blend as the source)
img = bpy.data.images["Elf_Waitress_BaseColor"]
shutil.copy(D + "Elf_Waitress_BaseColor.png", SRC + "Elf_Waitress_BaseColor_2048.png")
img.filepath = SRC + "Elf_Waitress_BaseColor_2048.png"; img.reload()
game = img.copy(); game.scale(1024, 1024)
game.filepath_raw = OUT + "Elf_Waitress_BaseColor.png"; game.file_format = 'PNG'; game.save()
bpy.data.images.remove(game)
pal = bpy.data.images["Elf_Trays_Palette"]
shutil.copy(D + "Elf_Trays_Palette.png", OUT + "Elf_Trays_Palette.png")
pal.filepath = OUT + "Elf_Trays_Palette.png"; pal.reload()
img.filepath = "//Elf_Waitress_BaseColor_2048.png"
pal.filepath = "//../Assets/Characters/NPC_Elf_Waitress/Elf_Trays_Palette.png"

for pb in arm.pose.bones: pb.matrix_basis.identity()
arm.animation_data.action = None
for tr in arm.animation_data.nla_tracks: tr.mute = True
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=SRC + "Elf_Waitress.blend", relative_remap=False)

for tr in arm.animation_data.nla_tracks: tr.mute = False
bpy.ops.object.select_all(action="DESELECT")
for o in (arm, body, bpy.data.objects["Tray_Bread"], bpy.data.objects["Tray_Beer"]):
    o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=OUT + "Elf_Waitress.fbx", use_selection=True, object_types={'ARMATURE', 'MESH'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
    add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
    bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.5,
    mesh_smooth_type='FACE', use_mesh_modifiers=True, use_armature_deform_only=False,
    path_mode='STRIP', embed_textures=False)
print("EXPORT OK", os.path.getsize(OUT + "Elf_Waitress.fbx"))
