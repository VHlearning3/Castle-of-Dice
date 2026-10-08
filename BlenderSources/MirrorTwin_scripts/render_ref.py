import bpy, sys, math
from mathutils import Vector
OUT=sys.argv[sys.argv.index("--")+1]
scn=bpy.context.scene
arm=bpy.data.objects["Armature"]
if arm.animation_data: arm.animation_data.action=None
for t in (arm.animation_data.nla_tracks if arm.animation_data else []): t.mute=True
for pb in arm.pose.bones: pb.rotation_quaternion=(1,0,0,0); pb.location=(0,0,0)
scn.render.engine='BLENDER_WORKBENCH'
scn.display.shading.light='FLAT'; scn.display.shading.color_type='TEXTURE'
scn.render.resolution_x=900; scn.render.resolution_y=1000
scn.render.film_transparent=False
scn.display.shading.background_type="VIEWPORT"; scn.display.shading.background_color=(0.35,0.35,0.37)
cam=bpy.data.objects.new("RC", bpy.data.cameras.new("RC")); scn.collection.objects.link(cam)
cam.data.type='ORTHO'; cam.data.ortho_scale=1.95
scn.camera=cam
for name,loc,rot in (("front",(0,-4,0.88),(90,0,0)),("side",(4,0,0.88),(90,0,90)),("back",(0,4,0.88),(90,0,180))):
    cam.location=loc; cam.rotation_euler=[math.radians(a) for a in rot]
    scn.render.filepath=OUT+"_"+name+".png"; bpy.ops.render.render(write_still=True)
