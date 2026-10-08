# Rasterizes the body's UV triangles: for each texel, the rest-pose 3D position and normal of the surface it paints.
import bpy, bmesh, numpy as np
N=2048
ob=bpy.data.objects["BlondeElf_Body"]
me=ob.data
bm=bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:])
uvl=bm.loops.layers.uv.active
pos=np.zeros((N,N,3),np.float32); nor=np.zeros((N,N,3),np.float32); cov=np.zeros((N,N),bool)
for f in bm.faces:
    uv=np.array([[l[uvl].uv.x*N-0.5, l[uvl].uv.y*N-0.5] for l in f.loops]); P=np.array([l.vert.co[:] for l in f.loops]); n=np.array(f.normal[:])
    x0=int(np.floor(uv[:,0].min()))-1; x1=int(np.ceil(uv[:,0].max()))+1; y0=int(np.floor(uv[:,1].min()))-1; y1=int(np.ceil(uv[:,1].max()))+1
    x0=max(x0,0); y0=max(y0,0); x1=min(x1,N-1); y1=min(y1,N-1)
    if x1<x0 or y1<y0: continue
    xs,ys=np.meshgrid(np.arange(x0,x1+1),np.arange(y0,y1+1))
    a,b,c=uv; v0=b-a; v1=c-a; d=v0[0]*v1[1]-v1[0]*v0[1]
    if abs(d)<1e-9: continue
    px=xs-a[0]; py=ys-a[1]
    w1=(px*v1[1]-v1[0]*py)/d; w2=(v0[0]*py-px*v0[1])/d; w0=1-w1-w2
    m=(w0>=-0.02)&(w1>=-0.02)&(w2>=-0.02)
    if not m.any(): continue
    p=w0[...,None]*P[0]+w1[...,None]*P[1]+w2[...,None]*P[2]
    yy=ys[m]; xx=xs[m]
    pos[yy,xx]=p[m]; nor[yy,xx]=n; cov[yy,xx]=True
np.save("texpos.npy",pos); np.save("texnor.npy",nor); np.save("texcov.npy",cov)
print("covered", cov.mean())
