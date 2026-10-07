# Paint a toothy grin over the mouth of the blonde elf's colour map (for the Smug state)
import bpy, bmesh, numpy as np, sys, math
W=r"D:\Unity\elf-blonde-work"
MODE=sys.argv[sys.argv.index("--")+1] if "--" in sys.argv else "probe"
bpy.ops.wm.open_mainfile(filepath=W+r"\BlondeElf.blend")
ob=bpy.data.objects["BlondeElf_Body"]
img=bpy.data.images.load(W+r"\Elf_BaseColor_2048.png"); T=img.size[0]
px=np.array(img.pixels[:],np.float32).reshape(T,T,4)
bm=bmesh.new(); bm.from_mesh(ob.data); bmesh.ops.triangulate(bm,faces=bm.faces)
uvl=bm.loops.layers.uv.active
P=[];IJ=[]
for f in bm.faces:
    c=f.calc_center_median(); n=f.normal
    if not (abs(c.x)<0.07 and 1.45<c.z<1.62 and c.y<-0.13 and n.y<-0.2): continue
    uv=np.array([l[uvl].uv[:] for l in f.loops])*T
    co=np.array([l.vert.co[:] for l in f.loops])
    x0,y0=np.floor(uv.min(0)).astype(int); x1,y1=np.ceil(uv.max(0)).astype(int)
    xs,ys=np.meshgrid(np.arange(max(x0,0),min(x1+1,T)),np.arange(max(y0,0),min(y1+1,T)))
    pts=np.stack([xs.ravel()+0.5,ys.ravel()+0.5],1)
    a,b,cc=uv; v0=b-a; v1=cc-a; d=v0[0]*v1[1]-v1[0]*v0[1]
    if abs(d)<1e-12: continue
    v2=pts-a; w1=(v2[:,0]*v1[1]-v1[0]*v2[:,1])/d; w2=(v0[0]*v2[:,1]-v2[:,0]*v0[1])/d; w0=1-w1-w2
    m=(w0>=-0.03)&(w1>=-0.03)&(w2>=-0.03)
    if not m.any(): continue
    Wt=np.stack([w0[m],w1[m],w2[m]],1)
    P.append(Wt@co); IJ.append(pts[m].astype(int))
P=np.concatenate(P); IJ=np.concatenate(IJ)
X=P[:,0]; Z=P[:,2]
col=px[IJ[:,1],IJ[:,0],:3]; lum=col@np.array([0.3,0.59,0.11])
print("face texels",len(P))
skin=np.median(col[(abs(X)<0.03)&(Z>1.49)&(Z<1.53)&(lum>0.6)],0); print("skin",skin.round(3))
dark=(abs(X)<0.045)&(Z>1.485)&(Z<1.525)&(lum<lum[(abs(X)<0.03)&(Z>1.49)&(Z<1.53)].mean()-0.12)
print("dark texels",dark.sum())
if dark.sum():
    dx=X[dark]; dz=Z[dark]
    for xb in np.arange(-0.04,0.041,0.005):
        s=abs(dx-xb)<0.0025
        if s.any(): print(" x=%.3f z=%.4f..%.4f n=%d"%(xb,dz[s].min(),dz[s].max(),s.sum()))
# texel density near the mouth
s=(abs(X)<0.03)&(abs(Z-1.50)<0.01)
print("mouth texels", s.sum(), "-> px per mm approx", math.sqrt(s.sum()/(60*20)))
if MODE=="paint":
    z0=float(sys.argv[sys.argv.index("--")+2]); w=0.030
    def top(x): return z0+0.0030+0.0085*np.minimum((x/w)**2,1)       # upper lip line, corners up
    def hgt(x): return 0.0145*np.clip(1-(x/w)**2,0,1)**0.6             # mouth opening, closes at the corners
    def bot(x): return top(x)-hgt(x)
    def mid(x): return top(x)-0.45*hgt(x)                                # gap between upper and lower teeth
    inside=(abs(X)<w)&(Z<top(X))&(Z>bot(X))
    # distance-ish to outline in metres
    edge=np.minimum(np.minimum(top(X)-Z, Z-bot(X)), (w-abs(X))*0.5)
    out=px.copy()
    # 1) erase the old smile line around the mouth with skin colour
    er=(abs(X)<0.040)&(Z>z0-0.016)&(Z<z0+0.009)&~inside
    blend=np.clip((0.040-abs(X[er]))/0.006,0,1)[:,None]
    out[IJ[er,1],IJ[er,0],:3]=col[er]*(1-blend)+skin*blend
    # 2) teeth
    teeth=np.array([0.97,0.96,0.93]); gap=np.array([0.42,0.16,0.16]); line=np.array([0.22,0.10,0.09])
    c=np.tile(teeth,(inside.sum(),1))
    xi=X[inside]; zi=Z[inside]
    g=abs(zi-mid(xi))<0.0009
    c[g]=gap
    # faint tooth separations
    sep=(abs(((xi+0.0035)%0.0070)-0.0035)<0.00035)&~g
    c[sep]=teeth*0.82
    # shade the mouth corners darker (depth)
    cor=np.clip((abs(xi)/w-0.80)/0.2,0,1)[:,None]
    c=c*(1-cor)+gap*cor
    out[IJ[inside,1],IJ[inside,0],:3]=c
    # 3) outline
    ol=(abs(X)<w+0.0010)&(Z<top(X)+0.0012)&(Z>bot(X)-0.0012)&~inside
    out[IJ[ol,1],IJ[ol,0],:3]=line
    # 4) little cheek lines at the corners (anime grin)
    for sgn in (-1,1):
        cx=sgn*(w+0.0035); cz=float(top(np.array(w)))+0.0005
        dd=np.hypot(X-cx,(Z-cz)*1.0)
        arc=(abs(dd-0.0035)<0.00045)&(sgn*(X-cx)>-0.001)&(Z<cz+0.002)&(Z>cz-0.004)
        out[IJ[arc,1],IJ[arc,0],:3]=line*1.4
    o=bpy.data.images.new("grin",T,T,alpha=False); o.pixels.foreach_set(out.ravel())
    o.filepath_raw=W+r"\Elf_BaseColor_Grin_2048.png"; o.file_format='PNG'; o.save()
    print("SAVED")
