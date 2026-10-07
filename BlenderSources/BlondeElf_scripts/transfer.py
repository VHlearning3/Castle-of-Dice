# Nearest-surface texture transfer high->low (robust for thin double-sided cloth)
import bpy, bmesh, numpy as np, mathutils, time, sys
from mathutils.bvhtree import BVHTree
OUT=r"D:\Unity\elf-blonde-work"
TEX=int(sys.argv[sys.argv.index("--")+1]) if "--" in sys.argv else 2048
bpy.ops.wm.open_mainfile(filepath=OUT+r"\BlondeElf_stage1.blend")
hi=bpy.data.objects["Elf_High"]; lo=bpy.data.objects["Elf"]
src=bpy.data.images["Blonde+fantasy+elf+character+3d+model_basecolor.jpg"]
SW,SH=src.size; spx=np.array(src.pixels[:],dtype=np.float32).reshape(SH,SW,4)[:,:,:3]
hb=bmesh.new(); hb.from_mesh(hi.data); bmesh.ops.triangulate(hb,faces=hb.faces); hb.faces.ensure_lookup_table()
huv=hb.loops.layers.uv.active
HT=np.array([[l.vert.co[:] for l in f.loops] for f in hb.faces]); HU=np.array([[l[huv].uv[:] for l in f.loops] for f in hb.faces])
tree=BVHTree.FromBMesh(hb)
lb=bmesh.new(); lb.from_mesh(lo.data); bmesh.ops.triangulate(lb,faces=lb.faces)
luv=lb.loops.layers.uv.active
t0=time.time()
P=[];N=[];IJ=[]
for f in lb.faces:
    uv=np.array([l[luv].uv[:] for l in f.loops])*TEX
    co=np.array([l.vert.co[:] for l in f.loops]); nn=np.array([l.vert.normal[:] for l in f.loops])
    x0,y0=np.floor(uv.min(0)).astype(int); x1,y1=np.ceil(uv.max(0)).astype(int)
    xs,ys=np.meshgrid(np.arange(max(x0,0),min(x1+1,TEX)),np.arange(max(y0,0),min(y1+1,TEX)))
    pts=np.stack([xs.ravel()+0.5,ys.ravel()+0.5],1)
    a,b,c=uv; v0=b-a; v1=c-a; d=v0[0]*v1[1]-v1[0]*v0[1]
    if abs(d)<1e-12: continue
    v2=pts-a; w1=(v2[:,0]*v1[1]-v1[0]*v2[:,1])/d; w2=(v0[0]*v2[:,1]-v2[:,0]*v0[1])/d; w0=1-w1-w2
    m=(w0>=-0.02)&(w1>=-0.02)&(w2>=-0.02)
    if not m.any(): continue
    W=np.stack([w0[m],w1[m],w2[m]],1)
    P.append(W@co); N.append(W@nn); IJ.append(pts[m].astype(int))
P=np.concatenate(P);N=np.concatenate(N);IJ=np.concatenate(IJ)
print("texels",len(P),time.time()-t0)
UV=np.zeros((len(P),2))
fn=tree.find_nearest; V=mathutils.Vector
for i in range(len(P)):
    p=P[i]; n=N[i]; nl=(n[0]**2+n[1]**2+n[2]**2)**0.5 or 1
    q=V((p[0]+n[0]/nl*0.0015,p[1]+n[1]/nl*0.0015,p[2]+n[2]/nl*0.0015))
    loc,_,fi,_=fn(q)
    t=HT[fi]; 
    # barycentric of loc in tri
    a=t[0];v0=t[1]-a;v1=t[2]-a;v2=np.array(loc[:])-a
    d00=v0@v0;d01=v0@v1;d11=v1@v1;d20=v2@v0;d21=v2@v1;den=d00*d11-d01*d01 or 1e-12
    bb=(d11*d20-d01*d21)/den; cc=(d00*d21-d01*d20)/den
    UV[i]=HU[fi][0]*(1-bb-cc)+HU[fi][1]*bb+HU[fi][2]*cc
print("nearest done",time.time()-t0)
# bilinear sample
x=(UV[:,0]%1)*SW-0.5; y=(UV[:,1]%1)*SH-0.5
x0=np.floor(x).astype(int);y0=np.floor(y).astype(int);fx=(x-x0)[:,None];fy=(y-y0)[:,None]
def g(xx,yy): return spx[np.clip(yy,0,SH-1),np.clip(xx,0,SW-1)]
col=g(x0,y0)*(1-fx)*(1-fy)+g(x0+1,y0)*fx*(1-fy)+g(x0,y0+1)*(1-fx)*fy+g(x0+1,y0+1)*fx*fy
img=np.zeros((TEX,TEX,3),np.float32); filled=np.zeros((TEX,TEX),bool)
img[IJ[:,1],IJ[:,0]]=col; filled[IJ[:,1],IJ[:,0]]=True
# dilate 12 px
for _ in range(12):
    acc=np.zeros_like(img);cnt=np.zeros((TEX,TEX),np.float32)
    for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
        sh=np.roll(np.roll(img,dy,0),dx,1); sf=np.roll(np.roll(filled,dy,0),dx,1)
        acc+=sh*sf[:,:,None]; cnt+=sf
    new=(~filled)&(cnt>0); img[new]=acc[new]/cnt[new][:,None]; filled|=new
out=bpy.data.images.get("Elf_BaseColor")
out.scale(TEX,TEX)
rgba=np.concatenate([img,np.ones((TEX,TEX,1),np.float32)],2)
out.pixels.foreach_set(rgba.ravel())
out.filepath_raw=OUT+r"\Elf_BaseColor_%d.png"%TEX; out.file_format='PNG'; out.save()
bpy.ops.wm.save_as_mainfile(filepath=OUT+r"\BlondeElf_stage1.blend")
print("DONE",time.time()-t0)
