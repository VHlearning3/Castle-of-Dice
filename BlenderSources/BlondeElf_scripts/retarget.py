import bpy, math, numpy as np
from mathutils import Matrix, Vector, Quaternion
W=r"D:\Unity\elf-blonde-work"
SRC=r"D:\Unity\3D DnD selainpeli\BlenderSources\Elira_Victory.blend"
CLIPS={"Walk":"Elira|walk","Idle":"Elira|idle","Die":"Elira|death","AttackFrost":"Elira|FrostRay","AttackFire":"Elira|Fireball","StandIdle":"Elira|standing_idle"}
bpy.ops.wm.open_mainfile(filepath=W+r"\BlondeElf_rig.blend")
with bpy.data.libraries.load(SRC) as (s,d):
    d.objects=["Armature"]; d.actions=list(CLIPS.values())
src=d.objects[0]; src.name="Src_Elira"; bpy.context.scene.collection.objects.link(src)
bpy.context.view_layer.update()
tgt=bpy.data.objects["Armature"]
sc=bpy.context.scene; sc.render.fps=30
P="mixamorig:"
order=[b.name for b in tgt.data.bones]  # parents before children
order.sort(key=lambda n: len(tgt.data.bones[n].parent_recursive))
order=[n for n in order if src.data.bones.get(n) and not n.endswith("_End")]
def rot3(m):
    m3=m.to_3x3(); 
    for i in range(3): m3.col[i].normalize()
    return m3
srest={n:src.matrix_world@src.data.bones[n].matrix_local for n in order}
trest={n:tgt.data.bones[n].matrix_local.copy() for n in order}
S={}
for n in order:
    sb=src.data.bones[n]; tb=tgt.data.bones[n]
    sd=(src.matrix_world.to_3x3()@(sb.tail_local-sb.head_local)).normalized()
    td=(tb.tail_local-tb.head_local).normalized()
    S[n]=sd.rotation_difference(td).to_matrix()
sh=srest[P+"Hips"].translation; th=trest[P+"Hips"].translation
ratio=th.z/sh.z
print("hip ratio",ratio)
results={}
for name,an in CLIPS.items():
    act=bpy.data.actions[an]
    src.animation_data_create(); src.animation_data.action=act
    if hasattr(src.animation_data,"action_slot") and act.slots: src.animation_data.action_slot=act.slots[0]
    f0,f1=[int(round(x)) for x in act.frame_range]
    ta=bpy.data.actions.new("Elf|"+name)
    tgt.animation_data_create(); tgt.animation_data.action=ta
    frames=list(range(f0,f1+1))
    hipsT=[]
    for f in frames:
        sc.frame_set(f)
        M={}
        for n in order:
            spb=src.pose.bones[n]
            sw=src.matrix_world@spb.matrix
            D=rot3(sw)@rot3(srest[n]).inverted()
            R=D@S[n].inverted()@rot3(trest[n])
            if n==P+"Hips":
                pos=th+(sw.translation-sh)*ratio
                hipsT.append(pos.copy())
                Mb=Matrix.Translation(pos)@R.to_4x4()
                basis=trest[n].inverted()@Mb
            else:
                p=tgt.data.bones[n].parent.name
                rel=trest[p].inverted()@trest[n]
                # child arm-space = M[p] @ rel @ basis ; desired rotation R with translation from chain
                Mt=M[p]@rel
                basis_r=(rot3(Mt).inverted()@R)
                basis=basis_r.to_4x4()
                Mb=Mt@basis
            M[n]=Mb
            pb=tgt.pose.bones[n]; pb.rotation_mode='QUATERNION'
            loc,q,_=basis.decompose()
            pb.rotation_quaternion=q
            pb.keyframe_insert("rotation_quaternion",frame=f-f0+1,group=n)
            if n==P+"Hips":
                pb.location=loc; pb.keyframe_insert("location",frame=f-f0+1,group=n)
    results[name]=(ta,len(frames),hipsT)
    H=np.array([h[:] for h in hipsT]); print(name,"frames",len(frames),"hips start",H[0].round(3),"end",H[-1].round(3),"min z",H[:,2].min().round(3))
tgt.animation_data.action=None
for o in [src]: bpy.data.objects.remove(o)
for an in CLIPS.values(): 
    a=bpy.data.actions.get(an); 
    if a: bpy.data.actions.remove(a)
for a in bpy.data.actions: a.use_fake_user=True
bpy.ops.wm.save_as_mainfile(filepath=W+r"\BlondeElf_anim.blend")
print("DONE")
