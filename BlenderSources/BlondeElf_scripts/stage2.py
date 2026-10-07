import bpy, bmesh, numpy as np, mathutils
from mathutils import Vector
W=r"D:\Unity\elf-blonde-work"
bpy.ops.wm.open_mainfile(filepath=W+r"\BlondeElf_stage1.blend")
bpy.data.objects.remove(bpy.data.objects["Elf_High"])
lo=bpy.data.objects["Elf"]
P="mixamorig:"
J={ # head, tail, parent
 "Hips":((0,-0.075,0.88),(0,-0.075,0.97),None),
 "Spine":((0,-0.078,0.97),(0,-0.078,1.09),"Hips"),
 "Spine1":((0,-0.08,1.09),(0,-0.08,1.21),"Spine"),
 "Spine2":((0,-0.08,1.21),(0,-0.085,1.40),"Spine1"),
 "Neck":((0,-0.085,1.40),(0,-0.095,1.48),"Spine2"),
 "Head":((0,-0.095,1.48),(0,-0.095,1.72),"Neck"),
}
for s,sx in (("Left",1),("Right",-1)):
    J.update({
     s+"Shoulder":((sx*0.035,-0.07,1.37),(sx*0.16,-0.06,1.36),"Spine2"),
     s+"Arm":((sx*0.16,-0.06,1.36),(sx*0.41,-0.058,1.35),s+"Shoulder"),
     s+"ForeArm":((sx*0.41,-0.058,1.35),(sx*0.635,-0.06,1.355),s+"Arm"),
     s+"Hand":((sx*0.635,-0.06,1.355),(sx*0.80,-0.06,1.36),s+"ForeArm"),
     s+"UpLeg":((sx*0.088,-0.07,0.84),(sx*0.115,-0.06,0.445),"Hips"),
     s+"Leg":((sx*0.115,-0.06,0.445),(sx*0.13,-0.045,0.115),s+"UpLeg"),
     s+"Foot":((sx*0.13,-0.045,0.115),(sx*0.13,-0.14,0.03),s+"Leg"),
     s+"ToeBase":((sx*0.13,-0.14,0.03),(sx*0.13,-0.20,0.02),s+"Foot"),
    })
arm_data=bpy.data.armatures.new("Armature"); arm=bpy.data.objects.new("Armature",arm_data)
bpy.context.scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active=arm; bpy.ops.object.mode_set(mode='EDIT')
for n,(h,t,p) in J.items():
    eb=arm_data.edit_bones.new(P+n); eb.head=h; eb.tail=t
for n,(h,t,p) in J.items():
    eb=arm_data.edit_bones[P+n]
    if p: eb.parent=arm_data.edit_bones[P+p]; eb.use_connect=False
    # consistent rolls: legs/spine face forward (-Y), arms Z up
    if "Arm" in n or "Hand" in n or "Shoulder" in n: eb.align_roll(Vector((0,0,1)))
    else: eb.align_roll(Vector((0,-1,0)))
# end bones
for n,par,h,t in (("HeadTop_End","Head",(0,-0.095,1.72),(0,-0.095,1.78)),("LeftToe_End","LeftToeBase",(0.13,-0.20,0.02),(0.13,-0.24,0.02)),("RightToe_End","RightToeBase",(-0.13,-0.20,0.02),(-0.13,-0.24,0.02))):
    eb=arm_data.edit_bones.new(P+n); eb.head=h; eb.tail=t; eb.parent=arm_data.edit_bones[P+par]; eb.align_roll(Vector((0,-1,0)))
bpy.ops.object.mode_set(mode='OBJECT')
# auto weights
bpy.ops.object.select_all(action='DESELECT'); lo.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
me=lo.data
for end in ("HeadTop_End","LeftToe_End","RightToe_End"):
    vg=lo.vertex_groups.get(P+end)
    if vg: lo.vertex_groups.remove(vg)
co=np.array([v.co[:] for v in me.vertices])
unw=[v.index for v in me.vertices if not any(g.weight>0.001 for g in v.groups)]
print("UNWEIGHTED", len(unw))
# hair: loose parts mostly yellow + yellow verts
hair=np.load(W+r"\hairmask.npy")
bm=bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
part=-np.ones(len(co),int); pid=0
for v0 in bm.verts:
    if part[v0.index]>=0: continue
    st=[v0]; part[v0.index]=pid
    while st:
        v=st.pop()
        for e in v.link_edges:
            o=e.other_vert(v)
            if part[o.index]<0: part[o.index]=pid; st.append(o)
    pid+=1
hairpart=np.zeros(len(co),bool)
for p in range(pid):
    m=part==p
    if m.sum()>0 and hair[m].mean()>0.6: hairpart|=m
print("parts",pid,"hairpart verts",hairpart.sum(),"hair col verts",hair.sum())
H=hair|hairpart
# exclude face/ears region from forced hair (eyebrows etc are skin parts); keep only yellow-ish/hairpart
def setw(i,wd):
    for g in lo.vertex_groups:
        g.remove([i])
    for n,w in wd.items():
        if w>0.001: lo.vertex_groups[P+n].add([i],w,'REPLACE')
def lerp_chain(z):
    # head above 1.47, blend to spine chain by height
    keys=[(1.47,"Head"),(1.38,"Neck"),(1.25,"Spine2"),(1.10,"Spine1"),(0.97,"Spine"),(0.85,"Hips")]
    if z>=keys[0][0]: return {"Head":1}
    for (z0,a),(z1,b) in zip(keys,keys[1:]):
        if z>=z1:
            t=(z-z1)/(z0-z1); return {a:t,b:1-t}
    return {"Hips":1}
n=0
for i in np.where(H)[0]:
    x,y,z=co[i]
    if z>1.47 and abs(x)<0.11 and y<-0.12: continue  # bangs over face keep heat weights (head)
    setw(int(i),lerp_chain(z)); n+=1
print("hair reweighted",n)
for i in unw:
    setw(i,lerp_chain(co[i][2]))
bpy.ops.wm.save_as_mainfile(filepath=W+r"\BlondeElf_rig.blend")
print("DONE")
