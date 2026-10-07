import bpy, bmesh, math, mathutils, sys
OUT=r"D:\Unity\elf-blonde-work"
TARGET_TRIS=int(sys.argv[sys.argv.index("--")+1]) if "--" in sys.argv else 11000
TEX=2048
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=OUT+r"\blonde_elf_orig.glb")
hi=[o for o in bpy.data.objects if o.type=='MESH'][0]
root=bpy.data.objects['RootNode']
# scale to 1.72 m tall, feet at 0
s=1.72/98.014
hi.parent=None; bpy.data.objects.remove(root)
hi.scale=(s,s,s); hi.name="Elf_High"
bpy.context.view_layer.objects.active=hi; hi.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
print("HI tris", sum(len(p.vertices)-2 for p in hi.data.polygons))
# low copy
lo=hi.copy(); lo.data=hi.data.copy(); lo.name="Elf"; lo.data.name="Elf_Mesh"
bpy.context.scene.collection.objects.link(lo)
bm=bmesh.new(); bm.from_mesh(lo.data)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0004)
bm.to_mesh(lo.data); bm.free()
tris=sum(len(p.vertices)-2 for p in lo.data.polygons)
mod=lo.modifiers.new("Dec",'DECIMATE'); mod.ratio=TARGET_TRIS/tris; mod.use_collapse_triangulate=True
bpy.ops.object.select_all(action='DESELECT'); lo.select_set(True); bpy.context.view_layer.objects.active=lo
bpy.ops.object.modifier_apply(modifier="Dec")
print("LO tris", sum(len(p.vertices)-2 for p in lo.data.polygons), "verts", len(lo.data.vertices))
# new UVs
for uv in list(lo.data.uv_layers): lo.data.uv_layers.remove(uv)
lo.data.uv_layers.new(name="UVMap")
# classify low faces by original colour (hair = yellow) via nearest high-poly sample
import numpy as np
from mathutils.bvhtree import BVHTree
src=bpy.data.images["Blonde+fantasy+elf+character+3d+model_basecolor.jpg"]
W,H=src.size; px=np.array(src.pixels[:],dtype=np.float32).reshape(H,W,4)
hbm=bmesh.new(); hbm.from_mesh(hi.data); hbm.faces.ensure_lookup_table()
huv=hbm.loops.layers.uv.active; tree=BVHTree.FromBMesh(hbm)
def sample(p):
    loc,n,i,d=tree.find_nearest(p); f=hbm.faces[i]
    uv=sum((l[huv].uv for l in f.loops),mathutils.Vector((0,0)))/len(f.loops)
    x=int((uv.x%1)*W)%W; y=int((uv.y%1)*H)%H
    return px[y,x,:3]
def is_hair(col):
    r,g,b=col; return r>0.55 and g>0.45 and b<0.35 and (r-b)>0.3
bpy.ops.object.mode_set(mode='EDIT')
bm=bmesh.from_edit_mesh(lo.data)
bm.faces.ensure_lookup_table()
cat={}
for f in bm.faces:
    c=f.calc_center_median(); h=is_hair(sample(c))
    if c.z>1.47 and c.y<-0.005 and abs(c.x)<0.10 and not h: cat[f.index]='face'
    elif h: cat[f.index]='hair'
    else: cat[f.index]='body'
print("CATS", {k:list(cat.values()).count(k) for k in ('face','hair','body')})
for f in bm.faces: f.select=True
bmesh.update_edit_mesh(lo.data)
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.0, scale_to_bounds=False)
bpy.ops.uv.select_all(action='SELECT')
bpy.ops.uv.average_islands_scale()
bm=bmesh.from_edit_mesh(lo.data); bm.faces.ensure_lookup_table()
uvl=bm.loops.layers.uv.active
SC={'face':2.6,'hair':0.55,'body':1.0}
def key(uv): return (round(uv.x,5),round(uv.y,5))
seen=set()
for f0 in bm.faces:
    if f0.index in seen: continue
    isl=[];stack=[f0];seen.add(f0.index)
    while stack:
        f=stack.pop(); isl.append(f)
        for l in f.loops:
            e=l.edge; u1={key(l[uvl].uv),key(l.link_loop_next[uvl].uv)}
            for l2 in e.link_loops:
                lf=l2.face
                if lf.index in seen: continue
                if {key(l2[uvl].uv),key(l2.link_loop_next[uvl].uv)}==u1:
                    seen.add(lf.index); stack.append(lf)
    cs=[cat[f.index] for f in isl]; k=max(set(cs),key=cs.count)
    allu=[l[uvl].uv.copy() for f in isl for l in f.loops]
    cen=sum(allu,mathutils.Vector((0,0)))/len(allu)
    for f in isl:
        for l in f.loops: l[uvl].uv=cen+(l[uvl].uv-cen)*SC[k]
bmesh.update_edit_mesh(lo.data)
bpy.ops.uv.select_all(action='SELECT')
bpy.ops.uv.pack_islands(rotate=True, scale=True, margin=0.004)
bpy.ops.object.mode_set(mode='OBJECT')
# material + bake target
img=bpy.data.images.new("Elf_BaseColor", TEX, TEX, alpha=False)
mat=bpy.data.materials.new("M_BlondeElf")
nt=mat.node_tree; bsdf=nt.nodes.get("Principled BSDF")
tex=nt.nodes.new("ShaderNodeTexImage"); tex.image=img
nt.links.new(tex.outputs[0], bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value=0.85
lo.data.materials.clear(); lo.data.materials.append(mat)
nt.nodes.active=tex
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.samples=4; sc.cycles.device='CPU'
sc.render.bake.use_selected_to_active=True; sc.render.bake.cage_extrusion=0.004; sc.render.bake.max_ray_distance=0.012
sc.render.bake.margin=8
bpy.ops.object.select_all(action='DESELECT'); hi.select_set(True); lo.select_set(True); bpy.context.view_layer.objects.active=lo
bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'})
img.filepath_raw=OUT+r"\Elf_BaseColor_2048.png"; img.file_format='PNG'; img.save()
bpy.ops.wm.save_as_mainfile(filepath=OUT+r"\BlondeElf_stage1.blend")
print("DONE")
