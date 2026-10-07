import bpy, math, sys
from mathutils import Matrix, Vector, Quaternion
W=r"D:\Unity\elf-blonde-work"
RENDER="--render" in sys.argv
bpy.ops.wm.open_mainfile(filepath=W+r"\BlondeElf_anim.blend")
arm=bpy.data.objects["Armature"]; P="mixamorig:"; sc=bpy.context.scene
B=arm.pose.bones; vl=bpy.context.view_layer
idle=bpy.data.actions["Elf|Idle"]
arm.animation_data.action=idle
if idle.slots: arm.animation_data.action_slot=idle.slots[0]
sc.frame_set(1)
base={pb.name:(pb.rotation_quaternion.copy(),pb.location.copy()) for pb in B}
arm.animation_data.action=None
for pb in B:
    pb.rotation_mode='QUATERNION'; pb.rotation_quaternion,pb.location=base[pb.name]
vl.update()
def rot3(m):
    m3=m.to_3x3()
    for i in range(3): m3.col[i].normalize()
    return m3
def wrot(n,axis,deg):
    """rotate bone n (and children) about a world axis"""
    pb=B[P+n]; R=Matrix.Rotation(math.radians(deg),3,axis)
    Mr=rot3(pb.matrix)
    pb.rotation_quaternion=(pb.rotation_quaternion.to_matrix()@(Mr.inverted()@R@Mr)).to_quaternion()
    vl.update()
def set_world(n,R):
    pb=B[P+n]; b=pb.bone
    if b.parent:
        rel=rot3(b.parent.matrix_local).inverted()@rot3(b.matrix_local)
        Mp=rot3(pb.parent.matrix)@rel
    else: Mp=rot3(b.matrix_local)
    pb.rotation_quaternion=(Mp.inverted()@R).to_quaternion(); vl.update()
def aim(n,target_dir):
    pb=B[P+n]; Mr=rot3(pb.matrix); cur=Mr.col[1].normalized()
    set_world(n,cur.rotation_difference(target_dir.normalized()).to_matrix()@Mr)
# Reference: her LEFT hand flat on the upper chest, right arm relaxed, chin up, looking up to her right.
HAND="Left"; OTHER="Right"; k=-1   # k mirrors Y/Z world rotations designed for a right-hand version
sx=1 if HAND=="Left" else -1
# --- hips: weight on one hip
wrot("Hips",'Y',-4*k)
wrot("Hips",'Z',6*k)
wrot("LeftUpLeg",'Y',4*k); wrot("RightUpLeg",'Y',4*k)
wrot(HAND+"UpLeg",'X',-6); wrot(HAND+"Leg",'X',12)
# --- chest out, lean back, counter the tilt
wrot("Spine",'Y',3*k); wrot("Spine1",'X',-4); wrot("Spine2",'X',-5); wrot("Spine1",'Y',2*k)
# --- head: chin up, look up and away from the hand
wrot("Neck",'X',-10); wrot("Head",'X',-19); wrot("Neck",'Z',14*k); wrot("Head",'Z',18*k); wrot("Head",'Y',-8*k)
# --- hand flat on chest under the collarbone (2-bone IK)
sh=B[P+HAND+"Arm"].head.copy()
chest=Vector((sx*0.045,-0.200,1.290))          # palm centre on chest
fing=Vector((-sx*0.60,0.30,0.74)).normalized()  # fingers point up and in, following the chest
wrist=chest-fing*0.075
a=(B[P+HAND+"Arm"].tail-B[P+HAND+"Arm"].head).length; b=(B[P+HAND+"ForeArm"].tail-B[P+HAND+"ForeArm"].head).length
d=(wrist-sh); L=min(d.length,a+b-1e-4)
cosA=(a*a+L*L-b*b)/(2*a*L); A=math.acos(max(-1,min(1,cosA)))
pole=Vector((sx*0.6,-0.15,-1.0)).normalized()
dn=d.normalized(); pp=(pole-dn*pole.dot(dn)).normalized()
elbow=sh+dn*math.cos(A)*a+pp*math.sin(A)*a
# hinge-consistent arm: elbow bends only about the upper arm's local Z (Unity humanoid elbow axis)
Yu=(elbow-sh).normalized(); F=(wrist-elbow).normalized()
Zu=Yu.cross(F).normalized()
if Zu.z<0: Zu=-Zu        # keep the hinge axis near the rest Z (up) so the upper arm is not rolled over
Xu=Yu.cross(Zu)
set_world(HAND+"Arm",Matrix((Xu,Yu,Zu)).transposed())
Xf=F.cross(Zu)
set_world(HAND+"ForeArm",Matrix((Xf,F,Zu)).transposed())
# hand: Y=fingers, Z = -palm normal; palm faces the body (+Y) -> Z = -Y world
Y=fing; Z=Vector((0,-1,0)); Z=(Z-Y*Z.dot(Y)).normalized(); X=Y.cross(Z)
Rh=Matrix((X,Y,Z)).transposed()
# share wrist twist with forearm
fa=B[P+HAND+"ForeArm"]; Mf=rot3(fa.matrix)
hrel=Mf.inverted()@Rh
tw=hrel.to_quaternion().to_swing_twist('Y')[1]
fa.rotation_quaternion=(fa.rotation_quaternion.to_matrix()@Matrix.Rotation(tw*0.5,3,'Y')).to_quaternion(); vl.update()
set_world(HAND+"Hand",Rh)
# --- other arm relaxed, a touch out from the hip
wrot(OTHER+"Arm",'Y',-4*k); wrot(OTHER+"ForeArm",'Z',6*k)
hr=rot3(B[P+HAND+"ForeArm"].matrix).inverted()@rot3(B[P+HAND+"Hand"].matrix)
print("hand rel euler", [round(math.degrees(a),1) for a in hr.to_euler('YXZ')])
print("wrist err",(B[P+HAND+"Hand"].head-wrist).length)
smug={pb.name:(pb.rotation_quaternion.copy(),pb.location.copy()) for pb in B}
if RENDER:
    sc.render.engine='BLENDER_WORKBENCH'; sc.display.shading.light='STUDIO'; sc.display.shading.color_type='TEXTURE'
    sc.render.resolution_x=900; sc.render.resolution_y=1200
    cam=bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam); sc.camera=cam
    cam.data.type='ORTHO'
    for name,ang,z,os in [("front",0,0.9,1.9),("q",-30,0.9,1.9),("side",-90,0.9,1.9),("top",-20,1.35,0.7)]:
        cam.data.ortho_scale=os
        dv=Vector((math.sin(math.radians(ang)),-math.cos(math.radians(ang)),0))
        cam.location=Vector((0,0,z))+dv*5; cam.rotation_euler=(math.radians(90),0,math.radians(ang))
        sc.render.filepath=W+r"\smug_%s.png"%name; bpy.ops.render.render(write_still=True)
# --- build the clip: ease from idle into smug, then hold with breathing
act=bpy.data.actions.get("Elf|Smug") or bpy.data.actions.new("Elf|Smug")
arm.animation_data.action=act
def ease(t): return t*t*(3-2*t)
N=90; IN=16
for f in range(1,N+1):
    t=ease(min(1,(f-1)/IN))
    br=math.sin((f-IN)/30*2*math.pi/2.4)*(1 if f>IN else 0)
    for pb in B:
        q0,l0=base[pb.name]; q1,l1=smug[pb.name]
        q=q0.slerp(q1,t); l=l0.lerp(l1,t)
        if pb.name==P+"Spine2": q=q@Quaternion((1,0,0),math.radians(1.2*br))
        if pb.name==P+"Head": q=q@Quaternion((1,0,0),math.radians(-0.8*br))
        pb.rotation_quaternion=q; pb.keyframe_insert("rotation_quaternion",frame=f,group=pb.name)
        if pb.name==P+"Hips": pb.location=l; pb.keyframe_insert("location",frame=f,group=pb.name)
act.use_fake_user=True
arm.animation_data.action=None
for pb in B: pb.rotation_quaternion=Quaternion(); pb.location=Vector()
bpy.ops.wm.save_as_mainfile(filepath=W+r"\BlondeElf_anim.blend")
print("DONE")
